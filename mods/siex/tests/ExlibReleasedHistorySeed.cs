using System.Collections.Generic;
using ExpandedLib.Testing;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// exlib's own contribution to the release-history registry, reproduced here rather than referenced:
/// exlib's own test project does not ship as a dependency of this repository, and this suite is the
/// only one that needs exlib's shipped history alongside iiex's and siex's own (see
/// <see cref="ReleasedHistorySeed"/>). Keep this in step with exlib's own copy by hand.
/// </summary>
internal static class ExlibReleasedHistorySeed {
  /// <summary>exlib 0.7.0 - no blocktype JSON, but the filler is written into released worlds by
  /// every mega-block footprint, so it carries the same migration contract as a placed block.</summary>
  private static readonly IReadOnlyList<ReleasedCodes.Shipped> Shipped =
  [
    new(
      "exlib",
      "structurefiller",
      "exlib:structurefiller",
      ["exlib:structurefiller"]
    ),
  ];

  internal static void Register() =>
    ReleasedHistory.Register(
      "exlib",
      Shipped,
      [],
      new Dictionary<string, string> { ["exlib"] = "0.7.2" },
      []
    );
}
