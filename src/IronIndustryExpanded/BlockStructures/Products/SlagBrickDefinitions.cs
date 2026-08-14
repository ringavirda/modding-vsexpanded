using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;

namespace IronIndustryExpanded.BlockStructures.Products;

/// <summary>
/// Slag-brick masonry: the block, slab and stairs laid up from eight cast <c>slagbrick</c>s and mortar.
/// All three mirror vanilla's <c>stonebricks</c> family - plain cube, stone material, pickaxe-mined - and
/// use vanilla block classes, so this file is definitions only. The bricks are cast from tapped slag in the
/// bed's brick molds; the mortar comes from smex's <c>mortarfromslag</c>.
/// </summary>
public class SlagBrickDefinitions : IExBlockDefProvider {
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [SlagBricks(domain), Slab(domain), Stairs(domain)];

  // Shared by all three: cast slag behaves as stone, so the family chisels, takes a pickaxe and sounds
  // like stone, matching vanilla's stonebrick trio.
  private static ExBlockDef Common(ExBlockDef def) =>
    def.Material(EnumBlockMaterial.Stone)
      .Attribute("canChisel", true)
      .Resistance(4f)
      .MineTool(EnumTool.Pickaxe)
      .Sound("place", "game:block/stone")
      .Sound("walk", "game:walk/stone")
      .SoundByTool(
        EnumTool.Pickaxe,
        "game:block/rock-hit-pickaxe",
        "game:block/rock-break-pickaxe"
      )
      .HeldTpIdleAnimation("holdbothhandslarge")
      .HeldRightReadyAnimation("heldblockready")
      .HeldTpUseAnimation("twohandplaceblock");

  private static ExBlockDef SlagBricks(string domain) =>
    Common(ExBlockDef.Create(domain, "slag-bricks", "slag/bricks"))
      .Shape("game:block/basic/cube")
      // Base plus overlay, as vanilla's coloured bricks are built. `slagbrick` is 82 % transparent - it
      // carries only the coursing and mortar lines - so it needs the opaque cast-slag `slag` texture
      // underneath rather than being used alone.
      .Texture(
        "all",
        SlagItemDefinitions.Texture,
        SlagItemDefinitions.BrickTexture
      )
      .Attribute("mapColorCode", "settlement")
      .LightAbsorption(99)
      .Replaceable(200)
      .CreativeTab("general", "*")
      .CreativeTab("construction", "*")
      .CreativeTab("iiex", "*")
      .TpHandTransform(-1.23, -0.91, -0.8, -2, 25, -78, 0.4);

  // Vanilla's stonebrickslab: six rotations plus a snow cover, placed by OmniRotatable and re-aimed with a
  // wrench. `*-up-snow` is skipped: snow cannot sit under an upward-facing slab.
  private static ExBlockDef Slab(string domain) =>
    Common(ExBlockDef.Create(domain, "slag-brickslab", "slag/brickslab"))
      .Class("BlockSlabSnowRemove")
      .Behavior("OmniRotatable", new { rotateSides = true, facing = "block" })
      .Behavior(
        "WrenchOrientable",
        new { baseCode = "slag-brickslab-*-{cover}" }
      )
      .VariantGroup("rot", "north", "east", "south", "west", "up", "down")
      .VariantGroup("cover", "free", "snow")
      .SkipVariants("*-up-snow")
      .Attribute("chiselShapeFromCollisionBox", true)
      .AttributeByType("partialAttachableByType", "*-down", true)
      .AttributeByType("partialAttachableByType", "*-up", true)
      .AttributeByType(
        "liquidBarrierOnSidesByType",
        "*-down-*",
        new[] { 0.5, 0.5, 0.5, 0.5 }
      )
      .Replaceable(200)
      .ShapeByType("*-snow", "game:block/basic/slab/snow-slab-{rot}")
      .ShapeByType("*", "game:block/basic/slab/slab-{rot}")
      .Texture("sides", SlagItemDefinitions.BrickTexture)
      .RawByType(
        "sideSolidOpaqueAoByType",
        "*-north-*",
        new { all = false, north = true }
      )
      .RawByType(
        "sideSolidOpaqueAoByType",
        "*-east-*",
        new { all = false, east = true }
      )
      .RawByType(
        "sideSolidOpaqueAoByType",
        "*-south-*",
        new { all = false, south = true }
      )
      .RawByType(
        "sideSolidOpaqueAoByType",
        "*-west-*",
        new { all = false, west = true }
      )
      .RawByType(
        "sideSolidOpaqueAoByType",
        "*-up-*",
        new { all = false, up = true }
      )
      .RawByType(
        "sideSolidOpaqueAoByType",
        "*-down-*",
        new { all = false, down = true }
      )
      .SideAo(true)
      .RawByType("emitSideAoByType", "*-up-*", new { all = false, up = true })
      .RawByType(
        "emitSideAoByType",
        "*-down-*",
        new { all = false, down = true }
      )
      .RawByType("emitSideAoByType", "*", new { all = false })
      .Raw("collisionbox", SlabBox())
      .Raw("selectionbox", SlabBox())
      .CreativeTab("general", "*-down-free")
      .CreativeTab("construction", "*-down-free")
      .CreativeTab("iiex", "*-down-free")
      .Drop("block", "slag-brickslab-down-free")
      .Raw(
        "tpHandTransform",
        new {
          translation = new {
            x = -1.49,
            y = -0.22,
            z = -0.7,
          },
          rotation = new {
            x = 6,
            y = 16,
            z = 98,
          },
          origin = new {
            x = 0.5,
            y = 0.25,
            z = 0.5,
          },
          scale = 0.4,
        }
      );

