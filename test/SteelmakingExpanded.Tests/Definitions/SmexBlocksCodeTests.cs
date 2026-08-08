using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The generated <c>SmexBlocks</c> table must stay in step with the definitions it was emitted from: a
/// variant-group change that is not regenerated leaves the table wrong with no other signal.
/// </summary>
public class SmexBlocksCodeTests {
  private static readonly Assembly Mod =
    typeof(SteelmakingExpanded.BlockStructures.SmokeStack.Blocks.BlockSmokeStackIntake).Assembly;

  [Fact]
  public void Generated_block_codes_match_the_definitions() {
    var (ok, message) = BlockCodeEmitter.CheckOrWrite(
      "smex",
      Mod,
      "SmexBlocks",
      "SteelmakingExpanded",
      "src/SteelmakingExpanded/Generated/SmexBlocks.g.cs"
    );

    Assert.True(ok, message);
  }
}
