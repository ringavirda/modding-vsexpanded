using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace HighPressureExpanded.Tests;

/// <summary>
/// No <c>hpex</c> source literal may name a variant-grouped block by its bare code - such a code
/// resolves to <c>null</c>, and every call site is written defensively enough that the failure is
/// silent. See <see cref="CodeLiterals"/>.
/// </summary>
public class HpexCodeLiteralTests
{
  private const string Domain = "hpex";
  private static readonly Assembly Mod = typeof(HighPressureExpanded.BlockStructures.Boiler.Blocks.BlockBoilerLancashire).Assembly;

  [Fact]
  public void No_source_literal_names_a_variant_grouped_block_by_its_bare_code()
  {
    var bad = CodeLiterals.UnresolvableBareCodes(Domain, Mod, "src/HighPressureExpanded");

    Assert.True(
      bad.Count == 0,
      $"{bad.Count} unresolvable block-code literal(s):\n  " + string.Join("\n  ", bad)
    );
  }
}
