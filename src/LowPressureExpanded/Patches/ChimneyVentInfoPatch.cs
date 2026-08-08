using ExpandedLib.Blocks.Networks;
using ExpandedLib.Networks;
using HarmonyLib;
using IronworkingExpanded;
using LowPressureExpanded.BlockNetworkPipe.Blocks;
using ExpandedLib.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace LowPressureExpanded.Patches;

/// <summary>
/// Harmony patch on the vanilla chimney block: when a chimney caps the open top connector of one
/// of our passthrough/outlet pipes, the network draws gas through it (see <c>PipeNetwork</c>); this
/// postfix adds a look-at info line so the player sees it venting.
/// </summary>
[HarmonyPatch(typeof(Block), nameof(Block.GetPlacedBlockInfo))]
public static class ChimneyVentInfoPatch
{
  public static void Postfix(
    Block __instance,
    IWorldAccessor world,
    BlockPos pos,
    ref string __result
  )
  {
    // Only chimney blocks (shared predicate with the vent classification); everything else
    // passes through untouched.
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
        hasGas ? "lpex:chimney-info-venting" : "lpex:chimney-info-idle",
        ExMeasure.FlowRate(IwexValues.ChimneyGasDrawRate, "F0")
      ) + "\n";
  }
}
