using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>
/// A piece of stock part-way through a rolling schedule: its <see cref="Form"/> and the thickness of each
/// <b>strip</b> across its width. Carried on the item stack, so the piece keeps its state while it is in the
/// player's hands or lying on the ground between passes - which is most of its life, because a two-high stand
/// cannot be fed backwards and the piece has to be walked back around after every pass.
/// <para>
/// <b>Why strips.</b> A piece that has spread wider than the roll barrel cannot be taken in one bite, so it is
/// rolled in side-by-side strips - one down each side. Tracking them separately is what lets a half-rolled
/// piece exist at all: thin and wide down one side, still thick down the other, and visibly so. The pass count
/// then stops being an abstract number - four passes <em>is</em> right strip, left strip, turn over, and again.
/// </para>
/// <para>
/// <b>How many strips is not a property of the piece</b> - it is the piece measured against the barrel it is
/// being rolled on: <c>ceil(width / barrelWidth)</c>. A wide set swallows the whole piece in one, a narrow one
/// has to take it a side at a time, and that difference is the entire reason to build wide rolls. The piece
/// stores a thickness per side either way; a piece that fits is simply a one-sided piece.
/// </para>
/// <para>
/// Each side also takes <b>two feeds</b> to reach a gap: one pass, then the piece is turned over and passed
/// again, because a single bite comes out with a camber. So a gap costs <c>2 × sides</c> trips through the
/// mill - two on wide rolls, four on narrow ones once the piece has spread.
/// </para>
/// </summary>
/// <param name="Form">The stock form, which supplies the base dimensions and how it spreads.</param>
/// <param name="Strips">Thickness of each side of the piece.</param>
/// <param name="Turned">Per side, whether its first pass at the current gap is already done - so the next feed
/// is the turn-over that completes the reduction.</param>
public sealed record WorkPiece(StockForm Form, float[] Strips, bool[] Turned)
{
  /// <summary>Feeds each side needs to reach a gap: a pass, then the same pass with the piece turned over.</summary>
  public const int FeedsPerSide = 2;

  private const string FormKey = "stockForm";
  private const string StripsKey = "stripThickness";
  private const string TurnedKey = "stripTurned";

  /// <summary>
  /// How many sides a piece of <paramref name="width"/> must be rolled in on a barrel of
  /// <paramref name="barrelWidth"/>. One when it fits, more when it overhangs and has to be taken in
  /// overlapping strips.
  /// </summary>
  public static int SidesFor(float width, float barrelWidth) =>
    barrelWidth <= 0f ? 1 : Math.Max(1, (int)MathF.Ceiling(width / barrelWidth));

  /// <summary>Trips through the mill one gap costs for this piece on the given barrel.</summary>
  public static int PassesForGap(float width, float barrelWidth) =>
    FeedsPerSide * SidesFor(width, barrelWidth);

  /// <summary>A fresh piece straight off the helve: one side, at the form's as-shingled thickness. It gains
  /// sides only when it has spread wider than whatever barrel it meets.</summary>
  public static WorkPiece Fresh(StockForm form) =>
    new(form, [form.BaseThickness], [false]);

  /// <summary>
  /// The same piece re-divided for a barrel that needs <paramref name="sides"/> of it. Growing or shrinking
  /// only makes sense on an <see cref="IsEven"/> piece, so an uneven one is returned untouched - it will simply
  /// fail to bite at the next gap until its sides are levelled, which is the natural consequence rather than a
  /// special case.
  /// </summary>
  public WorkPiece Resplit(int sides)
  {
    if (sides == Strips.Length || sides < 1 || !IsEven)
      return this;
    float thickness = Strips.Length > 0 ? Strips[0] : Form.BaseThickness;
    return this with
    {
      Strips = [.. Enumerable.Repeat(thickness, sides)],
      Turned = new bool[sides],
    };
  }

  /// <summary>The thickest strip - what the piece still reads as overall, since a piece is only as finished as
  /// its least-worked side.</summary>
  public float Thickest => Strips.Length == 0 ? Form.BaseThickness : Strips.Max();

  /// <summary>The thinnest strip.</summary>
  public float Thinnest => Strips.Length == 0 ? Form.BaseThickness : Strips.Min();

  /// <summary>Whether every strip has been worked to the same thickness - i.e. the piece is evenly rolled and
  /// ready to move on to the next gap (or to come off the mill as a product).</summary>
  public bool IsEven => Strips.Length == 0 || Strips.All(t => Math.Abs(t - Strips[0]) < 1e-4f);

  /// <summary>
  /// Width of one strip at <paramref name="thickness"/>. Each strip starts with its share of the form's base
  /// width and spreads on its own, capped at its share of the form's ceiling - so summing the strips reproduces
  /// the whole-piece width exactly (pinned in the tests), and a half-rolled piece is correspondingly lopsided.
  /// </summary>
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

  /// <summary>
  /// How far a strip at <paramref name="thickness"/> runs through the rolls. Whatever a reduction does not put
  /// into width goes into length, so a well-worked strip is long - and takes correspondingly longer to feed,
  /// which is why the later passes of a schedule visibly drag.
  /// </summary>
  public float StripLength(float thickness) =>
    Form.BaseLength
    * RollingPass.LengthMultiplier(
      Form.BaseWidth / Sides,
      Form.BaseThickness,
      thickness,
      StripWidth(thickness)
    );

  /// <summary>The same piece with side <paramref name="index"/> set to <paramref name="thickness"/> outright,
  /// clearing its turn state. For tests and for completing a reduction.</summary>
  public WorkPiece WithStrip(int index, float thickness)
  {
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

  /// <summary>
  /// The piece after one feed of side <paramref name="index"/> toward <paramref name="gap"/>. The first pass
  /// only marks the side turned - the metal does not reach the gap until it has been through twice - so an
  /// interrupted reduction leaves the thickness untouched.
  /// </summary>
  public WorkPiece Fed(int index, float gap)
  {
    if (index < 0 || index >= Strips.Length)
      return this;
    if (!IsTurned(index))
    {
      bool[] turned = (bool[])Turned.Clone();
      turned[index] = true;
      return this with { Turned = turned };
    }
    return WithStrip(index, gap);
  }

  #region Stack round-trip

  /// <summary>Reads the piece off a stack, or null when the stack carries no work-piece state (a fresh item
  /// that has never been rolled, or something else entirely).</summary>
  public static WorkPiece? FromStack(ItemStack? stack)
  {
    ITreeAttribute? tree = stack?.Attributes;
    if (tree == null)
      return null;

    string? formName = tree.GetString(FormKey);
    if (formName == null || !StockForm.All.TryGetValue(formName, out StockForm? form))
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
  public void ToStack(ItemStack stack)
  {
    stack.Attributes.SetString(FormKey, Form.Name);
    stack.Attributes[StripsKey] = new FloatArrayAttribute(Strips);
    stack.Attributes[TurnedKey] = new BoolArrayAttribute(Turned);
  }

  #endregion
}
