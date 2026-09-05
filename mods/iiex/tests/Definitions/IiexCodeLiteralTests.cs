using System.IO;
using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// No <c>iiex</c> source literal may name a variant-grouped block by its bare code: such a code
/// resolves to <c>null</c> and every call site handles null defensively, so the failure is silent.
/// See <see cref="CodeLiterals"/>.
/// </summary>
public class IiexCodeLiteralTests {
  private const string Domain = "iiex";
  private static readonly Assembly Mod =
    typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

  [Fact]
  public void No_source_literal_names_a_variant_grouped_block_by_its_bare_code() {
    var bad = CodeLiterals.UnresolvableBareCodes(
      Domain,
      Mod,
      Path.Combine(RepoPaths.Mod("iiex"), "src")
    );

    Assert.True(
      bad.Count == 0,
      $"{bad.Count} unresolvable block-code literal(s):\n  "
        + string.Join("\n  ", bad)
    );
  }
}
