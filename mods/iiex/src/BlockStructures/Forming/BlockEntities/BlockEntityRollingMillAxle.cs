using ExpandedLib.Blocks;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Forming.BlockEntities;

/// <summary>
/// Block entity for a rolling-mill axle cell: an inert pass-through node of the <c>mpenergy</c> run, so
/// the mill's three-cell drive line forms one connected bus, drivable from either end and able to chain
/// stands on a shared line. It stores and draws no energy; the consumer is the mill principal
/// (<see cref="BlockEntityRollingMill"/>), which places and clears these cells and stamps
/// <see cref="Principal"/> so a break here takes the whole machine.
/// </summary>
[BlockEntityRegister]
public class BlockEntityRollingMillAxle : BlockEntityNetworkNode {
  public override string NetworkType {
    get => "mpenergy";
    set { }
  }

  /// <summary>The mill principal that owns this axle cell (<c>null</c> only if orphaned).</summary>
  [Persist("pr")]
  public BlockPos? Principal { get; set; }
}
