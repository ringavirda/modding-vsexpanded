using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Every <c>siex</c> block code must resolve to a name in every locale. An unresolved key produces no
/// build, golden or runtime error; it renders raw in game.
/// </summary>
public class SiexLangCoverageTests {
  private const string Domain = "siex";
  private static readonly Assembly Mod =
    typeof(SteelIndustryExpanded.BlockStructures.SmokeStack.Blocks.BlockSmokeStackIntake).Assembly;

  [Fact]
  public void Every_block_code_resolves_to_a_name_in_every_locale() {
    var missing = LangCoverage.MissingNames(
      Domain,
      Mod,
      System.IO.Path.Combine(RepoPaths.Assets(Domain), "lang")
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

  /// <summary>
  /// The other direction, which <see cref="LangCoverage"/> cannot see: a key the C# asks for by hand.
  /// A misspelt one renders raw in game with the build, the goldens and the runtime all silent.
  /// </summary>
  [Fact]
  public void Every_key_the_source_asks_for_exists_in_every_locale() {
    var missing = LangCallSites.Unresolvable(
      Domain,
      System.IO.Path.Combine(RepoPaths.Mod(Domain), "src"),
      System.IO.Path.Combine(RepoPaths.Assets(Domain), "lang")
    );

    Assert.True(
      missing.Count == 0,
      $"{missing.Count} lang key(s) named in source that no locale carries - each renders as the raw "
        + "key in game:\n  "
        + string.Join("\n  ", missing)
    );
  }

  /// <summary>The premise of the check above: a scan that matched nothing would pass it.</summary>
  [Fact]
  public void The_call_site_scan_finds_keys_to_check() {
    Assert.NotEmpty(
      LangCallSites.Keys(
        Domain,
        System.IO.Path.Combine(RepoPaths.Mod(Domain), "src")
      )
    );
  }

  [Fact]
  public void No_block_description_key_is_orphaned() {
    // A missing description is not a defect, but a key matching no live code is text that stopped
    // appearing in game. Nothing else reads these keys.
    var orphans = LangCoverage.OrphanedDescriptions(
      Domain,
      Mod,
      System.IO.Path.Combine(RepoPaths.Assets(Domain), "lang")
    );

    Assert.True(
      orphans.Count == 0,
      $"{orphans.Count} blockdesc key(s) match no live block code - the block was renamed and its "
        + $"description went dark:\n  "
        + string.Join("\n  ", orphans)
    );
  }
}
