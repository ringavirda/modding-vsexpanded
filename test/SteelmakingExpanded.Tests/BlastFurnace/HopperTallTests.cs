using ExpandedLib.Blocks.Structures;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.Items;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The tall hopper: a one-stack burden tank that drips its contents into the furnace shaft below,
/// continuously and with no toggle. It fills either burden family (the furnace it feeds gates
/// acceptance), caps at one stack, and takes every interaction from its top filler cell (routed to the
/// block entity), not its base. Covers the tank fill/cap, the continuous drip into a shaft pile, the
/// filler-routed deposit/withdraw, and persistence.
/// </summary>
public class HopperTallTests
{
  private static readonly BlockPos HopperPos = new(0, 16, 0);

  private static (
    TestWorld world,
    BlockEntityHopperTall be,
    Item burden
  ) NewHopper()
  {
    var world = new TestWorld();
    world.RegisterItem("iwex:burden");
    var burden = world.World.GetItem(new AssetLocation("iwex:burden"))!;

    // A real BlockHopperTall so the filler-routing tests can reach its IFillerInteractionTarget.
    var block = TestBlocks.Configure(
      new BlockHopperTall(),
      "iwex:hopper-tall",
      80
    );
    var be = new BlockEntityHopperTall { Pos = HopperPos, Block = block };
    world.Place(HopperPos, block, be);
    world.Attach(be);
    return (world, be, burden);
  }

  private static void Deposit(
    BlockEntityHopperTall be,
    Item burden,
    int units
  ) =>
    be.TryDeposit(
      new DummySlot(new ItemStack(burden, units)),
      wholeStack: true
    );

  /// <summary>A burning-free coal pile at <paramref name="pos"/> holding <paramref name="units"/> of
  /// <paramref name="content"/> (0 leaves the slot empty), the way the furnace shaft carries charge.</summary>
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

  private static IPlayer PlayerHolding(ItemSlot active, bool ctrl)
  {
    var player = Substitute.For<IPlayer>();
    var entity = Substitute.For<EntityPlayer>();
    entity.Controls.CtrlKey = ctrl; // Controls is a real field on the proxy
    player.Entity.Returns(entity);
    var inv = Substitute.For<IPlayerInventoryManager>();
    inv.ActiveHotbarSlot.Returns(active);
    player.InventoryManager.Returns(inv);
    return player;
  }

  #region Tank fill / cap

  [Fact]
  public void Deposits_burden_into_the_tank()
  {
    var (_, be, burden) = NewHopper();
    var slot = new DummySlot(new ItemStack(burden, 50));

    Assert.True(be.TryDeposit(slot, wholeStack: true));

    Assert.Equal(50, be.TankCount);
    Assert.True(slot.Empty);
  }

  [Fact]
  public void A_plain_deposit_takes_one_unit()
  {
    var (_, be, burden) = NewHopper();
    var slot = new DummySlot(new ItemStack(burden, 50));

    Assert.True(be.TryDeposit(slot, wholeStack: false));

    Assert.Equal(1, be.TankCount);
    Assert.Equal(49, slot.StackSize);
  }

  [Fact]
  public void Same_burden_stacks_into_the_tank()
  {
    var (_, be, burden) = NewHopper();

    Deposit(be, burden, 30);
    Deposit(be, burden, 20);

    Assert.Equal(50, be.TankCount);
  }

  [Fact]
  public void The_tank_fills_to_capacity_and_refuses_the_overflow()
  {
    var (_, be, burden) = NewHopper();
    int cap = IwexValues.HopperTallCapacity;

    Assert.True(be.TryDeposit(new DummySlot(new ItemStack(burden, 100)), true));
    Assert.Equal(100, be.TankCount);

    // Only the remaining room (cap - 100) fits; the rest stays in hand.
    var overflow = new DummySlot(new ItemStack(burden, 100));
    Assert.True(be.TryDeposit(overflow, true));
    Assert.Equal(cap, be.TankCount);
    Assert.Equal(100 - (cap - 100), overflow.StackSize);
    Assert.True(be.IsFull);

    // A full tank accepts nothing more.
    var more = new DummySlot(new ItemStack(burden, 10));
    Assert.False(be.TryDeposit(more, true));
    Assert.Equal(10, more.StackSize);
  }

  [Fact]
  public void A_different_grade_is_refused_once_the_tank_is_loaded()
  {
    var (_, be, burden) = NewHopper();

    var first = new ItemStack(burden, 20);
    Burden.Write(first, new BurdenMix(80f, 10f, 10f));
    Assert.True(be.TryDeposit(new DummySlot(first), true));

    // One stack cannot carry two grades, so a differently-stamped burden does not merge.
    var other = new ItemStack(burden, 15);
    Burden.Write(other, new BurdenMix(60f, 10f, 30f));
    var otherSlot = new DummySlot(other);
    Assert.False(be.TryDeposit(otherSlot, true));

    Assert.Equal(20, be.TankCount);
    Assert.Equal(15, otherSlot.StackSize);
  }

  #endregion

  #region Continuous drip

  [Fact]
  public void Drips_burden_into_the_shaft_pile_below()
  {
    var (world, be, burden) = NewHopper();
    Deposit(be, burden, 50);
    var pile = CoalPile(world, HopperPos.DownCopy(), burden, 4);

    ReflectionHelpers.Invoke(be, "OnServerTick", 1f);

    int per = IwexValues.HopperTallDropPerSecond;
    Assert.Equal(4 + per, pile.inventory[0].StackSize); // pile grew by one drip
    Assert.Equal(50 - per, be.TankCount); // tank fell by the same
  }

