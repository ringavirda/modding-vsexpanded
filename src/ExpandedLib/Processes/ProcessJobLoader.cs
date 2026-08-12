using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Processes;

/// <summary>
/// Reads the terminal-job catalogue - every domain's <c>config/processjobs/*.json</c> - and populates
/// <see cref="ProcessJobRegistry"/> from it. One file per machine is the convention and nothing enforces
/// it: the registry merges whatever arrives, so two mods may both add jobs to one machine. The sibling of
/// <see cref="StageLadderLoader"/>, and deliberately the same shape.
/// </summary>
public static class ProcessJobLoader {
  /// <summary>The asset path every domain's jobs are read from.</summary>
  public const string CataloguePath = "config/processjobs/";

  /// <summary>
  /// Parses the catalogue out of already-read files, each a <c>(source, json)</c> pair whose source names
  /// the file in an error. A malformed file is reported and skipped, so one bad declaration does not cost
  /// every other mod its jobs. Asset-free, so it runs headless.
  /// </summary>
  public static List<ProcessJobSet> Parse(
    IEnumerable<(string Source, string Json)> files,
    out List<string> errors
  ) {
    var sets = new List<ProcessJobSet>();
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
        !ProcessJobSet.TryParse(
          new JsonObject(token),
          out ProcessJobSet? set,
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
    ProcessJobRegistry? registry = null
  ) {
    registry ??= ProcessJobRegistry.Shared;

    var sources = new List<(string Source, string Json)>(files);
    List<ProcessJobSet> sets = Parse(sources, out List<string> errors);

    registry.Clear();
    for (int i = 0; i < sets.Count; i++) {
      // Parse preserves order, so when nothing was dropped the nth set is the nth file; otherwise the
      // machine name is the honest answer.
      string source =
        sources.Count == sets.Count ? sources[i].Source : sets[i].Machine;
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
