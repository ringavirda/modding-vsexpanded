using Vintagestory.API.Common;

namespace IronIndustryExpanded.BlockNetworkMolten.Blocks;

/// <summary>
/// Heat ceiling of the ceramic mold tier: a fired-clay tool mold shatters when metal hotter than
/// <see cref="IiexValues.ClayMoldHeatCeiling"/> is poured into it. Only the small molds that fit the mold
/// pedestal are gated (<see cref="MoldKinds.FitsPedestal"/>); the large anvil and helve-hammer molds, cast
/// at iron temperatures in the canal tap, are exempt, and cast-iron molds
/// (<see cref="BlockStructures.Casting.Blocks.BlockCastMold"/>) are a separate block class the predicate never matches.
/// Both pour paths call it: the pedestal's automated drain, which bypasses <c>CanReceive</c>, and the
/// crucible-pour gate <see cref="Patches.ToolMoldHeatGatePatch"/>.
/// </summary>
public static class ClayHeatGate {
  /// <summary>Ingame-error code raised when a clay mold shatters, resolved client-side from
  /// <c>game:ingameerror-{code}</c>.</summary>
  public const string ShatterErrorCode = "iiex-clayshatter";

  /// <summary>
  /// True when pouring metal at <paramref name="pourTemp"/> °C into <paramref name="mold"/> would shatter
  /// it: a small fired-clay tool mold and metal hotter than the clay ceiling. The comparison is strict, so
  /// metal sitting exactly at the ceiling still casts.
  /// </summary>
  /// <remarks>
  /// The steel crucible is a documented exception. It holds metal far over this ceiling and passes because
  /// the test is a TYPE test: a pot is a smelting container, never a <c>BlockToolMold</c>, so the
  /// temperature is never compared. Deliberate - this ceiling governs molds, which are reused
  /// indefinitely, while the pot dies of the abuse after <c>CruciblePotFirings</c> heats. No branch is
  /// added, because there is nothing to branch on. See docs/design/machines/crucible-furnace.md.
  /// </remarks>
  public static bool WouldShatter(Block? mold, float pourTemp) =>
    MoldKinds.FitsPedestal(mold) && pourTemp > IiexValues.ClayMoldHeatCeiling;
}
