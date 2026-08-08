using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// No two iwex recipe-cost selectors may match the same block.
/// <para>
/// <c>ExRecipeCosts</c> applies every catalogue entry in sequence, not first-match, so an overlap
/// means the later entry silently overwrites the earlier's costs - in dictionary order, which is not a
/// contract. Written after the slag rename made <c>iwex:slag-path-*</c> swallow the path slab and
/// stairs, where the flat <c>iwex:slagpath-*</c> could not.
/// </para>
/// </summary>
public class IwexCostSelectorTests
{
  private static readonly Assembly Mod = typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

  [Fact]
  public void No_two_cost_selectors_match_the_same_block()
  {
    var overlaps = CostSelectorOverlap.Overlaps(
      IwexRecipeConfig.DefaultCatalogue(),
      CostSelectorOverlap.CodesOf("iwex", Mod)
    );

    Assert.True(
      overlaps.Count == 0,
      $"{overlaps.Count} overlapping cost selector pair(s):\n  " + string.Join("\n  ", overlaps)
    );
  }

  [Fact]
  public void Every_block_a_grid_recipe_outputs_has_a_cost_catalogue_row()
  {
    // Until this existed the catalogue was checked for overlap and nothing else, so a block with no
    // row at all passed - and several units of the completion plan state "the recipe is in the catalogue"
    // as a gate clause, which was therefore decorative. A missing row is not inert: ExRecipeCosts is what
    // rebalances the grid cost per mod, so an uncatalogued block ships at whatever its definition happened
    // to say and silently escapes the tier's economy.
    // The Match selectors, not the dictionary keys. The keys are stable config identifiers
    // ("blastfurnacecore-grid") deliberately decoupled from the code they select, so a rename does not
    // orphan a player's edited ex_recipes.json - matching on them would report every row as missing.
    IReadOnlyList<string> selectors =
    [
      .. IwexRecipeConfig.DefaultCatalogue().Values.Select(e => e.Match),
    ];

    var uncovered = RecipeCodes
      .OutputBlockCodes("iwex", Mod)
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
