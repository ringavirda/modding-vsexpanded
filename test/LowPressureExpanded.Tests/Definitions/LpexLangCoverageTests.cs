using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// Every <c>lpex</c> block code must resolve to a name in every locale. An unresolved key renders
/// raw in game and is reported by nothing in the build, the goldens or the runtime.
/// </summary>
public class LpexLangCoverageTests {
  private const string Domain = "lpex";
  private static readonly Assembly Mod =
    typeof(LowPressureExpanded.LpexConfig).Assembly;

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
    // A missing description is not a defect, but a key matching no live code is text that no longer
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
