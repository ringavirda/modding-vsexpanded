using ExpandedLib.Helpers;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using HarmonyLib;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockNetworkPipe.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.Patches;

/// <summary>
/// Harmony patch on the vanilla chimney block. When a chimney caps the open top connector of a
/// passthrough or outlet pipe the network draws gas through it (see <c>PipeNetwork</c>); this postfix
/// adds a look-at info line reporting the draw.
/// </summary>
[HarmonyPatch(typeof(Block), nameof(Block.GetPlacedBlockInfo))]
public static class ChimneyVentInfoPatch {
  public static void Postfix(
    Block __instance,
    IWorldAccessor world,
    BlockPos pos,
    ref string __result
  ) {
    // Chimney test, sharing the predicate with the vent classification.
    if (!ChimneyVent.IsChimney(__instance))
      return;

    // The chimney only draws when sitting directly on a passthrough / passthrough-bend /
    // outlet whose top face is a connector.
    BlockPos below = pos.DownCopy();
    Block belowBlock = world.BlockAccessor.GetBlock(below);
    if (belowBlock is not (BlockPipePassthrough or BlockPipeOutlet))
      return;
    if (
      belowBlock is not BlockNetworkNode node
      || !node.HasConnectorAt(BlockFacing.UP)
    )
      return;

    bool hasGas =
      world.BlockAccessor.GetBlockEntity(below) is BlockEntityPipe pipe
      && pipe.Volume > 0f;

    __result +=
      Lang.Get(
        hasGas ? "iiex:chimney-info-venting" : "iiex:chimney-info-idle",
        ExMeasure.FlowRate(IiexValues.ChimneyGasDrawRate, "F0")
      ) + "\n";
  }
}
