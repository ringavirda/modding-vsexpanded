using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;
using static SteelmakingExpanded.Recipes.RecipeIngredients;

namespace SteelmakingExpanded.Recipes.Grid;

/// <summary>
/// Grid recipes for the cowper stove: the intake that anchors the structure, and the heat sink its
/// regenerator column is stacked from. Both are pipe wrapped in refractory brick.
/// </summary>
public class CowperStoveRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "cowperstove")
        .Grid(r =>
          r.Name("Cowper Stove Intake")
            .Pattern("BHB,BPB,BNB")
            .Size(3, 3)
            .Ingredient("B", RefractoryTier(2))
            .Ingredient("N", Nails(2))
            .Ingredient("P", PipeStar(1))
            .Ingredient("H", Hammer)
            .OutputBlock("smex:cowperstove-intake-{tier}-s")
        )
        .Grid(r =>
          r.Name("Heat Sink")
            .Pattern("HP_,PNP,_P_")
            .Size(3, 3)
            .Ingredient("N", Nails(2))
            .Ingredient("H", Hammer)
            .Ingredient("P", PipeStar(1))
            .OutputBlock("smex:cowperstoveheatsink-n")
        ),
    ];
}
