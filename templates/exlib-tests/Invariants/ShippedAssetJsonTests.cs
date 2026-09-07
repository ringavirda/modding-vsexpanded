using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using ExpandedLib.Testing;
using Xunit;

namespace YourMod.Tests;

/// <summary>The per-mod JSON-defect rule (<see cref="ShippedJson"/>) over your mod's <c>assets/</c>
/// tree. Trivially true (no files found, no failure) until your mod project has an assets folder to
/// check.</summary>
public class ShippedAssetJsonTests {
  private static string AssetsRoot([CallerFilePath] string here = "") =>
    Path.GetFullPath(
      Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "YourModProject", "assets")
    );

  [Fact]
  public void Every_shipped_json_asset_parses() {
    IReadOnlyList<string> offenders = ShippedJson.Check(AssetsRoot());
    Assert.True(offenders.Count == 0, string.Join("\n", offenders));
  }
}
