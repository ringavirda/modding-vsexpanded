namespace IronworkingExpanded.BlockStructures.Casting;

/// <summary>How much sand is rammed into a casting cell.</summary>
public enum SandLevel
{
  /// <summary>The bare brick shell - no sand.</summary>
  Empty,

  /// <summary>Half-rammed - the state a cell drops to after shake-out; one ram returns it to full.</summary>
  Half,

  /// <summary>Fully rammed - ready to take a pattern impression.</summary>
  Full,
}

/// <summary>What a right-click on a casting cell resolves to, given what the player holds and the cell's state.</summary>
public enum CellAction
{
  /// <summary>Nothing to do - fall through to the default block interaction.</summary>
  None,

  /// <summary>Ram the held sand into the cell (Empty/Half → Full).</summary>
  RamSand,

  /// <summary>Ram the held pattern into full sand, forming the impression.</summary>
  Imprint,

  /// <summary>Collect the hardened casting (shake-out).</summary>
  Harvest,

  /// <summary>The metal is still liquid - refuse the shake-out.</summary>
  TooHot,
}

/// <summary>
/// The casting cell's interaction and cast-outcome rules, as pure functions so they can be pinned without
/// a world (the block entity only orchestrates: it reads state, calls <see cref="Decide"/>, and applies
/// the result). Mirrors the pig bed's split of testable arithmetic from in-game wiring.
/// </summary>
public static class CastingCellLogic
{
  /// <summary>
  /// Resolves a right-click into the one action it should perform. Precedence matters: a hardened cast is
  /// collected before anything else, a still-liquid cast blocks the empty-hand click with a "too hot"
  /// refusal (never a fall-through), and ram/imprint only apply with the matching held item and an empty,
  /// impression-free cell.
  /// </summary>
  /// <param name="holdingSand">The player holds a sand block.</param>
  /// <param name="holdingPattern">The player holds a mold pattern.</param>
  /// <param name="emptyHand">The player's active hand is empty.</param>
  /// <param name="sand">Current sand level.</param>
  /// <param name="hasImpression">A pattern has been rammed in (an impression is present).</param>
  /// <param name="hasMetal">The cell currently holds metal (liquid or solidified).</param>
  /// <param name="isHardened">That metal has cooled past the hardened threshold.</param>
  public static CellAction Decide(
    bool holdingSand,
    bool holdingPattern,
    bool emptyHand,
    SandLevel sand,
    bool hasImpression,
    bool hasMetal,
    bool isHardened
  )
  {
    // Metal present: an empty hand either shakes it out (hardened) or is refused (still hot). Nothing
    // else may touch a cell with metal in it - no re-ramming, no re-patterning.
    if (hasMetal)
    {
      if (emptyHand)
        return isHardened ? CellAction.Harvest : CellAction.TooHot;
      return CellAction.None;
    }

    if (holdingSand && sand != SandLevel.Full)
      return CellAction.RamSand;

    if (holdingPattern && sand == SandLevel.Full && !hasImpression)
      return CellAction.Imprint;

    return CellAction.None;
  }

  /// <summary>The sand level a cell drops to once its casting is shaken out.</summary>
  public const SandLevel AfterShakeOut = SandLevel.Half;

  /// <summary>
  /// Whether a completed pour is a <b>misrun</b>: the cavity filled but the metal was below the pattern's
  /// minimum pour temperature when it did, so it comes out as scrap rather than the part. A
  /// <paramref name="minPourTemp"/> of 0 disables the check (always a good cast).
  /// </summary>
  public static bool IsMisrun(bool cavityFull, float metalTemp, float minPourTemp) =>
    cavityFull && minPourTemp > 0f && metalTemp < minPourTemp;

  /// <summary>
  /// Whether the cell is currently able to draw metal from its feed: an impression is present and the
  /// cavity is neither full nor already solidified. (The cell pulls only while there is somewhere for the
  /// metal to go and it is still liquid enough to matter.)
  /// </summary>
  public static bool CanIntake(bool hasImpression, bool cavityFull, bool solidified) =>
    hasImpression && !cavityFull && !solidified;

  /// <summary>
  /// Whether the rammed sand block survives shake-out and returns to the player. Green sand is
  /// reconditioned and reused for years - a foundry replaces only a few percent - so it comes back by
  /// default; a <paramref name="returnChance"/> below 1 loses a small fraction each heat, so sand is a live
  /// consumable rather than purely ceremonial. At or above 1 it always returns; <paramref name="roll"/> is a
  /// 0..1 draw. Pure so the loss policy can be pinned without an RNG or a world.
  /// </summary>
  public static bool SandIsReturned(float returnChance, double roll) =>
    returnChance >= 1f || roll < returnChance;

  /// <summary>The flat rammed-sand mesh, shown once the cell is fully rammed but not yet impressed.</summary>
  public const string BaseFillShape = "iwex:casting/cell-filling-base";

  /// <summary>The half-height sand the cell drops to after shake-out (<see cref="AfterShakeOut"/>).</summary>
  public const string HalfFillShape = "iwex:casting/cell-filling-half";

  /// <summary>
  /// Which rammed-sand filling shape the cell should render for its current state, or <c>null</c> for the
  /// bare brick shell (no sand). A bare cell shows nothing; a half-rammed cell shows the flat half sand; a
  /// fully-rammed cell shows the pattern's cavity (<paramref name="impressionShape"/>) once impressed, else
  /// the flat full sand - falling back to the flat full sand if an impression is present but its shape is
  /// unresolved. Pure so the state→shape mapping can be pinned without a world; the entity only loads and
  /// tesselates whatever this returns.
  /// </summary>
  public static string? FillingShape(SandLevel sand, bool hasImpression, string? impressionShape) =>
    sand switch
    {
      SandLevel.Empty => null,
      SandLevel.Half => HalfFillShape,
      SandLevel.Full => hasImpression ? impressionShape ?? BaseFillShape : BaseFillShape,
      _ => null,
    };
}
