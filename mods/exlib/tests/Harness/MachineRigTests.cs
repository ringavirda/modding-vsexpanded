using System;
using ExpandedLib.Testing;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="MachineRig"/> over the existing <see cref="TestProductionMachine"/>: each of its three
/// stepping loops (<c>RunUntil</c>, <c>RunLive</c>, <c>RunWhile</c>) fires a block-entity tick per
/// step, so a machine that only counts production ticks is enough to exercise all three.
/// </summary>
public class MachineRigTests {
  private sealed class TestRig(TestWorld world) : MachineRig(world);

  private static (TestRig rig, TestProductionMachine machine) NewRig() {
    var world = new TestWorld();
    var machine = new TestProductionMachine { Pos = new BlockPos(0, 0, 0) };
    world.Attach(machine);
    machine.StartTicking();
    return (new TestRig(world), machine);
  }

  [Fact]
  public void RunUntil_stops_the_moment_the_condition_holds_and_returns_the_elapsed_seconds() {
    var (rig, machine) = NewRig();

    float elapsed = rig.RunUntil(
      () => machine.ProductionTicks >= 3,
      ceilingSeconds: 10
    );

    Assert.Equal(3f, elapsed);
    Assert.Equal(3, machine.ProductionTicks);
  }

  [Fact]
  public void RunUntil_throws_a_named_timeout_when_the_condition_never_holds() {
    var (rig, _) = NewRig();

    var ex = Assert.Throws<TimeoutException>(() =>
      rig.RunUntil(() => false, ceilingSeconds: 3)
    );
    Assert.Contains("3", ex.Message);
  }

  [Fact]
  public void RunLive_steps_for_the_full_duration_and_calls_the_observer_each_step() {
    var (rig, machine) = NewRig();
    int observed = 0;

    rig.RunLive(
      4,
      dt => {
        observed++;
        Assert.Equal(1f, dt);
      }
    );

    Assert.Equal(4, observed);
    Assert.Equal(4, machine.ProductionTicks);
  }

  [Fact]
  public void RunWhile_runs_the_action_before_every_step() {
    var (rig, machine) = NewRig();
    int before = 0;

    rig.RunWhile(
      () => {
        // The action runs before the step that would observe its effect - checked by asserting it
        // always sees one fewer production tick than the step it precedes.
        Assert.Equal(before, machine.ProductionTicks);
        before++;
      },
      seconds: 3
    );

    Assert.Equal(3, before);
    Assert.Equal(3, machine.ProductionTicks);
  }
}
