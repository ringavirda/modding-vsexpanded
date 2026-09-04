using ExpandedLib.Blocks.Networks;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
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
  public BlockPos? Principal { get; set; }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    if (Principal != null) {
      tree.SetInt("prX", Principal.X);
      tree.SetInt("prY", Principal.Y);
      tree.SetInt("prZ", Principal.Z);
    }
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    Principal = tree.HasAttribute("prX")
      ? new BlockPos(tree.GetInt("prX"), tree.GetInt("prY"), tree.GetInt("prZ"))
      : null;
  }
}
