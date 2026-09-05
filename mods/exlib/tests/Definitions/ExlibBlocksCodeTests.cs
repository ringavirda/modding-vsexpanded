using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The generated <c>ExlibBlocks</c> table must stay in step with the definitions it was emitted from:
/// a variant-group change that has not been regenerated fails here.
/// </summary>
public class ExlibBlocksCodeTests {
  private static readonly Assembly Mod =
    typeof(ExpandedLib.Blocks.Structures.StructureFillers).Assembly;

  [Fact]
  public void Generated_block_codes_match_the_definitions() {
    var (ok, message) = BlockCodeEmitter.CheckOrWrite(
      "exlib",
      Mod,
      "ExlibBlocks",
      "ExpandedLib",
      "mods/exlib/src/Generated/ExlibBlocks.g.cs"
    );

    Assert.True(ok, message);
  }
}
