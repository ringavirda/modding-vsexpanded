using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Verify;

/// <summary>
/// Every JSON asset loaded for this run, indexed the way the game itself addresses one:
/// <c>domain</c> (the top-level folder under a root's <c>assets/</c>) then a forward-slash path
/// relative to that domain, lower-cased to match <see cref="Vintagestory.API.Common.AssetLocation"/>'s
/// own normalisation. A domain can be fed by more than one root (a game install and a mod both
/// shipping <c>assets/game/...</c>), later roots overriding earlier ones on a path collision - the
/// same "last loaded wins" rule the game's own asset manager applies to a mod-vs-vanilla clash.
/// <para>
/// Holds parsed <see cref="JToken"/>s, not raw text: a file that fails to parse is dropped with a
/// call to <paramref name="onParseError"/> when one is supplied (used only for the primary mod,
/// whose own parse errors are a required finding; a game or <c>--mods</c> file that fails to parse
/// is silently absent from the index instead, so a target patch simply reports "not found" rather
/// than compounding into a second, less precise error).
/// </para>
/// </summary>
public sealed class AssetStore {
  // domain -> path -> parsed json.
  private readonly Dictionary<string, Dictionary<string, JToken>> _byDomain =
    new(StringComparer.Ordinal);

  /// <summary>Every domain this store has at least one file for.</summary>
  public IEnumerable<string> Domains => _byDomain.Keys;

  /// <summary>Loads every <c>*.json</c> file under <c>&lt;root&gt;/assets/*&#47;</c> into the
  /// store, one domain per first-level folder name (remapped through <paramref name="remapDomain"/>
  /// first, when given).</summary>
  /// <param name="remapDomain">
  /// A vanilla install's <c>assets/survival</c> and <c>assets/creative</c> are asset ORIGINS, not
  /// domains of their own - every code they define still resolves under the single <c>game</c>
  /// domain (the same "game:itemtypes/..." a compat patch names even though the file physically
  /// sits under <c>assets/survival/itemtypes/...</c>). <see cref="GameInstall"/>'s caller passes a
  /// remap folding both into <c>"game"</c>; a mod root passes none, since a mod's own asset folder
  /// name is its real domain.
  /// </param>
  public void AddRoot(
    string root,
    Func<string, string>? remapDomain = null,
    Action<string, string, int, int>? onParseError = null
  ) {
    string assetsDir = Path.Combine(root, "assets");
    if (!Directory.Exists(assetsDir))
      return;

    foreach (string domainDir in Directory.EnumerateDirectories(assetsDir)) {
      string domain = (
        remapDomain?.Invoke(Path.GetFileName(domainDir))
        ?? Path.GetFileName(domainDir)
      ).ToLowerInvariant();
      Dictionary<string, JToken> files = _byDomain.TryGetValue(
        domain,
        out var existing
      )
        ? existing
        : _byDomain[domain] = new Dictionary<string, JToken>(
          StringComparer.Ordinal
        );

      foreach (
        string file in Directory.EnumerateFiles(
          domainDir,
          "*.json",
          SearchOption.AllDirectories
        )
      ) {
        string relPath = Path.GetRelativePath(domainDir, file)
          .Replace(Path.DirectorySeparatorChar, '/')
          .ToLowerInvariant();
        string text = File.ReadAllText(file);
        try {
          files[relPath] = JToken.Parse(text);
        } catch (JsonReaderException reader) {
          onParseError?.Invoke(
            file,
            reader.Message,
            reader.LineNumber,
            reader.LinePosition
          );
        } catch (JsonException e) {
          onParseError?.Invoke(file, e.Message, 0, 0);
        }
      }
    }
  }

  /// <summary>The parsed JSON at <c>domain:path</c> (path without a leading slash, <c>.json</c>
  /// included), or null if no loaded root shipped it.</summary>
  public JToken? TryGet(string domain, string path) =>
    _byDomain.TryGetValue(domain.ToLowerInvariant(), out var files)
    && files.TryGetValue(path.ToLowerInvariant(), out JToken? token)
      ? token
      : null;

  /// <summary>Every loaded path under <paramref name="domain"/> whose path starts with
  /// <paramref name="prefix"/> (used for a patch's file-wildcard and for a category scan such as
  /// <c>recipes/</c>), paired with its parsed JSON.</summary>
  public IEnumerable<(string Path, JToken Json)> Under(
    string domain,
    string prefix
  ) {
    if (!_byDomain.TryGetValue(domain.ToLowerInvariant(), out var files))
      yield break;
    string p = prefix.ToLowerInvariant();
    foreach (var (path, json) in files)
      if (path.StartsWith(p, StringComparison.Ordinal))
        yield return (path, json);
  }

  /// <summary>Overwrites <c>domain:path</c> with <paramref name="patched"/> - how a patch's effect
  /// on one file becomes visible to every check and every later patch against the same file, the
  /// same cumulative behaviour <c>ModJsonPatchLoader</c>'s own document cache gives.</summary>
  public void Set(string domain, string path, JToken patched) {
    Dictionary<string, JToken> files = _byDomain.TryGetValue(
      domain.ToLowerInvariant(),
      out var existing
    )
      ? existing
      : _byDomain[domain.ToLowerInvariant()] = new Dictionary<string, JToken>(
        StringComparer.Ordinal
      );
    files[path.ToLowerInvariant()] = patched;
  }
}
