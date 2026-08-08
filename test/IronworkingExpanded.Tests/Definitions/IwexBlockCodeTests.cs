using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The generated <c>IwexBlocks</c> table must stay in step with the definitions it was emitted from.
/// <para>
/// The table exists so a layout author names a block code with a chosen variant instead of typing it -
/// and so <c>IwexCodes</c> stops being a hand-kept copy of what the definitions already state exactly.
/// A generated file with no drift test is worse than no generated file: it looks authoritative and is
/// silently wrong the first time a variant group changes.
/// </para>
/// </summary>
public class IwexBlockCodeTests
{
  private static readonly Assembly Mod =
    typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

  [Fact]
  public void Generated_block_codes_match_the_definitions()
  {
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
