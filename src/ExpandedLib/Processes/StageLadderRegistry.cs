using System;
using System.Collections.Generic;
using System.Linq;

namespace ExpandedLib.Processes;

/// <summary>
/// The merged catalogue of every <see cref="StageLadder"/> in the world, keyed by stock family. A process
/// registry is contributed to rather than owned, which is what makes the two extension directions cost the
/// same. Merging is by (thickness, accepting family) - a stage's address: an unclaimed pair is added, a pair
/// already drawn the same way is a no-op so contributing twice is safe, and a pair redrawn differently is
/// reported with the first declaration standing, since taking the last writer would make the ladder depend
/// on mod load order. <see cref="Shared"/> is the process-wide one; the type is instantiable so a caller can
/// compose an isolated catalogue. World-free, so it runs headless.
/// See docs/design/mechanics/process-extension.md.
/// </summary>
public sealed class StageLadderRegistry {
  /// <summary>The process-wide catalogue. Repopulated at <c>AssetsFinalize</c>, so it clears first and
  /// entries do not accumulate across world reloads within one process.</summary>
  public static StageLadderRegistry Shared { get; } = new();

  private readonly Dictionary<string, StageLadder> _byFamily = new(
    StringComparer.OrdinalIgnoreCase
  );

  /// <summary>
  /// Merges <paramref name="ladder"/> into the family it names. Returns one human-readable message per
  /// clash (empty when the contribution was taken whole), so the caller can log them against the mod that
  /// declared them.
  /// </summary>
  public IReadOnlyList<string> Contribute(StageLadder ladder) {
    if (!_byFamily.TryGetValue(ladder.Family, out StageLadder? merged)) {
      _byFamily[ladder.Family] = ladder with {
        Schema = StageLadder.CurrentSchema,
      };
      return [];
    }

    var conflicts = new List<string>();
    string? shape = merged.Shape;
    if (ladder.Shape != null && shape != null && ladder.Shape != shape)
      conflicts.Add(
        $"{ladder.Family}: shape '{ladder.Shape}' clashes with '{shape}'; a family's stages must all be "
          + "addressable from one file, so the first one stands"
      );
    shape ??= ladder.Shape;

    var stages = merged.Stages.ToList();
    foreach (ProcessStage incoming in ladder.Stages)
      foreach (string family in incoming.AcceptedBy)
        Absorb(stages, incoming, family, ladder.Family, conflicts);

    _byFamily[ladder.Family] = merged with {
      Shape = shape,
      Stages = [.. stages],
    };
    return conflicts;
  }

  // One (thickness, family) pair of an incoming stage, against the stages already merged.
  private static void Absorb(
    List<ProcessStage> stages,
    ProcessStage incoming,
    string family,
    string stockFamily,
    List<string> conflicts
  ) {
    int occupied = stages.FindIndex(s =>
      StageLadder.SameThickness(s.Thickness, incoming.Thickness)
      && s.IsAcceptedBy(family)
    );
    if (occupied >= 0) {
      ProcessStage held = stages[occupied];
      if (held.Element != incoming.Element || held.Code != incoming.Code)
        conflicts.Add(
          $"{stockFamily} {incoming.Thickness} '{family}': declared as "
            + $"{Describe(incoming)} but already drawn as {Describe(held)}; the first one stands"
        );
      return;
    }

    // Not claimed for this family. An identical stage is the same rung seen from another machine, so it
    // widens; anything else is a new rung of its own.
    int twin = stages.FindIndex(s =>
      StageLadder.SameThickness(s.Thickness, incoming.Thickness)
      && s.Element == incoming.Element
      && s.Code == incoming.Code
    );
    if (twin >= 0)
      stages[twin] = stages[twin] with {
        AcceptedBy = [.. stages[twin].AcceptedBy, family],
      };
    else
      stages.Add(incoming with { AcceptedBy = [family] });
  }

  private static string Describe(ProcessStage stage) =>
    $"'{stage.Element ?? "-"}' -> '{stage.Code ?? "-"}'";

  /// <summary>The merged ladder for <paramref name="family"/>, or null when nothing has claimed it.</summary>
  public StageLadder? Ladder(string? family) =>
    family != null && _byFamily.TryGetValue(family, out StageLadder? ladder)
      ? ladder
      : null;

  /// <summary>Looks up a family's merged ladder; <c>false</c> when nothing has claimed it.</summary>
  public bool TryGet(string family, out StageLadder? ladder) =>
    _byFamily.TryGetValue(family, out ladder);

  /// <summary>The stock families with a ladder.</summary>
  public IReadOnlyCollection<string> Families => _byFamily.Keys;

  /// <summary>Drops every family. The loader clears before repopulating on each world load.</summary>
  public void Clear() => _byFamily.Clear();
}
