using ExpandedLib.Industry.MechanicalPower;
using ExpandedLib.Machines;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SteelIndustryExpanded.BlockStructures.Converter.BlockEntities;

/// <summary>
/// Mechanical-power node for the converter transmission. In its natural (north) orientation the axle
/// couples on the south face; the connector rotates with the block's "side" variant. The node is a
/// network endpoint: it feeds only the converter's control block, which reads the network speed. Static
/// body render and per-axis rotation sense come from <see cref="BEBehaviorMPSubmachineBase"/>.
/// </summary>
[BlockEntityBehaviorRegister]
public class BEBehaviorMPConverterTransmission(BlockEntity blockentity)
  : BEBehaviorMPSubmachineBase(blockentity) {
  public override float GetResistance() => 0.25f;

  protected override BlockFacing ResolveDiscoveryFace() =>
    Block.Variant["side"] switch {
      "north" => BlockFacing.SOUTH,
      "east" => BlockFacing.WEST,
      "south" => BlockFacing.NORTH,
      "west" => BlockFacing.EAST,
      _ => BlockFacing.NORTH,
    };
}
