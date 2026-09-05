using System;

namespace ExpandedLib.Helpers;

/// <summary>
/// Advances a machine on game time (the world calendar) rather than real time. A block entity's
/// per-tick listener stops firing while its chunk is unloaded, so only BE-resident machines need to
/// catch up; the network sims tick from a global manager. The calendar is the only clock that survives
/// an unload, so the away interval is measured in game hours and replayed as a bounded number of
/// normal sub-ticks rather than one large <c>dt</c>.
/// </summary>
public static class GameTime {
  /// <summary>Game-seconds between two <c>Calendar.TotalHours</c> readings; never negative (a rewound
  /// or unset clock yields 0).</summary>
  public static double SecondsBetween(double fromHours, double toHours) =>
    Math.Max(0.0, toHours - fromHours) * 3600.0;

  /// <summary>
  /// Replays <paramref name="elapsedSeconds"/> as sub-steps of at most <paramref name="stepSeconds"/>,
  /// calling <paramref name="step"/> once per sub-step with that step's <c>dt</c>, and returns the
  /// number of sub-steps run. At most <paramref name="maxSteps"/> run, so a long absence is capped
  /// rather than replayed in full, and the last step carries only the leftover. Each step gets the same
  /// <c>dt</c> the machine sees while loaded, so its rate math and clamps apply unchanged.
  /// </summary>
  public static int CatchUp(
    double elapsedSeconds,
    float stepSeconds,
    int maxSteps,
    Action<float> step
  ) {
    if (stepSeconds <= 0f || maxSteps <= 0 || elapsedSeconds <= 0.0)
      return 0;

    double remaining = Math.Min(elapsedSeconds, (double)stepSeconds * maxSteps);
    int steps = 0;
    while (remaining > 1e-6 && steps < maxSteps) {
      float dt = (float)Math.Min(remaining, stepSeconds);
      step(dt);
      remaining -= dt;
      steps++;
    }
    return steps;
  }
}
