using ExpandedLib;
using ExpandedLib.Blocks.Networks;
using PipesAndPowerExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Common;

namespace PipesAndPowerExpanded.BlockNetworkPipe;

/// <summary>
/// Shared water-transfer helpers for the two fluid pumps - the engine sub-machine
/// <see cref="BlockStructures.Engine.BlockEntities.BlockEntityEngineFluidPump"/> and the hand-cranked
/// <see cref="BlockStructures.ManualPump.BlockEntities.BlockEntityManualFluidPump"/>. Both move water
/// the same way (the <see cref="BlockEntityFluidIntake"/> on the source line is the generator; the pump
/// only transfers), so the "find the intake" and "how much can the delivery line still take" logic lives
/// here once instead of being copy-pasted into each pump.
/// </summary>
public static class FluidPumpCore
{
  /// <summary>The first fluid intake on <paramref name="net"/> that can currently draw water, or <c>null</c>.</summary>
  public static BlockEntityFluidIntake? FindIntake(
    IBlockAccessor ba,
    PipeNetwork? net
  )
  {
    if (net == null)
      return null;
    foreach (var p in net.Nodes)
    {
      if (
        ba.GetBlockEntity(p) is BlockEntityFluidIntake intake && intake.CanIntake
      )
        return intake;
    }
    return null;
  }

  /// <summary>Litres of water the output network can still accept.</summary>
  public static float OutputFreeCapacity(PipeNetwork? net) =>
    net == null
      ? 0f
      : net.Nodes.Count * ExlibValues.LitresPerPipe - (net.State?.Volume ?? 0f);
}
