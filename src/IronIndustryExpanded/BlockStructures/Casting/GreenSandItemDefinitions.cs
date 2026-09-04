using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.BlockStructures.Casting;

/// <summary>
/// Green sand: the molding sand a casting cell is rammed with. Silica sand blended with a clay binder and
/// damped; "green" means moist and uncured, not the colour. It is a single prepared item with no variants,
/// so the cell does not have to remember which sand was rammed into it, and any vanilla <c>sand-{rock}</c>
/// is a valid input to the mix. Shake-out returns it rather than consuming it - the cell comes back to
/// <see cref="BlockStructures.Casting.CastingCellLogic.AfterShakeOut"/> rammed, not empty.
/// </summary>
public class GreenSandItemDefinitions : IExItemDefProvider {
  /// <summary>The item code; the cell's ram check and the shake-out return both use it.</summary>
  public const string Code = "greensand";

  /// <summary>
  /// The texture of the item and of the cell's rammed-sand meshes. Basalt sand is the dark grade in
  /// vanilla's set, distinct at a glance from the pale construction sands.
  /// </summary>
  public const string Texture = "game:block/stone/sand/basalt";

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [GreenSand(domain)];

  private static ExItemDef GreenSand(string domain) =>
    ExItemDef
      .Create(domain, Code)
      // A loose heap: the flour pile shape carries the form, the sand texture the material.
      .Shape("game:item/food/flour")
      .TextureAll(Texture)
      .MaxStackSize(64)
      .MaterialDensity(1600)
      .CreativeCommon("*")
      .GuiTransform(
        new {
          translation = new {
            x = 0,
            y = 0,
            z = 0,
          },
          rotation = new {
            x = 149,
            y = 12,
            z = 0,
          },
          origin = new {
            x = 0.41,
            y = -0.1,
            z = 0.8,
          },
          scale = 2.54,
        }
      )
      .TpHandTransform(
        new {
          translation = new {
            x = -1.87,
            y = -1.25,
            z = -0.8,
          },
          rotation = new {
            x = 70,
            y = 11,
            z = -65,
          },
          scale = 0.41,
        }
      )
      .GroundTransform(
        new {
          translation = new {
            x = 0,
            y = 0.45,
            z = 0,
          },
          rotation = new {
            x = 0.1,
            y = 8,
            z = -0.1,
          },
          scale = 4.5,
        }
      );
}