  [Fact]
  public void Continuous_dripping_drains_the_tank_over_several_ticks()
  {
    var (world, be, burden) = NewHopper();
    Deposit(be, burden, 20);
    var pile = CoalPile(world, HopperPos.DownCopy(), burden, 2);

    for (int i = 0; i < 3; i++)
      ReflectionHelpers.Invoke(be, "OnServerTick", 1f);

    // 20 units at 8/s empties over three ticks (8 + 8 + 4), all landing in the pile.
    Assert.Equal(0, be.TankCount);
    Assert.Equal(22, pile.inventory[0].StackSize);
  }

  [Fact]
  public void Drips_either_burden_family()
  {
    // The hopper is a dumb tank: remelt burden drips exactly like ore burden (the furnace, not the
    // hopper, is what refuses the wrong family).
    var world = new TestWorld();
    world.RegisterItem("iwex:remeltburden");
    var remelt = world.World.GetItem(new AssetLocation("iwex:remeltburden"))!;
    var block = TestBlocks.Configure(
      new BlockHopperTall(),
      "iwex:hopper-tall",
      80
    );
    var be = new BlockEntityHopperTall { Pos = HopperPos, Block = block };
    world.Place(HopperPos, block, be);
    world.Attach(be);

    Deposit(be, remelt, 20);
    var pile = CoalPile(world, HopperPos.DownCopy(), remelt, 3);

    ReflectionHelpers.Invoke(be, "OnServerTick", 1f);

    Assert.True(be.TankCount < 20);
    Assert.Equal(
      "remeltburden",
      pile.inventory[0].Itemstack!.Collectible.Code.Path
    );
  }

  [Fact]
  public void Holds_burden_when_the_shaft_pile_is_full()
  {
    var (world, be, burden) = NewHopper();
    Deposit(be, burden, 20);
    int cap = IwexValues.HopperTallPileCap;
    var pile = CoalPile(world, HopperPos.DownCopy(), burden, cap);

    ReflectionHelpers.Invoke(be, "OnServerTick", 1f);

    // Nowhere to put it (the one reachable pile is full, and it cannot grow into the hopper's own cell).
    Assert.Equal(20, be.TankCount);
    Assert.Equal(cap, pile.inventory[0].StackSize);
  }

  #endregion

  #region Interaction routed from the top filler cell

  private BlockStructureFiller PlaceFiller(
    TestWorld world,
    BlockPos principalPos
  )
  {
    var fillerPos = principalPos.UpCopy();
    var filler = TestBlocks.Configure(
      new BlockStructureFiller(),
      "exlib:structurefiller",
      81
    );
    var fillerBe = new BlockEntityStructureFiller
    {
      Pos = fillerPos,
      Principal = principalPos,
    };
    world.Place(fillerPos, filler, fillerBe);
    world.Attach(fillerBe);
    return filler;
  }

  [Fact]
  public void A_click_on_the_top_filler_cell_deposits_into_the_hopper()
  {
    var (world, be, burden) = NewHopper();
    world.World.Side.Returns(EnumAppSide.Server);
    BlockStructureFiller filler = PlaceFiller(world, be.Pos);

    var player = PlayerHolding(
      new DummySlot(new ItemStack(burden, 30)),
      ctrl: true
    );
    var sel = new BlockSelection
    {
      Position = be.Pos.UpCopy(),
      Face = BlockFacing.UP,
    };

    bool handled = filler.OnBlockInteractStart(world.World, player, sel);

    // The filler forwarded the click to the hopper base's block entity - not its own inert cell.
    Assert.True(handled);
    Assert.Equal(30, be.TankCount);
  }

  [Fact]
  public void An_empty_handed_click_on_the_filler_withdraws_from_the_hopper()
  {
    var (world, be, burden) = NewHopper();
    world.World.Side.Returns(EnumAppSide.Server);
    Deposit(be, burden, 40);
    BlockStructureFiller filler = PlaceFiller(world, be.Pos);

    var player = PlayerHolding(new DummySlot(), ctrl: false);
    player
      .InventoryManager.TryGiveItemstack(Arg.Any<ItemStack>())
      .Returns(true);
    var sel = new BlockSelection
    {
      Position = be.Pos.UpCopy(),
      Face = BlockFacing.UP,
    };

    filler.OnBlockInteractStart(world.World, player, sel);

    Assert.Equal(0, be.TankCount);
    player
      .InventoryManager.Received()
      .TryGiveItemstack(Arg.Is<ItemStack>(s => s.StackSize == 40));
  }

  #endregion

  #region Persistence

  [Fact]
  public void The_tank_round_trips_through_the_tree()
  {
    var (world, be, burden) = NewHopper();
    Deposit(be, burden, 77);

    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);

    var dst = new BlockEntityHopperTall { Pos = HopperPos, Block = be.Block };
    world.Attach(dst);
    dst.FromTreeAttributes(tree, world.World);

    Assert.Equal(77, dst.TankCount);
  }

  [Fact]
  public void An_empty_tank_serializes_as_empty()
  {
    var (world, be, _) = NewHopper();

    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);

    var dst = new BlockEntityHopperTall { Pos = HopperPos, Block = be.Block };
    world.Attach(dst);
    dst.FromTreeAttributes(tree, world.World);

    Assert.Equal(0, dst.TankCount);
  }

  #endregion
}
