using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Processes;
using ExpandedLib.Testing;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The shipped stage ladders, read off the source tree exactly as the game reads them out of the asset
/// manager. Headless there is no asset manager for
/// <see cref="ExpandedLib.Processes.StageLadderLoader"/> to read, so this stands in for it - and because it
/// goes through the same parser on the same files, a ladder that would not load in game does not load here.
/// </summary>
public static class StageLadderSeeds {
  private static bool _seeded;

  /// <summary>Where iiex ships its catalogue, relative to the repo root.</summary>
  public const string CataloguePath = "assets/iiex/config/stageladders";

  /// <summary>Idempotently loads every shipped iiex ladder into the shared registry, which is what the
  /// mill reads through <c>MillSchedule.For</c>.</summary>
  public static void SeedIiexLadders() {
    if (_seeded)
      return;
    _seeded = true;

    List<string> errors = StageLadderLoader.Load(Files());
    Assert.True(
      errors.Count == 0,
      "The shipped stage catalogue does not load:\n    "
        + string.Join("\n    ", errors)
    );
  }

  /// <summary>Every shipped catalogue file, as the <c>(source, json)</c> pairs the loader takes.</summary>
  public static List<(string Source, string Json)> Files() {
    string root = DefinitionGoldens.SolutionRelative(CataloguePath);
    return
    [
      .. Directory
        .EnumerateFiles(root, "*.json")
        .OrderBy(p => p, System.StringComparer.Ordinal)
        .Select(path =>
          ($"iiex:{Path.GetFileName(path)}", File.ReadAllText(path))
        ),
    ];
  }

  /// <summary>Every shipped ladder, parsed. Throws if one does not parse, which is the premise every
  /// caller depends on rather than a condition to be tolerated.</summary>
  public static List<StageLadder> Shipped() {
    List<StageLadder> ladders = StageLadderLoader.Parse(
      Files(),
      out List<string> errors
    );
    Assert.True(
      errors.Count == 0,
      "The shipped stage catalogue does not parse:\n    "
        + string.Join("\n    ", errors)
    );
    return ladders;
  }
}
