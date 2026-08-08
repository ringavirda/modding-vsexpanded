using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace HighPressureExpanded.Tests;

/// <summary>
/// The generated <c>HpexBlocks</c> table must stay in step with the definitions it was emitted from.
/// A generated file with no drift test is worse than no generated file: it looks authoritative and is
/// silently wrong the first time a variant group changes.
/// </summary>
public class HpexBlocksCodeTests
{
  private static readonly Assembly Mod = typeof(HighPressureExpanded.BlockStructures.Boiler.Blocks.BlockBoilerLancashire).Assembly;

  [Fact]
  public void Generated_block_codes_match_the_definitions()
  {
    var (ok, message) = BlockCodeEmitter.CheckOrWrite(
      "hpex",
      Mod,
      "HpexBlocks",
      "HighPressureExpanded",
      "src/HighPressureExpanded/Generated/HpexBlocks.g.cs"
    );

    Assert.True(ok, message);
  }
}
