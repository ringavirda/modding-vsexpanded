using System;
using System.Collections.Generic;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace ExpandedLib.Materials;

/// <summary>
/// Populates <see cref="MaterialRoleRegistry"/> at <c>AssetsFinalize</c>: clear, overlay every domain's
/// <c>config/materialroles.json</c>, then invoke the registered code contributors that cover the
/// mod-gated registrations JSON cannot express. With no <c>materialroles.json</c> and no contributor the
/// registry stays empty and nothing is classified. <see cref="Overlay"/> is asset-free so it can be
/// unit-tested; only the asset read needs a running game.
/// </summary>
public static class MaterialRoleLoader {
  /// <summary>Reads the assets and repopulates <see cref="MaterialRoleRegistry"/>. Call from
  /// <c>ExpandedLibModSystem.AssetsFinalize</c>.</summary>
  public static void Load(ICoreAPI api) {
    MaterialRoleRegistry.Clear();
    Overlay(
      AssetCatalogueLoader.GetMany<MaterialRoleCatalogue>(
        api,
        "config/materialroles.json"
      ),
      api.Logger.Warning
    );
    // Contributors run after the clear and the JSON overlay so their registrations survive a reload.
    MaterialRoleRegistry.InvokeContributors(api);
  }

  /// <summary>
  /// Registers every valid def from already-read catalogues, skipping and warning on a def with no
  /// role or with neither code nor path prefix. The caller must have cleared the registry first; this
  /// does not touch contributors. Exposed for unit tests.
  /// </summary>
  internal static void Overlay(
    IEnumerable<MaterialRoleCatalogue> catalogues,
    Action<string>? warn = null
  ) {
    foreach (MaterialRoleCatalogue cat in catalogues) {
      if (cat.Materials == null)
        continue;
      foreach (MaterialRoleDef def in cat.Materials) {
        if (string.IsNullOrEmpty(def.Role)) {
          warn?.Invoke("[exlib] Skipping material role def with no role");
          continue;
        }
        if (
          string.IsNullOrEmpty(def.Code) && string.IsNullOrEmpty(def.PathPrefix)
        ) {
          warn?.Invoke(
            "[exlib] Skipping material role def '"
              + def.Role
              + "' with neither code nor pathPrefix"
          );
          continue;
        }
        MaterialRoleRegistry.Register(def);
      }
    }
  }
}
