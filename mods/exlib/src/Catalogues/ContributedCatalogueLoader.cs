using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Catalogues;

/// <summary>
/// The one shape a JSON catalogue with C# contributors loads through: read every domain's files under
/// one asset path, audit their keys, parse each once, merge into the registry, invoke the contributors,
/// report. <see cref="ProcessRouteLoader"/>, <see cref="ProcessJobLoader"/> and
/// <see cref="BayOccupancyLoader"/> derive from this - a catalogue that hand-rolls its own
/// <see cref="JsonObject"/> parsing and merges a set of records into an instance registry through a
/// <c>Contribute</c> method. A catalogue that overlays a static registry from a deserialized type
/// through <see cref="AssetCatalogueLoader"/> (metals, material roles, liquids) is a different shape
/// and does not derive from this.
/// </summary>
/// <typeparam name="TSet">One file's parsed declaration (a route, a job set, an occupancy set).</typeparam>
/// <typeparam name="TRegistry">The instance registry the parsed sets merge into.</typeparam>
public abstract class ContributedCatalogueLoader<TSet, TRegistry> {
  /// <summary>The asset path (with trailing slash) every domain's files are read from.</summary>
  protected abstract string AssetPath { get; }

  /// <summary>The catalogue's own name, for <see cref="CatalogueLoadReport"/>.</summary>
  protected abstract string CatalogueName { get; }

  /// <summary>Every key on <paramref name="root"/> that the schema does not read, root and nested,
  /// each already formatted for a report. Empty when the file is clean.</summary>
  protected abstract IReadOnlyList<string> UnknownKeys(JsonObject root);

  /// <summary>Parses one already key-audited file. False with a human-readable
  /// <paramref name="error"/> on any malformed field, so a bad declaration is reported and skipped
  /// rather than merged half-read.</summary>
  protected abstract bool TryParse(JsonObject root, out TSet set, out string? error);

  /// <summary>Merges <paramref name="set"/> into <paramref name="registry"/>. Returns one message per
  /// clash naming what lost; the caller attaches the file it came from.</summary>
  protected abstract IReadOnlyList<string> Contribute(TRegistry registry, TSet set);

  /// <summary>How many records <paramref name="set"/> counts as, for the report.</summary>
  protected abstract int CountEntries(TSet set);

  /// <summary>The code contributions re-invoked after every load, so a C# entry survives the clear
  /// that precedes it.</summary>
  protected abstract CatalogueContributors Contributors(TRegistry registry);

  /// <summary>Empties <paramref name="registry"/> before repopulating it.</summary>
  protected abstract void Clear(TRegistry registry);

  // One file that parsed, paired with the source it came from. Built and consumed together, so a
  // clash is always blamed on the file that lost - never on a fallback name recovered after the fact.
  private readonly record struct Parsed(TSet Set, string Source);

  // The whole per-file cycle, once: JSON syntax, the schema's own key audit, then the domain parse.
  // Each stage adds at most one message per file, so one bad file never costs the others theirs, and
  // every accepted set keeps the exact source it was read from.
  private List<Parsed> ParseInternal(
    IEnumerable<(string Source, string Json)> files,
    List<string> errors
  ) {
    var parsed = new List<Parsed>();
    foreach ((string source, string json) in files) {
      JToken token;
      try {
        token = JToken.Parse(json);
      } catch (Exception e) {
        errors.Add($"{source}: not readable as JSON - {e.Message}");
        continue;
      }

      var root = new JsonObject(token);
      IReadOnlyList<string> unknown = UnknownKeys(root);
      if (unknown.Count > 0) {
        errors.Add($"{source}: unknown key(s) {string.Join(", ", unknown)}");
        continue;
      }

      if (!TryParse(root, out TSet set, out string? error)) {
        errors.Add($"{source}: {error}");
        continue;
      }
      parsed.Add(new Parsed(set, source));
    }
    return parsed;
  }

  /// <summary>
  /// Parses the catalogue out of already-read files, each a <c>(source, json)</c> pair whose source is
  /// only used to name the file in an error. A malformed file - including one carrying a key the
  /// schema does not read - is reported and skipped, so one bad declaration does not cost every other
  /// mod its entries. Asset-free, so it runs headless.
  /// </summary>
  protected List<TSet> ParseFiles(
    IEnumerable<(string Source, string Json)> files,
    out List<string> errors
  ) {
    errors = [];
    return [.. ParseInternal(files, errors).Select(p => p.Set)];
  }

  /// <summary>
  /// Parses <paramref name="files"/> and replaces <paramref name="registry"/>'s contents with them.
  /// Returns one message per malformed file or clash, each naming the file it came from. Clearing
  /// first is what stops a world reload in the same process accumulating a second copy of every entry.
  /// </summary>
  protected List<string> MergeFiles(
    IEnumerable<(string Source, string Json)> files,
    TRegistry registry
  ) {
    var errors = new List<string>();
    List<Parsed> parsed = ParseInternal(files, errors);

    Clear(registry);
    foreach (Parsed p in parsed)
      foreach (string conflict in Contribute(registry, p.Set))
        errors.Add($"{p.Source}: {conflict}");
    return errors;
  }

  /// <summary>Reads every domain's catalogue out of the asset manager.</summary>
  protected List<(string Source, string Json)> ReadAssets(ICoreAPI api) {
    var files = new List<(string, string)>();
    foreach (IAsset asset in api.Assets.GetMany(AssetPath))
      files.Add((asset.Location.ToString(), asset.ToText()));
    return files;
  }

  /// <summary>
  /// Reads the catalogue, repopulates <paramref name="registry"/> and runs its code contributors. Each
  /// file is parsed exactly once - the same parse both counts entries and merges them, so a conflict
  /// is reported once and the count never disagrees with what actually landed.
  /// </summary>
  protected CatalogueLoadReport LoadCatalogue(ICoreAPI api, TRegistry registry) {
    List<(string Source, string Json)> files = ReadAssets(api);
    var errors = new List<string>();
    List<Parsed> parsed = ParseInternal(files, errors);

    Clear(registry);
    int entries = 0;
    foreach (Parsed p in parsed) {
      entries += CountEntries(p.Set);
      foreach (string conflict in Contribute(registry, p.Set))
        errors.Add($"{p.Source}: {conflict}");
    }
    // Contributors run after the JSON read so a set registered from C# survives the clear above.
    Contributors(registry).Invoke(api, api.Logger);

    return new CatalogueLoadReport(CatalogueName, files.Count, entries, errors);
  }
}
