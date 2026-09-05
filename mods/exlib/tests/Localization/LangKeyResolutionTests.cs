using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Every literal <c>Lang.Get("domain:key")</c> in the mods must name a key the English lang file
/// actually carries. A miss is not an error at runtime: <c>TranslationService.GetUnformatted</c> does
/// one exact dictionary lookup and returns the key verbatim when it fails, so the player is shown
/// <c>iiex:furnace-heatinghearth-loaded</c> where a sentence should be.
/// <para>
/// A lang file may write a key bare or domain-qualified and the game accepts both, so both sides are
/// normalised to the bare form before comparison.
/// </para>
/// </summary>
public class LangKeyResolutionTests {
  /// <summary>
  /// A literal followed by <c>+</c>, or ending in a separator, is a prefix completed at runtime
  /// (<c>"iiex:bf-state-" + state</c>); the whole key is not knowable statically.
  /// </summary>
  private static readonly Regex Call = new(
    @"Lang\.Get\w*\(\s*""([a-z]+):([A-Za-z0-9_.-]+)""(\s*\+)?",
    RegexOptions.Compiled
  );

  /// <summary>Domains whose lang files are somebody else's to ship: vanilla's, and the compat targets
  /// under <c>.compat/</c>. Anything else a literal names must be one of ours and must resolve.</summary>
  private static readonly HashSet<string> Foreign =
  [
    "game",
    "creative",
    "survival",
  ];

  [Fact]
  public void Every_literal_lang_key_resolves_in_english() {
    var langs = EnglishKeysByDomain();
    var missing = new List<string>();

    foreach ((string domain, string key, string file) in LiteralKeys()) {
      // Vanilla's own keys are not ours to carry.
      if (Foreign.Contains(domain))
        continue;

      // An unknown domain used to be skipped, which made this guard go blind exactly when it was
      // needed most: rename or merge away a domain and every literal still naming it silently stops
      // being checked instead of failing. A domain we do not ship is a dead key, so it fails.
      if (!langs.TryGetValue(domain, out HashSet<string>? keys)) {
        missing.Add(
          $"{domain}:{key} ({file}) - no lang tree ships domain '{domain}'"
        );
        continue;
      }
      if (!keys.Contains(key))
        missing.Add($"{domain}:{key} ({file})");
    }

    Assert.True(
      missing.Count == 0,
      $"{missing.Count} lang key(s) render as their own code in game: "
        + string.Join("; ", missing)
    );
  }

  [Fact]
  public void The_scan_finds_the_calls_it_is_meant_to_guard() {
    // A regex that stops matching turns the guard above into an unconditional pass.
    Assert.True(
      LiteralKeys().Count() > 100,
      "the Lang.Get scan found almost nothing - the pattern no longer matches the call sites"
    );
  }

  #region Corpus

  private static IEnumerable<(
    string Domain,
    string Key,
    string File
  )> LiteralKeys() {
    string root = RepoRoot();
    // Every mod's own source (src/) plus exlib's source generators - the whole non-test C# surface,
    // the same set the old single src/ tree held.
    var srcRoots = Directory
      .EnumerateDirectories(Path.Combine(root, "mods"))
      .SelectMany(mod =>
        new[] { Path.Combine(mod, "src"), Path.Combine(mod, "generators") }
      )
      .Where(Directory.Exists);

    foreach (string srcRoot in srcRoots)
      foreach (
        string path in Directory.EnumerateFiles(
          srcRoot,
          "*.cs",
          SearchOption.AllDirectories
        )
      ) {
        string rel = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (rel.Contains("/bin/") || rel.Contains("/obj/"))
          continue;
        foreach (Match m in Call.Matches(File.ReadAllText(path))) {
          string key = m.Groups[2].Value;
          if (m.Groups[3].Success || key.EndsWith('-'))
            continue;
          yield return (m.Groups[1].Value, key, rel);
        }
      }
  }

  private static Dictionary<string, HashSet<string>> EnglishKeysByDomain() {
    var byDomain = new Dictionary<string, HashSet<string>>(
      StringComparer.Ordinal
    );
    foreach (string assetsRoot in ExpandedLib.Testing.RepoPaths.AllAssetTrees()) {
      string path = Path.Combine(assetsRoot, "lang", "en.json");
      if (!File.Exists(path))
        continue;
      string domain = new DirectoryInfo(assetsRoot).Name;
      var keys = byDomain.TryGetValue(domain, out HashSet<string>? existing)
        ? existing
        : byDomain[domain] = new HashSet<string>(StringComparer.Ordinal);
      foreach (
        JProperty p in JObject.Parse(File.ReadAllText(path)).Properties()
      )
        keys.Add(
          p.Name.Contains(':') ? p.Name[(p.Name.IndexOf(':') + 1)..] : p.Name
        );
    }

    Assert.NotEmpty(byDomain);
    return byDomain;
  }

  private static string RepoRoot() {
    DirectoryInfo? dir = new(AppContext.BaseDirectory);
    while (
      dir != null
      && !File.Exists(Path.Combine(dir.FullName, "VintageStory.sln"))
    )
      dir = dir.Parent;
    Assert.True(dir != null, "could not locate repo root (VintageStory.sln)");
    return dir!.FullName;
  }

  #endregion
}
