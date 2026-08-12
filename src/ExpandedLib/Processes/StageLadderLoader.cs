using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Processes;

/// <summary>
/// Reads the stage catalogue - every domain's <c>config/stageladders/*.json</c> - and populates
/// <see cref="StageLadderRegistry"/> from it. One file per stock family is the convention and nothing
/// enforces it: the registry merges whatever arrives, so two mods may both contribute to one family.
/// <para>
/// Ladders live in a config asset rather than on an item because items are generated from them
/// (<see cref="ProcessItemEmitter"/>), and that has to happen before the object loader builds items. A
/// ladder carried on an itemtype could not be read in time to generate one.
/// </para>
/// </summary>
public static class StageLadderLoader {
  /// <summary>The asset path every domain's ladders are read from.</summary>
  public const string CataloguePath = "config/stageladders/";

  /// <summary>
  /// Parses the catalogue out of already-read files, each a <c>(source, json)</c> pair whose source is
  /// only used to name the file in an error. A malformed file is reported and skipped, so one bad
  /// declaration does not cost every other mod its ladders. Asset-free, so it runs headless.
  /// </summary>
  public static List<StageLadder> Parse(
    IEnumerable<(string Source, string Json)> files,
    out List<string> errors
  ) {
    var ladders = new List<StageLadder>();
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
        !StageLadder.TryParse(
          new JsonObject(token),
          out StageLadder? ladder,
          out string? error
        )
      ) {
        errors.Add($"{source}: {error}");
        continue;
      }
      ladders.Add(ladder!);
    }
    return ladders;
  }

  /// <summary>
  /// Parses <paramref name="files"/> and replaces <paramref name="registry"/>'s contents with them (the
  /// shared registry when null). Returns one human-readable message per malformed file or clash, each
  /// naming the file it came from. Clearing first is what stops a world reload in the same process
  /// accumulating a second copy of every rung.
  /// </summary>
  public static List<string> Load(
    IEnumerable<(string Source, string Json)> files,
    StageLadderRegistry? registry = null
  ) {
    registry ??= StageLadderRegistry.Shared;

    // Parsed before the clear, so a catalogue that fails wholesale does not leave the registry empty
    // partway through.
    var sources = new List<(string Source, string Json)>(files);
    List<StageLadder> ladders = Parse(sources, out List<string> errors);

    registry.Clear();
    int i = 0;
    foreach (StageLadder ladder in ladders) {
      // Parse preserves order and skips nothing silently, so the nth ladder is the nth file that parsed;
      // a clash is reported against the file that lost, which is the one its author can fix.
      string source = SourceOf(sources, ladders, i++);
      foreach (string conflict in registry.Contribute(ladder))
        errors.Add($"{source}: {conflict}");
    }
    return errors;
  }

  // The file a parsed ladder came from. Parse drops malformed files, so the indices only line up when
  // every file parsed; otherwise the family name is the honest answer.
  private static string SourceOf(
    List<(string Source, string Json)> sources,
    List<StageLadder> ladders,
    int index
  ) =>
    sources.Count == ladders.Count
      ? sources[index].Source
      : ladders[index].Family;

  /// <summary>
  /// Reads every domain's catalogue out of the asset manager. Callable from <c>AssetsLoaded</c> (before
  /// the object loader, for item generation) and from <c>AssetsFinalize</c> (after the patch pipeline,
  /// for the authoritative registry).
  /// </summary>
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
