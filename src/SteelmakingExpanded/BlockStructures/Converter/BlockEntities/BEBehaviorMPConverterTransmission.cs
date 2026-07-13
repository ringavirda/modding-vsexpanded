using ExpandedLib.Blocks.Machines;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SteelmakingExpanded.BlockStructures.Converter.BlockEntities;

/// <summary>
/// Mechanical-power node for the Bessemer transmission. In its natural (north)
/// orientation the axle couples on the south face; the connector rotates with
/// the block's "side" variant. The transmission is a network endpoint - it only
/// feeds the converter's control block, which reads the network speed. The static
/// body render + per-axis rotation sense are handled by
/// <see cref="BEBehaviorMPSubmachineBase"/>.
/// </summary>
[BlockEntityBehaviorRegister]
public class BEBehaviorMPConverterTransmission(BlockEntity blockentity)
  : BEBehaviorMPSubmachineBase(blockentity)
{
  public override float GetResistance() => 0.25f;

  protected override BlockFacing ResolveDiscoveryFace() =>
    Block.Variant["side"] switch
    {
      "north" => BlockFacing.SOUTH,
      "east" => BlockFacing.WEST,
      "south" => BlockFacing.NORTH,
      "west" => BlockFacing.EAST,
      _ => BlockFacing.NORTH,
    };
}
