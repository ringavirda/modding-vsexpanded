using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Every <c>iiex</c> grid recipe's block output must name a block <c>iiex</c> registers. An output is
/// the one code that cannot be a wildcard, and a dead one does not throw: the recipe simply never
/// resolves and the block becomes uncraftable. See <see cref="RecipeCodes"/>.
/// </summary>
public class IiexRecipeOutputTests {
  private const string Domain = "iiex";
  private static readonly Assembly Mod =
    typeof(IronIndustryExpanded.Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

  [Fact]
  public void Every_recipe_output_names_a_registered_block() {
    IReadOnlyList<RecipeCodes.Unresolvable> bad =
      RecipeCodes.UnresolvableOutputs(Domain, Mod);

    Assert.True(
      bad.Count == 0,
      $"{bad.Count} recipe output(s) in {Domain} name no registered block - each one is a block that "
        + "cannot be crafted, with no error anywhere:\n  "
        + string.Join("\n  ", bad.Select(b => $"{b.RecipePath}: {b.Code}"))
    );
  }
}
