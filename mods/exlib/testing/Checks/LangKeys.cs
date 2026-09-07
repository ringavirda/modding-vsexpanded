using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Every literal <c>Lang.Get("domain:key")</c> under a set of source roots must name a key the given
/// lang tree's English file actually carries. A miss is not an error at runtime:
/// <c>TranslationService.GetUnformatted</c> does one exact dictionary lookup and returns the key
/// verbatim when it fails, so the player is shown <c>iiex:furnace-heatinghearth-loaded</c> where a
/// sentence should be.
/// <para>
/// A lang file may write a key bare or domain-qualified and the game accepts both, so both sides are
/// normalised to the bare form before comparison. A literal naming a domain other than the lang
/// tree's own (and not a vanilla/compat domain, which is somebody else's to ship) fails outright
/// rather than being skipped - a domain we do not check for here is a dead key just the same.
/// </para>
/// </summary>
public static class LangKeys {
  /// <summary>A literal followed by <c>+</c>, or ending in a separator, is a prefix completed at
  /// runtime (<c>"iiex:bf-state-" + state</c>); the whole key is not knowable statically.</summary>
  private static readonly Regex Call = new(
    @"Lang\.Get\w*\(\s*""([a-z]+):([A-Za-z0-9_.-]+)""(\s*\+)?",
    RegexOptions.Compiled
  );

  /// <summary>Domains whose lang files are somebody else's to ship: vanilla's, and the compat targets
  /// under <c>.compat/</c>. Anything else a literal names must resolve against the lang tree
  /// <see cref="Check"/> is given.</summary>
  private static readonly HashSet<string> Foreign = ["game", "creative", "survival"];

  /// <summary>Every literal lang key found under <paramref name="sourceRoots"/> resolves in
  /// <paramref name="langTree"/>'s <c>en.json</c>. Empty means clean.</summary>
  public static IReadOnlyList<string> Check(
    IEnumerable<string> sourceRoots,
    string langTree
  ) {
    string domain = new DirectoryInfo(langTree).Parent?.Name ?? "";
    HashSet<string> keys = EnglishKeys(langTree);
    var missing = new List<string>();

    foreach ((string litDomain, string key, string file) in LiteralKeys(sourceRoots)) {
      // Vanilla's own keys are not ours to carry.
      if (Foreign.Contains(litDomain))
        continue;

      // A domain this tree does not ship used to be skipped, which made this guard go blind exactly
      // when it was needed most: rename or merge away a domain and every literal still naming it
      // silently stops being checked instead of failing.
      if (!string.Equals(litDomain, domain, StringComparison.Ordinal)) {
        missing.Add(
          $"{litDomain}:{key} ({file}) - no lang tree ships domain '{litDomain}'"
        );
        continue;
      }
      if (!keys.Contains(key))
        missing.Add($"{litDomain}:{key} ({file})");
    }

    return missing;
  }

  /// <summary>Every literal lang key found under <paramref name="sourceRoots"/> - the premise a caller
  /// asserts is non-empty, since a regex that stops matching would otherwise make <see cref="Check"/>
  /// pass trivially.</summary>
  public static IReadOnlyList<string> Literals(IEnumerable<string> sourceRoots) =>
    [.. LiteralKeys(sourceRoots).Select(t => $"{t.Domain}:{t.Key} ({t.File})")];

  #region Corpus

  private static IEnumerable<(
    string Domain,
    string Key,
    string File
  )> LiteralKeys(IEnumerable<string> sourceRoots) {
    string root = RepoPaths.Root;
    foreach (string srcRoot in sourceRoots) {
      if (!Directory.Exists(srcRoot))
        continue;
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
  }

  private static HashSet<string> EnglishKeys(string langTree) {
    var keys = new HashSet<string>(StringComparer.Ordinal);
    string path = Path.Combine(langTree, "en.json");
    if (!File.Exists(path))
      return keys;
    foreach (JProperty p in JObject.Parse(File.ReadAllText(path)).Properties())
      keys.Add(
        p.Name.Contains(':') ? p.Name[(p.Name.IndexOf(':') + 1)..] : p.Name
      );
    return keys;
  }

  #endregion
}
