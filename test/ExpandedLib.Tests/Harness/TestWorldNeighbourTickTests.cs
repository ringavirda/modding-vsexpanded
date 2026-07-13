using System.Collections.Generic;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The opt-in harness capabilities that let a test exercise engine behaviours the low-level helpers
/// deliberately do not auto-fire: <see cref="TestWorld.NotifyNeighbours"/> (the engine's post-change
/// neighbour notification, which drives self-break / reorientation / connector-update reactions) and
/// <see cref="TestWorld.AdvanceBlockEntityTime"/> (interval-aware ticking, honouring the interval each
/// block entity registered rather than firing everything uniformly).
/// </summary>
public class TestWorldNeighbourTickTests
{
  #region Neighbour propagation

  [Fact]
  public void NotifyNeighbours_calls_each_of_the_six_neighbours_with_its_own_and_the_changed_position()
  {
    var world = new TestWorld();
    var center = new BlockPos(10, 10, 10);
    var eastPos = center.AddCopy(BlockFacing.EAST);
    var upPos = center.AddCopy(BlockFacing.UP);
    var farPos = center.AddCopy(BlockFacing.EAST).AddCopy(BlockFacing.EAST); // 2 away - not a neighbour

    var east = Probe("test:probe-east", 1);
    var up = Probe("test:probe-up", 2);
    var far = Probe("test:probe-far", 3);
    world.Place(eastPos, east);
    world.Place(upPos, up);
    world.Place(farPos, far);

    world.NotifyNeighbours(center);

    var eastCall = Assert.Single(east.Calls);
    Assert.Equal(eastPos, eastCall.own); // told its own pos...
    Assert.Equal(center, eastCall.changed); // ...and what changed
    var upCall = Assert.Single(up.Calls);
    Assert.Equal(upPos, upCall.own);
    Assert.Equal(center, upCall.changed);
    Assert.Empty(far.Calls); // not adjacent - never notified
  }

  [Fact]
  public void NotifyNeighbours_treats_empty_neighbour_cells_as_a_no_op()
  {
    var world = new TestWorld();
    // No blocks placed: every neighbour resolves to Air, whose base OnNeighbourBlockChange does nothing.
    var ex = Record.Exception(() => world.NotifyNeighbours(new BlockPos(0, 0, 0)));
    Assert.Null(ex);
  }

  [Fact]
  public void NotifyNeighbours_lets_a_neighbour_reaction_mutate_the_world()
  {
    // Faithful to a real reaction: a block that finds itself unsupported when a neighbour changes and
    // removes itself (as BlockNetworkNode self-breaks). The notification must be able to drive that.
    var world = new TestWorld();
    var center = new BlockPos(4, 4, 4);
    var reactingPos = center.AddCopy(BlockFacing.NORTH);
    var reacting = new SelfRemovingBlock();
    world.Place(reactingPos, TestBlocks.Configure(reacting, "test:selfremove", 5));

    world.NotifyNeighbours(center);

    Assert.Same(world.Air, world.GetBlock(reactingPos)); // it reacted by removing itself
  }

  #endregion

  #region Interval-aware ticking

  [Fact]
  public void AdvanceBlockEntityTime_fires_a_listener_once_per_whole_interval_carrying_remainders()
  {
    var world = new TestWorld();
    var be = Placed(world, new IntervalBe(1000), 6);

    world.AdvanceBlockEntityTime(2500); // 2 whole 1000 ms intervals, 500 ms left over
    Assert.Equal(2, be.Ticks);
    Assert.Equal(1.0f, be.LastDt, 3); // dt = interval / 1000

    world.AdvanceBlockEntityTime(500); // 500 carried + 500 = one more interval
    Assert.Equal(3, be.Ticks);
  }

  [Fact]
  public void AdvanceBlockEntityTime_honours_each_block_entitys_own_interval()
  {
    var world = new TestWorld();
    var slow = Placed(world, new IntervalBe(1000), 7);
    var fast = Placed(world, new IntervalBe(250), 8);

    world.AdvanceBlockEntityTime(1000);

    Assert.Equal(1, slow.Ticks); // one 1000 ms interval
    Assert.Equal(4, fast.Ticks); // four 250 ms intervals
    Assert.Equal(0.25f, fast.LastDt, 3);
  }

  [Fact]
  public void AdvanceBlockEntityTime_stops_firing_a_listener_that_unregisters_mid_advance()
  {
    var world = new TestWorld();
    var be = new IntervalBe(1000) { UnregisterAfterFirst = true };
    Placed(world, be, 9);

    world.AdvanceBlockEntityTime(3000); // would be 3 fires, but it tears its listener down after 1

    Assert.Equal(1, be.Ticks);
  }

  #endregion

  #region Helpers

  private static NeighbourProbeBlock Probe(string code, int id)
  {
    var block = new NeighbourProbeBlock();
    TestBlocks.Configure(block, code, id);
    return block;
  }

  private static IntervalBe Placed(TestWorld world, IntervalBe be, int id)
  {
    var pos = new BlockPos(id, 0, 0);
    world.Place(pos, TestBlocks.Configure(new Block(), "test:interval-" + id, id), be);
    world.Initialize(be);
    return be;
  }

  /// <summary>Records every neighbour-change notification it receives (its own pos, the changed pos).</summary>
  private sealed class NeighbourProbeBlock : Block
  {
    public readonly List<(BlockPos own, BlockPos changed)> Calls = [];

    public override void OnNeighbourBlockChange(
      IWorldAccessor world,
      BlockPos pos,
      BlockPos neibpos
    ) => Calls.Add((pos.Copy(), neibpos.Copy()));
  }

  /// <summary>Reacts to any neighbour change by removing itself, like an unsupported block self-breaking.</summary>
  private sealed class SelfRemovingBlock : Block
  {
    public override void OnNeighbourBlockChange(
      IWorldAccessor world,
      BlockPos pos,
      BlockPos neibpos
    ) => world.BlockAccessor.SetBlock(0, pos);
  }

  /// <summary>Registers a tick listener at a chosen interval and counts its fires; can tear its own
  /// listener down after the first fire to model a self-unregistering block entity.</summary>
  private sealed class IntervalBe : BlockEntity
  {
    private readonly int _interval;
    private long _id;

    public int Ticks;
    public float LastDt;
    public bool UnregisterAfterFirst;

    public IntervalBe(int interval) => _interval = interval;

    public override void Initialize(ICoreAPI api)
    {
      base.Initialize(api);
      _id = RegisterGameTickListener(OnTick, _interval);
    }

    private void OnTick(float dt)
    {
      Ticks++;
      LastDt = dt;
      if (UnregisterAfterFirst)
        Api.Event.UnregisterGameTickListener(_id);
    }
  }

  #endregion
}
