using System;
using System.Collections.Generic;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace ExpandedLib.Metals;

/// <summary>
/// Populates <see cref="MetalRegistry"/> at <c>AssetsFinalize</c> in two passes: a derived baseline of
/// one convention entry per metal in every loaded <c>worldproperties/block/metal</c> (vanilla and mods),
/// so a metal shipping no <see cref="MetalDef"/> still has an entry, then an overlay of every domain's
/// <c>config/metals/*.json</c> <see cref="MetalDef"/>, which enriches or replaces that entry. The
/// registry is cleared first, so a world reload in the same process repopulates rather than
/// accumulating stale entries.
/// </summary>
public static class MetalCatalogueLoader {
  /// <summary>Reads the assets and repopulates <see cref="MetalRegistry"/>. Call from
  /// <c>ExpandedLibModSystem.AssetsFinalize</c>.</summary>
  public static void Load(ICoreAPI api) {
    MetalRegistry.Clear();
    Populate(
      WorldPropertyMetalCodes(api),
      AssetCatalogueLoader.GetMany<MetalDef>(api, "config/metals/"),
      api.Logger.Warning
    );
  }

  /// <summary>
  /// The two passes over already-read inputs: a convention baseline for every
  /// <paramref name="baselineCodes"/> metal short-code, then the <paramref name="overlays"/>, which
  /// replace by molten-item or short code. The caller must clear the registry first. Asset-free so it
  /// can be unit-tested.
  /// </summary>
  internal static void Populate(
    IEnumerable<string> baselineCodes,
    IEnumerable<MetalDef> overlays,
    Action<string>? warn = null
  ) {
    // Pass 1 - baseline. Pure string work, no item resolution: the derived entry matches what the
    // convention branch produces, so it only makes the metal enumerable and gives pass 2 something
    // to enrich.
    foreach (string raw in baselineCodes) {
      string code = ShortCode(raw);
      if (code.Length == 0)
        continue;
      string moltenItem = new AssetLocation("game", "ingot-" + code).ToString();
      if (MetalRegistry.TryGet(moltenItem, out _))
        continue; // two worldproperties listing the same metal - derive once
      MetalRegistry.Register(
        new MetalDef { Code = code, MoltenItem = moltenItem }
      );
    }

    // Pass 2 - overlay. A def missing either required field cannot be keyed, so it is skipped.
    foreach (MetalDef def in overlays) {
      if (
        string.IsNullOrEmpty(def.Code) || string.IsNullOrEmpty(def.MoltenItem)
      ) {
        warn?.Invoke(
          "[exlib] Skipping metal def missing code/moltenItem (code='"
            + def.Code
            + "', moltenItem='"
            + def.MoltenItem
            + "')"
        );
        continue;
      }
      MetalRegistry.Register(def);
    }
  }

  // Every metal short-code declared in every loaded metal worldproperty (vanilla + mods).
  private static IEnumerable<string> WorldPropertyMetalCodes(ICoreAPI api) {
    var codes = new List<string>();
    foreach (IAsset asset in api.Assets.GetMany("worldproperties/block/metal")) {
      MetalWorldProperty? wp = null;
      try {
        wp = asset.ToObject<MetalWorldProperty>();
      } catch (Exception e) {
        api.Logger.Warning(
          "[exlib] Skipping malformed metal worldproperty {0}: {1}",
          asset.Location,
          e.Message
        );
      }
      if (wp?.Variants == null)
        continue;
      foreach (MetalWorldVariant v in wp.Variants)
        if (!string.IsNullOrEmpty(v.Code))
          codes.Add(v.Code);
    }
    return codes;
  }

  // Worldproperty codes are bare ("copper") or "domain:copper"; take the segment after any colon.
  private static string ShortCode(string worldPropCode) {
    if (string.IsNullOrEmpty(worldPropCode))
      return "";
    int colon = worldPropCode.IndexOf(':');
    return colon >= 0 ? worldPropCode[(colon + 1)..] : worldPropCode;
  }

  // Minimal shapes for reading the metal worldproperty; only the variant codes are needed.
  private sealed class MetalWorldProperty {
    public List<MetalWorldVariant>? Variants { get; set; }
  }

  private sealed class MetalWorldVariant {
    public string Code { get; set; } = "";
  }
}
