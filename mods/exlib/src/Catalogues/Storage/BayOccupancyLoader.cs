using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Catalogues;

/// <summary>
/// Reads the bay-occupancy catalogue - every domain's <c>config/bayoccupancy/*.json</c> - and populates
/// <see cref="BayOccupancyRegistry"/> from it. One file per store is the convention and nothing enforces
/// it: the registry merges whatever arrives, so two mods may both make their stock rackable. The sibling
/// of <c>ProcessJobLoader</c>, and deliberately the same shape.
/// </summary>
public sealed class BayOccupancyLoader
  : ContributedCatalogueLoader<BayOccupancySet, BayOccupancyRegistry> {
  /// <summary>The asset path every domain's rules are read from.</summary>
  public const string CataloguePath = "config/bayoccupancy/";

  private static readonly BayOccupancyLoader _instance = new();

  // The root keys BayOccupancySet.TryParse reads, and the keys each item entry reads.
  // "schema" is accepted though BayOccupancySet.TryParse does not yet read it, for parity with the
  // route and job catalogues' versioning convention every shipped file already carries.
  private static readonly HashSet<string> RootKeys = ["schema", "store", "items"];
  private static readonly HashSet<string> ItemKeys = ["item", "cells"];

  private BayOccupancyLoader() { }

  protected override string AssetPath => CataloguePath;
  protected override string CatalogueName => "bayoccupancy";

  protected override IReadOnlyList<string> UnknownKeys(JsonObject root) =>
    [
      .. JsonKeyAudit.UnknownKeys(root, RootKeys),
      .. (root["items"].AsArray() ?? []).SelectMany(item =>
        JsonKeyAudit.UnknownKeys(item, ItemKeys)
      ),
    ];

  protected override bool TryParse(
    JsonObject root,
    out BayOccupancySet set,
    out string? error
  ) {
    bool ok = BayOccupancySet.TryParse(root, out BayOccupancySet? parsed, out error);
    set = parsed!;
    return ok;
  }

  protected override IReadOnlyList<string> Contribute(
    BayOccupancyRegistry registry,
    BayOccupancySet set
  ) => registry.Contribute(set);

  protected override int CountEntries(BayOccupancySet set) => set.Rules.Count;

  protected override CatalogueContributors Contributors(BayOccupancyRegistry registry) =>
    BayOccupancyRegistry.Contributors;

  protected override void Clear(BayOccupancyRegistry registry) => registry.Clear();

  /// <summary>
  /// Parses the catalogue out of already-read files, each a <c>(source, json)</c> pair whose source names
  /// the file in an error. A malformed file - including one carrying a key the schema does not read - is
  /// reported and skipped, so one bad declaration does not cost every other mod its rules. Asset-free, so
  /// it runs headless.
  /// </summary>
  public static List<BayOccupancySet> Parse(
    IEnumerable<(string Source, string Json)> files,
    out List<string> errors
  ) => _instance.ParseFiles(files, out errors);

  /// <summary>
  /// Parses <paramref name="files"/> and replaces <paramref name="registry"/>'s contents with them (the
  /// shared registry when null). Returns one message per malformed file or clash, naming the file it came
  /// from.
  /// </summary>
  public static List<string> Load(
    IEnumerable<(string Source, string Json)> files,
    BayOccupancyRegistry? registry = null
  ) => _instance.MergeFiles(files, registry ?? BayOccupancyRegistry.Shared);

  /// <summary>Reads every domain's catalogue out of the asset manager.</summary>
  public static List<(string Source, string Json)> Read(ICoreAPI api) =>
    _instance.ReadAssets(api);

  /// <summary>Reads the catalogue, repopulates the shared registry and runs its code contributors. Call
  /// from <c>ExpandedLibModSystem.AssetsFinalize</c>.</summary>
  public static CatalogueLoadReport Load(ICoreAPI api) =>
    _instance.LoadCatalogue(api, BayOccupancyRegistry.Shared);
}
