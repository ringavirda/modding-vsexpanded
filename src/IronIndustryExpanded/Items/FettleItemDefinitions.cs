using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.Items;

/// <summary>
/// Puddling fettle: the iron-oxide layer rammed over a puddling furnace's cast-iron bottom plate before a
/// charge goes in. The oxide supplies the oxygen that burns the carbon out of the pig, which is the
/// puddling reaction, and shields the bottom plate from the charge, so the process cannot run without it.
/// A layer is consumed every heat.
/// <para>
/// One prepared item rather than any iron ore: vanilla crushes magnetite, hematite and limonite nuggets to
/// the single <c>game:crushed-iron</c>, and has no graded nugget item at all - richness lives on the ore
/// block as how many nuggets a vein drops - so ore grade needs no rule here.
/// </para>
/// </summary>
public class FettleItemDefinitions : IExItemDefProvider {
  /// <summary>The item code the puddling hearth checks when a player offers to fettle it.</summary>
  public const string Code = "puddlingfettle";

  /// <summary>
  /// The prepared ore a fettle batch starts from. Vanilla's shared crushed-iron: magnetite, hematite and
  /// limonite all reduce to it, so every iron ore fettles and none is special-cased.
  /// </summary>
  public const string CrushedOre = "game:crushed-iron";

  /// <summary>
  /// The tag carried by every material a fettle batch can be made from, so one recipe slot accepts mined
  /// ore and recovered oxide interchangeably: a batch is three parts crushed ore, and those parts can be
  /// replaced one at a time with tap cinder off the puddling hearth and scale off the rolling mill.
  /// Unnamespaced, following vanilla's own <c>flux</c> and <c>tool-chisel</c>, so another mod can add a
  /// fettle material without touching iiex.
  /// </summary>
  public const string StockTag = "fettlestock";

  /// <summary>Fettle spread by one hearth cell. Three cells, so a full ram-up costs three.</summary>
  public const int PerHearthCell = 1;

  /// <summary>
  /// The loose-oxide texture, shared with the hearth's rendered fettle layer so the item in hand and the
  /// layer on the bed are visibly the same material. Crushed hematite rather than a nugget: fettle can be
  /// made from ore, tap cinder or mill scale, so it has to read as warm red-brown oxide and not as one
  /// specific ore.
  /// </summary>
  public const string Texture = "game:item/resource/crushed/hematite";

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Fettle(domain), TapCinder(domain), MillScale(domain)];

  // The two recovered oxides: iron the works did not keep, returned as fettle stock. Neither is craftable;
  // both come only from running the machines.
  private static ExItemDef Recovered(
    string domain,
    string code,
    string texture,
    string shape
  ) =>
    ExItemDef
      .Create(domain, code)
      .Shape(shape)
      .TextureAll(texture)
      .MaxStackSize(64)
      .MaterialDensity(2500)
      .Raw("tags", new[] { StockTag })
      .CreativeCommon("*");

  /// <summary>
  /// Tap cinder: the iron-rich slag raked out when a puddling hearth is cleaned for the next run.
  /// Historically the standard British fettling once roasted ("bull dog").
  /// </summary>
  private static ExItemDef TapCinder(string domain) =>
    Recovered(
      domain,
      "tapcinder",
      SlagItemDefinitions.Texture,
      "game:item/ore/ungraded/coke"
    );

  /// <summary>
  /// Mill scale: the oxide skin that flakes off hot stock under the rolls. A rare by-product of running the
  /// mill rather than a modelled mass loss - rolling swaps one piece for another and never weighs them.
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
