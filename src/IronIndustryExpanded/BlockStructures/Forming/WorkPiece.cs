using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace IronIndustryExpanded.BlockStructures.Forming;

/// <summary>
/// A piece of stock part-way through a rolling schedule. Carried on the item stack, because a two-high stand
/// cannot be fed backwards and the piece is walked back around between passes. A gap costs two rounds and a
/// round is one feed per side, so the piece is one gauge across its whole width at every moment a player can
/// see it. See docs/design/processes/rolling.md § Feed arithmetic.
/// </summary>
/// <param name="Form">The stock form, which supplies the base dimensions and how it spreads.</param>
/// <param name="Thickness">The gauge of the whole piece.</param>
/// <param name="Gap">The gap this piece is half way through - set when round 1 lands its half-step, cleared
/// when round 2 lands the gap. Zero for a piece standing between gaps.</param>
/// <param name="Fed">Per side, whether it has already been through the rolls this round.</param>
/// <param name="Family">The roller family that last worked this piece, or null for one never rolled. A stage
/// is addressed by (thickness, family), so at a fork the gauge alone cannot say which branch it looks
/// like.</param>
/// <param name="Cropped">Crops already taken out of this piece. Counted up, not down, so zero means
/// untouched and the job's declared count stays the authority on how many there were.</param>
public sealed record WorkPiece(
  StockForm Form,
  float Thickness,
  float Gap,
  bool[] Fed,
  string? Family = null,
  int Cropped = 0
) {
  /// <summary>Feeds each side needs to reach a gap: the round that lands the half-step, then the round that
  /// lands the gap.</summary>
  public const int FeedsPerSide = 2;

  private const string FormKey = "stockForm";
  private const string ThicknessKey = "stockThickness";
  private const string GapKey = "stockGap";
  private const string FedKey = "stockFed";
  private const string FamilyKey = "rollerFamily";
  private const string CroppedKey = "stockCropped";

  // The pre-two-round form, when a piece carried a thickness per side and a turn flag per side.
  private const string LegacyStripsKey = "stripThickness";
  private const string LegacyTurnedKey = "stripTurned";

  /// <summary>How many sides a piece of <paramref name="width"/> must be rolled in on a barrel of
  /// <paramref name="barrelWidth"/>: one when it fits, more when it overhangs and has to be taken in
  /// side-by-side bites.</summary>
  public static int SidesFor(float width, float barrelWidth) =>
    barrelWidth <= 0f
      ? 1
      : Math.Max(1, (int)MathF.Ceiling(width / barrelWidth));

  /// <summary>Trips through the mill one gap costs for this piece on the given barrel.</summary>
  public static int PassesForGap(float width, float barrelWidth) =>
    FeedsPerSide * SidesFor(width, barrelWidth);

  /// <summary>A fresh piece off the helve: one side, at the form's as-shingled thickness.</summary>
  public static WorkPiece Fresh(StockForm form) =>
    new(form, form.BaseThickness, 0f, [false]);

  /// <summary>How many sides this piece is currently divided into.</summary>
  public int Sides => Math.Max(1, Fed.Length);

  /// <summary>
  /// The same piece divided into <paramref name="sides"/> for the barrel it is being offered to. A barrel
  /// that divides it differently starts the round over: the feeds recorded so far were of a different set of
  /// sides and say nothing about these ones. The gauge and the gap it is half way through both survive,
  /// since those are the metal rather than the tooling.
  /// </summary>
  public WorkPiece ForSides(int sides) =>
    sides < 1 || sides == Fed.Length
      ? this
      : this with {
        Fed = new bool[sides],
      };

  /// <summary>Whether side <paramref name="index"/> has already been through the rolls this round, so
  /// feeding it again would pass it through untouched.</summary>
  public bool IsFed(int index) =>
    index >= 0 && index < Fed.Length && Fed[index];

  /// <summary>
  /// The gauge a round fed at <paramref name="gap"/> lands on: the half-step half way there for a piece
  /// meeting that gap, and the gap itself for one that has already taken the half-step. Two rounds per gap
  /// is the "in, and turned back" beat, and it is why a bite is never the whole reduction.
  /// </summary>
  public float RoundTarget(float gap) =>
    ExpandedLib.Processes.StageLadder.SameThickness(Gap, gap)
      ? gap
      : (Thickness + gap) / 2f;

  /// <summary>
  /// The piece after one feed of side <paramref name="index"/> at <paramref name="gap"/>. The gauge moves
  /// only on the feed that completes the round, so an interrupted round leaves the piece exactly as it was
  /// and the sides already through it keep their credit.
  /// </summary>
  public WorkPiece Feed(int index, float gap) {
    if (index < 0 || index >= Fed.Length)
      return this;

    bool[] fed = (bool[])Fed.Clone();
    fed[index] = true;
    if (!fed.All(f => f))
      return this with { Fed = fed };

    float target = RoundTarget(gap);
    return this with {
      Thickness = target,
      // Round 1 records the gap it is half way through; round 2 lands it and the piece is between gaps again.
      Gap = ExpandedLib.Processes.StageLadder.SameThickness(target, gap)
        ? 0f
        : gap,
      Fed = new bool[fed.Length],
    };
  }

  #region Cropping

  /// <summary>Whether any of this piece has already been cut off it, which is what makes it a part piece
  /// rather than a length of stock.</summary>
  public bool IsPartCropped => Cropped > 0;

  /// <summary>How many more products a job yielding <paramref name="count"/> can still take out of this
  /// piece. The job's declared count is the authority, so retuning it moves every piece already in a world
  /// with it rather than stranding them on the number they were cut against.</summary>
  public int CropsLeft(int count) => Math.Max(0, count - Cropped);

  /// <summary>Whether a job yielding <paramref name="count"/> has nothing left to take: the piece is worked
  /// out and the last stroke is the one that removes it.</summary>
  public bool IsSpent(int count) => CropsLeft(count) <= 0;

  /// <summary>
  /// The piece after one product has been cut off it, or the piece unchanged when there was nothing left to
  /// take. Only the tally moves: a crop does not touch the gauge, so a part-cropped piece is still stock at
  /// the same stage and still knows which branch drew it.
  /// </summary>
  public WorkPiece Crop(int count) =>
    IsSpent(count) ? this : this with { Cropped = Cropped + 1 };

  #endregion

  /// <summary>Width the piece reaches at <paramref name="thickness"/> - what the reduction puts sideways,
  /// capped at the form's ceiling.</summary>
  public float WidthAt(float thickness) => Form.WidthAt(thickness);

  /// <summary>The whole piece's width at its current gauge.</summary>
  public float Width => WidthAt(Thickness);

  /// <summary>Width of one bite: the piece taken a side at a time, so a piece wider than the barrel presents
  /// only its share to the rolls. What the roll force acts across.</summary>
  public float BiteWidthAt(float thickness) => WidthAt(thickness) / Sides;

  /// <summary>
  /// How far the piece runs through the rolls at <paramref name="thickness"/>. Whatever a reduction does not
  /// put into width goes into length, so a well-worked piece is long and takes longer to feed - and every
  /// side of it is drawn through that whole length.
  /// </summary>
  public float LengthAt(float thickness) =>
    Form.BaseLength
    * RollingPass.LengthMultiplier(
      Form.BaseWidth,
      Form.BaseThickness,
      thickness,
      WidthAt(thickness)
    );

  /// <summary>The whole piece's length at its current gauge.</summary>
  public float Length => LengthAt(Thickness);

  #region Stack round-trip

  /// <summary>Reads the piece off a stack, or null when the stack is not stock at all.</summary>
  /// <remarks>
  /// Two sources, stack first. A piece that has been rolled carries its own form and gauge in the
  /// stack tree. One that has not carries nothing there - the form is on the **item type**, where
  /// <c>StockItemDefinitions</c> declared it - so the fallback is what makes fresh stock off the
  /// crafting grid a work piece at all. Without it every unrolled piece reads as "not stock", the mill
  /// refuses it as <c>WrongForm</c>, and nothing can ever take its first pass.
  /// </remarks>
  public static WorkPiece? FromStack(ItemStack? stack) {
    ITreeAttribute? tree = stack?.Attributes;
    if (tree == null)
      return null;

    string? formName =
      tree.GetString(FormKey)
      ?? stack!.Collectible?.Attributes?[FormKey]?.AsString();
    if (formName == null || !StockForm.TryGet(formName, out StockForm? form))
      return null;

    if (tree[ThicknessKey] is not FloatAttribute thickness)
      return FromLegacyStack(tree, form!);

    bool[] fed = (tree[FedKey] as BoolArrayAttribute)?.value ?? [false];
    // Absent on a piece rolled before the family was recorded, which reads as "no branch known" and falls
    // back to the composed mesh rather than guessing one.
    return new WorkPiece(
      form!,
      thickness.value,
      tree.GetFloat(GapKey),
      fed.Length > 0 ? fed : [false],
      tree.GetString(FamilyKey),
      // Absent on every piece rolled before there was a shear, which reads as uncropped - the state a piece
      // that has never met one is actually in, so no migration is owed.
      tree.GetInt(CroppedKey)
    );
  }

  // A piece rolled under the per-side model reads as its least-worked side, which is the gauge it could
  // always be fed at, and starts its round over. Nothing carried a target gap there, so nothing is lost.
  private static WorkPiece FromLegacyStack(ITreeAttribute tree, StockForm form) {
    float[]? strips = (tree[LegacyStripsKey] as FloatArrayAttribute)?.value;
    return strips is not { Length: > 0 }
      ? Fresh(form)
      : new WorkPiece(
        form,
        strips.Max(),
        0f,
        new bool[strips.Length],
        tree.GetString(FamilyKey)
      );
  }

  /// <summary>Writes this piece onto a stack, replacing any state already there.</summary>
  public void ToStack(ItemStack stack) {
    stack.Attributes.SetString(FormKey, Form.Name);
    stack.Attributes.SetFloat(ThicknessKey, Thickness);
    stack.Attributes.SetFloat(GapKey, Gap);
    stack.Attributes[FedKey] = new BoolArrayAttribute(Fed);
    if (Family != null)
      stack.Attributes.SetString(FamilyKey, Family);
    else
      stack.Attributes.RemoveAttribute(FamilyKey);

    // Removed rather than written as 0, so an uncropped piece carries no crop state at all and two of them
    // still stack together.
    if (Cropped > 0)
      stack.Attributes.SetInt(CroppedKey, Cropped);
    else
      stack.Attributes.RemoveAttribute(CroppedKey);

    // One-way: the per-side keys are never written again, so a migrated piece stops carrying both forms.
    stack.Attributes.RemoveAttribute(LegacyStripsKey);
    stack.Attributes.RemoveAttribute(LegacyTurnedKey);
  }

  #endregion
}
