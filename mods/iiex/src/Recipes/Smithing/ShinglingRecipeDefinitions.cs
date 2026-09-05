using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using IronIndustryExpanded.Items;

namespace IronIndustryExpanded.Recipes.Smithing;

/// <summary>
/// The helve recipe that turns a pile of puddled balls into one shingled bar. Authored through the
/// <see cref="ExRecipeDef.Body"/> escape hatch, as <see cref="PigRecipeDefinitions"/> is: smithing recipes
/// are declarative voxel patterns and the mod ships no builder for them.
/// </summary>
/// <remarks>
/// The target shape is the pile, filled solid - <see cref="Shingling.BarVoxels"/> voxels, exactly what
/// two balls lay - so the helve's <c>FullyWorkable</c> pass sheds nothing and the iron is conserved
/// exactly. It is also the only recipe the pile matches, which is why the anvil selects it without a
/// dialog: whatever is piled, a bar is the only thing it can make.
/// </remarks>
public class ShinglingRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "smithing", "shingle")
        .Body(
          new
          {
            ingredient = new
            {
              type = "item",
              code = $"{domain}:{WroughtBallItemDefinitions.Code}",
            },
            name = "Shingled bar",
            pattern = SolidPile(),
            code = Shingling.RecipeCode,
            output = new
            {
              type = "item",
              code = $"{domain}:stock-shingledbar",
            },
          }
        ),
    ];

  // One string per z-row, one array per y-layer: the pile as the balls lay it, filled solid.
  private static string[][] SolidPile() =>
    [
      .. Enumerable
        .Range(0, Shingling.Layers)
        .Select(_ =>
          Enumerable
            .Range(0, Shingling.Depth)
            .Select(_ => new string('#', Shingling.Width))
            .ToArray()
        ),
    ];
}
