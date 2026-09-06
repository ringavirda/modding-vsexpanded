using System;
using System.Collections.Generic;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace ExpandedLib.Catalogues;

/// <summary>
/// Populates <see cref="MaterialRoleRegistry"/> at <c>AssetsFinalize</c>: clear, overlay every domain's
/// <c>config/materialroles.json</c>, then invoke the registered code contributors that cover the
/// mod-gated registrations JSON cannot express. With no <c>materialroles.json</c> and no contributor the
/// registry stays empty and nothing is classified. <see cref="Overlay"/> is asset-free so it can be
/// unit-tested; only the asset read needs a running game.
/// </summary>
public static class MaterialRoleLoader {
  /// <summary>Reads the assets, repopulates <see cref="MaterialRoleRegistry"/> and runs its code
  /// contributors. Call from <c>ExpandedLibModSystem.AssetsFinalize</c>.</summary>
  public static CatalogueLoadReport Load(ICoreAPI api) {
    MaterialRoleRegistry.Clear();
    AssetCatalogueLoader.ReadResult<MaterialRoleCatalogue> read =
      AssetCatalogueLoader.Read<MaterialRoleCatalogue>(
        api,
        "config/materialroles.json"
      );
    var warnings = new List<string>();
    int registered = Overlay(
      read.Items,
      warnings.Add,
      api.ModLoader.IsModEnabled,
      read.Sources
    );
    // Contributors run after the clear and the JSON overlay so their registrations survive a reload.
    MaterialRoleRegistry.InvokeContributors(api);

    var errors = new List<string>(read.Errors);
    errors.AddRange(warnings);
    return new CatalogueLoadReport("materialroles", read.Files, registered, errors);
  }

  /// <summary>
  /// Registers every valid def from already-read catalogues, skipping and warning on a def with no
  /// role or with neither code nor path prefix. The caller must have cleared the registry first; this
  /// does not touch contributors. Exposed for unit tests.
  /// </summary>
  /// <param name="modPresent">Answers whether a mod id is loaded, gating
  /// <see cref="MaterialRoleDef.RequiresMod"/>. Null answers "nothing is loaded", which is the truthful
  /// headless reading and the one that keeps another mod's rows out of a bare test registry.</param>
  /// <param name="sources">One source location per entry of <paramref name="catalogues"/>, same index,
  /// named in a skip warning. Null (the default, for tests that build catalogues by hand) reads as
  /// "unknown source".</param>
  /// <returns>How many defs across every catalogue were registered.</returns>
  internal static int Overlay(
    IEnumerable<MaterialRoleCatalogue> catalogues,
    Action<string>? warn = null,
    // Qualified: Vintagestory.API.Common declares its own Func<,>, so the bare name is ambiguous here.
    System.Func<string, bool>? modPresent = null,
    IReadOnlyList<string>? sources = null
  ) {
    var catalogueList =
      catalogues as IReadOnlyList<MaterialRoleCatalogue> ?? [.. catalogues];
    int registered = 0;
    for (int i = 0; i < catalogueList.Count; i++) {
      MaterialRoleCatalogue cat = catalogueList[i];
      if (cat.Materials == null)
        continue;
      string source =
        sources != null && i < sources.Count ? sources[i] : "unknown source";
      foreach (MaterialRoleDef def in cat.Materials) {
        if (string.IsNullOrEmpty(def.Role)) {
          warn?.Invoke(source + ": skipping material role def with no role");
          continue;
        }
        if (
          string.IsNullOrEmpty(def.Code) && string.IsNullOrEmpty(def.PathPrefix)
        ) {
          warn?.Invoke(
            source
              + ": skipping material role def '"
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
        registered++;
      }
    }
    return registered;
  }
}
