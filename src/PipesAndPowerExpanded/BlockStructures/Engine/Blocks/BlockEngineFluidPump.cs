using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PipesAndPowerExpanded.BlockStructures.Engine.Blocks;

/// <summary>
/// The fluid-pump sub-machine. Exposes pipe connectors on its bottom (water source)
/// and left (delivery) faces, rotated to the placed orientation.
/// </summary>
[BlockRegister]
public partial class BlockEngineFluidPump
  : BlockEngineSubmachine,
    INetworkConnector,
    IExBlockDefProvider
{
  /// <summary>The fluid-pump sub-machine blocktype, authored in C# (migrated from engine/fluidpump.json).</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "enginefluidpump", "engine/fluidpump")
        .Class<BlockEngineFluidPump>()
        .EntityClass("ppex.BlockEntityEngineFluidPump")
        .Behavior("HorizontalOrientable")
        .EntityBehavior("Animatable")
        .Material(EnumBlockMaterial.Metal)
        .VariantGroupFromProperties("side", "abstract/horizontalorientation")
        .CreativeCommon("*-north")
        .ShapeByTypeSpunPerOrientation("ppex:engine/fluidpump")
        .NonSolid(),
    ];

  public string NetworkType => "pipe";

  private BlockFacing LeftFace =>
    ExOrientation.RotateFacing(
      BlockFacing.WEST,
      ExOrientation.AngleFromSide(Variant["side"])
    );

  public bool HasConnectorAt(BlockFacing face) =>
    face == BlockFacing.DOWN || face == LeftFace;
}
