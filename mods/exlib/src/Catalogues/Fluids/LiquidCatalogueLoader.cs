using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ExpandedLib.Catalogues;

/// <summary>
/// Populates <see cref="ExLiquids"/> at <c>AssetsFinalize</c>: re-seed the four built-in media, overlay
/// every domain's <c>config/liquids.json</c> via <see cref="AssetCatalogueLoader"/>, then invoke the
/// registered code contributors. Mirrors <see cref="ExpandedLib.Industry.Metals.MetalCatalogueLoader"/>.
/// </summary>
public static class LiquidCatalogueLoader {
  /// <summary>Re-seeds the built-ins, overlays every domain's <c>config/liquids.json</c> and runs the
  /// code contributors. Call from <c>ExpandedLibModSystem.AssetsFinalize</c>.</summary>
  public static CatalogueLoadReport Load(ICoreAPI api) {
    ExLiquids.Clear();
    ExLiquids.SeedDefaults();

    AssetCatalogueLoader.ReadResult<LiquidCatalogue> read =
      AssetCatalogueLoader.Read<LiquidCatalogue>(api, "config/liquids.json");
    var errors = new List<string>(read.Errors);
    int entries = 0;
    for (int i = 0; i < read.Items.Count; i++) {
      LiquidCatalogue cat = read.Items[i];
      if (cat.Liquids == null)
        continue;
      foreach (LiquidDef def in cat.Liquids) {
        if (string.IsNullOrEmpty(def.Code)) {
          errors.Add($"{read.Sources[i]}: skipping liquid def with no code");
          continue;
        }
        ExLiquids.Register(def);
        entries++;
      }
    }
    ExLiquids.Contributors.Invoke(api, api.Logger);

    return new CatalogueLoadReport("liquids", read.Files, entries, errors);
  }
}
