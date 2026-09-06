using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace ExpandedLib.Catalogues;

/// <summary>
/// Reads a JSON catalogue out of every domain's assets: the plain "read every
/// <c>assets/&lt;domain&gt;/&lt;pathBegins&gt;*.json</c> into a typed object and tell me what
/// failed" primitive underneath the metal and liquid registries, and the one
/// <see cref="ContributedCatalogueLoader{TSet, TRegistry}"/>'s own read step builds on for a
/// catalogue with C# contributors. Pulls every matching asset, deserializes each to the caller's
/// catalogue type, and returns them for a registry to key. Must run at <c>AssetsFinalize</c>, after
/// the VS patch pipeline has merged the raw JSON. The <c>config</c> category it reads is Universal,
/// so it populates identically on client and server. A malformed asset - including one carrying a
/// key the type does not declare - is reported and skipped so one bad file does not fail the whole
/// load.
/// </summary>
public static class AssetCatalogueLoader {
  /// <summary>
  /// One path's read: every asset that parsed, its source location alongside it (same index), the
  /// count of assets read whether or not they parsed, and one message per one that did not, naming the
  /// asset.
  /// </summary>
  /// <typeparam name="T">The catalogue type each asset deserializes to.</typeparam>
  /// <param name="Items">Every asset that parsed, deserialized to <typeparamref name="T"/>.</param>
  /// <param name="Sources">Each item's asset location, same index as <paramref name="Items"/>.</param>
  /// <param name="Files">How many assets matched the path, whether or not they parsed.</param>
  /// <param name="Errors">One message per asset that failed to parse, naming the asset.</param>
  public readonly record struct ReadResult<T>(
    IReadOnlyList<T> Items,
    IReadOnlyList<string> Sources,
    int Files,
    IReadOnlyList<string> Errors
  );

  /// <summary>
  /// Deserializes every loaded asset whose path begins with <paramref name="pathBegins"/> (across all
  /// domains) into <typeparamref name="T"/>. Pass a trailing slash (e.g. <c>"config/metals/"</c>) to
  /// match a directory's contents and not a sibling prefix. An unknown JSON key is a binding failure
  /// (see <see cref="Read{T}"/>) reported the same as any other malformed asset.
  /// </summary>
  /// <typeparam name="T">The catalogue type each matching asset deserializes to.</typeparam>
  /// <param name="api">Only <see cref="ICoreAPI.Assets"/> is used; safe to call from either side.</param>
  /// <param name="pathBegins">The asset path prefix to match, e.g. <c>"config/metals/"</c>.</param>
  /// <returns>Every asset that parsed. A malformed asset is dropped, not thrown.</returns>
  public static List<T> GetMany<T>(ICoreAPI api, string pathBegins)
    where T : class => [.. Read<T>(api, pathBegins).Items];

  /// <summary>
  /// As <see cref="GetMany{T}"/>, but keeps each item's source location and every failure, so the
  /// caller can build a <see cref="CatalogueLoadReport"/> naming exactly what went wrong and where.
  /// </summary>
  /// <typeparam name="T">The catalogue type each matching asset deserializes to.</typeparam>
  /// <param name="api">Only <see cref="ICoreAPI.Assets"/> is used; safe to call from either side.</param>
  /// <param name="pathBegins">The asset path prefix to match, e.g. <c>"config/metals/"</c>.</param>
  /// <returns>The full read outcome: parsed items, their sources, the file count, and errors.</returns>
  public static ReadResult<T> Read<T>(ICoreAPI api, string pathBegins)
    where T : class {
    var items = new List<T>();
    var sources = new List<string>();
    var errors = new List<string>();
    int files = 0;
    foreach (IAsset asset in api.Assets.GetMany(pathBegins)) {
      files++;
      T? def = SafeToObject<T>(asset, errors);
      if (def != null) {
        items.Add(def);
        sources.Add(asset.Location.ToString());
      }
    }
    return new ReadResult<T>(items, sources, files, errors);
  }

  // MissingMemberHandling.Error turns a misspelt or retired key into a JsonSerializationException
  // naming the member and the file position, rather than a silently-dropped field. A fresh settings
  // instance per asset: the engine's ToObject<T> may add a domain-specific AssetLocation converter to
  // the instance it is given, and sharing one across assets from different domains would accumulate a
  // converter per domain for the life of the process.
  private static T? SafeToObject<T>(IAsset asset, List<string> errors)
    where T : class {
    try {
      return asset.ToObject<T>(
        new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }
      );
    } catch (Exception e) {
      errors.Add($"{asset.Location}: {e.Message}");
      return null;
    }
  }
}
