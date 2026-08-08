using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The harness lifecycle helpers. <see cref="TestWorld.Reload"/> runs the real save, discard,
/// fresh-instance, FromTree, Initialize sequence, and a discarded block entity stops ticking because
/// the harness honours <c>UnregisterGameTickListener</c>.
/// </summary>
public class TestWorldLifecycleTests {
  [Fact]
  public void Reload_round_trips_state_and_the_old_instance_stops_ticking() {
    var world = new TestWorld();
    var pos = new BlockPos(0, 0, 0);
    var block = TestBlocks.Configure(new Block(), "test:stateful", 1);
    var original = new StatefulBe { Saved = 3 };
    world.Place(pos, block, original);
    world.Initialize(original);

    world.FireBlockEntityTicks();
    Assert.Equal(1, original.Ticks);

    original.Saved = 42; // state that must survive the reload
    var reloaded = Assert.IsType<StatefulBe>(world.Reload(pos));

    Assert.NotSame(original, reloaded); // a fresh instance, as on disk load
    Assert.Equal(42, reloaded.Saved); // restored via ToTree/FromTree
    Assert.Same(reloaded, world.GetBlockEntity(pos));

    world.FireBlockEntityTicks();
    Assert.Equal(1, reloaded.Ticks); // the reloaded instance ticks...
    Assert.Equal(1, original.Ticks); // ...and the discarded one no longer does (no double-tick)
  }

  [Fact]
  public void Unload_drops_the_entity_and_stops_its_listener_but_keeps_the_block() {
    var world = new TestWorld();
    var pos = new BlockPos(1, 0, 0);
    var block = TestBlocks.Configure(new Block(), "test:stateful", 2);
    var be = new StatefulBe();
    world.Place(pos, block, be);
    world.Initialize(be);

    world.FireBlockEntityTicks();
    Assert.Equal(1, be.Ticks);

    world.Unload(pos);
    Assert.Null(world.GetBlockEntity(pos)); // entity gone
    Assert.Same(block, world.GetBlock(pos)); // block still placed

    world.FireBlockEntityTicks();
    Assert.Equal(1, be.Ticks); // the unloaded listener no longer fires
  }

  [Fact]
  public void Reload_of_an_empty_cell_is_a_no_op() {
    var world = new TestWorld();
    Assert.Null(world.Reload(new BlockPos(5, 5, 5)));
  }

  [Fact]
  public void Break_runs_the_real_block_broken_and_removed_lifecycle() {
    // Production code breaks blocks through the accessor, so the break must run OnBlockBroken and
    // OnBlockRemoved rather than only wiping the store: a network node calls RemoveNode there.
    var world = new TestWorld();
    var pos = new BlockPos(2, 0, 0);
    var block = TestBlocks.Configure(new Block(), "test:lifecycle", 3);
    var be = new LifecycleBe();
    world.Place(pos, block, be);
    world.Initialize(be);

    world.Accessor.BreakBlock(pos, null, 0f);

    Assert.True(be.BrokenCalled);
    Assert.True(be.RemovedCalled);
    Assert.Null(world.GetBlockEntity(pos));
    Assert.Same(world.Air, world.GetBlock(pos));
  }

  /// <summary>Records that its break/removed lifecycle hooks ran.</summary>
  private sealed class LifecycleBe : BlockEntity {
    public bool BrokenCalled;
    public bool RemovedCalled;

    public override void OnBlockBroken(IPlayer? byPlayer = null) {
      BrokenCalled = true;
      base.OnBlockBroken(byPlayer);
    }

    public override void OnBlockRemoved() {
      RemovedCalled = true;
      base.OnBlockRemoved();
    }
  }

  /// <summary>A minimal block entity that persists one value and counts its own tick.</summary>
  private sealed class StatefulBe : BlockEntity {
    public int Saved;
    public int Ticks;

    public override void Initialize(ICoreAPI api) {
      base.Initialize(api);
      RegisterGameTickListener(_ => Ticks++, 1000);
    }

    public override void ToTreeAttributes(ITreeAttribute tree) {
      base.ToTreeAttributes(tree);
      tree.SetInt("saved", Saved);
    }

    public override void FromTreeAttributes(
      ITreeAttribute tree,
      IWorldAccessor world
    ) {
      base.FromTreeAttributes(tree, world);
      Saved = tree.GetInt("saved");
    }
  }
}
