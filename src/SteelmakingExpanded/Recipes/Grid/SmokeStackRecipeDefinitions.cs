using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;
using static SteelmakingExpanded.Recipes.RecipeIngredients;

namespace SteelmakingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipe for the smoke-stack intake (migrated from recipes/grid/smokestack.json) - the
/// anchor the stack is built up from. A single recipe authored as a lone object, refractory brick around a
/// pipe like the cowper's intake, but four bricks deep for the taller column.
/// </summary>
public class SmokeStackRecipeDefinitions : IExRecipeDefProvider
{
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
            .OutputBlock("smex:smokestack-intake-{tier}-n")
        ),
    ];
}
