using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Items;

/// <summary>
/// Code-first itemtype definitions for iwex's slag by-products (migrated from itemtypes/slag.json and
/// itemtypes/powderedslag.json). Both use the vanilla <c>Item</c> class, so a dedicated stand-alone provider
/// carries them (see <see cref="IExItemDefProvider"/>). Slag is a smeltable/grindable gravel-textured lump;
/// powdered slag is its ground form, a phosphate fertiliser.
/// </summary>
public class SlagItemDefinitions : IExItemDefProvider
{
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Slag(domain), PowderedSlag(domain)];

  // The surface both share: a 64 stack, the phyllite gravel texture, and the general/iwex creative tabs.
  private static ExItemDef Common(ExItemDef def) =>
    def.MaxStackSize(64)
      .TextureAll("game:block/stone/gravel/phyllite")
      .CreativeCommon("*");

  private static ExItemDef Slag(string domain) =>
    Common(ExItemDef.Create(domain, "slag"))
      .Shape("game:item/ore/ungraded/coke")
      .MaterialDensity(800)
      .GrindingProps(
        new { groundStack = new { type = "item", code = "iwex:powderedslag" } }
      )
      .CombustibleProps(new { meltingPoint = 720 })
      .Attribute("shatteredStack", new { type = "item", code = "iwex:slag" })
      .GuiTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 150, y = -38, z = 0 },
          origin = new { x = 0.5, y = 0.1, z = 0.5 },
          scale = 3.8,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -0.93, y = -0.19, z = -0.77 },
          rotation = new { x = -48, y = -180, z = 23 },
          origin = new { x = 0.5, y = 0.12, z = 0.5 },
          scale = 0.6,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 4.5,
        }
      );

  private static ExItemDef PowderedSlag(string domain) =>
    Common(ExItemDef.Create(domain, "powderedslag"))
      .Shape("game:item/food/flour")
      .MaterialDensity(500)
      .Attributes(
        new
        {
          dissolveInWater = true,
          fertilizerProps = new
          {
            n = 0,
            p = 20,
            k = 5,
            permaboost = new
            {
              n = 0,
              p = 5,
              k = 0,
              code = "powderedslag",
            },
          },
          fertilizerTextureCode = "potash",
        }
      )
      .GuiTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 149, y = 12, z = 0 },
          origin = new { x = 0.41, y = -0.1, z = 0.8 },
          scale = 2.54,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -1.87, y = -1.25, z = -0.8 },
          rotation = new { x = 70, y = 11, z = -65 },
          scale = 0.41,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0.45, z = 0 },
          rotation = new { x = 0.1, y = 8, z = -0.1 },
          scale = 4.5,
        }
      );
}
