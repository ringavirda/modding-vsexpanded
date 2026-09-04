using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Every <c>siex</c> grid recipe's block output must name a block <c>siex</c> registers. An output code
/// cannot be a wildcard, and a dead one does not throw: the recipe never resolves and the block becomes
/// uncraftable. See <see cref="RecipeCodes"/>.
/// </summary>
public class SiexRecipeOutputTests {
  private const string Domain = "siex";
  private static readonly Assembly Mod =
    typeof(SteelIndustryExpanded.SiexConfig).Assembly;

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
