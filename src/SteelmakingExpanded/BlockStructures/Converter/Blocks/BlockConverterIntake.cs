using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SteelmakingExpanded.BlockStructures.Converter.Blocks;

/// <summary>
/// The converter's blast intake: a fixed structure port, not a network node, exposing a single pipe
/// connector on the face it is turned to. A pipe run docks against that connector and
/// <see cref="BlockEntities.BlockEntityConverterControl"/> consumes blast from the network behind it.
/// Horizontally orientable so it can be aligned with the control block.
/// </summary>
[BlockRegister]
public partial class BlockConverterIntake
  : Block,
    INetworkConnector,
    IExBlockDefProvider {
  /// <summary>The converter blast-intake blocktype: one <c>type</c> variant, one per horizontal side.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "converter", "converter/intake")
        .Class<BlockConverterIntake>()
        .Behavior("ExOrientable")
        .Material(EnumBlockMaterial.Metal)
        .MetalSounds()
        .MaxStackSize(1)
        .CreativeCommon("*-n")
        .VariantGroup("type", "intake")
        .SideVariant()
        .ShapeByTypePerOrientation("smex:converter/intake")
        .NonSolid(),
    ];

  public string NetworkType => "pipe";

  /// <summary>
  /// The single horizontal face carrying the pipe connector, derived from the block's <c>side</c>
  /// variant (north for the north side, rotated for the others).
  /// </summary>
  public BlockFacing ConnectorFace =>
    ExOrientation.RotateFacing(
      BlockFacing.NORTH,
      ExOrientation.AngleFromSide(Variant["side"])
    );

  public bool HasConnectorAt(BlockFacing face) => face == ConnectorFace;

  public override bool CanAttachBlockAt(
    IBlockAccessor world,
    Block block,
    BlockPos pos,
    BlockFacing blockFace,
    Cuboidi attachmentArea
  ) => HasConnectorAt(blockFace) || SideSolid[blockFace.Index];
}
