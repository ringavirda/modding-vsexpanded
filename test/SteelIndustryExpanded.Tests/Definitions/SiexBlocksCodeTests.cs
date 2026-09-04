using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The generated <c>SiexBlocks</c> table must stay in step with the definitions it was emitted from: a
/// variant-group change that is not regenerated leaves the table wrong with no other signal.
/// </summary>
public class SiexBlocksCodeTests {
  private static readonly Assembly Mod =
    typeof(SteelIndustryExpanded.BlockStructures.SmokeStack.Blocks.BlockSmokeStackIntake).Assembly;

  [Fact]
  public void Generated_block_codes_match_the_definitions() {
    var (ok, message) = BlockCodeEmitter.CheckOrWrite(
      "siex",
      Mod,
      "SiexBlocks",
      "SteelIndustryExpanded",
      "src/SteelIndustryExpanded/Generated/SiexBlocks.g.cs"
    );

    Assert.True(ok, message);
  }
}
