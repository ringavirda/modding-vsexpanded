using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.Recipes.Smithing;

/// <summary>
/// Smithing (anvil) recipes for iiex's gears. Smithing recipes are declarative voxel patterns, so they are
/// authored through the <see cref="ExRecipeDef.Add"/> / <see cref="ExRecipeDef.Body"/> escape hatch with an
/// anonymous object per recipe rather than a dedicated builder. The gear def carries two recipes (the
/// 2-gear and 4-gear plans); the large gear is a single recipe body.
/// </summary>
public class GearRecipeDefinitions : IExRecipeDefProvider {
  // The iron/steel ingot the gears are smithed from (shared by all three recipes).
  private static object IngotMetal =>
    new {
      type = "item",
      code = "game:ingot-*",
      name = "metal",
      allowedVariants = new[] { "iron", "steel" },
    };

  // The 2-gear voxel plan; the 4-gear plan is this plan stacked twice.
  private static readonly string[] TwoGearRows =
  [
    "_#_#__#_#_",
    "##########",
    "_#_#__#_#_",
    "##########",
    "_#_#__#_#_",
  ];

  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "smithing", "gear")
        .Add(
          new
          {
            ingredient = IngotMetal,
            name = "2 gears",
            pattern = new[] { TwoGearRows },
            code = "2gears-{metal}",
            output = new
            {
              type = "item",
              code = "iiex:gear-{metal}",
              stacksize = 2,
            },
          }
        )
        .Add(
          new
          {
            ingredient = IngotMetal,
            name = "4 gears",
            pattern = new[] { TwoGearRows.Concat(TwoGearRows).ToArray() },
            code = "4gears-{metal}",
            output = new
            {
              type = "item",
              code = "iiex:gear-{metal}",
              stacksize = 4,
            },
          }
        ),
      ExRecipeDef
        .Create(domain, "smithing", "largegear")
        .Body(
          new
          {
            ingredient = IngotMetal,
            name = "largegear",
            pattern = new[]
            {
              new[]
              {
                "__######__",
                "_########_",
                "###____###",
                "##_#__#_##",
                "##__##__##",
                "##__##__##",
                "##_#__#_##",
                "###____###",
                "_########_",
                "__######__",
              },
            },
            code = "largegear-{metal}",
            output = new
            {
              type = "item",
              code = "iiex:largegear-{metal}",
              stacksize = 1,
            },
          }
        ),
    ];
}
