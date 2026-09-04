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
      api.Logger.Warning,
      api.ModLoader.IsModEnabled
    );
    // Contributors run after the clear and the JSON overlay so their registrations survive a reload.
    MaterialRoleRegistry.InvokeContributors(api);
  }

  /// <summary>
  /// Registers every valid def from already-read catalogues, skipping and warning on a def with no
  /// role or with neither code nor path prefix. The caller must have cleared the registry first; this
  /// does not touch contributors. Exposed for unit tests.
  /// </summary>
  /// <param name="modPresent">Answers whether a mod id is loaded, gating
  /// <see cref="MaterialRoleDef.RequiresMod"/>. Null answers "nothing is loaded", which is the truthful
  /// headless reading and the one that keeps another mod's rows out of a bare test registry.</param>
  internal static void Overlay(
    IEnumerable<MaterialRoleCatalogue> catalogues,
    Action<string>? warn = null,
    // Qualified: Vintagestory.API.Common declares its own Func<,>, so the bare name is ambiguous here.
    System.Func<string, bool>? modPresent = null
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
        // Silently, and before registering: a row waiting on a mod the player does not have is the
        // ordinary case, so warning on it would fill the log with noise on every load.
        if (
          !string.IsNullOrEmpty(def.RequiresMod)
          && !(modPresent?.Invoke(def.RequiresMod!) ?? false)
        )
          continue;
        MaterialRoleRegistry.Register(def);
      }
    }
  }
}
