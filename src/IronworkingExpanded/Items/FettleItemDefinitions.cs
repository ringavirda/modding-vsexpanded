using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Items;

/// <summary>
/// <b>Puddling fettle</b> - the iron-oxide layer rammed over a puddling furnace's cast-iron bottom plate
/// before a charge goes in. It does two jobs at once, which is why the process cannot run without it: the
/// oxide <b>feeds oxygen to burn the carbon out of the pig</b> (that reaction is what puddling <em>is</em>),
/// and it shields the bottom plate from the charge that would otherwise melt into it.
/// <para>
/// It is one prepared item rather than "whichever ore you happened to bring", and vanilla does most of that
/// work already: magnetite, hematite and limonite nuggets <b>all</b> crush to the single
/// <c>game:crushed-iron</c>, so the ore you fettle with is already unified before iwex sees it. That also
/// settles the ore-grade question - vanilla has no graded nugget item at all (richness lives on the ore
/// <em>block</em> and is expressed as how many nuggets a vein drops), so grade is already priced as mining
/// time and needs no rule here.
/// </para>
/// <para>
/// Consumed every heat, deliberately. Fettling was a real running cost of puddling - part of why puddled
/// iron was expensive - and it is the ore you chose <em>not</em> to smelt.
/// </para>
/// </summary>
public class FettleItemDefinitions : IExItemDefProvider
{
  /// <summary>The item code the puddling hearth checks when a player offers to fettle it.</summary>
  public const string Code = "puddlingfettle";

  /// <summary>
  /// The prepared ore a fettle batch starts from. Vanilla's shared crushed-iron: magnetite, hematite and
  /// limonite all reduce to it, so every iron ore fettles and none of them is special-cased.
  /// </summary>
  public const string CrushedOre = "game:crushed-iron";

  /// <summary>
  /// The tag every material a fettle batch can be made from carries. It exists so one recipe slot can accept
  /// <b>bought ore or recovered oxide interchangeably</b> - which is the whole shape of the mechanic: early
  /// on a batch is three parts crushed ore, and as the works starts running you replace those parts, one at a
  /// time, with tap cinder off the puddling hearth and scale off the rolling mill. Unnamespaced, following
  /// vanilla's own <c>flux</c> / <c>tool-chisel</c>, so another mod can add a fettle material without
  /// touching iwex.
  /// </summary>
  public const string StockTag = "fettlestock";

  /// <summary>Fettle spread by one hearth cell. Three cells, so a full ram-up costs three.</summary>
  public const int PerHearthCell = 1;

  /// <summary>
  /// The loose-oxide texture, shared with the hearth's rendered fettle layer so the stuff in the player's
  /// hand and the stuff on the bed are visibly the same material. Crushed hematite rather than a nugget:
  /// fettle can be made from ore, tap cinder or mill scale, so it has to read as warm red-brown <em>oxide</em>
  /// and not as one specific ore. (Magnetite's nugget is chromatically neutral - it looks like metal.)
  /// </summary>
  public const string Texture = "game:item/resource/crushed/hematite";

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Fettle(domain), TapCinder(domain), MillScale(domain)];

  // The two recovered oxides. Both are iron the works did not keep - so rather than vanishing, it comes back
  // as the material that makes the next heat possible. Neither is craftable: you get them by running the
  // machines, which is what makes a working shop cheaper to feed than a new one.
  private static ExItemDef Recovered(string domain, string code, string texture, string shape) =>
    ExItemDef
      .Create(domain, code)
      .Shape(shape)
      .TextureAll(texture)
      .MaxStackSize(64)
      .MaterialDensity(2500)
      .Raw("tags", new[] { StockTag })
      .CreativeCommon("*");

  /// <summary>
  /// <b>Tap cinder</b> - the iron-rich slag raked out when a puddling hearth is cleaned for the next run.
  /// Historically this was the standard British fettling once roasted ("bull dog"), so recycling it here is
  /// the authentic route rather than a convenience.
  /// </summary>
  private static ExItemDef TapCinder(string domain) =>
    Recovered(domain, "tapcinder", SlagItemDefinitions.Texture, "game:item/ore/ungraded/coke");

  /// <summary>
  /// <b>Mill scale</b> - the oxide skin that flakes off hot stock under the rolls. A rare by-product of
  /// running the mill rather than a modelled mass loss, since rolling swaps one piece for another and never
  /// weighs them.
  /// </summary>
  private static ExItemDef MillScale(string domain) =>
    Recovered(domain, "millscale", Texture, "game:item/food/flour");

  private static ExItemDef Fettle(string domain) =>
    ExItemDef
      .Create(domain, Code)
      .Shape("game:item/food/flour") // a loose heap of ground oxide
      .TextureAll(Texture)
      .MaxStackSize(64)
      .MaterialDensity(2500)
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
