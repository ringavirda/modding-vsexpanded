using ExpandedLib.Blocks.Machines;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Forming.BlockEntities;
using IronworkingExpanded.BlockStructures.Forming.Blocks;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The mill's own pass clock, driven through the real listener rather than by calling
/// <c>AdvancePass</c> directly. The rest of the mill suite drives the physics; this covers the clock
/// that feeds it - its interval, where its <c>dt</c> and its speed come from, and the bound on a
/// <c>dt</c> the engine hands over after a stall. See docs/design/machines/rolling-mill.md.
/// </summary>
public class RollingMillClockTests {
  private const float RollRadius = 4f; // IwexValues.RollingRollRadius
  private const int PassTickMs = 250; // BlockEntityRollingMill.PassTickMs

  /// <summary>A mill on a live <c>mpenergy</c> run turning at <paramref name="speed"/>, taken through
  /// the real <see cref="Vintagestory.API.Common.BlockEntity.Initialize"/> so its clock is registered
  /// the way the placement pipeline registers it.</summary>
  private static (TestWorld World, BlockEntityRollingMill Mill) Mill(
    float speed
  ) {
    var world = new TestWorld();
    world.RegisterNetwork("mpenergy", sys => new MpEnergyNetwork(sys));

    var block = TestBlocks.Configure(
      new BlockRollingMill(),
      "iwex:forming-rollingmill-we",
      1,
      ("type", "rollingmill"),
      ("orientation", "we")
    );
    ReflectionHelpers.SetProperty(block, "Type", "rollingmill");
    ReflectionHelpers.SetProperty(block, "Orientation", "we");

    var pos = new BlockPos(0, 0, 0);
    var mill = new BlockEntityRollingMill();
    world.Place(pos, block, mill);
    // The membership joins the graph from here, so the mill resolves its own network rather than
    // being handed one.
    world.Initialize(mill);

    ((MpEnergyNetwork)world.NetworkAt(pos)!).RestoreState(
      new MpEnergyNetworkState { Inertia = 1f, Speed = speed }
    );
    return (world, mill);
  }

  // A hot pass long enough that no single clamped tick can finish it.
  private static void Feed(BlockEntityRollingMill mill, float length) =>
    Assert.True(mill.BeginPass(draft: 0.25f, length: length, tempC: 1100f));

  #region What the mill publishes

  [Fact]
  public void A_pass_is_what_the_mill_publishes_as_readiness() {
    // Read through the contract rather than off the class: an answer declared in a shape the interface
    // map does not see falls back to the default, and the default for the second question - give the
    // tick up - is the wrong answer for a stand that has to notice its own next piece. A bare mill,
    // so this also holds before Initialize has run.
    var mill = new BlockEntityRollingMill();
    Assert.Null(mill.Api);

    Assert.False(ProductionReadiness.IsReady(mill));
    Assert.False(ProductionReadiness.StopsProductionWhenNotReady(mill));

    Assert.True(mill.BeginPass(draft: 0.25f, length: 4f, tempC: 1100f));
    Assert.True(ProductionReadiness.IsReady(mill));
  }

  #endregion

  #region The clock itself

  [Fact]
  public void The_mill_draws_its_stock_on_its_own_250_ms_clock() {
    var (world, mill) = Mill(speed: 1f);
    Feed(mill, length: 100f);
    float atRest = mill.Remaining;

    // Short of the interval nothing has turned, which is also the premise for the second half: any
    // movement below can only have come from a listener registered at 250 ms.
    world.AdvanceBlockEntityTime(PassTickMs - 10);
    Assert.Equal(atRest, mill.Remaining, 4);

    world.AdvanceBlockEntityTime(10);
    Assert.Equal(atRest - RollRadius * PassTickMs / 1000f, mill.Remaining, 4);
  }

  [Fact]
  public void The_clock_draws_the_stock_at_the_speed_the_run_settled_at() {
    // Same elapsed time, twice the shaft speed, twice the travel: the tick reads the live network
    // rather than a cached broadcast.
    var (slowWorld, slow) = Mill(speed: 1f);
    var (fastWorld, fast) = Mill(speed: 2f);
    Feed(slow, length: 100f);
    Feed(fast, length: 100f);

    slowWorld.AdvanceBlockEntityTime(1000);
    fastWorld.AdvanceBlockEntityTime(1000);

    Assert.Equal(100f - RollRadius * 1f, slow.Remaining, 3);
    Assert.Equal(100f - RollRadius * 2f, fast.Remaining, 3);
  }

  [Fact]
  public void An_empty_stand_keeps_its_clock_and_draws_the_next_piece() {
    // Nothing else watches the rolls, so a mill that gave the tick up when it emptied would never
    // start the pass that follows.
    var (world, mill) = Mill(speed: 1f);
    world.AdvanceBlockEntityTime(4000); // sixteen ticks with nothing under the rolls
    Assert.False(mill.IsRolling);

    Feed(mill, length: 100f);
    world.AdvanceBlockEntityTime(PassTickMs);

    Assert.Equal(100f - RollRadius * PassTickMs / 1000f, mill.Remaining, 4);
  }

  #endregion

  #region The bound on a stalled server's dt

  [Fact]
  public void A_stalled_server_cannot_push_a_whole_piece_through_the_rolls() {
    // A server that stalls hands its listeners one dt covering the whole stall. Unbounded, a 20 s one
    // travels 80 units at this speed and clears any real pass in a single step - a free finished pass
    // for a stand nobody was standing at.
    var (world, mill) = Mill(speed: 1f);
    Feed(mill, length: 20f);

    world.FireBlockEntityTicks(dt: 20f);

    Assert.True(
      mill.IsRolling,
      "the pass must not have been finished by one tick"
    );
    Assert.Empty(world.Drops);
    Assert.Equal(20f - RollRadius * 0.5f, mill.Remaining, 4);
  }

  [Fact]
  public void A_stalled_server_cannot_chill_the_piece_in_the_rolls_in_one_step() {
    // The other end of the same bug: cooling is exponential in dt, so an unbounded step past about
    // 40 s takes a piece at rolling heat below it in one go. The pass then freezes cold in the rolls,
    // which no amount of drive torque recovers - only a wrench does.
    var (world, mill) = Mill(speed: 1f);
    Feed(mill, length: 100f);

    world.FireBlockEntityTicks(dt: 3600f); // an hour of stall in a single tick

    Assert.False(
      mill.IsStalled,
      "the piece must not have gone cold in one step"
    );
    Assert.Equal(100f - RollRadius * 0.5f, mill.Remaining, 4);
  }

  #endregion
}
