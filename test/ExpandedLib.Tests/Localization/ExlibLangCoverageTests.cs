using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Every <c>exlib</c> block code must resolve to a name in every locale. An unresolved key is
/// invisible until a player looks at the block, and then renders raw - nothing in the build, the
/// goldens or the runtime says a word.
/// </summary>
public class ExlibLangCoverageTests
{
  private const string Domain = "exlib";
  private static readonly Assembly Mod = typeof(ExpandedLib.Blocks.Migrations.BlockMigrationModSystem).Assembly;

  [Fact]
  public void Every_block_code_resolves_to_a_name_in_every_locale()
  {
    var missing = LangCoverage.MissingNames(Domain, Mod, $"assets/{Domain}/lang");

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
}
