using System;
using System.Collections.Generic;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace ExpandedLib.Materials;

/// <summary>
/// Populates <see cref="MaterialRoleRegistry"/> at <c>AssetsFinalize</c>: clear, overlay every domain's
/// <c>config/materialroles.json</c>, then invoke the registered code contributors (the mod-gated
/// registrations JSON cannot express). Mirrors <see cref="Metals.MetalCatalogueLoader"/> - exlib ships
/// no content of its own, so with no <c>materialroles.json</c> and no contributor the registry is empty
/// and nothing is classified. The overlay core (<see cref="Overlay"/>) is asset-free for unit tests;
/// only the thin asset read needs a running game.
/// </summary>
public static class MaterialRoleLoader
{
  /// <summary>Reads the assets and repopulates <see cref="MaterialRoleRegistry"/>. Call from
  /// <c>ExpandedLibModSystem.AssetsFinalize</c>.</summary>
  public static void Load(ICoreAPI api)
  {
    MaterialRoleRegistry.Clear();
    Overlay(
      AssetCatalogueLoader.GetMany<MaterialRoleCatalogue>(
        api,
        "config/materialroles.json"
      ),
      api.Logger.Warning
    );
    // After the clear + JSON, run mod-gated code contributors so their registrations survive the reload.
    MaterialRoleRegistry.InvokeContributors(api);
  }

  /// <summary>
  /// The pure overlay over already-read catalogues: registers every valid def (skipping and warning on
  /// a def with no role, or with neither code nor path prefix). Assumes the registry was cleared by the
  /// caller; does not touch contributors. Exposed for unit tests.
  /// </summary>
  internal static void Overlay(
    IEnumerable<MaterialRoleCatalogue> catalogues,
    Action<string>? warn = null
  )
  {
    foreach (MaterialRoleCatalogue cat in catalogues)
    {
      if (cat.Materials == null)
        continue;
      foreach (MaterialRoleDef def in cat.Materials)
      {
        if (string.IsNullOrEmpty(def.Role))
        {
          warn?.Invoke("[exlib] Skipping material role def with no role");
          continue;
        }
        if (string.IsNullOrEmpty(def.Code) && string.IsNullOrEmpty(def.PathPrefix))
        {
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
