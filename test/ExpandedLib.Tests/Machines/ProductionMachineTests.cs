using ExpandedLib.Blocks.Machines;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>A concrete production machine for driving the base tick lifecycle in tests.</summary>
internal sealed class TestProductionMachine : BlockEntityProductionMachine {
  public bool Operational = true;
  public int ProductionTicks;
  public int IdleTicks;
  public float LastProductionDt;
  public float LastIdleDt;

  protected override bool CanRunProduction => Operational;

  protected override void OnProductionTick(float dt) {
    ProductionTicks++;
    LastProductionDt = dt;
  }

  protected override void OnIdleProductionTick(float dt) {
    IdleTicks++;
    LastIdleDt = dt;
  }

  /// <summary>Exposes the protected registration so a test can start ticking without full Initialize.</summary>
  public void StartTicking() => StartProductionTick();
}

/// <summary>The shared production-tick template: it gates each tick on <c>CanRunProduction</c>.</summary>
public class ProductionMachineTests {
  private static (TestWorld world, TestProductionMachine machine) NewMachine() {
    var world = new TestWorld();
    var machine = new TestProductionMachine { Pos = new BlockPos(0, 0, 0) };
    world.Attach(machine);
    machine.StartTicking();
    return (world, machine);
  }

  [Fact]
  public void Runs_production_each_tick_while_operational() {
    var (world, machine) = NewMachine();

    world.FireBlockEntityTicks(times: 3);

    Assert.Equal(3, machine.ProductionTicks);
    Assert.Equal(0, machine.IdleTicks);
  }

  [Fact]
  public void Routes_to_idle_while_not_operational() {
    var (world, machine) = NewMachine();
    machine.Operational = false;

    world.FireBlockEntityTicks(times: 2);

    Assert.Equal(0, machine.ProductionTicks);
    Assert.Equal(2, machine.IdleTicks);
  }

  [Fact]
  public void Resumes_production_when_it_becomes_operational_again() {
    var (world, machine) = NewMachine();

    machine.Operational = false;
    world.FireBlockEntityTicks();
    machine.Operational = true;
    world.FireBlockEntityTicks();

    Assert.Equal(1, machine.ProductionTicks);
    Assert.Equal(1, machine.IdleTicks);
  }

  #region Catch-up dt clamp (rejoin/hitch guard)

  // A chunk reload or server hitch can hand the machine one oversized catch-up dt; the base clamps
  // it to 2x the tick interval (default 1000 ms -> 2 s) so a grace timer cannot cross its whole
  // window in one step.
  [Fact]
  public void Clamps_an_oversized_catchup_dt_to_2x_the_interval() {
    var (world, machine) = NewMachine();

    world.FireBlockEntityTicks(dt: 3600f); // one hour of catch-up in a single tick

    Assert.Equal(1, machine.ProductionTicks);
    Assert.Equal(2f, machine.LastProductionDt); // 2 x 1000ms interval, not 3600
  }

  [Fact]
  public void Passes_a_normal_dt_through_unclamped() {
    var (world, machine) = NewMachine();

    world.FireBlockEntityTicks(dt: 1f);

    Assert.Equal(1f, machine.LastProductionDt);
  }

  [Fact]
  public void Clamps_the_idle_tick_dt_too() {
    var (world, machine) = NewMachine();
    machine.Operational = false;

    world.FireBlockEntityTicks(dt: 3600f);

    Assert.Equal(2f, machine.LastIdleDt);
  }

  #endregion
}
