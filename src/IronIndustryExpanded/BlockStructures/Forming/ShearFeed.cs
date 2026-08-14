using System;
using ExpandedLib.Processes;

namespace IronIndustryExpanded.BlockStructures.Forming;

/// <summary>Why a piece offered to the blades was or was not cut.</summary>
public enum ShearVerdict {
  /// <summary>The blades take it: one product leaves and the remainder stays on the deck.</summary>
  Ok,

  /// <summary>No blade set is fitted, so the machine has nothing to cut with.</summary>
  NoBladeSet,

  /// <summary>Nothing is declared for this piece at this gauge on this branch. A stage that names no job is
  /// not a stopping point, which is how the ladder stays open for a mod to close.</summary>
  NoJob,

  /// <summary>The piece is worked out: every crop its job declares has already been taken.</summary>
  Spent,

  /// <summary>The fitted blade is tempered below what the job asks. Temper is vanilla's ladder, so this is
  /// the tier gate and there is no hardness system of our own.</summary>
  BladeTooSoft,

  /// <summary>The shaft is at rest. A stroke is drawn from a turning run, so no amount of stored torque
  /// starts one.</summary>
  NotTurning,

  /// <summary>The run cannot deliver the stroke. Kept separate from <see cref="NotTurning"/> because the fix
  /// is more drive rather than any drive - and on a cold cut, the fix may instead be a reheat.</summary>
  NotEnoughDrive,
}

/// <summary>The outcome of offering a piece to the blades, and what the stroke would cost.</summary>
/// <param name="Verdict">Whether the blades take it, and why not if they do not.</param>
/// <param name="Job">The job that matched, or null when none did.</param>
/// <param name="RequiredTorque">Drive the stroke asks of the run, cold multiplier included. Zero when no job
/// matched.</param>
public readonly record struct ShearDecision(
  ShearVerdict Verdict,
  ProcessJob? Job,
  float RequiredTorque
) {
  public bool Accepted => Verdict == ShearVerdict.Ok;
}

/// <summary>
/// Resolves what happens when a player offers stock to the shear. Pure functions, callable without a world,
/// the same shape as <see cref="MillFeed"/>.
/// <para>
/// Temperature does not gate a cut the way it gates a bite: shearing needs force rather than friction, and
/// cold stock parts more cleanly than hot, which smears. So a cold cut is not refused - it simply costs more
/// drive, which makes cold shearing a power achievement rather than a tier unlock.
/// See docs/design/machines/shear.md.
/// </para>
/// </summary>
public static class ShearFeed {
  /// <summary>
  /// Drive one stroke of <paramref name="job"/> asks of the run. The job's own <c>minTorque</c> at rolling
  /// heat, multiplied by <paramref name="coldMultiplier"/> below it.
  /// </summary>
  public static float RequiredTorque(
    ProcessJob job,
    float tempC,
    float rollingTempC,
    float coldMultiplier
  ) =>
    tempC >= rollingTempC
      ? job.MinTorque
      : job.MinTorque * MathF.Max(1f, coldMultiplier);

  /// <summary>
  /// Whether the blades cut <paramref name="piece"/> under <paramref name="job"/>, and what the stroke costs
  /// if they do. <paramref name="piece"/> is null for a whole-item job, which takes the stack as it is.
  /// <paramref name="bladeTier"/> is the fitted blade's temper, checked against the job's floor.
  /// </summary>
  /// <remarks>
  /// The order of the refusals is the order the player can fix them in: what is missing from the machine,
  /// then what is wrong with the piece, then what the run cannot supply. A worked-out piece reports as spent
  /// rather than as a bad job, because the job is right and the piece is finished.
  /// </remarks>
  public static ShearDecision Decide(
    bool hasBladeSet,
    int bladeTier,
    ProcessJob? job,
    WorkPiece? piece,
    float tempC,
    float rollingTempC,
    float coldMultiplier,
    float availableTorque,
    float speed
  ) {
    if (!hasBladeSet)
      return new ShearDecision(ShearVerdict.NoBladeSet, null, 0f);
    if (job == null)
      return new ShearDecision(ShearVerdict.NoJob, null, 0f);
    // Only a staged job leaves a remainder to run out, so only a staged job can be spent. A whole-item job
    // consumes its input, and a stack that is still there has never been converted.
    if (job.Stage != null) {
      // A staged job is addressed by gauge, so a stack carrying no piece is not what it was declared for -
      // which is a job the machine does not have rather than a piece that is finished.
      if (piece == null)
        return new ShearDecision(ShearVerdict.NoJob, null, 0f);
      if (piece.IsSpent(job.Count))
        return new ShearDecision(ShearVerdict.Spent, job, 0f);
    }
    if (bladeTier < job.MinTier)
      return new ShearDecision(ShearVerdict.BladeTooSoft, job, 0f);

    float required = RequiredTorque(job, tempC, rollingTempC, coldMultiplier);
    if (speed <= 0f)
      return new ShearDecision(ShearVerdict.NotTurning, job, required);
    return RollingPass.CanCarry(required, availableTorque, speed)
      ? new ShearDecision(ShearVerdict.Ok, job, required)
      : new ShearDecision(ShearVerdict.NotEnoughDrive, job, required);
  }
}
