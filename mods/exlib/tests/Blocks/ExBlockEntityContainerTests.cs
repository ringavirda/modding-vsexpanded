using ExpandedLib.Blocks;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The container rung of the declared-state family: <see cref="ExBlockEntityContainer"/> gives a
/// <see cref="BlockEntityContainer"/> the same <c>[Persist]</c>/<c>Persisted</c> convenience
/// <see cref="ExBlockEntity"/> gives a plain block entity, layered on top of the inventory the vanilla
/// base already writes by hand.
/// </summary>
public class ExBlockEntityContainerTests {
  private sealed class Container : ExBlockEntityContainer {
    private readonly InventoryGeneric _inventory = new(2, null, null);

    [Persist]
    public int Extra;

    public override InventoryBase Inventory => _inventory;

    public override string InventoryClassName => "exblockentitycontainertests";

    protected override void DeclareState(ExBlockState state) { }
  }

  private static Container Place(TestWorld world, BlockPos pos) {
    Block block = TestBlocks.Configure(new Block(), "test:excontainer", 1);
    var be = new Container { Pos = pos, Block = block };
    world.Place(pos, block, be);
    world.Initialize(be);
    return be;
  }

  [Fact]
  public void A_Persist_field_round_trips() {
    var world = new TestWorld();
    Container source = Place(world, new BlockPos(0, 0, 0));
    source.Extra = 9;

    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    Container target = Place(world, new BlockPos(1, 0, 0));
    target.FromTreeAttributes(tree, world.World);

    Assert.Equal(9, target.Extra);
  }

  [Fact]
  public void The_inventory_still_round_trips_through_base() {
    var world = new TestWorld();
    Item item = world.RegisterItem("test:widget");
    Container source = Place(world, new BlockPos(0, 0, 0));
    source.Inventory[0].Itemstack = new ItemStack(item, 5);

    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    Container target = Place(world, new BlockPos(1, 0, 0));
    target.FromTreeAttributes(tree, world.World);

    Assert.Equal(5, target.Inventory[0].Itemstack?.StackSize);
  }
}
