using System;
using System.Collections.Generic;
using System.Linq;

namespace ExpandedLib.Processes;

/// <summary>
/// The merged catalogue of every terminal job, keyed by machine. Contributed to rather than owned, exactly
/// as <see cref="ProcessRouteRegistry"/> is: a mod adds a crop, a heading or a nail job by shipping a file,
/// and never by patching ours. A second job on one input is reported and the first stands, because taking
/// the last writer would make the answer depend on mod load order. World-free, so it runs headless.
/// See docs/design/mechanics/process-extension.md.
/// </summary>
public sealed class ProcessJobRegistry {
  /// <summary>The process-wide catalogue, repopulated at <c>AssetsFinalize</c>.</summary>
  public static ProcessJobRegistry Shared { get; } = new();

  private readonly Dictionary<string, List<ProcessJob>> _byMachine = new(
    StringComparer.OrdinalIgnoreCase
  );

  /// <summary>
  /// Merges <paramref name="set"/> into the machine it names. Returns one human-readable message per job
  /// whose input another job already claims.
  /// </summary>
  public IReadOnlyList<string> Contribute(ProcessJobSet set) {
    if (!_byMachine.TryGetValue(set.Machine, out List<ProcessJob>? jobs))
      _byMachine[set.Machine] = jobs = [];

    var conflicts = new List<string>();
    foreach (ProcessJob job in set.Jobs) {
      ProcessJob? held = jobs.FirstOrDefault(j =>
        j.Matches(job.Input, job.Stage, job.Family)
      );
      if (held != null) {
        if (held.Output != job.Output || held.Count != job.Count)
          conflicts.Add(
            $"{set.Machine}: '{job.Input}' already yields {held.Count} x '{held.Output}', so "
              + $"{job.Count} x '{job.Output}' is ignored; the first declaration stands"
          );
        continue;
      }
      jobs.Add(job);
    }
    return conflicts;
  }

  /// <summary>Every job <paramref name="machine"/> can do, in declaration order.</summary>
  public IReadOnlyList<ProcessJob> Jobs(string? machine) =>
    machine != null
    && _byMachine.TryGetValue(machine, out List<ProcessJob>? jobs)
      ? jobs
      : [];

  /// <summary>
  /// The job <paramref name="machine"/> has for a piece of <paramref name="input"/> at
  /// <paramref name="stage"/> on <paramref name="family"/>, or null when it has none. A staged job is
  /// preferred over a whole-item one, so a stock family with a crop at one gauge can still have a
  /// whole-item fallback.
  /// </summary>
  public ProcessJob? Job(
    string? machine,
    string? input,
    float? stage,
    string? family
  ) {
    if (input == null)
      return null;
    IReadOnlyList<ProcessJob> jobs = Jobs(machine);
    return jobs.FirstOrDefault(j =>
        j.Stage != null && j.Matches(input, stage, family)
      )
      ?? jobs.FirstOrDefault(j =>
        j.Stage == null && j.Matches(input, stage, family)
      );
  }

  /// <summary>The machines with at least one job.</summary>
  public IReadOnlyCollection<string> Machines => _byMachine.Keys;

  /// <summary>Drops every job. The loader clears before repopulating on each world load.</summary>
  public void Clear() => _byMachine.Clear();
}
