using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.BlockStructures.Casting;

/// <summary>
/// <b>Green sand</b> - the molding sand the casting cell is rammed with. Silica sand blended with a clay
/// binder and damped; "green" means <em>moist and uncured</em>, not the colour. It is neither baked nor
/// chemically set, which is exactly why it can be rammed around a pattern, poured, and then broken out.
/// <para>
/// It exists as its own prepared item rather than the cell simply accepting raw sand, and that choice does
/// real work. <b>The clay binder is what holds an impression</b> - dry sand holds nothing - so a prepared mix
/// is the honest material. It also settles the "which sands may we cast with?" question by dissolving it:
/// vanilla's <c>sand-{rock}</c> variants differ only cosmetically, so any rule admitting some rock types and
/// not others would be unlearnable - a thing to look up, never to derive. Every sand is a fine <em>input to
/// the mix</em> instead. And because green sand has no variants of its own, the cell no longer has to
/// remember and hand back the exact sand block that was rammed into it, which removed more state than this
/// item adds.
/// </para>
/// <para>
/// Separating it from construction sand is the point a player actually learns: the sand you build with is not
/// the sand you cast with. Prepared in bulk and reconditioned rather than spent - shake-out returns it (see
/// <see cref="CastingCellLogic.SandIsReturned"/>), so the standing cost of a casting operation is the labour
/// of ramming up, not the sand.
/// </para>
/// </summary>
public class GreenSandItemDefinitions : IExItemDefProvider
{
  /// <summary>The item code, as the cell's ram check and the shake-out return both need it.</summary>
  public const string Code = "greensand";

  /// <summary>
  /// The sand texture green sand wears, and the one the cell's rammed-sand meshes are drawn with. Basalt
  /// sand is the dark, damp-looking grade in vanilla's set - which reads as a prepared, clay-bearing mix
  /// rather than the pale sand players build with, so the two are told apart at a glance.
  /// </summary>
  public const string Texture = "game:block/stone/sand/basalt";

  public static IEnumerable<ExItemDef> Definitions(string domain) => [GreenSand(domain)];

  private static ExItemDef GreenSand(string domain) =>
    ExItemDef
      .Create(domain, Code)
      // A loose heap of damp sand: the flour pile carries the shape, the sand texture the material.
      .Shape("game:item/food/flour")
      .TextureAll(Texture)
      .MaxStackSize(64)
      .MaterialDensity(1600)
      .CreativeCommon("*")
      .GuiTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 149, y = 12, z = 0 },
          origin = new
          {
            x = 0.41,
            y = -0.1,
            z = 0.8,
          },
          scale = 2.54,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new
          {
            x = -1.87,
            y = -1.25,
            z = -0.8,
          },
          rotation = new
          {
            x = 70,
            y = 11,
            z = -65,
          },
          scale = 0.41,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0.45, z = 0 },
          rotation = new
          {
            x = 0.1,
            y = 8,
            z = -0.1,
          },
          scale = 4.5,
        }
      );
}
