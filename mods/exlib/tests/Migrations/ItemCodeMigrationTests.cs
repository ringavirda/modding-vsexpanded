using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Migrations;
using ExpandedLib.Testing;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="IItemCodeMigration"/>: a real implementation's declared pairs, fed into
/// <see cref="BlockMigrationModSystem.RemapInventory"/> the same way <see cref="BlockMigrationMutationTests"/>
/// drives a hand-built <c>ItemRemapEntry</c> - the mutation path itself is that class's job, this one
/// proves an actual <see cref="IItemCodeMigration"/> implementation resolves correctly into it.
/// </summary>
public class ItemCodeMigrationTests {
  /// <summary>A migration renaming one item code, in the shape a mod would actually declare.</summary>
  private sealed class RenameMigration : IItemCodeMigration {
    public string Name => "rename test";

    public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(ICoreServerAPI api) =>
      [(new AssetLocation("stub:olditem"), new AssetLocation("stub:newitem"))];
  }

  private static IInventory Inventory(params ItemSlot[] slots) {
    var list = slots.ToList();
    var inv = Substitute.For<IInventory>();
    inv.GetEnumerator().Returns(_ => list.GetEnumerator());
    return inv;
  }

  private static BlockMigrationModSystem System(TestWorld world) {
    var sys = new BlockMigrationModSystem();
    ReflectionHelpers.SetField(sys, "_sapi", world.Api);
    return sys;
  }

  [Fact]
  public void GetRemaps_declares_the_pair_the_migration_names() {
    var migration = new RenameMigration();

    var pairs = migration.GetRemaps(new TestWorld().Api).ToList();

    Assert.Equal(new AssetLocation("stub:olditem"), pairs[0].oldCode);
    Assert.Equal(new AssetLocation("stub:newitem"), pairs[0].newCode);
  }

  [Fact]
  public void A_declared_pair_remaps_a_held_stack_in_an_inventory_tree() {
    var world = new TestWorld();
    Item oldItem = world.RegisterItem("stub:olditem");
    Item newItem = world.RegisterItem("stub:newitem");

    var sys = System(world);
    foreach (var (oldCode, newCode) in new RenameMigration().GetRemaps(world.Api))
      sys._itemRemap[oldCode] = new BlockMigrationModSystem.ItemRemapEntry(
        world.GetItem(newCode)!,
        oldCode,
        newCode
      );

    var slot = new DummySlot(new ItemStack(oldItem, 5));
    int changed = sys.RemapInventory(Inventory(slot));

    Assert.Equal(1, changed);
    Assert.Same(newItem, slot.Itemstack!.Collectible);
    Assert.Equal(5, slot.Itemstack.StackSize);
  }

  [Fact]
  public void A_stack_the_migration_never_names_is_left_alone() {
    var world = new TestWorld();
    Item untouched = world.RegisterItem("stub:untouched");
    var sys = System(world);
    // Nothing registered in _itemRemap: this migration names only "stub:olditem".

    var slot = new DummySlot(new ItemStack(untouched, 3));
    int changed = sys.RemapInventory(Inventory(slot));

    Assert.Equal(0, changed);
    Assert.Same(untouched, slot.Itemstack!.Collectible);
  }
}
