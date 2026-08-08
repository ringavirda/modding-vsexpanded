using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The generated <c>SmexBlocks</c> table must stay in step with the definitions it was emitted from.
/// A generated file with no drift test is worse than no generated file: it looks authoritative and is
/// silently wrong the first time a variant group changes.
/// </summary>
public class SmexBlocksCodeTests
{
  private static readonly Assembly Mod = typeof(SteelmakingExpanded.BlockStructures.SmokeStack.Blocks.BlockSmokeStackIntake).Assembly;

  [Fact]
  public void Generated_block_codes_match_the_definitions()
  {
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
