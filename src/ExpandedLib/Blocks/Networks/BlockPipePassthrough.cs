using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// Passthrough pipe: carries gas straight through a wall. A connector butted against a
/// solid (non-air) block is not treated as a leak, so it seals against machine housings
/// without any cooperation from those blocks. A chimney capping its open top face draws gas
/// out of the run (<see cref="IChimneyVentable"/>).
/// <para>
/// Lives with the pipe base rather than with any one tier, as <see cref="BlockPipe.Segments"/> does;
/// every tier calls <see cref="Passthroughs"/> for its own domain. Unlike the segments, the geometry
/// is identical across tiers, so all tiers share the <c>iwex:pipe/passthrough</c> shape and override
/// only the sheet texture.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockPipePassthrough : BlockPipe, IChimneyVentable {
  /// <summary>Passthroughs never burst: they are embedded in walls and machine housings where a
  /// fracture would be unreachable, so they are exempt from over-pressure failure.</summary>
  public override float BurstPressure => float.MaxValue;

  /// <summary>The passthroughs for a tier <paramref name="domain"/>, registered by each tier's own
  /// provider. Yields nothing for exlib itself, which authors the factory but ships no pipe content.
  /// Must still return the real passthroughs for a tier domain:
  /// <see cref="BlockNetworkNode.AllowedOrientations"/> derives its map from them.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    domain == "exlib" ? [] : Passthroughs(domain);

  /// <summary>
  /// The passthrough + passthrough-bend blocktypes for one tier <paramref name="domain"/> (both back this
  /// class). AllowedOrientations and the fallback orientation are derived from these by the base
  /// <see cref="BlockPipe"/>.
  /// </summary>
  public static IEnumerable<ExBlockDef> Passthroughs(string domain) =>
    [Passthrough(domain), PassthroughBend(domain)];

  /// <summary>
  /// The sheet texture a tier's pipe is made of, the only thing that differs between a plated
  /// passthrough and a cast one. Overrides the <c>normal4</c> key only: <c>iron4</c>
  /// (<c>game:block/metal/sheet-plain</c>) is the trim around the opening and is the same on every
  /// tier, so repainting it would recolour the flange rather than the pipe.
  /// </summary>
  private static string Sheet(string domain) =>
    domain switch {
      "lpex" => "iwex:block/metal/castiron",
      _ => "game:block/metal/corroded/normal4",
    };

  private static readonly string[] Bricks =
  [
    "fire",
    "black",
    "brown",
    "cream",
    "gray",
    "orange",
    "red",
    "tan",
  ];

  // The ceramic-brick surface shared by both passthrough blocktypes.
  private static ExBlockDef Brick(
    string domain,
    string assetName,
    string creative,
    string handbookGroup
  ) =>
    ExBlockDef
      .Create(domain, "pipe", assetName)
      // Pinned as literals, as BlockPipe.Common does: the generic .Class<T>() / .EntityClass<T>()
      // overloads key off the definition's asset domain, not the type's owning mod, and
      // EntityRegistry scans one assembly per mod. This factory runs with a tier domain while the
      // class is registered by exlib, so a generated key would resolve to nothing and the block
      // would fall back to plain vanilla Block: no network node, no IChimneyVentable, no burst
      // exemption. Only the unresolved block entity is logged; the block half fails silently.
      .Class("exlib.BlockPipePassthrough")
      .EntityClass("exlib.BlockEntityPipePassthrough")
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
      .CreativeTab(domain, creative)
      .Handbook(handbookGroup)
      .Behavior("Lockable")
      .TextureByType(
        "*",
        "front1",
        "game:block/clay/brick/four/running/cream1",
        "game:block/clay/brick/four/running/{brick}1"
      )
      .Texture("normal4", Sheet(domain))
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(true)
      .SideOpaque(false);

  private static ExBlockDef Passthrough(string domain) =>
    Brick(
        domain,
        "pipe/passthrough",
        "*-passthrough-*-ns",
        "pipe-passthrough-*"
      )
      .VariantGroup("type", "passthrough")
      .VariantGroup("brick", Bricks)
      .VariantGroup("orientation", "ns", "we", "ud")
      .ShapeByType("*-passthrough-*-ns", "iwex:pipe/passthrough", rotateY: 0)
      .ShapeByType("*-passthrough-*-we", "iwex:pipe/passthrough", rotateY: 90)
      .ShapeByType("*-passthrough-*-ud", "iwex:pipe/passthrough", rotateX: 90);

  private static ExBlockDef PassthroughBend(string domain) {
    const string s = "iwex:pipe/passthroughbend";
    return Brick(
        domain,
        "pipe/passthroughbend",
        "*-passthroughbend-*-nw",
        "pipe-passthroughbend-*"
      )
      .VariantGroup("type", "passthroughbend")
      .VariantGroup("brick", Bricks)
      .VariantGroup(
        "orientation",
        "nw",
        "se",
        "en",
        "ws",
        "un",
        "us",
        "uw",
        "ue",
        "dn",
        "ds",
        "dw",
        "de"
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
