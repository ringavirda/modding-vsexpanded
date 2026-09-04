using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// No two iiex recipe-cost selectors may match the same block. <c>ExRecipeCosts</c> applies every
/// catalogue entry in sequence rather than first-match, so an overlap lets the later entry overwrite the
/// earlier's costs, in dictionary order, which is not a contract.
/// </summary>
public class IiexCostSelectorTests {
  private static readonly Assembly Mod =
    typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

  [Fact]
  public void No_two_cost_selectors_match_the_same_block() {
    var overlaps = CostSelectorOverlap.Overlaps(
      IiexRecipeConfig.DefaultCatalogue(),
      CostSelectorOverlap.CodesOf("iiex", Mod)
    );

    Assert.True(
      overlaps.Count == 0,
      $"{overlaps.Count} overlapping cost selector pair(s):\n  "
        + string.Join("\n  ", overlaps)
    );
  }

  [Fact]
  public void Every_block_a_grid_recipe_outputs_has_a_cost_catalogue_row() {
    // A missing row is not inert: ExRecipeCosts rebalances the grid cost per mod, so an uncatalogued
    // block ships at whatever its definition says and escapes the tier's economy.
    // Coverage is matched on the Match selectors, not the dictionary keys. The keys are stable config
    // identifiers ("blastfurnacecore-grid") decoupled from the code they select, so that a rename does
    // not orphan a player's edited ex_recipes.json; matching on them would report every row as missing.
    IReadOnlyList<string> selectors =
    [
      .. IiexRecipeConfig.DefaultCatalogue().Values.Select(e => e.Match),
    ];

    var uncovered = RecipeCodes
      .OutputBlockCodes("iiex", Mod)
      .Where(code =>
        !selectors.Any(s =>
          WildcardUtil.Match(new AssetLocation(s), new AssetLocation(code))
        )
      )
      .Distinct()
      .OrderBy(c => c)
      .ToList();

    Assert.True(
      uncovered.Count == 0,
      $"{uncovered.Count} craftable block(s) with no cost-catalogue row:\n  "
        + string.Join("\n  ", uncovered)
    );
  }
}
