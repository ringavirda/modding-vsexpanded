using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// No <c>lpex</c> source literal may name a variant-grouped block by its bare code - such a code
/// resolves to <c>null</c>, and every call site is written defensively enough that the failure is
/// silent. See <see cref="CodeLiterals"/>.
/// </summary>
public class LpexCodeLiteralTests
{
  private const string Domain = "lpex";
  private static readonly Assembly Mod = typeof(LowPressureExpanded.LpexConfig).Assembly;

  [Fact]
  public void No_source_literal_names_a_variant_grouped_block_by_its_bare_code()
  {
    var bad = CodeLiterals.UnresolvableBareCodes(Domain, Mod, "src/LowPressureExpanded");

    Assert.True(
      bad.Count == 0,
      $"{bad.Count} unresolvable block-code literal(s):\n  " + string.Join("\n  ", bad)
    );
  }
}
