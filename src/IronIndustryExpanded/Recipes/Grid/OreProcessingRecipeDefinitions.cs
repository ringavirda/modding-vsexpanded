using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for iiex's ore handling: a single machine, the burdenmaker. It is the only
/// source of burden, so without this recipe a world has no way to charge a furnace.
/// </summary>
public class OreProcessingRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [Burdenmaker(domain)];

  /// <summary>
  /// The burdenmaker's placed shell: twelve same-colour fired bricks laid out as the machine's own
  /// 3 × 2 floor plan. Ingredients stay brick-only because a world with no burden yet must be able to
  /// make them; the rest of the cost sits in the five RCC stages after it (28 more bricks, 9 iron
  /// plates, 2 ingots). The <c>brick</c> name binds one colour across all six cells and carries it
  /// into the output.
  /// </summary>
  private static ExRecipeDef Burdenmaker(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "burdenmaker")
      .GridObject(r =>
        r.Pattern("BBB,BBB")
          .Size(3, 2)
          .Ingredient(
            "B",
            i =>
              i.Item("game:burnedbrick-*")
                .Named(
                  "brick",
                  "black",
                  "brown",
                  "cream",
                  "gray",
                  "orange",
                  "red",
                  "tan"
                )
                .Quantity(2)
          )
          .OutputBlock($"{domain}:burdenmaker-{{brick}}-n", 1)
      );
}
