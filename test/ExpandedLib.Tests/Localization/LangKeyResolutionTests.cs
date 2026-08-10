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
/// <c>iwex:furnace-heatinghearth-loaded</c> where a sentence should be.
/// <para>
/// A lang file may write a key bare or domain-qualified and the game accepts both, so both sides are
/// normalised to the bare form before comparison.
/// </para>
/// </summary>
public class LangKeyResolutionTests {
  /// <summary>
  /// A literal followed by <c>+</c>, or ending in a separator, is a prefix completed at runtime
  /// (<c>"iwex:bf-state-" + state</c>); the whole key is not knowable statically.
  /// </summary>
  private static readonly Regex Call = new(
    @"Lang\.Get\w*\(\s*""([a-z]+):([A-Za-z0-9_.-]+)""(\s*\+)?",
    RegexOptions.Compiled
  );

  [Fact]
  public void Every_literal_lang_key_resolves_in_english() {
    var langs = EnglishKeysByDomain();
    var missing = new List<string>();

    foreach ((string domain, string key, string file) in LiteralKeys()) {
      // Vanilla's own keys and other mods' are not ours to carry.
      if (!langs.TryGetValue(domain, out HashSet<string>? keys))
        continue;
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
    foreach (
      string path in Directory.EnumerateFiles(
        Path.Combine(root, "src"),
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
    string assets = Path.Combine(RepoRoot(), "assets");
    foreach (
      string path in Directory.EnumerateFiles(
        assets,
        "en.json",
        SearchOption.AllDirectories
      )
    ) {
      string norm = path.Replace('\\', '/');
      if (!norm.Contains("/lang/"))
        continue;
      string domain = new DirectoryInfo(Path.GetDirectoryName(path)!)
        .Parent!
        .Name;
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
