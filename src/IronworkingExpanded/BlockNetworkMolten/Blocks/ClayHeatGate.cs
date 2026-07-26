using Vintagestory.API.Common;

namespace IronworkingExpanded.BlockNetworkMolten.Blocks;

/// <summary>
/// The ceramic mold tier's ceiling: a fired-clay tool mold shatters when metal hotter than
/// <see cref="IwexValues.ClayMoldHeatCeiling"/> is poured into it. Introducing cast-iron molds makes
/// molds a tiered family, and this is the honest ceiling on the clay tier - "clay is bronze max".
/// <para>
/// Only the SMALL clay molds are gated - the ones that fit the mold pedestal (ingot, dip tool molds),
/// picked out by <see cref="MoldKinds.FitsPedestal"/>. The large anvil / helve-hammer molds are cast at
/// iron temperatures in the canal tap and are exempt. Our own cast-iron molds are a separate block class
/// (<see cref="Casting.Blocks.BlockCastMold"/>), so they never satisfy the predicate and never shatter.
/// </para>
/// <para>
/// A pure decision shared by the two pour paths: the mold pedestal's automated drain (where the loophole
/// lives - it fills a mold by draining the run directly, bypassing <c>CanReceive</c>) and the vanilla
/// crucible-pour gate (<see cref="Patches.ToolMoldHeatGatePatch"/>).
/// </para>
/// </summary>
public static class ClayHeatGate
{
  /// <summary>Ingame-error code raised (resolved from <c>game:ingameerror-{code}</c>) when a clay mold
  /// shatters, so the failure is a clear message and a crack of sound, never a silent void.</summary>
  public const string ShatterErrorCode = "iwex-clayshatter";

  /// <summary>
  /// True when pouring metal at <paramref name="pourTemp"/> °C into <paramref name="mold"/> would shatter
  /// it - i.e. it is a small fired-clay tool mold and the metal is hotter than the clay ceiling. Uses a
  /// strict comparison so a metal sitting exactly at the ceiling still casts.
  /// </summary>
  public static bool WouldShatter(Block? mold, float pourTemp) =>
    MoldKinds.FitsPedestal(mold) && pourTemp > IwexValues.ClayMoldHeatCeiling;
}
