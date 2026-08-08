using System;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>Why a piece offered to the rolls was or was not taken.</summary>
public enum FeedVerdict {
  /// <summary>The rolls bite: the strip is reduced to the chosen gap.</summary>
  Ok,

  /// <summary>No roll set is fitted, so the stand has nothing to roll with.</summary>
  NoRollSet,

  /// <summary>The fitted set does not accept this form of stock.</summary>
  WrongForm,

  /// <summary>The chosen gap is no narrower than the strip already is, so the piece would pass straight
  /// through untouched.</summary>
  NoReduction,

  /// <summary>The reduction is deeper than friction can drag in (<c>δ_max = μ²R</c>), so the rolls skid. The
  /// fix is a wider gap.</summary>
  WontBite,

  /// <summary>The stock has dropped below rolling heat, so friction has collapsed and nothing will bite. Kept
  /// separate from <see cref="WontBite"/> because the fix is a reheat rather than a wider gap.</summary>
  TooCold,
}

/// <summary>The outcome of offering a piece to the rolls, and the draft it will take if accepted.</summary>
/// <param name="Verdict">Whether the rolls take it, and why not if they do not.</param>
/// <param name="Draft">Thickness the chosen strip loses. Zero unless <see cref="FeedVerdict.Ok"/>.</param>
public readonly record struct FeedDecision(FeedVerdict Verdict, float Draft) {
  public bool Accepted => Verdict == FeedVerdict.Ok;
}

/// <summary>
/// Resolves what happens when a player offers a piece of stock to the rolls. Pure functions, callable without
/// a world. The player's two choices at the deck are where along the barrel to feed (which gap) and which
/// strip of the piece to put through; everything else follows from geometry.
/// <para>
/// There is no explicit gap-order rule. Skipping ahead is bounded by <c>δ_max = μ²R</c> alone: a gap far
/// narrower than the stock makes the rolls skid, and a gap that is too wide lets the piece pass untouched.
/// See docs/design/processes/rolling.md.
/// </para>
/// </summary>
public static class MillFeed {
  /// <summary>
  /// Which gap zone a click at <paramref name="alongBarrel"/> (0 at one end of the deck, 1 at the other) falls
  /// in, for a set with <paramref name="gapCount"/> gaps. The deck splits into equal bands, so the barrel reads
  /// left-to-right as the schedule reads widest-to-narrowest. Clamped to 0..<c>gapCount - 1</c>.
  /// </summary>
  public static int GapZone(float alongBarrel, int gapCount) {
    if (gapCount <= 1)
      return 0;
    int zone = (int)(Math.Clamp(alongBarrel, 0f, 0.999999f) * gapCount);
    return Math.Clamp(zone, 0, gapCount - 1);
  }

  /// <summary>
  /// Which strip a click takes: plain right-click works the near (right) side, sneak the far (left) one, so
  /// both halves of a piece too wide for the barrel are reachable without a second control.
  /// </summary>
  public static int StripIndex(bool sneaking, int sides) =>
    sneaking ? 0 : Math.Max(0, sides - 1);

  /// <summary>Cells the feed deck spans along the barrel (the mill's footprint is three wide).</summary>
  public const int DeckCells = 3;

  /// <summary>Deck cell offsets in the mill's own frame run from here up to 0.</summary>
  public const int DeckOriginOffset = 2;

  /// <summary>
  /// Where along the deck a click landed, as 0..1. <paramref name="localX"/> must already be in the mill's own
  /// frame (x along the barrel, principal at 0, deck running back to <c>-DeckCells + 1</c>); the caller does
  /// the rotation.
  /// </summary>
  public static float AlongBarrel(double localX) =>
    (float)Math.Clamp((localX + DeckOriginOffset) / DeckCells, 0d, 1d);

  /// <summary>
  /// Whether the rolls take <paramref name="piece"/> on strip <paramref name="strip"/> at gap
  /// <paramref name="gapIndex"/> of <paramref name="set"/>, and the draft it takes if they do. Temperatures in
  /// degrees Celsius; <paramref name="rollRadius"/> in block-space units. An out-of-range gap or strip reads as
  /// <see cref="FeedVerdict.NoReduction"/>.
  /// </summary>
  public static FeedDecision Decide(
    RollSetSpec? set,
    WorkPiece? piece,
    int gapIndex,
    int strip,
    float tempC,
    float rollRadius,
    float rollingTempC
  ) {
    if (set == null)
      return new FeedDecision(FeedVerdict.NoRollSet, 0f);
    if (piece == null || !set.AcceptsForm(piece.Form.Name))
      return new FeedDecision(FeedVerdict.WrongForm, 0f);
    if (gapIndex < 0 || gapIndex >= set.Gaps.Length)
      return new FeedDecision(FeedVerdict.NoReduction, 0f);
    if (strip < 0 || strip >= piece.Sides)
      return new FeedDecision(FeedVerdict.NoReduction, 0f);

    float gap = set.Gaps[gapIndex];
    float thickness = piece.Strips[strip];
    if (gap >= thickness)
      return new FeedDecision(FeedVerdict.NoReduction, 0f); // passes straight through

    // Checked before CanBite so cold stock reports as cold rather than as a bad gap: both fail the same bite
    // check, but one is fixed at the furnace and the other at the barrel.
    if (tempC < rollingTempC)
      return new FeedDecision(FeedVerdict.TooCold, 0f);

    float draft = thickness - gap;
    return RollingPass.CanBite(draft, rollRadius, tempC, rollingTempC)
      ? new FeedDecision(FeedVerdict.Ok, draft)
      : new FeedDecision(FeedVerdict.WontBite, draft);
  }
}
