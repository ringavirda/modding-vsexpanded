using System.Collections.Generic;
using ExpandedLib.Definitions;
using Vintagestory.API.Common;

namespace SteelmakingExpanded.Molds;

/// <summary>
/// Code-first definitions for the two casting-mold blocktypes (migrated from
/// blocktypes/molds/toolmold{fired,raw}.json). Unlike the other migrations these are NOT authored on a mod
/// block class — both molds use VANILLA classes (fired = <c>BlockToolMold</c>/entity <c>ToolMold</c>, raw =
/// plain <c>Block</c>), so there is no mod type to host <see cref="IExBlockDefProvider"/>. This dedicated
/// provider carries them instead; discovery scans every concrete class in the assembly, so it is picked up and
/// its defs registered under the smex domain exactly like a block-hosted provider. The two files share one
/// <c>code</c> (<c>toolmold</c>) at distinct asset paths and most of their flat surface (<see cref="Shared"/>);
/// each adds its own class binding, behaviours, variants, textures, tong/hold transforms and per-type maps
/// (fired: fill/drop <c>attributesByType</c>; raw: beehive-kiln firing + <c>combustiblePropsByType</c>).
/// </summary>
public class ToolMoldDefinitions : IExBlockDefProvider
{
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Fired(domain), Raw(domain)];

  // The flat surface identical across both mold files (material/creative/scalars/sounds/shapes/boxes/sides +
  // the block-root gui & ground transforms). Each mold overlays its class/behaviours/variants/textures/attrs.
  private static ExBlockDef Shared(ExBlockDef def) =>
    def.Material(EnumBlockMaterial.Ceramic)
      .CreativeTab("general", "*")
      .CreativeTab("construction", "*")
      // Also surface the molds in the mod's own creative tab (an intentional add over the source JSON, which
      // only listed general + construction) so they're findable alongside the rest of smex's content.
      .CreativeTab("smex", "*")
      .Replaceable(700)
      .Resistance(1.5f)
      .MaxStackSize(8)
      .LightAbsorption(0)
      .Sound("walk", "game:walk/stone")
      .ShapeByType("*-plate", "iwex:molten/molds/plate", rotateY: 90)
      .ShapeByType("*-doubleingot", "iwex:molten/molds/doubleingot", rotateY: 90)
      .ShapeByType("*-quadrod", "iwex:molten/molds/quadrod", rotateY: 90)
      .SingleCollisionBox(0.0625f, 0f, 0.0625f, 0.9375f, 0.125f, 0.9375f)
      .SingleSelectionBox(0.0625f, 0f, 0.0625f, 0.9375f, 0.125f, 0.9375f)
      .SideOpaque(false)
      .SideSolid(false)
      .Raw(
        "guiTransform",
        new
        {
          translation = new { x = 0, y = 3, z = 0 },
          origin = new
          {
            x = 0.5,
            y = 0.0625,
            z = 0.5,
          },
          scale = 1.33,
        }
      )
      .Raw(
        "groundTransform",
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = -45, z = 0 },
          origin = new
          {
            x = 0.5,
            y = 0,
            z = 0.5,
          },
          scale = 2.2,
        }
      );

  // The metal-tong hold transform is byte-identical in both files.
  private static object MetalTongTransform =>
    new
    {
      translation = new
      {
        x = -2.1,
        y = -0.19,
        z = -1,
      },
      rotation = new { x = 180, y = 7, z = 4 },
      origin = new
      {
        x = 0.5,
        y = 0,
        z = 0.5,
      },
      scale = 0.55,
    };

  private static ExBlockDef Fired(string domain) =>
    Shared(ExBlockDef.Create(domain, "toolmold", "molds/toolmoldfired"))
      .Class("BlockToolMold")
      .EntityClass("ToolMold")
      .Behavior("Lockable")
      .Behavior("UnstableFalling")
      .EntityBehavior("TemperatureSensitive")
      .VariantGroup(
        "color",
        "blue", "fire", "black", "brown", "cream",
        "earthyorange", "gray", "orange", "red", "tan"
      )
      .VariantGroup("materialtype", "fired")
      .VariantGroup("tooltype", "plate", "doubleingot", "quadrod")
      .Texture("floor", "game:block/clay/hardened/{color}")
      .Texture("other", "game:block/clay/hardened/{color}")
      .HeldTpIdleAnimation("holdbothhandslarge")
      .Handbook("toolmold-*-{materialtype}-{tooltype}")
      .Attributes(
        new
        {
          reinforcable = true,
          shatteredShape = new { @base = "game:block/clay/mold/shattered-ingot" },
          onTongTransform = new
          {
            translation = new
            {
              x = -1.7,
              y = -1.3,
              z = -0.57,
            },
            rotation = new { x = 98, y = 16, z = 3 },
            scale = 0.55,
          },
          onMetalTongTransform = MetalTongTransform,
        }
      )
      .RawByType(
        "attributesByType",
        "toolmold-*-fired-plate",
        FiredFill(new { type = "item", code = "game:metalplate-{metal}" })
      )
      .RawByType(
        "attributesByType",
        "toolmold-*-fired-doubleingot",
        FiredFill(new { type = "item", code = "game:ingot-{metal}", quantity = 2 })
      )
      .RawByType(
        "attributesByType",
        "toolmold-*-fired-quadrod",
        FiredFill(new { type = "item", code = "game:rod-{metal}", quantity = 4 })
      )
      .Raw(
        "tpHandTransform",
        new
        {
          translation = new
          {
            x = -1.6,
            y = -1,
            z = -0.5,
          },
          rotation = new
          {
            x = 102,
            y = -16,
            z = -77,
          },
          scale = 0.55,
        }
      );

  // The fired mold's per-tooltype fill/drop attributes differ only in the produced drop.
  private static object FiredFill(object drop) =>
    new
    {
      requiredUnits = 200,
      fillHeight = 1,
      moldrackable = true,
      onmoldrackTransform = new { rotation = new { z = 90 } },
      fillQuadsByLevel = new[]
      {
        new
        {
          x1 = 2,
          z1 = 2,
          x2 = 14,
          z2 = 14,
        },
      },
      drop,
    };

  private static ExBlockDef Raw(string domain) =>
    Shared(ExBlockDef.Create(domain, "toolmold", "molds/toolmoldraw"))
      .Class("Block")
      .Behavior("GroundStorable", new { layout = "SingleCenter" })
      .Behavior("Unplaceable")
      .Behavior("RightClickPickup")
      .VariantGroup("color", "blue", "red", "fire")
      .VariantGroup("materialtype", "raw")
      .VariantGroup("tooltype", "plate", "doubleingot", "quadrod")
      .TextureAll("game:block/clay/{color}clay")
      .Handbook("toolmold-*-{materialtype}-{tooltype}")
      .Attributes(
        new
        {
          reinforcable = true,
          onTongTransform = new
          {
            translation = new
            {
              x = -0.9,
              y = -1.5,
              z = -0.6,
            },
            rotation = new { x = 117, y = 0, z = 0 },
            scale = 0.74,
          },
          shelvable = false,
          onMetalTongTransform = MetalTongTransform,
        }
      )
      .RawByType(
        "attributesByType",
        "toolmold-red-raw-*",
        Beehive(
          "smex:toolmold-tan-fired-{tooltype}",
          "smex:toolmold-orange-fired-{tooltype}",
          "smex:toolmold-red-fired-{tooltype}",
          "smex:toolmold-brown-fired-{tooltype}"
        )
      )
      .RawByType(
        "attributesByType",
        "toolmold-blue-raw-*",
        Beehive(
          "smex:toolmold-cream-fired-{tooltype}",
          "smex:toolmold-gray-fired-{tooltype}",
          "smex:toolmold-black-fired-{tooltype}",
          "smex:toolmold-black-fired-{tooltype}"
        )
      )
      .RawByType(
        "attributesByType",
        "toolmold-fire-raw-*",
        Beehive(
          "smex:toolmold-fire-fired-{tooltype}",
          "smex:toolmold-fire-fired-{tooltype}",
          "smex:toolmold-fire-fired-{tooltype}",
          "smex:toolmold-fire-fired-{tooltype}"
        )
      )
      .RawByType(
        "combustiblePropsByType",
        "toolmold-fire-raw-*",
        RawSmelt("smex:toolmold-fire-fired-{tooltype}")
      )
      .RawByType(
        "combustiblePropsByType",
        "toolmold-blue-raw-*",
        RawSmelt("smex:toolmold-blue-fired-{tooltype}")
      )
      .RawByType(
        "combustiblePropsByType",
        "toolmold-red-raw-*",
        RawSmelt("smex:toolmold-earthyorange-fired-{tooltype}")
      )
      .Raw(
        "tpHandTransform",
        new
        {
          translation = new
          {
            x = -1,
            y = -0.6,
            z = -1.05,
          },
          rotation = new { x = -87, y = 9, z = 4 },
          origin = new
          {
            x = 0.5,
            y = 0.125,
            z = 0.5,
          },
          scale = 0.5,
        }
      );

  // The raw mold's per-colour beehive-kiln firing table (kiln quality 0..3 -> the fired block it becomes).
  private static object Beehive(string q0, string q1, string q2, string q3) =>
    new
    {
      beehivekiln = new Dictionary<string, object>
      {
        ["0"] = new { type = "block", code = q0 },
        ["1"] = new { type = "block", code = q1 },
        ["2"] = new { type = "block", code = q2 },
        ["3"] = new { type = "block", code = q3 },
      },
    };

  // The raw mold's per-colour fire-pit smelting recipe (fires into the given fired block).
  private static object RawSmelt(string smeltedCode) =>
    new
    {
      meltingPoint = 650,
      meltingDuration = 45,
      smeltedRatio = 1,
      smeltingType = "fire",
      smeltedStack = new { type = "block", code = smeltedCode },
      requiresContainer = false,
    };
}