  // The half-height box, rotated per placement exactly as vanilla's slab does.
  private static object SlabBox() =>
    new {
      x1 = 0,
      y1 = 0,
      z1 = 0,
      x2 = 1,
      y2 = 0.5,
      z2 = 1,
      rotateXByType = new Dictionary<string, object> {
        ["*-north-*"] = 90,
        ["*-south-*"] = 270,
        ["*-up-*"] = 180,
        ["*-down-*"] = 0,
      },
      rotateZByType = new Dictionary<string, object> {
        ["*-east-*"] = 90,
        ["*-west-*"] = 270,
      },
    };

  // Vanilla's stonebrickstairs, limited to the up-facing half. A down-facing stair needs a separate art
  // set this family does not have.
  private static ExBlockDef Stairs(string domain) {
    const string free = "game:block/basic/stairs/normal";
    const string snow = "game:block/basic/stairs/snow-normal";
    return Common(
        ExBlockDef.Create(domain, "slag-brickstairs", "slag/brickstairs")
      )
      .Class("BlockStairs")
      .Behavior(
        "WrenchOrientable",
        new { baseCode = "slag-brickstairs-up-*-{cover}" }
      )
      .VariantGroup("updown", "up")
      .VariantGroupFromProperties("game:abstract/horizontalorientation")
      .VariantGroup("cover", "free", "snow")
      .Attribute("noDownVariant", true)
      .Attribute("mapColorCode", "settlement")
      .Attribute("chiselShapeFromCollisionBox", true)
      .Attribute("partialAttachable", true)
      .AttributeByType(
        "liquidBarrierOnSidesByType",
        "*-up-north-*",
        new[] { 1.0, 0.5, 0.5, 0.5 }
      )
      .AttributeByType(
        "liquidBarrierOnSidesByType",
        "*-up-south-*",
        new[] { 0.5, 0.5, 1.0, 0.5 }
      )
      .AttributeByType(
        "liquidBarrierOnSidesByType",
        "*-up-west-*",
        new[] { 0.5, 0.5, 0.5, 1.0 }
      )
      .AttributeByType(
        "liquidBarrierOnSidesByType",
        "*-up-east-*",
        new[] { 0.5, 1.0, 0.5, 0.5 }
      )
      .Replaceable(160)
      .ShapeByType("*-up-north-free", free, rotateY: 0)
      .ShapeByType("*-up-west-free", free, rotateY: 90)
      .ShapeByType("*-up-south-free", free, rotateY: 180)
      .ShapeByType("*-up-east-free", free, rotateY: 270)
      .ShapeByType("*-up-north-snow", snow, rotateY: 0)
      .ShapeByType("*-up-west-snow", snow, rotateY: 90)
      .ShapeByType("*-up-south-snow", snow, rotateY: 180)
      .ShapeByType("*-up-east-snow", snow, rotateY: 270)
      .Texture("sides", SlagItemDefinitions.BrickTexture)
      .FaceCullModeByTypeStairs()
      .RawByType(
        "sideSolidOpaqueAoByType",
        "*-up-north-*",
        new {
          all = false,
          down = true,
          north = true,
        }
      )
      .RawByType(
        "sideSolidOpaqueAoByType",
        "*-up-west-*",
        new {
          all = false,
          down = true,
          west = true,
        }
      )
      .RawByType(
        "sideSolidOpaqueAoByType",
        "*-up-south-*",
        new {
          all = false,
          down = true,
          south = true,
        }
      )
      .RawByType(
        "sideSolidOpaqueAoByType",
        "*-up-east-*",
        new {
          all = false,
          down = true,
          east = true,
        }
      )
      .RawByType(
        "collisionSelectionBoxesByType",
        "*-up-*",
        new object[]
        {
          new
          {
            x1 = 0,
            y1 = 0,
            z1 = 0,
            x2 = 1,
            y2 = 0.5,
            z2 = 1,
          },
          new
          {
            x1 = 0,
            y1 = 0.5,
            z1 = 0.5,
            x2 = 1,
            y2 = 1,
            z2 = 1,
            rotateYByType = new Dictionary<string, object>
            {
              ["*-north-*"] = 180,
              ["*-east-*"] = 90,
              ["*-south-*"] = 0,
              ["*-west-*"] = 270,
            },
          },
        }
      )
      .CreativeTab("general", "*-up-north-free")
      .CreativeTab("construction", "*-up-north-free")
      .CreativeTab("iiex", "*-up-north-free")
      .Drop("block", "slag-brickstairs-up-north-free")
      .TpHandTransform(-1.23, -0.91, -0.8, -2, 25, -78, 0.4);
  }
}

file static class SlagBrickDefExtensions {
  /// <summary>Stairs cull as stairs when bare and never when snow-capped, matching vanilla.</summary>
  internal static ExBlockDef FaceCullModeByTypeStairs(this ExBlockDef def) =>
    def.RawByType("faceCullModeByType", "*-snow", "NeverCull")
      .RawByType("faceCullModeByType", "*-free", "Stairs");
}
