using ExpandedLib.Testing;
using IronworkingExpanded.Items;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The bell hopper no longer mixes: it pulls ready-made burden from the reinforced tank above into its
/// magazine and drips that down the furnace shaft. Covers the magazine/dropping persistence, the
/// furnace-full check, the pull-from-tank feed, and the drip into a shaft pile (grade preserved).
/// </summary>
public class BellHopperTests
{
  private static readonly BlockPos BellPos = new(0, 16, 0);

  private static BlockEntityHopperBell Bell(TestWorld world)
  {
    var be = new BlockEntityHopperBell
    {
      Pos = BellPos,
      Block = TestBlocks.Configure(new Block(), "iwex:hopperbell", 90),
    };
    world.Place(BellPos, be.Block, be);
    world.Attach(be);
    return be;
  }

  private static (BlockEntityHopperReinforced hopper, Item burden) HopperAbove(
    TestWorld world,
    Item burden,
    int units
  )
  {
    var be = new BlockEntityHopperReinforced
    {
      Pos = BellPos.UpCopy(),
      Block = TestBlocks.Configure(new Block(), "iwex:hopperreinforced", 91),
    };
    world.Place(be.Pos, be.Block, be);
    world.Attach(be);
    if (units > 0)
      be.TryDeposit(new DummySlot(new ItemStack(burden, units)), wholeStack: true);
    return (be, burden);
  }

  private static BlockEntityCoalPile CoalPile(
    TestWorld world,
    BlockPos pos,
    Item content,
    int units
  )
  {
    var pile = new BlockEntityCoalPile { Pos = pos.Copy() };
    var inv = new InventoryGeneric(1, "coalpile", "test", world.Api, null);
    if (units > 0)
      inv[0].Itemstack = new ItemStack(content, units);
    ReflectionHelpers.SetField(pile, "inventory", inv);
    world.Place(
      pos,
      TestBlocks.Configure(new Block(), "game:coalpile", 50 + pos.Y),
      pile
    );
    world.Attach(pile);
    return pile;
  }

  #region Default state

  [Fact]
  public void A_freshly_placed_bell_hopper_is_dropping_by_default()
  {
    var bell = Bell(new TestWorld());
    Assert.True(bell.IsDropping);
  }

  [Fact]
  public void Dropping_defaults_on_when_a_saved_tree_omits_the_flag()
  {
    var world = new TestWorld();
    var bell = Bell(world);
    ReflectionHelpers.SetField(bell, "_isDropping", false);

    bell.FromTreeAttributes(new TreeAttribute(), world.World); // legacy tree, no key

    Assert.True(bell.IsDropping);
  }

  [Fact]
  public void An_explicitly_stopped_bell_stays_stopped_across_a_reload()
  {
    var world = new TestWorld();
    var src = Bell(world);
    src.IsDropping = false;

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var dst = Bell(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.False(dst.IsDropping);
  }

  #endregion

  #region Persistence

  [Fact]
  public void Magazine_and_dropping_round_trip_through_the_tree()
  {
    var world = new TestWorld();
    var burden = world.RegisterItem("iwex:burden");
    var src = Bell(world);
    ReflectionHelpers.SetField(src, "_magazine", new ItemStack(burden, 24));
    src.IsDropping = true;

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var dst = Bell(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.Equal(24, dst.BlastMixMagazine);
    Assert.True(dst.IsDropping);
  }

  #endregion

  #region Furnace-full check

  [Fact]
  public void IsFurnaceFull_is_false_with_no_coalpile_below()
  {
    Assert.False(Bell(new TestWorld()).IsFurnaceFull());
  }

  #endregion

  #region Feed + drip

  [Fact]
  public void OnServerTick_pulls_burden_from_the_tank_above_into_the_magazine()
  {
    var world = new TestWorld();
    var burden = world.RegisterItem("iwex:burden");
    var bell = Bell(world);
    var (hopper, _) = HopperAbove(world, burden, 30);

    ReflectionHelpers.Invoke(bell, "OnServerTick", 1f);

    Assert.Equal(30, bell.BlastMixMagazine);
    Assert.Equal(0, hopper.TankCount);
  }

  [Fact]
  public void OnServerTick_does_nothing_with_an_empty_tank_above()
  {
    var world = new TestWorld();
    var burden = world.RegisterItem("iwex:burden");
    var bell = Bell(world);
    HopperAbove(world, burden, 0);

    ReflectionHelpers.Invoke(bell, "OnServerTick", 1f);

    Assert.Equal(0, bell.BlastMixMagazine);
  }

  [Fact]
  public void OnServerTick_drips_the_pulled_burden_into_the_shaft_pile()
  {
    var world = new TestWorld();
    var burden = world.RegisterItem("iwex:burden");
    var bell = Bell(world);
    HopperAbove(world, burden, 30);

    // A solid floor two below, a burden pile just above it - a valid drop target for the bell.
    world.Place(
      BellPos.DownCopy(3),
      TestBlocks.Configure(new Block(), "game:rock", 40),
      null
    );
    var pile = CoalPile(world, BellPos.DownCopy(2), burden, 4);

    ReflectionHelpers.Invoke(bell, "OnServerTick", 1f);

    int drop = SmexValues.HopperDropAmount;
    Assert.Equal(4 + drop, pile.inventory[0].StackSize); // pile grew by one drip
    Assert.Equal(30 - drop, bell.BlastMixMagazine); // magazine fell by the same
    Assert.Equal("burden", pile.inventory[0].Itemstack!.Collectible.Code.Path);
  }

  #endregion
}
