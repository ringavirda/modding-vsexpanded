using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Drift check for the generated <c>IwexBlocks</c> table, which lets a layout author name a block code
/// with a chosen variant instead of typing it. The emitted table goes stale silently when a variant
/// group changes, so it is compared against the definitions it was emitted from.
/// </summary>
public class IwexBlockCodeTests {
  private static readonly Assembly Mod =
    typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

  [Fact]
  public void Generated_block_codes_match_the_definitions() {
    var (ok, message) = BlockCodeEmitter.CheckOrWrite(
      "iwex",
      Mod,
      "IwexBlocks",
      "IronworkingExpanded",
      "src/IronworkingExpanded/Generated/IwexBlocks.g.cs"
    );

    Assert.True(ok, message);
  }
}
