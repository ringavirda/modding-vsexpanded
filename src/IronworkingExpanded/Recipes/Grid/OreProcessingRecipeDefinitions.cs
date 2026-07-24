using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for iwex's ore-processing blocks (migrated from recipes/grid/bunker.json).
/// A one-recipe file authored as a lone object: 8 same-colour burned bricks -> an ore bunker of that brick.
/// </summary>
public class OreProcessingRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "bunker")
        .GridObject(r =>
          r.Pattern("B")
            .Size(1, 1)
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
                  .Quantity(8)
            )
            .OutputBlock("iwex:bunker-{brick}-north", 1)
        ),
    ];
}
