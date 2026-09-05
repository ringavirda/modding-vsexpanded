using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Drift check for the generated <c>IiexBlocks</c> table, which lets a layout author name a block code
/// with a chosen variant instead of typing it. The emitted table goes stale silently when a variant
/// group changes, so it is compared against the definitions it was emitted from.
/// </summary>
public class IiexBlockCodeTests {
  private static readonly Assembly Mod =
    typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

  [Fact]
  public void Generated_block_codes_match_the_definitions() {
    var (ok, message) = BlockCodeEmitter.CheckOrWrite(
      "iiex",
      Mod,
      "IiexBlocks",
      "IronIndustryExpanded",
      "mods/iiex/src/Generated/IiexBlocks.g.cs"
    );

    Assert.True(ok, message);
  }
}
