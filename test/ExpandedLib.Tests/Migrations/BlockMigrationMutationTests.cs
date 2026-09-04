using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Migrations;
using ExpandedLib.Testing;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The block migrator's mutation path, which rewrites save data; the per-mod <c>GetRemaps</c>
/// definitions are covered elsewhere. Exercises the two primitives the chunk sweep drives:
/// <see cref="BlockMigrationModSystem.ReplaceBlock"/> (a placed block swapped in the world, with and
/// without block-entity state) and <see cref="BlockMigrationModSystem.RemapInventory"/> (migrated
/// content held as item stacks in containers and inventories).
/// </summary>
public class BlockMigrationMutationTests {
  private static Block Block(string code, int id) =>
    TestBlocks.Configure(new Block(), code, id);

  private static BlockMigrationModSystem System(TestWorld world) {
    var sys = new BlockMigrationModSystem();
    // ReplaceBlock's block-entity branch reaches through _sapi.World when handing the old tree to a
    // migration. The headless harness never runs StartServerSide, which is what would set it.
    ReflectionHelpers.SetField(sys, "_sapi", world.Api);
    return sys;
  }

  #region ReplaceBlock - in-world swaps

  [Fact]
  public void ReplaceBlock_swaps_a_plain_block_in_place() {
    var world = new TestWorld();
    var pos = new BlockPos(3, 4, 5, 0);
    var oldBlock = Block("smex:pipe-old", 20);
    var newBlock = Block("iiex:pipe-new", 21);
    world.Place(pos, oldBlock);
    world.Register(newBlock);

    var sys = System(world);
    var entry = new BlockMigrationModSystem.RemapEntry(
      newBlock,
      oldBlock.Code,
      newBlock.Code,
      null
    );
    sys.ReplaceBlock(world.Accessor, pos, entry);

    Assert.Same(newBlock, world.GetBlock(pos));
  }

  [Fact]
  public void ReplaceBlock_deletes_the_block_and_its_entity_for_a_removal() {
    var world = new TestWorld();
    var pos = new BlockPos(6, 7, 8, 0);
    var oldBlock = Block("smex:gone", 22);
    world.Place(pos, oldBlock, new StatefulBe());

    var sys = System(world);
    // A removal entry: null replacement block.
    var entry = new BlockMigrationModSystem.RemapEntry(
      null,
      oldBlock.Code,
      null,
      null
    );
    sys.ReplaceBlock(world.Accessor, pos, entry);

    Assert.Same(world.Air, world.GetBlock(pos)); // block cleared
    Assert.Null(world.GetBlockEntity(pos)); // and its entity
  }

  [Fact]
  public void ReplaceBlock_copies_block_entity_state_onto_a_fresh_entity() {
    var world = new TestWorld();
    var pos = new BlockPos(1, 2, 3, 0);
    const string newClass = "migratetest";

    var oldBlock = Block("smex:furnace-old", 30);
    var newBlock = Block("iiex:furnace-new", 31);
    newBlock.EntityClass = newClass;
    world.Register(oldBlock);
    world.Register(newBlock);
    // The engine creates the new block's BE when the block is placed, and the harness mirrors that for
    // a registered class. ReplaceBlock relies on it to hand the old tree over.
    world.RegisterBlockEntityFactory(newClass, () => new StatefulBe());

    var oldBe = new StatefulBe { Value = 99 };
    world.Place(pos, oldBlock, oldBe);

    var sys = System(world);
    var entry = new BlockMigrationModSystem.RemapEntry(
      newBlock,
      oldBlock.Code,
      newBlock.Code,
      new CopyTreeMigration()
    );
    sys.ReplaceBlock(world.Accessor, pos, entry);

    var newBe = Assert.IsType<StatefulBe>(world.GetBlockEntity(pos));
    Assert.NotSame(oldBe, newBe); // a fresh entity, not the old instance
    Assert.Same(newBlock, world.GetBlock(pos)); // block swapped
    Assert.Equal(99, newBe.Value); // old saved state carried over
    Assert.True(newBe.Dirtied); // and marked dirty so it persists
  }

  #endregion

  #region RemapInventory - held item stacks

  [Fact]
  public void RemapInventory_swaps_a_block_stack_preserving_size_and_attributes() {
    var world = new TestWorld();
    var oldBlock = Block("smex:slag", 40);
    var newBlock = Block("iiex:slag", 41);

    var sys = System(world);
    sys._remap[oldBlock.Code] = new BlockMigrationModSystem.RemapEntry(
      newBlock,
      oldBlock.Code,
      newBlock.Code,
      null
    );

    var stack = new ItemStack(oldBlock, 7);
    stack.Attributes.SetInt("charge", 3); // makes attributes non-empty, so they must be carried
    var slot = new DummySlot(stack);

    int changed = sys.RemapInventory(Inventory(slot));

    Assert.Equal(1, changed);
    ItemStack? result = slot.Itemstack;
    Assert.NotNull(result);
    Assert.Same(newBlock, result.Collectible); // swapped to the replacement block
    Assert.Equal(7, result.StackSize); // stack size preserved
    Assert.Equal(3, result.Attributes.GetInt("charge")); // attributes preserved
  }

  [Fact]
  public void RemapInventory_drops_a_block_stack_that_maps_to_a_removal() {
    var world = new TestWorld();
    var oldBlock = Block("smex:purged", 42);

    var sys = System(world);
    sys._remap[oldBlock.Code] = new BlockMigrationModSystem.RemapEntry(
      null,
      oldBlock.Code,
      null,
      null
    );

    var slot = new DummySlot(new ItemStack(oldBlock, 4));

    int changed = sys.RemapInventory(Inventory(slot));

    Assert.Equal(1, changed);
    Assert.Null(slot.Itemstack); // the stack is emptied out
  }

