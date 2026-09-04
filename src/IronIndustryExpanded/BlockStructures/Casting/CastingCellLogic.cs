namespace IronIndustryExpanded.BlockStructures.Casting;

/// <summary>How much sand is rammed into a casting cell.</summary>
public enum SandLevel {
  /// <summary>The bare brick shell - no sand.</summary>
  Empty,

  /// <summary>
  /// Half-rammed. Nothing produces this state any more (shake-out returns straight to <see cref="Full"/>); it
  /// is kept because saved cells still carry it, and one ram clears them.
  /// </summary>
  Half,

  /// <summary>Fully rammed - ready to take a pattern impression.</summary>
  Full,
}

/// <summary>What a right-click on a casting cell resolves to, given what the player holds and the cell's state.</summary>
public enum CellAction {
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
/// The casting cell's interaction and cast-outcome rules as pure functions, callable without a world. The
/// block entity only orchestrates: it reads state, calls <see cref="Decide"/>, and applies the result.
/// See docs/design/machines/casting-cell.md.
/// </summary>
public static class CastingCellLogic {
  /// <summary>
  /// Resolves a right-click into the one action it performs. Precedence is fixed: a hardened cast is collected
  /// first, a still-liquid cast answers an empty-hand click with <see cref="CellAction.TooHot"/> rather than
  /// falling through, and ram/imprint apply only with the matching held item and an empty, impression-free cell.
  /// </summary>
  /// <param name="holdingSand">The player holds green sand (see <see cref="IsMoldingSand"/>).</param>
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
  ) {
    // Metal present: an empty hand either shakes it out (hardened) or is refused (still hot). Nothing else
    // may touch a cell holding metal - no re-ramming, no re-patterning.
    if (hasMetal) {
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

  /// <summary>
  /// The sand level a cell is at once its casting is shaken out: full again. Shake-out wrecks the impression,
  /// not the bed, so sand is rammed once per cell and only the pattern is re-impressed per casting.
  /// </summary>
  public const SandLevel AfterShakeOut = SandLevel.Full;

  /// <summary>The one material a cell may be rammed with - prepared green sand, not raw sand.</summary>
  public const string GreenSandCode = "iiex:" + GreenSandItemDefinitions.Code;

  /// <summary>
  /// Whether <paramref name="itemCode"/> is molding sand the cell will take. Only prepared green sand
  /// qualifies; raw sand has no binder and holds no impression. Matches the full code rather than a path
  /// prefix, so another domain's <c>greensand</c> does not satisfy the cell.
  /// </summary>
  public static bool IsMoldingSand(string? itemCode) =>
    itemCode == GreenSandCode;

  /// <summary>
  /// Whether a completed pour is a misrun: the cavity filled while the metal was below the pattern's minimum
  /// pour temperature, so it yields scrap rather than the part. Temperatures in degrees Celsius; a
  /// <paramref name="minPourTemp"/> of 0 disables the check.
  /// </summary>
  /// <param name="cavityFull">The impression holds its whole capacity.</param>
  /// <param name="pourTemp">The metal's temperature when the cavity filled, not at shake-out; null when
  /// no pour was recorded, which never counts as a misrun.</param>
  /// <param name="minPourTemp">The pattern's minimum pour temperature.</param>
  public static bool IsMisrun(
    bool cavityFull,
    float? pourTemp,
    float minPourTemp
  ) =>
    cavityFull
    && minPourTemp > 0f
    && pourTemp is { } poured
    && poured < minPourTemp;

  /// <summary>
  /// Whether the cell can draw metal from its feed: an impression is present and the cavity is neither full
  /// nor solidified.
  /// </summary>
  public static bool CanIntake(
    bool hasImpression,
    bool cavityFull,
    bool solidified
  ) => hasImpression && !cavityFull && !solidified;

  /// <summary>The flat rammed-sand mesh, shown once the cell is fully rammed but not yet impressed.</summary>
  public const string BaseFillShape = "iiex:casting/cell-filling-base";

  /// <summary>The half-height sand of a <see cref="SandLevel.Half"/> cell. Nothing reaches that state any more
  /// (see <see cref="AfterShakeOut"/>); this renders saved ones until they are next rammed.</summary>
  public const string HalfFillShape = "iiex:casting/cell-filling-half";

  /// <summary>
  /// The rammed-sand filling shape for the cell's current state, or <c>null</c> for the bare brick shell. A
  /// fully-rammed cell shows the pattern's cavity (<paramref name="impressionShape"/>) once impressed, and
  /// falls back to the flat full sand when an impression is present but its shape is unresolved. The entity
  /// only loads and tesselates whatever this returns.
  /// </summary>
  public static string? FillingShape(
    SandLevel sand,
    bool hasImpression,
    string? impressionShape
  ) =>
    sand switch {
      SandLevel.Empty => null,
      SandLevel.Half => HalfFillShape,
      SandLevel.Full => hasImpression
        ? impressionShape ?? BaseFillShape
        : BaseFillShape,
      _ => null,
    };
}
