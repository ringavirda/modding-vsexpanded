using System;

namespace ExpandedLib.Helpers;

/// <summary>
/// Helpers for advancing a machine on <em>game</em> time (the world calendar) rather than real time.
/// A block entity's per-tick listener stops firing while its chunk is unloaded, so a machine that was
/// away resumes frozen; the network sims don't (their manager ticks globally), which is why only the
/// BE-resident machines need to catch up. The calendar is the only clock that survives an unload, so
/// the away interval is measured in game hours and replayed as a <em>bounded</em> number of normal
/// sub-ticks - never one giant <c>dt</c> (that is the "detonates on rejoin" class of bug).
/// </summary>
public static class GameTime
{
  /// <summary>Game-seconds between two <c>Calendar.TotalHours</c> readings; never negative (a rewound
  /// or unset clock yields 0).</summary>
  public static double SecondsBetween(double fromHours, double toHours) =>
    Math.Max(0.0, toHours - fromHours) * 3600.0;

  /// <summary>
  /// Replays <paramref name="elapsedSeconds"/> of catch-up as sub-steps of at most
  /// <paramref name="stepSeconds"/>, invoking <paramref name="step"/> once per sub-step. Bounded on
  /// BOTH ends: never more than <paramref name="maxSteps"/> sub-steps run (so a long absence is capped,
  /// not replayed in full), and the last step carries only the leftover. Returns the number of
  /// sub-steps actually run. A machine gets the same per-tick <c>dt</c> it sees while loaded, so its
  /// existing rate math and safety clamps apply unchanged.
  /// </summary>
  public static int CatchUp(
    double elapsedSeconds,
    float stepSeconds,
    int maxSteps,
    Action<float> step
  )
  {
    if (stepSeconds <= 0f || maxSteps <= 0 || elapsedSeconds <= 0.0)
      return 0;

    double remaining = Math.Min(elapsedSeconds, (double)stepSeconds * maxSteps);
    int steps = 0;
    while (remaining > 1e-6 && steps < maxSteps)
    {
      float dt = (float)Math.Min(remaining, stepSeconds);
      step(dt);
      remaining -= dt;
      steps++;
    }
    return steps;
  }
}
