using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Grid recipes for the plated pipe tier: the four plain straight/bend/T/X segments, hammered together
/// from a metal plate and nails. Plated is the bootstrap rung, craftable before any machine exists;
/// the cast tier (<see cref="CastPipeRecipeDefinitions"/>) and hpex's rolled tier author their own
/// segment recipes for casting and rolling.
/// </summary>
public class PlatedPipeRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "pipes-plated")
        .Grid(r =>
          r.Name("Piping (Straight)")
            .Pattern("HPN")
            .Size(3, 1)
            .Ingredient("P", Plate(1))
            .Ingredient("N", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("iiex:pipe-plated-straight-ns", 2)
        )
        .Grid(r =>
          r.Name("Piping (Bend)")
            .Pattern("_H,NP,_N")
            .Size(2, 3)
            .Ingredient("P", Plate(1))
            .Ingredient("N", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("iiex:pipe-plated-bend-nw")
        )
        .Grid(r =>
          r.Name("Piping (TJunction)")
            .Pattern("_H_,NPN,_N_")
            .Size(3, 3)
            .Ingredient("P", Plate(1))
            .Ingredient("N", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("iiex:pipe-plated-tjunction-uns")
        )
        .Grid(r =>
          r.Name("Piping (XJunction)")
            .Pattern("HN_,NPN,_N_")
            .Size(3, 3)
            .Ingredient("P", Plate(1))
            .Ingredient("N", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("iiex:pipe-plated-xjunction-nswe")
        ),
    ];
}
