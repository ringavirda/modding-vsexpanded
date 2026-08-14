using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Engine;
using IronIndustryExpanded.BlockStructures.Engine.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SteelmakingExpanded.BlockStructures.Engine.Blocks;

/// <summary>
/// The air-blower sub-machine. Exposes a single pipe connector on its left face, rotated to the placed
/// orientation, through which it pushes pressurised air.
/// </summary>
[BlockRegister]
public partial class BlockEngineAirBlower
  : BlockEngineSubmachine,
    INetworkConnector,
    IExBlockDefProvider {
  /// <summary>The air-blower sub-machine blocktype, one per horizontal side.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "engineairblower", "engine/airblower")
        .Class<BlockEngineAirBlower>()
        .EntityClass("smex.BlockEntityEngineAirBlower")
        .Behavior("ExOrientable")
        .EntityBehavior("Animatable")
        .Material(EnumBlockMaterial.Metal)
        .SideVariant()
        .CreativeCommon("*-n")
        .ShapeByTypePerOrientation("smex:engine/airblower")
        .NonSolid(),
    ];

  public string NetworkType => "pipe";

  private BlockFacing LeftFace =>
    ExOrientation.RotateFacing(
      BlockFacing.WEST,
      ExOrientation.AngleFromSide(Variant["side"])
    );

  public bool HasConnectorAt(BlockFacing face) => face == LeftFace;
}
