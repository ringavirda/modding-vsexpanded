using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// The generated <c>LpexBlocks</c> table must stay in step with the definitions it was emitted from;
/// a variant-group change otherwise leaves it silently wrong.
/// </summary>
public class LpexBlocksCodeTests {
  private static readonly Assembly Mod =
    typeof(LowPressureExpanded.LpexConfig).Assembly;

  [Fact]
  public void Generated_block_codes_match_the_definitions() {
    var (ok, message) = BlockCodeEmitter.CheckOrWrite(
      "lpex",
      Mod,
      "LpexBlocks",
      "LowPressureExpanded",
      "src/LowPressureExpanded/Generated/LpexBlocks.g.cs"
    );

    Assert.True(ok, message);
  }
}
