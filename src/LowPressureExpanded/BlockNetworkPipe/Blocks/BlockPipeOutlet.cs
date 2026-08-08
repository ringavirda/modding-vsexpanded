using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using LowPressureExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LowPressureExpanded.BlockNetworkPipe.Blocks;

/// <summary>
/// Gas outlet: a single-faced pipe that connects the gas network to a structure's
/// face (e.g. a furnace or cowper-stove port), used to inject or extract gas there.
/// A chimney capping its open top face draws gas out of the run (<see cref="IChimneyVentable"/>).
/// </summary>
[BlockRegister]
public partial class BlockPipeOutlet : BlockPipe, IChimneyVentable
{
  /// <summary>Outlets never burst - a machine-port connector is a fixed fitting, not a length
  /// of run that should fail under pressure, so it's exempt from over-pressure failure.</summary>
  public override float BurstPressure => float.MaxValue;

  /// <summary>The gas-outlet blocktype, authored in C# (migrated from pipes/outlet.json).</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Outlet(domain)];

  private static ExBlockDef Outlet(string domain) =>
    ExBlockDef
      .Create(domain, "pipe", "pipe/outlet")
      .Class<BlockPipeOutlet>()
      .EntityClass<BlockEntityPipeOutlet>()
      .Material(EnumBlockMaterial.Ceramic)
      .Sound("walk", "game:walk/stone")
      .Sound("place", "game:block/ceramicplace")
      .SoundByTool(
        EnumTool.Pickaxe,
        "game:block/rock-hit-pickaxe",
        "game:block/rock-break-pickaxe"
      )
      .MaxStackSize(1)
      .CreativeTab("general", "*-outlet-*-n")
      .CreativeTab("lpex", "*-outlet-*-n")
      .Handbook("pipe-outlet-*")
      .Behavior("Lockable")
      .VariantGroup("type", "outlet")
      .VariantGroup(
        "brick",
        "fire", "black", "brown", "cream", "gray", "orange", "red", "tan"
      )
      .VariantGroup("orientation", "s", "n", "w", "e", "u", "d")
      .ShapeByType("*-outlet-*-s", "lpex:pipe/outlet")
      .ShapeByType("*-outlet-*-n", "lpex:pipe/outlet", rotateY: 180)
      .ShapeByType("*-outlet-*-e", "lpex:pipe/outlet", rotateY: 90)
      .ShapeByType("*-outlet-*-w", "lpex:pipe/outlet", rotateY: -90)
      .ShapeByType("*-outlet-*-u", "lpex:pipe/outlet", rotateX: -90)
      .ShapeByType("*-outlet-*-d", "lpex:pipe/outlet", rotateX: 90)
      .TextureByType(
        "*",
        "front1",
        "game:block/clay/brick/four/running/cream1",
        "game:block/clay/brick/four/running/{brick}1"
      )
      .SideSolid(true)
      .SideOpaque(false);

  public override bool HasConnectorAt(BlockFacing face) =>
    Orientation != null && Orientation.EndsWith(face.Code[0]);

  public override bool CanAttachBlockAt(
    IBlockAccessor world,
    Block block,
    BlockPos pos,
    BlockFacing blockFace,
    Cuboidi attachmentArea
  ) => HasConnectorAt(blockFace) || SideSolid[blockFace.Index];
}
