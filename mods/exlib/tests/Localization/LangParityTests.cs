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
    // Lang files live at mods/<mod>/assets/<domain>/lang/ - one tree per shipped domain.
    foreach (string assetsRoot in ExpandedLib.Testing.RepoPaths.AllAssetTrees()) {
      string domain = new DirectoryInfo(assetsRoot).Name;
      string dir = Path.Combine(assetsRoot, "lang");
      string enPath = Path.Combine(dir, "en.json");
      if (!File.Exists(enPath))
        continue;

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
}
