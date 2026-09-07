using ExpandedLib.Helpers;
using ExpandedLib.Machines;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>A production machine that opts into away-catch-up with a configurable step cap.</summary>
internal sealed class CatchupMachine : BlockEntityProductionMachine {
  public int ProductionTicks;
  public int Steps;

  protected override bool CanRunProduction => true;

  protected override void OnProductionTick(float dt) => ProductionTicks++;

  protected override int MaxAwayCatchupSteps => Steps;

  public void StartTicking() => StartProductionTick();
}

/// <summary>
/// Game-time away-catch-up. The calendar is the only clock that survives a chunk unload, so a machine
/// that opts in (<c>MaxAwayCatchupSteps &gt; 0</c>) replays the game hours it spent unloaded as bounded
/// sub-ticks on reload: proportional for a short absence, capped for a long one. Off by default.
/// </summary>
public class GameTimeCatchupTests {
  #region GameTime helper

  [Theory]
  [InlineData(0.0, 1.0, 3600.0)] // one hour -> 3600 game-seconds
  [InlineData(2.0, 2.5, 1800.0)] // half an hour
  [InlineData(5.0, 4.0, 0.0)] // clock ran backwards -> clamped to 0
  public void SecondsBetween_is_the_nonnegative_hour_gap_in_seconds(
    double from,
    double to,
    double expected
  ) {
    Assert.Equal(expected, GameTime.SecondsBetween(from, to), 3);
  }

  [Fact]
  public void CatchUp_replays_a_short_gap_in_full() {
    int steps = 0;
    float total = 0f;
    int ran = GameTime.CatchUp(
      3.6,
      1f,
      100,
      dt => {
        steps++;
        total += dt;
      }
    );

    Assert.Equal(4, ran); // 1 + 1 + 1 + 0.6
    Assert.Equal(4, steps);
    Assert.Equal(3.6f, total, 3); // conserves the elapsed time
  }

  [Fact]
  public void CatchUp_caps_a_long_gap_at_the_step_budget() {
    int ran = GameTime.CatchUp(3600.0, 1f, 10, _ => { });
    Assert.Equal(10, ran); // an hour would be 3600 steps; capped at 10
  }

  [Theory]
  [InlineData(0)] // no step budget
  [InlineData(5)] // a step budget, but no elapsed time
  public void CatchUp_does_nothing_without_elapsed_time_or_budget(int maxSteps) {
    Assert.Equal(0, GameTime.CatchUp(0.0, 1f, maxSteps, _ => { }));
  }

  #endregion

  #region Machine integration (reload)

  private static Block MachineBlock() =>
    TestBlocks.Configure(new Block(), "test:catchupmachine", 99);

  // Ticks m1 once, unloads it, advances the calendar, and reloads a fresh machine from m1's save tree.
  private static CatchupMachine Reload(int steps, double awayHours) {
    var world = new TestWorld();
    Block block = MachineBlock();
    var m1 = new CatchupMachine {
      Pos = new BlockPos(0, 0, 0),
      Block = block,
      Steps = steps,
    };
    world.Attach(m1);
    m1.StartTicking();
    world.FireBlockEntityTicks(); // simulated once at hour 0
    var tree = new TreeAttribute();
    m1.ToTreeAttributes(tree);
    m1.OnBlockUnloaded(); // stop m1's tick before the reload

    world.AdvanceHours(awayHours);

    var m2 = new CatchupMachine {
      Pos = new BlockPos(0, 0, 0),
      Block = block,
      Steps = steps,
    };
    m2.FromTreeAttributes(tree, world.World);
    world.Attach(m2);
    m2.StartTicking();
    world.FireBlockEntityTicks(); // first tick after reload: catch up, then the normal tick
    return m2;
  }

  [Fact]
  public void A_short_absence_catches_up_proportionally() {
    // 3.6 game-seconds away -> 4 catch-up sub-ticks, then 1 normal tick.
    CatchupMachine m = Reload(steps: 100, awayHours: 0.001);
    Assert.Equal(5, m.ProductionTicks);
  }

  [Fact]
  public void A_long_absence_is_capped_at_the_step_budget() {
    // An hour away would be 3600 sub-ticks; capped at 10, plus the 1 normal tick.
    CatchupMachine m = Reload(steps: 10, awayHours: 1.0);
    Assert.Equal(11, m.ProductionTicks);
  }

  [Fact]
  public void Away_catchup_is_off_by_default() {
    // Steps = 0 (the base default): the machine just resumes, no replay.
    CatchupMachine m = Reload(steps: 0, awayHours: 1.0);
    Assert.Equal(1, m.ProductionTicks);
  }

  [Fact]
  public void A_fresh_machine_never_catches_up() {
    var world = new TestWorld();
    var m = new CatchupMachine { Pos = new BlockPos(0, 0, 0), Steps = 10 };
    world.Attach(m);
    m.StartTicking();

    world.AdvanceHours(5); // time passes, but the machine was never simulated before
    world.FireBlockEntityTicks();

    Assert.Equal(1, m.ProductionTicks); // just the one normal tick
  }

  #endregion
}
