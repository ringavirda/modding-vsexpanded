using System.Collections.Generic;
using ExpandedLib.Definitions;
using Vintagestory.API.Common;

namespace IronworkingExpanded.BlockStructures.Products;

/// <summary>
/// Definitions for the slag-path decorative family: path, slab and stairs. All three use vanilla classes
/// (path and slab are plain <c>Block</c>, stairs are <c>BlockStairs</c>), so there is no mod block class to
/// host <see cref="IExBlockDefProvider"/> and this standalone provider carries them. The shared gravel-path
/// surface - 11-pebble scatter texture, snow/free resistance and sound splits, held-block animations, road
/// map colour - is authored once in <see cref="Common"/>; each block adds its own shape, boxes, side flags,
/// liquid barrier and drop.
/// </summary>
public class SlagPathDefinitions : IExBlockDefProvider {
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Path(domain), Slab(domain), Stairs(domain)];

  // The slag-gravel road texture: a base pebble1 overlay plus pebble2..11 as atlas alternates, the scatter
  // that keeps a laid path from looking repetitive. The same value in all three blocktypes; only the
  // texture key differs ("all" for the full cube, "normal1" for the slab and stairs shapes). The pebble
  // overlays are vanilla's; the base is slag's own texture.
  private static object RoadTexture() {
    var alternates = new List<object>();
    for (int i = 2; i <= 11; i++)
      alternates.Add(
        new {
          @base = Items.SlagItemDefinitions.Texture,
          overlays = new[] { $"game:block/overlay/pebble{i}" },
        }
      );
    return new {
      @base = Items.SlagItemDefinitions.Texture,
      overlays = new[] { "game:block/overlay/pebble1" },
      alternates = alternates.ToArray(),
    };
  }

  // The gravel-path surface shared by all three blocktypes.
  private static ExBlockDef Common(ExBlockDef def) =>
    def.Attribute("reinforcable", true)
      .Attribute("mapColorCode", "road")
      .Attribute(
        "inContainerTexture",
        new { @base = Items.SlagItemDefinitions.Texture }
      )
      .RawByType("resistanceByType", "*-snow", 0.2)
      .RawByType("resistanceByType", "*-free", 2.4)
      .RawByType(
        "behaviorsByType",
        "*-snow",
        new[] { new { name = "BreakSnowFirst" } }
      )
      .Material(EnumBlockMaterial.Gravel)
      .Replaceable(900)
      .LightAbsorption(99)
      .SideOpaque(new { all = false, down = true })
      .HeldTpIdleAnimation("holdbothhandslarge")
      .HeldRightReadyAnimation("heldblockready")
      .HeldTpUseAnimation("twohandplaceblock")
      .Sound("place", "game:block/gravel")
      .SoundByType("break", "*-snow", "game:block/snow")
      .SoundByType("break", "*-free", "game:block/gravel")
      .SoundByType("hit", "*-snow", "game:block/snow")
      .SoundByType("hit", "*-free", "game:block/gravel")
      .Sound("walk", "game:walk/gravel");

  // The three creative tabs every slag-path block lists, with the block's own selector.
  private static ExBlockDef PathTabs(ExBlockDef def, string selector) =>
    def.CreativeTab("general", selector)
      .CreativeTab("decorative", selector)
      .CreativeTab("iwex", selector);

  private static ExBlockDef Path(string domain) =>
    PathTabs(
        Common(ExBlockDef.Create(domain, "slag-path", "slag/path")),
        "*-free"
      )
      .Behavior("Lockable")
      .VariantGroup("cover", "free", "snow")
      .Attribute("liquidBarrierOnSides", new[] { 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 })
      .Shape("game:block/basic/cube-lowered-{cover}")
      .Texture("all", RoadTexture())
      .WalkSpeedMultiplier(1.3)
      .FaceCullMode("FlushExceptTop")
      .SideSolid(new { all = true, up = false })
      .SingleSelectionBox(0f, 0f, 0f, 1f, 0.9375f, 1f)
      .SingleCollisionBox(0f, 0f, 0f, 1f, 0.9375f, 1f)
      .TpHandTransform(-1.23, -0.91, -0.8, -2, 25, -78, 0.4)
      .Drop("block", "slag-path-free");

  private static ExBlockDef Slab(string domain) =>
    PathTabs(
        Common(ExBlockDef.Create(domain, "slag-pathslab", "slag/pathslab")),
        "*-free"
      )
      .Behavior("Lockable")
      .VariantGroup("cover", "free", "snow")
      .Attribute("liquidBarrierOnSides", new[] { 0.5, 0.5, 0.5, 0.5 })
      .Shape("iwex:legacy/basic/slab/stonepath-slab-{cover}")
      .Texture("normal1", RoadTexture())
      .WalkSpeedMultiplier(1.3)
      .FaceCullMode("FlushExceptTop")
      .SideSolid(new { all = false, down = true })
      .SingleSelectionBox(0f, 0f, 0f, 1f, 0.4375f, 1f)
      .SingleCollisionBox(0f, 0f, 0f, 1f, 0.4375f, 1f)
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
      )
      .Drop("block", "slag-pathslab-free");

  private static ExBlockDef Stairs(string domain) {
    const string free = "iwex:legacy/basic/stairs/stonepath-stairs-free";
    const string snow = "iwex:legacy/basic/stairs/stonepath-stairs-snow";
    return PathTabs(
        Common(ExBlockDef.Create(domain, "slag-pathstairs", "slag/pathstairs")),
        "*-up-north-free"
      )
      .Class("BlockStairs")
      .Behavior(
        "WrenchOrientable",
        new { baseCode = "slag-pathstairs-up-*-{cover}" }
      )
      .VariantGroup("updown", "up")
      .VariantGroupFromProperties("game:abstract/horizontalorientation")
      .VariantGroup("cover", "free", "snow")
      .Attribute("noDownVariant", true)
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
      .ShapeByType("*-up-north-free", free, rotateY: 0)
      .ShapeByType("*-up-west-free", free, rotateY: 90)
      .ShapeByType("*-up-south-free", free, rotateY: 180)
      .ShapeByType("*-up-east-free", free, rotateY: 270)
      .ShapeByType("*-up-north-snow", snow, rotateY: 0)
      .ShapeByType("*-up-west-snow", snow, rotateY: 90)
      .ShapeByType("*-up-south-snow", snow, rotateY: 180)
      .ShapeByType("*-up-east-snow", snow, rotateY: 270)
      .Texture("normal1", RoadTexture())
      .WalkSpeedMultiplier(1.2)
      .FaceCullMode("NeverCull")
      .EmitSideAo(true)
      .RawByType(
        "sidesolidByType",
        "*-up-north-*",
        new {
          all = false,
          down = true,
          north = true,
        }
      )
      .RawByType(
        "sidesolidByType",
        "*-up-west-*",
        new {
          all = false,
          down = true,
          west = true,
        }
      )
      .RawByType(
        "sidesolidByType",
        "*-up-south-*",
        new {
          all = false,
          down = true,
          south = true,
        }
      )
      .RawByType(
        "sidesolidByType",
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
            y2 = 0.4375,
            z2 = 1,
          },
          new
          {
            x1 = 0,
            y1 = 0.4375,
            z1 = 0.5,
            x2 = 1,
            y2 = 0.9375,
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
      .TpHandTransform(-1.23, -0.91, -0.8, -2, 25, -78, 0.4)
      .Drop("block", "slag-pathstairs-up-north-free");
  }
}
