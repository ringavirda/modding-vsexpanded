using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Catalogues;

/// <summary>
/// Reads the terminal-job catalogue - every domain's <c>config/processjobs/*.json</c> - and populates
/// <see cref="ProcessJobRegistry"/> from it. One file per machine is the convention and nothing enforces
/// it: the registry merges whatever arrives, so two mods may both add jobs to one machine. The sibling of
/// <see cref="ProcessRouteLoader"/>, and deliberately the same shape.
/// </summary>
public sealed class ProcessJobLoader
  : ContributedCatalogueLoader<ProcessJobSet, ProcessJobRegistry> {
  /// <summary>The asset path every domain's jobs are read from.</summary>
  public const string CataloguePath = "config/processjobs/";

  private static readonly ProcessJobLoader _instance = new();

  // The root keys ProcessJobSet.TryParse reads, and the keys each job entry reads.
  private static readonly HashSet<string> RootKeys =
  [
    "schema",
    "machine",
    "jobs",
  ];
  private static readonly HashSet<string> JobKeys =
  [
    "input",
    "output",
    "count",
    "stage",
    "family",
    "minTorque",
    "minTier",
    "seconds",
  ];

  private ProcessJobLoader() { }

  protected override string AssetPath => CataloguePath;
  protected override string CatalogueName => "processjobs";

  protected override IReadOnlyList<string> UnknownKeys(JsonObject root) =>
    [
      .. JsonKeyAudit.UnknownKeys(root, RootKeys),
      .. (root["jobs"].AsArray() ?? []).SelectMany(job =>
        JsonKeyAudit.UnknownKeys(job, JobKeys)
      ),
    ];

  protected override bool TryParse(
    JsonObject root,
    out ProcessJobSet set,
    out string? error
  ) {
    bool ok = ProcessJobSet.TryParse(
      root,
      out ProcessJobSet? parsed,
      out error
    );
    set = parsed!;
    return ok;
  }

  protected override IReadOnlyList<string> Contribute(
    ProcessJobRegistry registry,
    ProcessJobSet set
  ) => registry.Contribute(set);

  protected override int CountEntries(ProcessJobSet set) => set.Jobs.Length;

  protected override CatalogueContributors Contributors(
    ProcessJobRegistry registry
  ) => ProcessJobRegistry.Contributors;

  protected override void Clear(ProcessJobRegistry registry) =>
    registry.Clear();

  /// <summary>
  /// Parses the catalogue out of already-read files, each a <c>(source, json)</c> pair whose source names
  /// the file in an error. A malformed file - including one carrying a key the schema does not read - is
  /// reported and skipped, so one bad declaration does not cost every other mod its jobs. Asset-free, so
  /// it runs headless.
  /// </summary>
  public static List<ProcessJobSet> Parse(
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
    ProcessJobRegistry? registry = null
  ) => _instance.MergeFiles(files, registry ?? ProcessJobRegistry.Shared);

  /// <summary>Reads every domain's catalogue out of the asset manager.</summary>
  public static List<(string Source, string Json)> Read(ICoreAPI api) =>
    _instance.ReadAssets(api);

  /// <summary>Reads the catalogue, repopulates the shared registry and runs its code contributors. Call
  /// from <c>ExpandedLibModSystem.AssetsFinalize</c>.</summary>
  public static CatalogueLoadReport Load(ICoreAPI api) =>
    _instance.LoadCatalogue(api, ProcessJobRegistry.Shared);
}
