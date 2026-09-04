using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Localization guard over every mod's shipped lang files. English is the source of truth: each
/// translated locale must carry the same key set (a missing key renders as its raw code in game, an
/// extra one is dead weight), and every shared value must reference the same set of <c>{0}</c> /
/// <c>{1:F0}</c> argument placeholders (an index the format call has no argument for throws
/// <see cref="FormatException"/> at runtime, a dropped index loses information). Lang directories are
/// discovered from the source tree, so a new mod or locale is covered automatically.
/// </summary>
public class LangParityTests {
  private static readonly Regex Placeholder = new(
    @"\{(\d+)(?::[^}]*)?\}",
    RegexOptions.Compiled
  );

  /// <summary>One case per non-English locale file: (label, englishPath, localePath).</summary>
  public static IEnumerable<object[]> LocaleFiles() {
    // Lang files live at assets/<domain>/lang/ under the repo-root assets tree.
    string assets = Path.Combine(RepoRoot(), "assets");
    foreach (
      string enPath in Directory.EnumerateFiles(
        assets,
        "en.json",
        SearchOption.AllDirectories
      )
    ) {
      string norm = enPath.Replace('\\', '/');
      // Source lang assets only: no build output, no unrelated en.json.
      if (
        norm.Contains("/bin/")
        || !norm.Contains("/assets/")
        || !norm.Contains("/lang/")
      )
        continue;

      string dir = Path.GetDirectoryName(enPath)!;
      string domain = new DirectoryInfo(dir).Parent!.Name; // assets/<domain>/lang
      foreach (string locPath in Directory.EnumerateFiles(dir, "*.json")) {
        if (
          Path.GetFileName(locPath)
            .Equals("en.json", StringComparison.OrdinalIgnoreCase)
        )
          continue;
        yield return [$"{domain}/{Path.GetFileName(locPath)}", enPath, locPath];
      }
    }
  }

  [Theory]
  [MemberData(nameof(LocaleFiles))]
  public void Locale_has_exactly_the_english_key_set(
    string label,
    string enPath,
    string locPath
  ) {
    HashSet<string> en = [.. Load(enPath).Keys];
    HashSet<string> loc = [.. Load(locPath).Keys];

    List<string> missing = [.. en.Except(loc).Order()];
    List<string> extra = [.. loc.Except(en).Order()];

    Assert.True(
      missing.Count == 0 && extra.Count == 0,
      $"{label}: {missing.Count} missing, {extra.Count} extra vs en.\n"
        + (
          missing.Count > 0
            ? "  missing: " + string.Join(", ", missing.Take(20)) + "\n"
            : ""
        )
        + (
          extra.Count > 0 ? "  extra: " + string.Join(", ", extra.Take(20)) : ""
        )
    );
  }

  [Theory]
  [MemberData(nameof(LocaleFiles))]
  public void Locale_placeholders_match_english(
    string label,
    string enPath,
    string locPath
  ) {
    Dictionary<string, string> en = Load(enPath);
    Dictionary<string, string> loc = Load(locPath);

    List<string> drift = [];
    foreach ((string key, string enVal) in en) {
      // Missing keys are the other test's job; only compare values present in both.
      if (!loc.TryGetValue(key, out string? locVal))
        continue;
      HashSet<int> enIx = Indices(enVal);
      HashSet<int> locIx = Indices(locVal);
      if (!enIx.SetEquals(locIx))
        drift.Add($"{key} (en:[{Join(enIx)}] loc:[{Join(locIx)}])");
    }

    Assert.True(
      drift.Count == 0,
      $"{label}: placeholder drift in {drift.Count} key(s):\n  "
        + string.Join("\n  ", drift.Take(20))
    );
  }

  private static string Join(HashSet<int> ix) => string.Join(",", ix.Order());

  private static HashSet<int> Indices(string value) {
    HashSet<int> set = [];
    foreach (Match m in Placeholder.Matches(value))
      set.Add(int.Parse(m.Groups[1].Value));
    return set;
  }

  private static Dictionary<string, string> Load(string path) {
    var obj = JObject.Parse(File.ReadAllText(path));
    Dictionary<string, string> map = [];
    foreach (JProperty p in obj.Properties())
      map[p.Name] =
        p.Value.Type == JTokenType.String
          ? (string)p.Value!
          : p.Value.ToString();
    return map;
  }

  private static string RepoRoot() {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (
      dir != null
      && !File.Exists(Path.Combine(dir.FullName, "VintageStory.sln"))
    )
      dir = dir.Parent;
    return dir?.FullName
      ?? throw new InvalidOperationException(
        "Could not locate the repo root (VintageStory.sln) from "
          + AppContext.BaseDirectory
      );
  }
}
