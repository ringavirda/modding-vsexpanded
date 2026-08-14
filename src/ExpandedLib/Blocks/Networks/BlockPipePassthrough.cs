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
/// every tier calls <see cref="Passthroughs"/> with its own domain and tier. Unlike the segments, the
/// geometry is identical across tiers, so all tiers share the <c>iwex:pipe/passthrough</c> shape and
/// override only the sheet texture - which is exactly why the tier has to be on the code: two of these
/// are otherwise indistinguishable to the registry.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockPipePassthrough : BlockPipe, IChimneyVentable {
  /// <summary>Passthroughs never burst: they are embedded in walls and machine housings where a
  /// fracture would be unreachable, so they are exempt from over-pressure failure.</summary>
  public override float BurstPressure => float.MaxValue;

  /// <summary>The defs this class declares itself. As with <see cref="BlockPipe.Definitions"/>, only
  /// the <c>type</c> and <c>orientation</c> pairs are read from these - they feed
  /// <see cref="BlockNetworkNode.AllowedOrientations"/> - so they carry no tier and are never
  /// registered. Yields nothing for exlib itself, which authors the factory but ships no pipe
  /// content.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    domain == "exlib" ? [] : Passthroughs(domain, tier: null);

  /// <summary>
  /// The passthrough + passthrough-bend blocktypes of one <paramref name="tier"/> under
  /// <paramref name="domain"/> (both back this class). AllowedOrientations and the fallback
  /// orientation are derived from these by the base <see cref="BlockPipe"/>.
  /// <para>
  /// Tiered for the same reason the segments are, though a passthrough bears no pressure: two tiers
  /// ship a passthrough apiece, and without the axis they carry one code between them and the later
  /// registration silently replaces the earlier the moment both land in one domain.
  /// </para>
  /// </summary>
  public static IEnumerable<ExBlockDef> Passthroughs(
    string domain,
    string? tier
  ) => [Passthrough(domain, tier), PassthroughBend(domain, tier)];

  /// <summary>
  /// The sheet texture a tier's pipe is made of, the only thing that differs between a plated
  /// passthrough and a cast one. Overrides the <c>normal4</c> key only: <c>iron4</c>
  /// (<c>game:block/metal/sheet-plain</c>) is the trim around the opening and is the same on every
  /// tier, so repainting it would recolour the flange rather than the pipe.
  /// </summary>
  private static string Sheet(string? tier) =>
    tier == CastTier
      ? "iwex:block/metal/castiron"
      : "game:block/metal/corroded/normal4";

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
    string? tier,
    string assetName,
    string creative,
    string handbookGroup
  ) {
    ExBlockDef def = ExBlockDef
      .Create(domain, "pipe", assetName)
      // Typed, and safe across assemblies: KeyFor resolves the domain from the TYPE's assembly, so
      // this factory generates exlib's own keys even while running under a tier domain. It used to be
      // pinned as literals because the overloads keyed off the definition's domain instead - which
      // silently produced a key nobody registered, and the block half of that failure is not logged.
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
      .CreativeTab(domain, creative)
      // Grouped within a tier, never across: an undomained groupBy selector is qualified with the
      // block's own domain, so two tiers sharing a domain would merge into one handbook entry.
      .Handbook(
        tier == null ? $"pipe-{handbookGroup}" : $"pipe-{tier}-{handbookGroup}"
      )
      .Behavior("Lockable")
      .TextureByType(
        "*",
        "front1",
        "game:block/clay/brick/four/running/cream1",
        "game:block/clay/brick/four/running/{brick}1"
      )
      .Texture("normal4", Sheet(tier))
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(true)
      .SideOpaque(false);

    // Declared first, before `type`, exactly as the segments do it: every selector below leads with a
    // `*` so the new segment is absorbed, and declaring it last would move each code out from under
    // them silently.
    return tier == null ? def : def.VariantGroup("tier", tier);
  }

  private static ExBlockDef Passthrough(string domain, string? tier) =>
    Brick(
        domain,
        tier,
        "pipe/passthrough",
        "*-passthrough-*-ns",
        "passthrough-*"
      )
      .VariantGroup("type", "passthrough")
      .VariantGroup("brick", Bricks)
      .VariantGroup("orientation", "ns", "we", "ud")
      .ShapeByType("*-passthrough-*-ns", "iwex:pipe/passthrough", rotateY: 0)
      .ShapeByType("*-passthrough-*-we", "iwex:pipe/passthrough", rotateY: 90)
      .ShapeByType("*-passthrough-*-ud", "iwex:pipe/passthrough", rotateX: 90);

  private static ExBlockDef PassthroughBend(string domain, string? tier) {
    const string s = "iwex:pipe/passthroughbend";
    return Brick(
        domain,
        tier,
        "pipe/passthroughbend",
        "*-passthroughbend-*-nw",
        "passthroughbend-*"
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
