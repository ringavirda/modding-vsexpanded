using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// No <c>siex</c> source literal may name a variant-grouped block by its bare code: such a code
/// resolves to <c>null</c> and the call sites handle null quietly, so the failure is silent. See
/// <see cref="CodeLiterals"/>.
/// </summary>
public class SiexCodeLiteralTests {
  private const string Domain = "siex";
  private static readonly Assembly Mod =
    typeof(SteelIndustryExpanded.BlockStructures.SmokeStack.Blocks.BlockSmokeStackIntake).Assembly;

  [Fact]
  public void No_source_literal_names_a_variant_grouped_block_by_its_bare_code() {
    var bad = CodeLiterals.UnresolvableBareCodes(
      Domain,
      Mod,
      "src/SteelIndustryExpanded"
    );

    Assert.True(
      bad.Count == 0,
      $"{bad.Count} unresolvable block-code literal(s):\n  "
        + string.Join("\n  ", bad)
    );
  }
}
