using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace SteelmakingExpanded.Recipes.Barrel;

/// <summary>
/// Code-first barrel (sealed-mixing) recipe (migrated from recipes/barrel/mortar.json): slaked lime + powdered
/// slag make mortar. A single recipe object authored via the <see cref="ExRecipeDef.Body"/> escape hatch;
/// barrel ingredients are measured in litres (the lime) or by stack quantity (the slag).
/// </summary>
public class MortarRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "barrel", "mortar")
        .Body(
          new
          {
            code = "mortarfromslag",
            ingredients = new object[]
            {
              new
              {
                type = "item",
                code = "game:slakedlimeportion",
                litres = 1,
              },
              new
              {
                type = "item",
                code = "iwex:powderedslag",
                quantity = 8,
              },
            },
            output = new
            {
              type = "item",
              code = "game:mortar",
              stackSize = 4,
            },
          }
        ),
    ];
}
