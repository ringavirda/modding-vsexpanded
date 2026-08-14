using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace SteelIndustryExpanded.Recipes.Barrel;

/// <summary>
/// Barrel (sealed-mixing) recipe: slaked lime plus powdered slag makes mortar. Authored as a single
/// recipe object through the <see cref="ExRecipeDef.Body"/> escape hatch; barrel ingredients are
/// measured in litres (the lime) or by stack quantity (the slag).
/// </summary>
public class MortarRecipeDefinitions : IExRecipeDefProvider {
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
                code = "iiex:powderedslag",
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
