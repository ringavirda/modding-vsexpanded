using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Every lang key exlib's own source names by hand must exist in every locale it ships. Nothing else
/// reports one that does not: the build accepts any string, the goldens pin what a def emits rather than
/// what its text resolves to, and the runtime silently renders the raw key.
/// </summary>
public class ExlibLangCallSiteTests {
  private const string Domain = "exlib";
  private static readonly string SrcDir = System.IO.Path.Combine(
    RepoPaths.Mod(Domain),
    "src"
  );

  [Fact]
  public void Every_key_the_source_asks_for_exists_in_every_locale() {
    var missing = LangCallSites.Unresolvable(
      Domain,
      SrcDir,
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
    Assert.NotEmpty(LangCallSites.Keys(Domain, SrcDir));
  }
}
