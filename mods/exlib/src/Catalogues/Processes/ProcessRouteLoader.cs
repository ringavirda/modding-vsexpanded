using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Catalogues;

/// <summary>
/// Reads the stage catalogue - every domain's <c>config/processroutes/*.json</c> - and populates
/// <see cref="ProcessRouteRegistry"/> from it. One file per stock family is the convention and nothing
/// enforces it: the registry merges whatever arrives, so two mods may both contribute to one family.
/// <para>
/// Routes live in a config asset rather than on an item because items are generated from them
/// (<see cref="ProcessItemEmitter"/>), and that has to happen before the object loader builds items. A
/// route carried on an itemtype could not be read in time to generate one.
/// </para>
/// </summary>
public sealed class ProcessRouteLoader
  : ContributedCatalogueLoader<ProcessRoute, ProcessRouteRegistry> {
  /// <summary>The asset path every domain's routes are read from.</summary>
  public const string CataloguePath = "config/processroutes/";

  private static readonly ProcessRouteLoader _instance = new();

  // The root keys ProcessRoute.TryParse reads, and the keys each stage entry reads. An unknown key -
  // a rename the file never caught up with, most often - is a hand-authored schema's version of a
  // MissingMemberHandling failure: the deserialising loaders get that for free, this one does not.
  private static readonly HashSet<string> RootKeys =
  [
    "schema",
    "family",
    "shape",
    "stages",
  ];
  private static readonly HashSet<string> StageKeys =
  [
    "thickness",
    "acceptedBy",
    "halfStep",
    "code",
    "generate",
    "formerCodes",
    "element",
  ];

  private ProcessRouteLoader() { }

  protected override string AssetPath => CataloguePath;
  protected override string CatalogueName => "processroutes";

  // Root keys plus every stage's keys, since the stage schema is fixed and small enough to check
  // eagerly rather than lazily on each element.
  protected override IReadOnlyList<string> UnknownKeys(JsonObject root) =>
    [
      .. JsonKeyAudit.UnknownKeys(root, RootKeys),
      .. (root["stages"].AsArray() ?? [])
        .SelectMany(stage => JsonKeyAudit.UnknownKeys(stage, StageKeys)),
    ];

  protected override bool TryParse(
    JsonObject root,
    out ProcessRoute set,
    out string? error
  ) {
    bool ok = ProcessRoute.TryParse(root, out ProcessRoute? route, out error);
    set = route!;
    return ok;
  }

  protected override IReadOnlyList<string> Contribute(
    ProcessRouteRegistry registry,
    ProcessRoute set
  ) => registry.Contribute(set);

  protected override int CountEntries(ProcessRoute set) => set.Stages.Length;

  protected override CatalogueContributors Contributors(ProcessRouteRegistry registry) =>
    ProcessRouteRegistry.Contributors;

  protected override void Clear(ProcessRouteRegistry registry) => registry.Clear();

  /// <summary>
  /// Parses the catalogue out of already-read files, each a <c>(source, json)</c> pair whose source is
  /// only used to name the file in an error. A malformed file - including one carrying a key the
  /// schema does not read - is reported and skipped, so one bad declaration does not cost every other
  /// mod its routes. Asset-free, so it runs headless.
  /// </summary>
  public static List<ProcessRoute> Parse(
    IEnumerable<(string Source, string Json)> files,
    out List<string> errors
  ) => _instance.ParseFiles(files, out errors);

  /// <summary>
  /// Parses <paramref name="files"/> and replaces <paramref name="registry"/>'s contents with them (the
  /// shared registry when null). Returns one human-readable message per malformed file or clash, each
  /// naming the file it came from. Clearing first is what stops a world reload in the same process
  /// accumulating a second copy of every rung.
  /// </summary>
  public static List<string> Load(
    IEnumerable<(string Source, string Json)> files,
    ProcessRouteRegistry? registry = null
  ) => _instance.MergeFiles(files, registry ?? ProcessRouteRegistry.Shared);

  /// <summary>
  /// Reads every domain's catalogue out of the asset manager. Callable from <c>AssetsLoaded</c> (before
  /// the object loader, for item generation) and from <c>AssetsFinalize</c> (after the patch pipeline,
  /// for the authoritative registry).
  /// </summary>
  public static List<(string Source, string Json)> Read(ICoreAPI api) =>
    _instance.ReadAssets(api);

  /// <summary>Reads the catalogue, repopulates the shared registry and runs its code contributors. Call
  /// from <c>ExpandedLibModSystem.AssetsFinalize</c>.</summary>
  public static CatalogueLoadReport Load(ICoreAPI api) =>
    _instance.LoadCatalogue(api, ProcessRouteRegistry.Shared);
}
