using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// The generated <c>LpexBlocks</c> table must stay in step with the definitions it was emitted from.
/// A generated file with no drift test is worse than no generated file: it looks authoritative and is
/// silently wrong the first time a variant group changes.
/// </summary>
public class LpexBlocksCodeTests
{
  private static readonly Assembly Mod = typeof(LowPressureExpanded.LpexConfig).Assembly;

  [Fact]
  public void Generated_block_codes_match_the_definitions()
  {
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
