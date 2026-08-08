using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Registries.Config;

/// <summary>
/// A shared, mod-sectioned config file under <c>ModConfig</c> (e.g. <c>ex_values.json</c> /
/// <c>ex_recipes.json</c>): one file whose top-level keys are mod ids, each holding that mod's config
/// object. Each <see cref="ExConfigRegister{TConfig}"/> reads and writes only its own section, and a
/// mod's nested <c>ConfigVersion</c> and migrations are unaffected by the sharing.
/// <para>
/// The document is cached per <see cref="ICoreAPI"/> instance: all mods in a running game share one
/// API and therefore one document loaded once, while each headless test has its own API and so its
/// own cache. Mod load is single-threaded and sections bind sequentially, so between a load and its
/// <see cref="Flush"/> the in-memory document, not the file, holds the current state.
/// </para>
/// </summary>
public sealed class ExConfigDocument {
  private static readonly ConditionalWeakTable<
    ICoreAPI,
    Dictionary<string, ExConfigDocument>
  > _byApi = new();

  private readonly ICoreAPI _api;
  private readonly string _fileName;
  private readonly JObject _doc;

  private ExConfigDocument(ICoreAPI api, string fileName) {
    _api = api;
    _fileName = fileName;
    _doc = LoadOrEmpty();
  }

  /// <summary>Returns the shared document for <paramref name="fileName"/>, loading it once per API.</summary>
  public static ExConfigDocument ForFile(ICoreAPI api, string fileName) {
    var perApi = _byApi.GetValue(
      api,
      _ => new Dictionary<string, ExConfigDocument>(
        StringComparer.OrdinalIgnoreCase
      )
    );
    if (!perApi.TryGetValue(fileName, out var doc)) {
      doc = new ExConfigDocument(api, fileName);
      perApi[fileName] = doc;
    }
    return doc;
  }

  /// <summary>True if the document already carries a section for <paramref name="modId"/>.</summary>
  public bool HasSection(string modId) => _doc[modId] is JObject;

  /// <summary>Deserializes <paramref name="modId"/>'s section to <typeparamref name="TConfig"/>, or
  /// <c>null</c> if the section is absent or unreadable (the caller then uses coded defaults).</summary>
  public TConfig? GetSection<TConfig>(string modId)
    where TConfig : class {
    if (_doc[modId] is not JObject section)
      return null;
    try {
      return section.ToObject<TConfig>();
    } catch (Exception e) {
      _api.Logger.Warning(
        "[{0}] Config '{1}' section could not be read; using defaults. {2}",
        modId,
        _fileName,
        e
      );
      return null;
    }
  }

  /// <summary>Replaces <paramref name="modId"/>'s section in memory with <paramref name="value"/>.</summary>
  public void SetSection(string modId, object value) =>
    _doc[modId] = JObject.FromObject(value);

  /// <summary>Writes the whole document back to disk.</summary>
  public void Flush() => _api.StoreModConfig(_doc, _fileName);

  /// <summary>
  /// One-time migration of a legacy per-mod file into this document's <paramref name="modId"/>
  /// section: when the section is absent and a legacy file exists under <c>ModConfig</c>, its
  /// contents become the section and the old file is renamed to <c>&lt;name&gt;.migrated</c> rather
  /// than deleted, keeping the carry-over reversible. First existing name wins. No-op once the
  /// section exists, so it never re-runs.
  /// </summary>
  public void FoldLegacy(string modId, IReadOnlyList<string> legacyFileNames) {
    if (
      legacyFileNames == null
      || legacyFileNames.Count == 0
      || HasSection(modId)
    )
      return;

    foreach (var legacy in legacyFileNames) {
      if (string.IsNullOrWhiteSpace(legacy))
        continue;
      string path = Path.Combine(GamePaths.ModConfig, legacy);
      if (!File.Exists(path))
        continue;

      try {
        _doc[modId] = JObject.Parse(File.ReadAllText(path));
        File.Move(path, path + ".migrated");
        _api.Logger.Notification(
          "[{0}] Folded legacy config '{1}' into the '{0}' section of '{2}'.",
          modId,
          legacy,
          _fileName
        );
      } catch (Exception e) {
        _api.Logger.Warning(
          "[{0}] Could not fold legacy config '{1}' into '{2}': {3}",
          modId,
          legacy,
          _fileName,
          e
        );
      }
      return;
    }
  }

  private JObject LoadOrEmpty() {
    try {
      return _api.LoadModConfig<JObject>(_fileName) ?? new JObject();
    } catch (Exception e) {
      // A parse failure would otherwise take out every mod's section at once. Back the bad file up
      // and start from an empty document, leaving each section on its coded defaults.
      _api.Logger.Warning(
        "[exlib] Config file '{0}' could not be parsed; backing it up to '{0}.corrupt' and starting fresh. {1}",
        _fileName,
        e
      );
      TryBackupCorrupt();
      return new JObject();
    }
  }

  private void TryBackupCorrupt() {
    try {
      string path = Path.Combine(GamePaths.ModConfig, _fileName);
      if (File.Exists(path))
        File.Copy(path, path + ".corrupt", overwrite: true);
    } catch {
      // Best effort - a failed backup must not stop the game from starting on defaults.
    }
  }
}
