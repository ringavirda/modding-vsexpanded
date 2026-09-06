using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace YourMod.Tests;

/// <summary>Every JSON file under your mod's <c>assets/</c> tree parses. Trivially true (no files
/// found, no failure) until your mod project has an assets folder to check - an empty xUnit
/// <c>[Theory]</c> would report as a failed run instead, so this is a plain fact that loops itself.</summary>
public class ShippedAssetJsonTests {
  private static string AssetsRoot([CallerFilePath] string here = "") =>
    Path.GetFullPath(
      Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "YourModProject", "assets")
    );

  [Fact]
  public void Every_shipped_json_asset_parses() {
    string root = AssetsRoot();
    if (!Directory.Exists(root))
      return;

    var offenders = new List<string>();
    foreach (string file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)) {
      try {
        JToken.Parse(File.ReadAllText(file));
      } catch (System.Exception ex) {
        offenders.Add($"{file}: {ex.Message}");
      }
    }

    Assert.True(offenders.Count == 0, string.Join("\n", offenders));
  }
}
