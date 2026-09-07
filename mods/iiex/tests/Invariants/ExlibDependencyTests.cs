using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExpandedLib.Testing;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// modinfo.json's <c>exlib</c> dependency is a floor, not a pin (see siex's
/// <c>A_dependency_on_a_sibling_mod_is_not_below_that_mod_s_version</c>) - but exlib no longer builds
/// in this repository, so that guard cannot compare it against a sibling's own version any more. This
/// compares it against the version this repository actually restores exlib at: the
/// <c>ExpandedLib</c> row in <c>Directory.Packages.props</c>, package mode's source of truth and
/// source mode's ceiling (a source-mode build against a newer exlib checkout is exactly what this
/// pair is meant to catch drifting apart).
/// </summary>
public class ExlibDependencyTests {
  [Fact]
  public void The_exlib_floor_matches_the_ExpandedLib_package_row() {
    string modinfo = File.ReadAllText(
      Path.Combine(RepoPaths.Src("iiex"), "modinfo.json")
    );
    using JsonDocument doc = JsonDocument.Parse(modinfo);
    string floor = doc
      .RootElement.GetProperty("dependencies")
      .GetProperty("exlib")
      .GetString()!;

    string packages = File.ReadAllText(
      Path.Combine(RepoPaths.Root, "Directory.Packages.props")
    );
    Match m = Regex.Match(
      packages,
      @"<PackageVersion\s+Include=""ExpandedLib""\s+Version=""([^""]+)""\s*/>"
    );
    Assert.True(
      m.Success,
      "Directory.Packages.props declares no ExpandedLib PackageVersion row"
    );

    Assert.Equal(m.Groups[1].Value, floor);
  }
}
