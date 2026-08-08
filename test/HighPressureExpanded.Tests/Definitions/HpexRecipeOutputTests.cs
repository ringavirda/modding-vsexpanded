using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace HighPressureExpanded.Tests;

/// <summary>
/// Every <c>hpex</c> grid recipe's block output must name a block <c>hpex</c> registers.
/// <para>
/// <b>An output is the one code that cannot be a wildcard</b>, and until this test nothing checked
/// it. A dead output does not throw - the recipe just never resolves - so the block silently becomes
/// uncraftable while the suite stays green. See <see cref="RecipeCodes"/> for the two live defects that
/// prompted it, one in each direction of the same rewrite.
/// </para>
/// </summary>
public class HpexRecipeOutputTests
{
  private const string Domain = "hpex";
  private static readonly Assembly Mod = typeof(HighPressureExpanded.HpexConfig).Assembly;

  [Fact]
  public void Every_recipe_output_names_a_registered_block()
  {
    IReadOnlyList<RecipeCodes.Unresolvable> bad = RecipeCodes.UnresolvableOutputs(Domain, Mod);

    Assert.True(
      bad.Count == 0,
      $"{bad.Count} recipe output(s) in {Domain} name no registered block - each one is a block that "
        + "cannot be crafted, with no error anywhere:\n  "
        + string.Join("\n  ", bad.Select(b => $"{b.RecipePath}: {b.Code}"))
    );
  }
}
