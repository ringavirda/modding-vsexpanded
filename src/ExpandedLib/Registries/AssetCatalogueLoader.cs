using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries;

/// <summary>
/// Reads a JSON catalogue out of every domain's assets, backing the metal and liquid registries.
/// Pulls every <c>assets/&lt;domain&gt;/&lt;pathBegins&gt;*.json</c>, deserializes each to
/// <typeparamref name="T"/>, and returns them for a registry to key. Must run at
/// <c>AssetsFinalize</c>, after the VS patch pipeline has merged the raw JSON. The <c>config</c>
/// category it reads is Universal, so it populates identically on client and server. A malformed
/// asset is logged and skipped so one bad file does not fail the whole load.
/// </summary>
public static class AssetCatalogueLoader {
  /// <summary>
  /// Deserializes every loaded asset whose path begins with <paramref name="pathBegins"/> (across all
  /// domains) into <typeparamref name="T"/>. Pass a trailing slash (e.g. <c>"config/metals/"</c>) to
  /// match a directory's contents and not a sibling prefix.
  /// </summary>
  public static List<T> GetMany<T>(ICoreAPI api, string pathBegins)
    where T : class {
    var result = new List<T>();
    foreach (IAsset asset in api.Assets.GetMany(pathBegins)) {
      T? def = SafeToObject<T>(api, asset);
      if (def != null)
        result.Add(def);
    }
    return result;
  }

  private static T? SafeToObject<T>(ICoreAPI api, IAsset asset)
    where T : class {
    try {
      return asset.ToObject<T>();
    } catch (Exception e) {
      api.Logger.Warning(
        "[exlib] Skipping malformed catalogue asset {0}: {1}",
        asset.Location,
        e.Message
      );
      return null;
    }
  }
}
