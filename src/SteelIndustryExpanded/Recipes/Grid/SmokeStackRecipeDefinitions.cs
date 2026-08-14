using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;
using static SteelIndustryExpanded.Recipes.RecipeIngredients;

namespace SteelIndustryExpanded.Recipes.Grid;

/// <summary>
/// Grid recipe for the smoke-stack intake, the anchor the stack column is built up from. Authored as a
/// lone recipe object: four refractory bricks around a pipe.
/// </summary>
public class SmokeStackRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "smokestack")
        .GridObject(r =>
          r.Name("Smoke Stack Intake")
            .Pattern("BHB,_P_,BNB")
            .Size(3, 3)
            .Ingredient("B", RefractoryTier(4))
            .Ingredient("N", Nails(2))
            .Ingredient("H", Hammer)
            .Ingredient("P", PipeStar(1))
            .OutputBlock("siex:smokestack-intake-{tier}-n")
        ),
    ];
}
