using HarmonyLib;
using IronworkingExpanded.BlockNetworkMolten.Blocks;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronworkingExpanded.Patches;

/// <summary>
/// The ceramic mold heat ceiling on the vanilla crucible-pour path. A fired-clay tool mold cannot hold
/// iron-family metal, so a pour hotter than <see cref="IwexValues.ClayMoldHeatCeiling"/> is refused before
/// it starts (<see cref="ClayHeatGate"/>). Only the small clay molds are gated; large anvil / helve-hammer
/// molds and our own cast-iron molds fall straight through.
/// <para>
/// This mirrors, for the hand-pour path, the shatter the mold pedestal applies to its automated drain -
/// but <b>refuses</b> rather than shatters: a hand-pour is a deliberate act, and destroying a player's
/// mold on a mis-click would be a worse gotcha than a "too hot for clay" refusal. In this suite it is
/// also a defensive guard more than a live path: no metal the crucible can pour is both hot enough to trip
/// the ceiling and has a clay-mold product, so the refusal fires only if a future config gives clay a drop
/// for an iron-family metal. The pedestal is where the reachable loophole is.
/// </para>
/// <para>
/// A PREFIX that short-circuits with the refusal when the gate trips, otherwise runs vanilla's own
/// <c>CanReceive</c> unchanged. It replaces the retired <c>ToolMoldCastDomainPatch</c>, which hooked the
/// same area to make cast iron come <i>out</i> of a clay mold - the opposite of what the tier now wants.
/// </para>
/// </summary>
[HarmonyPatch]
public static class ToolMoldHeatGatePatch
{
  [HarmonyPrefix]
  [HarmonyPatch(typeof(BlockEntityToolMold), nameof(BlockEntityToolMold.CanReceive))]
  public static bool CanReceivePrefix(
    BlockEntityToolMold __instance,
    ItemStack metal,
    ref bool __result
  )
  {
    var world = __instance?.Api?.World;
    if (world == null || metal?.Collectible == null)
      return true; // nothing to judge on; let vanilla decide

    float temp = metal.Collectible.GetTemperature(world, metal);
    if (!ClayHeatGate.WouldShatter(__instance!.Block, temp))
      return true;

    __result = false; // too hot for clay - refuse the pour
    return false; // skip the original
  }
}
