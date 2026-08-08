using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace HighPressureExpanded.Tests;

/// <summary>
/// Every <c>hpex</c> block code must resolve to a name in every locale. Neither the build, the
/// goldens nor the runtime reports an unresolved key: it renders raw when a player looks at the block.
/// </summary>
public class HpexLangCoverageTests {
  private const string Domain = "hpex";
  private static readonly Assembly Mod =
    typeof(HighPressureExpanded.BlockStructures.Boiler.Blocks.BlockBoilerLancashire).Assembly;

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
    // A description is optional, so a missing one is not a defect. A key matching no live code is
    // text that no longer appears in game; nothing else reads these keys.
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
