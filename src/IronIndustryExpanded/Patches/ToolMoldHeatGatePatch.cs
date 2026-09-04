using HarmonyLib;
using IronIndustryExpanded.BlockNetworkMolten.Blocks;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.Patches;

/// <summary>
/// The ceramic mold heat ceiling on the vanilla crucible-pour path. A fired-clay tool mold cannot hold
/// iron-family metal, so a pour hotter than <see cref="IiexValues.ClayMoldHeatCeiling"/> is refused before
/// it starts (<see cref="ClayHeatGate"/>). Only the small clay molds are gated; large anvil and
/// helve-hammer molds, and iiex's cast-iron molds, fall straight through.
/// <para>
/// The mold pedestal shatters on the same condition along its automated drain; the hand-pour path refuses
/// instead, so a mis-click costs the pour rather than the mold. The prefix short-circuits with that refusal
/// when the gate trips and otherwise runs vanilla's <c>CanReceive</c> unchanged.
/// </para>
/// </summary>
[HarmonyPatch]
public static class ToolMoldHeatGatePatch {
  [HarmonyPrefix]
  [HarmonyPatch(
    typeof(BlockEntityToolMold),
    nameof(BlockEntityToolMold.CanReceive)
  )]
  public static bool CanReceivePrefix(
    BlockEntityToolMold __instance,
    ItemStack metal,
    ref bool __result
  ) {
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
