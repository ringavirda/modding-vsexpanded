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
/// <b>A passthrough is a pipe run through a brick wall - it is not a fitting.</b> That is why it lives
/// here rather than with any one tier: it belongs to whoever owns the pipe base, exactly as
/// <see cref="BlockPipe.Segments"/> does, and every tier calls <see cref="Passthroughs"/> for its own
/// domain. A tier that had pipe but no passthrough would have no way through a wall.
/// </para>
/// <para>
/// <b>The tiers differ by one texture.</b> Unlike the segments - plated, cast and rolled are genuinely
/// different models, which is why each ships its own shape - a passthrough is the same geometry
/// everywhere, so all tiers share <c>iwex:pipe/passthrough</c> and override only the sheet.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockPipePassthrough : BlockPipe, IChimneyVentable
{
  /// <summary>Passthroughs never burst - they're embedded in walls/machine housings where a
  /// fracture would be unreachable, so they're exempt from over-pressure failure.</summary>
  public override float BurstPressure => float.MaxValue;

  /// <summary>The passthroughs for a tier <paramref name="domain"/>. Every tier registers them through
  /// its own thin provider (iwex's <c>PlatedPipeDefinitions</c>, lpex's <c>CastPipeDefinitions</c>); the
  /// framework's own scan gets nothing - exlib authors the factory but ships no pipe content itself. The
  /// declared factory must still return the real passthroughs for tier domains:
  /// <see cref="BlockNetworkNode.AllowedOrientations"/> derives its map from it.</summary>
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
  /// The sheet a tier's pipe is made of - the <b>only</b> thing that differs between a plated passthrough
  /// and a cast one.
  /// <para>
  /// This overrides <c>normal4</c> and deliberately leaves <c>iron4</c>
  /// (<c>game:block/metal/sheet-plain</c>) alone: that key is the <b>trim</b> around the opening, which is
  /// the same on every tier. Repainting it would recolour the flange rather than the pipe.
  /// </para>
  /// </summary>
  private static string Sheet(string domain) =>
    domain switch
    {
      "lpex" => "iwex:block/metal/castiron",
      _ => "game:block/metal/corroded/normal4",
    };

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
      // Pinned as literals, exactly as BlockPipe.Common does, and for a reason that is invisible
      // from here: the generic `.Class<T>()` / `.EntityClass<T>()` overloads key off the definition's
      // asset domain, not the type's owning mod. This factory is called by lpex
      // (`CastPipeDefinitions`) with `domain == "lpex"`, so it emitted `lpex.BlockPipePassthrough` /
      // `lpex.BlockEntityPipePassthrough` - keys nobody registers, because the class lives in and is
      // registered by exlib, and `EntityRegistry` scans one assembly per mod.
      // The block half was the worse half. The logged warning named only the block entity, but the
      // block class was equally unbound, so every `lpex:pipe-passthrough*` silently fell back to plain
      // vanilla `Block`: no network node, no IChimneyVentable, no burst exemption - and hpex's
      // Lancashire boiler builds out of these. A warning about a missing BE was hiding a whole tier of
      // pipe that was not a pipe.
      // iwex's own passthroughs looked fine only because there the domain coincidentally matched.
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
    Brick(domain, "pipe/passthrough", "*-passthrough-*-ns", "pipe-passthrough-*")
      .VariantGroup("type", "passthrough")
      .VariantGroup("brick", Bricks)
      .VariantGroup("orientation", "ns", "we", "ud")
      .ShapeByType("*-passthrough-*-ns", "iwex:pipe/passthrough", rotateY: 0)
      .ShapeByType("*-passthrough-*-we", "iwex:pipe/passthrough", rotateY: 90)
      .ShapeByType("*-passthrough-*-ud", "iwex:pipe/passthrough", rotateX: 90);

  private static ExBlockDef PassthroughBend(string domain)
  {
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
