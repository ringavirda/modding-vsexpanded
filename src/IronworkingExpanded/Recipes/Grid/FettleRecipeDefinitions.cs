using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronworkingExpanded.Items;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Preparing puddling fettle: three parts iron oxide in, three fettle out.
/// <para>
/// Each of the three slots takes any <see cref="FettleItemDefinitions.StockTag"/> material - crushed iron
/// ore, tap cinder off the puddling hearth, or mill scale off the rolls - and the recipe does not
/// distinguish between them, all three being iron oxide with the iron still locked up. A works that is
/// running displaces bought ore with the oxide its own machines return.
/// </para>
/// </summary>
public class FettleRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "puddlingfettle")
        .Grid(r =>
          r.Name("Puddling Fettle")
            .Pattern("FFF")
            .Size(3, 1)
            .Ingredient(
              "F",
              i => i.Tagged("item", FettleItemDefinitions.StockTag).Quantity(1)
            )
            .OutputItem($"{domain}:{FettleItemDefinitions.Code}", 3)
        ),
    ];
}
