using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using PipesAndPowerExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PipesAndPowerExpanded.BlockNetworkPipe.Blocks;

/// <summary>
/// Passthrough pipe: carries gas straight through a wall. A connector butted against a
/// solid (non-air) block is not treated as a leak, so it seals against machine housings
/// without any cooperation from those blocks.
/// </summary>
[BlockRegister]
public partial class BlockPipePassthrough : BlockPipe
{
  /// <summary>Passthroughs never burst - they're embedded in walls/machine housings where a
  /// fracture would be unreachable, so they're exempt from over-pressure failure.</summary>
  public override float BurstPressure => float.MaxValue;

  /// <summary>The passthrough + passthrough-bend blocktypes (both back this class), authored in C#
  /// (migrated from pipes/passthrough.json + pipes/passthroughbend.json). AllowedOrientations and the
  /// fallback orientation are derived from these by the base <see cref="BlockPipe"/>.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Passthrough(domain), PassthroughBend(domain)];

  private static readonly string[] Bricks =
    ["fire", "black", "brown", "cream", "gray", "orange", "red", "tan"];

  // The ceramic-brick surface shared by both passthrough blocktypes.
  private static ExBlockDef Brick(
    string domain,
    string assetName,
    string creative,
    string handbookGroup
  ) =>
    ExBlockDef
      .Create(domain, "pipe", assetName)
      .Class<BlockPipePassthrough>()
      .EntityClass<BlockEntityPipePassthrough>()
      .Material(EnumBlockMaterial.Ceramic)
      .Sound("walk", "game:walk/stone")
      .Sound("place", "game:block/ceramicplace")
      .SoundByTool(
        EnumTool.Pickaxe,
        "game:block/rock-hit-pickaxe",
        "game:block/rock-break-pickaxe"
      )
      .MaxStackSize(1)
      .CreativeTab("general", creative)
      .CreativeTab("ppex", creative)
      .Handbook(handbookGroup)
      .Behavior("Lockable")
      .TextureByType(
        "*",
        "front1",
        "game:block/clay/brick/four/running/cream1",
        "game:block/clay/brick/four/running/{brick}1"
      )
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(true)
      .SideOpaque(false);

  private static ExBlockDef Passthrough(string domain) =>
    Brick(domain, "pipes/passthrough", "*-passthrough-*-ns", "pipe-passthrough-*")
      .VariantGroup("type", "passthrough")
      .VariantGroup("brick", Bricks)
      .VariantGroup("orientation", "ns", "we", "ud")
      .ShapeByType("*-passthrough-*-ns", "ppex:pipes/passthrough", rotateY: 0)
      .ShapeByType("*-passthrough-*-we", "ppex:pipes/passthrough", rotateY: 90)
      .ShapeByType("*-passthrough-*-ud", "ppex:pipes/passthrough", rotateX: 90);

  private static ExBlockDef PassthroughBend(string domain)
  {
    const string s = "ppex:pipes/passthroughbend";
    return Brick(
        domain,
        "pipes/passthroughbend",
        "*-passthroughbend-*-nw",
        "pipe-passthroughbend-*"
      )
      .VariantGroup("type", "passthroughbend")
      .VariantGroup("brick", Bricks)
      .VariantGroup(
        "orientation",
        "nw", "se", "en", "ws", "un", "us", "uw", "ue", "dn", "ds", "dw", "de"
      )
      .ShapeByType("*-passthroughbend-*-nw", s)
      .ShapeByType("*-passthroughbend-*-en", s, rotateY: 270)
      .ShapeByType("*-passthroughbend-*-se", s, rotateY: 180)
      .ShapeByType("*-passthroughbend-*-ws", s, rotateY: 90)
      .ShapeByType("*-passthroughbend-*-dn", s, rotateZ: 90)
      .ShapeByType("*-passthroughbend-*-de", s, rotateY: 270, rotateZ: 90)
      .ShapeByType("*-passthroughbend-*-ds", s, rotateY: 180, rotateZ: 90)
      .ShapeByType("*-passthroughbend-*-dw", s, rotateY: 90, rotateZ: 90)
      .ShapeByType("*-passthroughbend-*-un", s, rotateZ: 270)
      .ShapeByType("*-passthroughbend-*-ue", s, rotateY: 270, rotateZ: 270)
      .ShapeByType("*-passthroughbend-*-us", s, rotateY: 180, rotateZ: 270)
      .ShapeByType("*-passthroughbend-*-uw", s, rotateY: 90, rotateZ: 270);
  }

  public override bool CanAttachBlockAt(
    IBlockAccessor world,
    Block block,
    BlockPos pos,
    BlockFacing blockFace,
    Cuboidi attachmentArea
  ) => SideSolid[blockFace.Index] || HasConnectorAt(blockFace);

  public override void OnNeighbourBlockChange(
    IWorldAccessor world,
    BlockPos pos,
    BlockPos neighbour
  ) { }
}
