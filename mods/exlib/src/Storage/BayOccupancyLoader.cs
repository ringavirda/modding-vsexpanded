using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Storage;

/// <summary>
/// Reads the bay-occupancy catalogue - every domain's <c>config/bayoccupancy/*.json</c> - and populates
/// <see cref="BayOccupancyRegistry"/> from it. One file per store is the convention and nothing enforces
/// it: the registry merges whatever arrives, so two mods may both make their stock rackable. The sibling
/// of <c>ProcessJobLoader</c>, and deliberately the same shape.
/// </summary>
public static class BayOccupancyLoader {
  /// <summary>The asset path every domain's rules are read from.</summary>
  public const string CataloguePath = "config/bayoccupancy/";

  /// <summary>
  /// Parses the catalogue out of already-read files, each a <c>(source, json)</c> pair whose source names
  /// the file in an error. A malformed file is reported and skipped, so one bad declaration does not cost
  /// every other mod its rules. Asset-free, so it runs headless.
  /// </summary>
  public static List<BayOccupancySet> Parse(
    IEnumerable<(string Source, string Json)> files,
    out List<string> errors
  ) {
    var sets = new List<BayOccupancySet>();
    errors = [];

    foreach ((string source, string json) in files) {
      JToken token;
      try {
        token = JToken.Parse(json);
      } catch (Exception e) {
        errors.Add($"{source}: not readable as JSON - {e.Message}");
        continue;
      }

      if (
        !BayOccupancySet.TryParse(
          new JsonObject(token),
          out BayOccupancySet? set,
          out string? error
        )
      ) {
        errors.Add($"{source}: {error}");
        continue;
      }
      sets.Add(set!);
    }
    return sets;
  }

  /// <summary>
  /// Parses <paramref name="files"/> and replaces <paramref name="registry"/>'s contents with them (the
  /// shared registry when null). Returns one message per malformed file or clash, naming the file it came
  /// from.
  /// </summary>
  public static List<string> Load(
    IEnumerable<(string Source, string Json)> files,
    BayOccupancyRegistry? registry = null
  ) {
    registry ??= BayOccupancyRegistry.Shared;

    var sources = new List<(string Source, string Json)>(files);
    List<BayOccupancySet> sets = Parse(sources, out List<string> errors);

    registry.Clear();
    for (int i = 0; i < sets.Count; i++) {
      // Parse preserves order, so when nothing was dropped the nth set is the nth file; otherwise the
      // store name is the honest answer.
      string source =
        sources.Count == sets.Count ? sources[i].Source : sets[i].Store;
      foreach (string conflict in registry.Contribute(sets[i]))
        errors.Add($"{source}: {conflict}");
    }
    return errors;
  }

  /// <summary>Reads every domain's catalogue out of the asset manager.</summary>
  public static List<(string Source, string Json)> Read(ICoreAPI api) {
    var files = new List<(string, string)>();
    foreach (IAsset asset in api.Assets.GetMany(CataloguePath))
      files.Add((asset.Location.ToString(), asset.ToText()));
    return files;
  }

  /// <summary>Reads the catalogue and repopulates the shared registry. Call from
  /// <c>ExpandedLibModSystem.AssetsFinalize</c>.</summary>
  public static List<string> Load(ICoreAPI api) => Load(Read(api));
}
