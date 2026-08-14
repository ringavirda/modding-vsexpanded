using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;
using static IronIndustryExpanded.Recipes.RecipeIngredients;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for iiex's molten-metal handling: the molten barrel, and the canal network's
/// six shapes in each of their three materials plus the tap. Every canal shape is craftable from
/// cobblestone (20-rock capture), a coloured running-brick (7-brick capture) or fire brick; all are
/// hammered and chiselled over fire clay, folded into the shared F/H/K helper. The diagram-crafted
/// equivalents live in <see cref="DiagramRecipeDefinitions"/> and share this file's captures through
/// <see cref="RecipeIngredients"/>.
/// </summary>
public class MoltenRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [MoltenBarrel(domain), MoltenCanal(domain)];

  private static ExRecipeDef MoltenBarrel(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "moltenbarrel")
      // Fabricated route: plates hammered over a fire-clay lining, nailed shut.
      .Grid(r =>
        r.Name("Molten Barrel (Plated)")
          .Pattern("PHP,PCP,PNP")
          .Size(3, 3)
          .Ingredient("P", Plate(1))
          .Ingredient("C", FireClay(4))
          .Ingredient("N", Nails(4))
          .Ingredient("H", Hammer)
          .OutputBlock("iiex:molten-barrel-plated")
      )
      // Cast route: a sand-cast cast-barrel blank lined with fire clay. One blank replaces the six
      // plates and the nails, so it is cheaper in bulk than the plated craft.
      .Grid(r =>
        r.Name("Molten Barrel (Cast, lined)")
          .Pattern("BC")
          .Size(2, 1)
          .Ingredient("B", i => i.Item("iiex:cast-barrel").Quantity(1))
          .Ingredient("C", FireClay(4))
          .OutputBlock("iiex:molten-barrel-cast")
      );

  private static ExRecipeDef MoltenCanal(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "moltencanal")
      // cobblestone family
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Straight)")
              .Pattern("H_K,FCF")
              .Size(3, 2)
              .Ingredient("C", Cobble),
            2
          )
          .OutputBlock("iiex:molten-canal-straight-{rock}-ns", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Bend)")
              .Pattern("HFK,FC_")
              .Size(3, 2)
              .Ingredient("C", Cobble),
            2
          )
          .OutputBlock("iiex:molten-canal-bend-{rock}-nw", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (T-Junction)")
              .Pattern("HFK,FCF")
              .Size(3, 2)
              .Ingredient("C", Cobble),
            2
          )
          .OutputBlock("iiex:molten-canal-tjunction-{rock}-esw", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (X-Junction)")
              .Pattern("HFK,FCF,_F_")
              .Size(3, 3)
              .Ingredient("C", Cobble),
            2
          )
          .OutputBlock("iiex:molten-canal-xjunction-{rock}-nswe", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Start)")
              .Pattern("HCK,FFF")
              .Size(3, 2)
              .Ingredient("C", Cobble),
            2
          )
          .OutputBlock("iiex:molten-canal-start-{rock}-s", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Mold Pedestal)")
              .Pattern("HFK,CFC")
              .Size(3, 2)
              .Ingredient("C", Cobble),
            2
          )
          .OutputBlock("iiex:molten-canal-moldpedestal-{rock}-s", 1)
      )
      .Grid(r =>
        Fhk(r.Name("Molten Canal (Tap)").Pattern("HFK").Size(3, 1), 4)
          .OutputBlock("iiex:molten-canal-tap-s", 1)
      )
      // coloured running-brick + fire-brick pairs
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Straight, Colored Brick)")
              .Pattern("H_K,FBF")
              .Size(3, 2)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iiex:molten-canal-straight-{brick}-ns", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Straight, Fire Brick)")
              .Pattern("H_K,FBF")
              .Size(3, 2)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iiex:molten-canal-straight-fire-ns", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Bend, Colored Brick)")
              .Pattern("HFK,FB_")
              .Size(3, 2)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iiex:molten-canal-bend-{brick}-nw", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Bend, Fire Brick)")
              .Pattern("HFK,FB_")
              .Size(3, 2)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iiex:molten-canal-bend-fire-nw", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (T-Junction, Colored Brick)")
              .Pattern("HFK,FBF")
              .Size(3, 2)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iiex:molten-canal-tjunction-{brick}-esw", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (T-Junction, Fire Brick)")
              .Pattern("HFK,FBF")
              .Size(3, 2)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iiex:molten-canal-tjunction-fire-esw", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (X-Junction, Colored Brick)")
              .Pattern("HFK,FBF,_F_")
              .Size(3, 3)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iiex:molten-canal-xjunction-{brick}-nswe", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (X-Junction, Fire Brick)")
              .Pattern("HFK,FBF,_F_")
              .Size(3, 3)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iiex:molten-canal-xjunction-fire-nswe", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Start, Colored Brick)")
              .Pattern("HBK,FFF")
              .Size(3, 2)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iiex:molten-canal-start-{brick}-s", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Start, Fire Brick)")
              .Pattern("HBK,FFF")
              .Size(3, 2)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iiex:molten-canal-start-fire-s", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Mold Pedestal, Colored Brick)")
              .Pattern("HFK,BFB")
              .Size(3, 2)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iiex:molten-canal-moldpedestal-{brick}-s", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Mold Pedestal, Fire Brick)")
              .Pattern("HFK,BFB")
              .Size(3, 2)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iiex:molten-canal-moldpedestal-fire-s", 1)
      );

  // Fhk (fire clay + hammer + chisel), the RunningBrick / FireBrick captures and the Bricks palette are
  // shared with the casting-cell craft, so they live in RecipeIngredients, reached via `using static`.
}
