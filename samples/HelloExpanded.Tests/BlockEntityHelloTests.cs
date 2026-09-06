using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace HelloExpanded.Tests;

/// <summary>The whole "test it" walk: place the block, drive its production tick, check the saved
/// state round-trips, then reset it through a sneak click.</summary>
public class BlockEntityHelloTests {
  private static (TestWorld world, BlockEntityHello be) PlaceHello() {
    var world = new TestWorld();
    var pos = new BlockPos(0, 0, 0);
    Block block = TestBlocks.Configure(new BlockHello(), "helloexpanded:hello", 1);
    var be = new BlockEntityHello();
    world.Place(pos, block, be);
    world.Initialize(be);
    return (world, be);
  }

  [Fact]
  public void Counts_a_tick_and_round_trips_through_the_tree() {
    (TestWorld world, BlockEntityHello be) = PlaceHello();

    world.FireBlockEntityTicks(times: 3);

    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);
    Assert.Equal(3, tree.GetInt("ticks"));

    var restored = new BlockEntityHello { Pos = be.Pos, Block = be.Block };
    restored.FromTreeAttributes(tree, world.World);

    var restoredTree = new TreeAttribute();
    restored.ToTreeAttributes(restoredTree);
    Assert.Equal(3, restoredTree.GetInt("ticks"));
  }

  [Fact]
  public void Sneak_click_resets_the_counter() {
    (TestWorld world, BlockEntityHello be) = PlaceHello();
    world.FireBlockEntityTicks(times: 5);

    TestPlayer player = world.Player();
    player.Entity.Controls.ShiftKey = true;
    var selection = new BlockSelection {
      Position = be.Pos,
      Face = BlockFacing.NORTH,
    };

    bool handled = be.Block.OnBlockInteractStart(
      world.World,
      player.Player,
      selection
    );

    Assert.True(handled);
    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);
    Assert.Equal(0, tree.GetInt("ticks"));
  }
}
