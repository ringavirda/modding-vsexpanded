using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>
/// A piece of stock part-way through a rolling schedule: its <see cref="Form"/> and the thickness of each
/// strip across its width. Carried on the item stack, because a two-high stand cannot be fed backwards and
/// the piece is walked back around between passes. Strip count is the piece measured against the barrel it
/// is rolled on (<c>ceil(width / barrelWidth)</c>), so an overhanging piece can be thin on one side and
/// thick on another. A gap costs <c>2 × sides</c> trips, two feeds per side. See docs/design/machines/rolling-mill.md.
/// </summary>
/// <param name="Form">The stock form, which supplies the base dimensions and how it spreads.</param>
/// <param name="Strips">Thickness of each side of the piece.</param>
/// <param name="Turned">Per side, whether its first pass at the current gap is done, so the next feed is the
/// turn-over that completes the reduction.</param>
public sealed record WorkPiece(StockForm Form, float[] Strips, bool[] Turned) {
  /// <summary>Feeds each side needs to reach a gap: a pass, then the same pass with the piece turned over.</summary>
  public const int FeedsPerSide = 2;

  private const string FormKey = "stockForm";
  private const string StripsKey = "stripThickness";
  private const string TurnedKey = "stripTurned";

  /// <summary>How many sides a piece of <paramref name="width"/> must be rolled in on a barrel of
  /// <paramref name="barrelWidth"/>: one when it fits, more when it overhangs and has to be taken in
  /// overlapping strips.</summary>
  public static int SidesFor(float width, float barrelWidth) =>
    barrelWidth <= 0f
      ? 1
      : Math.Max(1, (int)MathF.Ceiling(width / barrelWidth));

  /// <summary>Trips through the mill one gap costs for this piece on the given barrel.</summary>
  public static int PassesForGap(float width, float barrelWidth) =>
    FeedsPerSide * SidesFor(width, barrelWidth);

  /// <summary>A fresh piece off the helve: one side, at the form's as-shingled thickness. It gains sides
  /// only once it has spread wider than the barrel it meets.</summary>
  public static WorkPiece Fresh(StockForm form) =>
    new(form, [form.BaseThickness], [false]);

  /// <summary>The same piece re-divided into <paramref name="sides"/> strips. Only an <see cref="IsEven"/>
  /// piece can be re-divided; an uneven one is returned untouched and fails to bite at the next gap until
  /// its sides are levelled.</summary>
  public WorkPiece Resplit(int sides) {
    if (sides == Strips.Length || sides < 1 || !IsEven)
      return this;
    float thickness = Strips.Length > 0 ? Strips[0] : Form.BaseThickness;
    return this with {
      Strips = [.. Enumerable.Repeat(thickness, sides)],
      Turned = new bool[sides],
    };
  }

  /// <summary>The thickest strip - what the piece reads as overall, since it is only as finished as its
  /// least-worked side.</summary>
  public float Thickest =>
    Strips.Length == 0 ? Form.BaseThickness : Strips.Max();

  /// <summary>The thinnest strip.</summary>
  public float Thinnest =>
    Strips.Length == 0 ? Form.BaseThickness : Strips.Min();

  /// <summary>Whether every strip is worked to the same thickness, so the piece can move on to the next gap
  /// or come off the mill as a product.</summary>
  public bool IsEven =>
    Strips.Length == 0 || Strips.All(t => Math.Abs(t - Strips[0]) < 1e-4f);

  /// <summary>Width of one strip at <paramref name="thickness"/>. Each strip starts with its share of the
  /// form's base width and spreads on its own, capped at its share of the form's ceiling, so the strips sum
  /// to the whole-piece width exactly.</summary>
  public float StripWidth(float thickness) =>
    RollingPass.SpreadWidth(
      Form.BaseWidth / Sides,
      Form.BaseThickness,
      thickness,
      Form.SpreadExponent,
      Form.MaxWidth / Sides
    );

  /// <summary>How many sides this piece is currently divided into.</summary>
  public int Sides => Math.Max(1, Strips.Length);

  /// <summary>Total width of the piece: the sum of its strips, each spread by its own reduction.</summary>
  public float Width => Strips.Sum(StripWidth);

  /// <summary>How far a strip at <paramref name="thickness"/> runs through the rolls. Whatever a reduction
  /// does not put into width goes into length, so a well-worked strip is long and takes longer to
  /// feed.</summary>
  public float StripLength(float thickness) =>
    Form.BaseLength
    * RollingPass.LengthMultiplier(
      Form.BaseWidth / Sides,
      Form.BaseThickness,
      thickness,
      StripWidth(thickness)
    );

  /// <summary>The same piece with side <paramref name="index"/> set to <paramref name="thickness"/>
  /// outright, clearing its turn state. Used to complete a reduction.</summary>
  public WorkPiece WithStrip(int index, float thickness) {
    float[] next = (float[])Strips.Clone();
    bool[] turned = (bool[])Turned.Clone();
    next[index] = thickness;
    turned[index] = false;
    return this with { Strips = next, Turned = turned };
  }

  /// <summary>Whether side <paramref name="index"/> has had its first pass at the current gap, so the next feed
  /// is the turn-over that finishes it.</summary>
  public bool IsTurned(int index) =>
    index >= 0 && index < Turned.Length && Turned[index];

  /// <summary>The piece after one feed of side <paramref name="index"/> toward <paramref name="gap"/>. The
  /// first feed only marks the side turned and the metal reaches the gap on the second, so an interrupted
  /// reduction leaves the thickness untouched.</summary>
  public WorkPiece Fed(int index, float gap) {
    if (index < 0 || index >= Strips.Length)
      return this;
    if (!IsTurned(index)) {
      bool[] turned = (bool[])Turned.Clone();
      turned[index] = true;
      return this with { Turned = turned };
    }
    return WithStrip(index, gap);
  }

  #region Stack round-trip

  /// <summary>Reads the piece off a stack, or null when the stack carries no work-piece state (an item that
  /// has never been rolled, or something else entirely).</summary>
  public static WorkPiece? FromStack(ItemStack? stack) {
    ITreeAttribute? tree = stack?.Attributes;
    if (tree == null)
      return null;

    string? formName = tree.GetString(FormKey);
    if (
      formName == null
      || !StockForm.All.TryGetValue(formName, out StockForm? form)
    )
      return null;

    float[]? strips = (tree[StripsKey] as FloatArrayAttribute)?.value;
    if (strips is not { Length: > 0 })
      return Fresh(form);

    bool[]? turned = (tree[TurnedKey] as BoolArrayAttribute)?.value;
    if (turned == null || turned.Length != strips.Length)
      turned = new bool[strips.Length];
    return new WorkPiece(form, strips, turned);
  }

  /// <summary>Writes this piece onto a stack, replacing any state already there.</summary>
  public void ToStack(ItemStack stack) {
    stack.Attributes.SetString(FormKey, Form.Name);
    stack.Attributes[StripsKey] = new FloatArrayAttribute(Strips);
    stack.Attributes[TurnedKey] = new BoolArrayAttribute(Turned);
  }

  #endregion
}
