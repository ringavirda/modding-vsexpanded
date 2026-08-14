using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Every <c>iiex</c> block code must resolve to a name in every locale. An unresolved key is invisible
/// until a player looks at the block, which then renders the raw key.
/// </summary>
public class IiexLangCoverageTests {
  private const string Domain = "iiex";
  private static readonly Assembly Mod =
    typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

  [Fact]
  public void Every_block_code_resolves_to_a_name_in_every_locale() {
    var missing = LangCoverage.MissingNames(
      Domain,
      Mod,
      $"assets/{Domain}/lang"
    );

    Assert.True(
      missing.Count == 0,
      $"{missing.Count} unresolved name key(s) across "
        + $"{LangCoverage.MissingBaseCodes(missing).Count} block(s):\n  "
        + string.Join("\n  ", missing.Take(60))
        + (missing.Count > 60 ? $"\n  ... and {missing.Count - 60} more" : "")
        + "\n\nBase codes needing a key: "
        + string.Join(", ", LangCoverage.MissingBaseCodes(missing))
    );
  }

  [Fact]
  public void No_block_description_key_is_orphaned() {
    // A missing description is not a defect; a key matching no live code is text that no longer
    // appears in game. Nothing else reads these keys.
    var orphans = LangCoverage.OrphanedDescriptions(
      Domain,
      Mod,
      $"assets/{Domain}/lang"
    );

    Assert.True(
      orphans.Count == 0,
      $"{orphans.Count} blockdesc key(s) match no live block code - the block was renamed and its "
        + $"description went dark:\n  "
        + string.Join("\n  ", orphans)
    );
  }
}