  [Fact]
  public void RemapInventory_swaps_an_item_stack_preserving_size() {
    var world = new TestWorld();
    Item oldItem = world.RegisterItem("smex:olditem");
    Item newItem = world.RegisterItem("iiex:newitem");

    var sys = System(world);
    sys._itemRemap[oldItem.Code] = new BlockMigrationModSystem.ItemRemapEntry(
      newItem,
      oldItem.Code,
      newItem.Code
    );

    var slot = new DummySlot(new ItemStack(oldItem, 12));

    int changed = sys.RemapInventory(Inventory(slot));

    Assert.Equal(1, changed);
    ItemStack? result = slot.Itemstack;
    Assert.NotNull(result);
    Assert.Same(newItem, result.Collectible);
    Assert.Equal(12, result.StackSize);
    Assert.Equal(EnumItemClass.Item, result.Class);
  }

  [Fact]
  public void RemapInventory_picks_the_table_by_the_stacks_class_for_a_dual_code() {
    // "slag" exists as both a block and an item; the block table and item table map it to different
    // replacements, and RemapInventory must resolve each stack by its own class.
    var world = new TestWorld();
    var oldSlagBlock = Block("smex:slag", 50);
    var newSlagBlock = Block("iiex:slag", 51);
    Item oldSlagItem = world.RegisterItem("smex:slag");
    Item newSlagItem = world.RegisterItem("iiex:slag");

    var sys = System(world);
    sys._remap[oldSlagBlock.Code] = new BlockMigrationModSystem.RemapEntry(
      newSlagBlock,
      oldSlagBlock.Code,
      newSlagBlock.Code,
      null
    );
    sys._itemRemap[oldSlagItem.Code] =
      new BlockMigrationModSystem.ItemRemapEntry(
        newSlagItem,
        oldSlagItem.Code,
        newSlagItem.Code
      );

    var blockSlot = new DummySlot(new ItemStack(oldSlagBlock, 1));
    var itemSlot = new DummySlot(new ItemStack(oldSlagItem, 1));

    int changed = sys.RemapInventory(Inventory(blockSlot, itemSlot));

    Assert.Equal(2, changed);
    Assert.NotNull(blockSlot.Itemstack);
    Assert.NotNull(itemSlot.Itemstack);
    Assert.Same(newSlagBlock, blockSlot.Itemstack.Collectible); // block -> block table
    Assert.Same(newSlagItem, itemSlot.Itemstack.Collectible); // item  -> item table
  }

  [Fact]
  public void RemapInventory_leaves_unmapped_and_empty_slots_untouched() {
    var world = new TestWorld();
    var mapped = Block("smex:old", 60);
    var newBlock = Block("iiex:new", 61);
    var unmapped = Block("game:cobblestone", 62);

    var sys = System(world);
    sys._remap[mapped.Code] = new BlockMigrationModSystem.RemapEntry(
      newBlock,
      mapped.Code,
      newBlock.Code,
      null
    );

    var unmappedSlot = new DummySlot(new ItemStack(unmapped, 5));
    var emptySlot = new DummySlot(); // no stack

    int changed = sys.RemapInventory(Inventory(unmappedSlot, emptySlot));

    Assert.Equal(0, changed);
    Assert.NotNull(unmappedSlot.Itemstack);
    Assert.Same(unmapped, unmappedSlot.Itemstack.Collectible); // left as-is
    Assert.Null(emptySlot.Itemstack);
  }

  #endregion

  #region Helpers

  /// <summary>An inventory that enumerates the given slots, which is all
  /// <see cref="BlockMigrationModSystem.RemapInventory"/> asks of an <see cref="IInventory"/>.</summary>
  private static IInventory Inventory(params ItemSlot[] slots) {
    var list = slots.ToList();
    var inv = Substitute.For<IInventory>();
    inv.GetEnumerator().Returns(_ => list.GetEnumerator());
    return inv;
  }

  /// <summary>A copy-verbatim block-entity migration, the same class either side of the swap, as the
  /// relocated furnace and canal entities use.</summary>
  private sealed class CopyTreeMigration : IBlockEntityMigration {
    public void MigrateBlockEntity(
      AssetLocation oldCode,
      AssetLocation newCode,
      ITreeAttribute? oldState,
      BlockEntity newBlockEntity,
      IWorldAccessor world
    ) {
      if (oldState != null)
        newBlockEntity.FromTreeAttributes(oldState, world);
    }
  }

  /// <summary>A block entity carrying one saved value, so a state hand-off can be observed, plus a flag
  /// recording that it was marked dirty.</summary>
  private sealed class StatefulBe : BlockEntity {
    public int Value { get; set; }
    public bool Dirtied { get; private set; }

    public override void ToTreeAttributes(ITreeAttribute tree) {
      base.ToTreeAttributes(tree);
      tree.SetInt("value", Value);
    }

    public override void FromTreeAttributes(
      ITreeAttribute tree,
      IWorldAccessor worldAccessForResolve
    ) {
      base.FromTreeAttributes(tree, worldAccessForResolve);
      Value = tree.GetInt("value");
    }

    public override void MarkDirty(
      bool redrawOnClient = false,
      IPlayer? skipPlayer = null
    ) => Dirtied = true;
  }

  #endregion
}
