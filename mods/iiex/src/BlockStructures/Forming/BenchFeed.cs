using ExpandedLib.Catalogues;

namespace IronIndustryExpanded.BlockStructures.Forming;

/// <summary>Why a blank offered to a fastener bench was or was not worked.</summary>
public enum BenchVerdict {
  /// <summary>The press takes it: the blank is consumed and every bundle it is worth leaves at once.</summary>
  Ok,

  /// <summary>No die is fitted. A die names the job, so a bench without one has no work at all - which is
  /// what separates it from a bench missing a tool, which has work it cannot reach.</summary>
  NoDie,

  /// <summary>The fitted die has no job for this blank. Either the wrong die, or the wrong blank.</summary>
  NoJob,

  /// <summary>The shaft is at rest. A stroke is drawn from a turning run, so no amount of stored torque
  /// starts one.</summary>
  NotTurning,

  /// <summary>The run cannot deliver the stroke. Kept separate from <see cref="NotTurning"/> because the fix
  /// is more drive rather than any drive.</summary>
  NotEnoughDrive,
}

/// <summary>The outcome of offering a blank to a fastener bench.</summary>
/// <param name="Verdict">Whether the press takes it, and why not if it does not.</param>
/// <param name="Job">The job the fitted die matched, or null when none did.</param>
public readonly record struct BenchDecision(
  BenchVerdict Verdict,
  ProcessJob? Job
) {
  public bool Accepted => Verdict == BenchVerdict.Ok;
}

/// <summary>
/// Resolves what happens when a blank is offered to a fastener bench. Pure functions, callable without a
/// world, the same shape as <see cref="MillFeed"/> and <see cref="ShearFeed"/>.
/// <para>
/// Deliberately shorter than the shear's. A fastener bench has no temper ladder - a die carries a job, not
/// a hardness - and no stage, because a blank is converted whole rather than cropped, so there is no
/// remainder to run out and nothing to be spent. What is left is the drive, and that is the only thing a
/// player can be short of. See docs/design/mechanics/machining-line.md § Tooling.
/// </para>
/// </summary>
public static class BenchFeed {
  /// <summary>
  /// Whether the bench works <paramref name="job"/>, given what is fitted and what the run is doing.
  /// </summary>
  /// <remarks>
  /// The order of the refusals is the order the player can fix them in: what is missing from the machine,
  /// then what is wrong with the blank, then what the run cannot supply.
  /// </remarks>
  public static BenchDecision Decide(
    bool hasDie,
    ProcessJob? job,
    float availableTorque,
    float speed
  ) {
    if (!hasDie)
      return new BenchDecision(BenchVerdict.NoDie, null);
    if (job == null)
      return new BenchDecision(BenchVerdict.NoJob, null);
    if (speed <= 0f)
      return new BenchDecision(BenchVerdict.NotTurning, job);
    return RollingPass.CanCarry(job.MinTorque, availableTorque, speed)
      ? new BenchDecision(BenchVerdict.Ok, job)
      : new BenchDecision(BenchVerdict.NotEnoughDrive, job);
  }
}
