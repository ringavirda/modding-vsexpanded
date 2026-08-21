using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronIndustryExpanded.Items;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// The puddler's two bars. Both are a long iron shaft the smith drew out and worked an end onto - a rod
/// and a plate over the anvil - so the patterns differ only in where the plate sits, which is where the
/// working end is: bent under for the rabble, cupped on top for the paddle.
/// </summary>
/// <remarks>
/// Without these the machine could not be run at all: the hearth gates rabbling and drawing out on
/// holding the matching tool, and both items were creative-only. Nothing caught it - the cost-catalogue
/// guard covers the blocks a grid recipe outputs, not the items.
/// </remarks>
public class PuddlingToolRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "puddlingtools")
        .Grid(r =>
          r.Name("Rabbling Bar")
            .Pattern("_H_,_R_,P__")
            .Size(3, 3)
            .Ingredient("R", Rod(2))
            .Ingredient("P", Plate(1))
            .Ingredient("H", Hammer)
            .OutputItem($"{domain}:{PuddlingToolItemDefinitions.RabbleCode}", 1)
        )
        .Grid(r =>
          r.Name("Puddler's Paddle")
            .Pattern("_H_,_R_,__P")
            .Size(3, 3)
            .Ingredient("R", Rod(2))
            .Ingredient("P", Plate(1))
            .Ingredient("H", Hammer)
            .OutputItem($"{domain}:{PuddlingToolItemDefinitions.PaddleCode}", 1)
        ),
    ];
}
