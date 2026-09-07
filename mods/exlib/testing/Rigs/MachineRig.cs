using System;
using ExpandedLib.Machines;

namespace ExpandedLib.Testing;

/// <summary>
/// Drives one machine on a <see cref="TestWorld"/> to a condition rather than to a fixed offset. Every
/// step fires block-entity ticks then the network tick, the same order <see cref="Scene.Step"/> and a
/// live server use; a rig that needs a different order (feeding a fuel source directly, invoking a
/// production tick by reflection) is doing more than this base covers and steps by hand instead.
/// </summary>
public abstract class MachineRig(TestWorld world) {
  /// <summary>The world this rig steps.</summary>
  public TestWorld World { get; } = world;

  private void Step(float stepSeconds) {
    World.FireBlockEntityTicks(stepSeconds);
    // The network tick always advances a whole server second; a sub-second step still ticks it once,
    // matching Scene.Step (which only ever steps by whole seconds).
    World.Tick(Math.Max(1, (int)stepSeconds));
  }

  /// <summary>
  /// Advances the world in <paramref name="stepSeconds"/> steps until <paramref name="until"/> holds.
  /// </summary>
  /// <returns>The seconds elapsed.</returns>
  /// <exception cref="TimeoutException"><paramref name="until"/> never held within
  /// <paramref name="ceilingSeconds"/>.</exception>
  public float RunUntil(
    Func<bool> until,
    float ceilingSeconds,
    float stepSeconds = 1f
  ) {
    for (float elapsed = 0f; elapsed < ceilingSeconds; elapsed += stepSeconds) {
      Step(stepSeconds);
      if (until())
        return elapsed + stepSeconds;
    }

    throw new TimeoutException(
      $"condition did not hold within {ceilingSeconds} s"
    );
  }

  /// <summary>Advances the world for <paramref name="seconds"/>, calling <paramref name="observer"/>
  /// (with the seconds just elapsed) after every step.</summary>
  public void RunLive(
    float seconds,
    Action<float>? observer = null,
    float stepSeconds = 1f
  ) {
    for (float elapsed = 0f; elapsed < seconds; elapsed += stepSeconds) {
      Step(stepSeconds);
      observer?.Invoke(stepSeconds);
    }
  }

  /// <summary>
  /// Runs <paramref name="beforeEachStep"/> before every step, for <paramref name="seconds"/>: the
  /// "hold a source at a level and step" loop the fixtures otherwise write by hand (recharge a pipe,
  /// crank a pump) so the machine sees a fed line rather than one that drains on the first tick.
  /// </summary>
  public void RunWhile(
    Action beforeEachStep,
    float seconds,
    float stepSeconds = 1f
  ) {
    for (float elapsed = 0f; elapsed < seconds; elapsed += stepSeconds) {
      beforeEachStep();
      Step(stepSeconds);
    }
  }
}

/// <summary>
/// Test-only hooks for the machine base types, for a fixture that drives a production tick or the
/// access check by hand instead of through <see cref="MachineRig"/>'s stepping.
/// </summary>
public static class MachineTestHooks {
  /// <summary>Turns off <see cref="BlockEntityMachineStation"/>'s engine interaction-range check, so a
  /// headless test's substitute player - never "in range" of anything - can still exercise a packet
  /// route gated on it. The claim check is unaffected.</summary>
  public static void DisablePickRangeCheck(this BlockEntityMachineStation station) =>
    station.ValidatePickRange = false;

  /// <summary>Runs one production tick on <paramref name="behavior"/> exactly as the registered
  /// listener would, including the readiness gate and the catch-up <c>dt</c> clamp - without
  /// registering the listener at all.</summary>
  public static void DriveProductionTick(this BEBehaviorProductionMachine behavior, float dt) =>
    behavior.DriveProductionTick(dt);

  /// <summary>Runs one production tick on <paramref name="machine"/>'s hosted process, the block-entity
  /// counterpart of <see cref="DriveProductionTick(BEBehaviorProductionMachine, float)"/>.</summary>
  public static void DriveProductionTick(this BlockEntityProductionMachine machine, float dt) =>
    machine.DriveProductionTick(dt);
}
