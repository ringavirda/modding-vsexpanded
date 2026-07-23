using ExpandedLib.Testing;
using IronworkingExpanded.Items;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The reinforced hopper is now a plain burden tank (the ore mixer makes the burden; the hopper no longer
/// mixes, and has no window). This pins the tank fill/cap/grade rules, the withdraw, the
/// <see cref="BlockEntityHopperReinforced.DrawBurden"/> feed the bell hopper pulls through, the bell-drop
/// toggle, and persistence.
/// </summary>
public class HopperReinforcedBeTests
{
  private static readonly BlockPos HopperPos = new(0, 16, 0);

  private static (
    TestWorld world,
    BlockEntityHopperReinforced be,
    Item burden
  ) NewHopper()
  {
    var world = new TestWorld();
    var burden = world.RegisterItem("iwex:burden");
    var be = new BlockEntityHopperReinforced
    {
      Pos = HopperPos,
      Block = TestBlocks.Configure(new Block(), "iwex:hopperreinforced", 91),
    };
    world.Place(HopperPos, be.Block, be);
    world.Attach(be);
    return (world, be, burden);
  }

  private static void Deposit(
    BlockEntityHopperReinforced be,
    Item burden,
    int units,
    bool wholeStack = true
  ) => be.TryDeposit(new DummySlot(new ItemStack(burden, units)), wholeStack);

  #region Tank fill / cap / grade

  [Fact]
  public void Deposits_burden_into_the_tank()
  {
    var (_, be, burden) = NewHopper();
    var slot = new DummySlot(new ItemStack(burden, 20));

    Assert.True(be.TryDeposit(slot, wholeStack: true));

    Assert.Equal(20, be.TankCount);
    Assert.True(slot.Empty);
  }

  [Fact]
  public void A_plain_deposit_takes_one_unit()
  {
    var (_, be, burden) = NewHopper();
    var slot = new DummySlot(new ItemStack(burden, 20));

    Assert.True(be.TryDeposit(slot, wholeStack: false));

    Assert.Equal(1, be.TankCount);
    Assert.Equal(19, slot.StackSize);
  }

  [Fact]
  public void The_tank_fills_to_capacity_and_refuses_the_overflow()
  {
    var (_, be, burden) = NewHopper();
    int cap = SmexValues.HopperReinforcedCapacity;

    var slot = new DummySlot(new ItemStack(burden, cap + 10));
    Assert.True(be.TryDeposit(slot, wholeStack: true));

    Assert.Equal(cap, be.TankCount);
    Assert.Equal(10, slot.StackSize); // the overflow stays in hand
    Assert.True(be.IsFull);
    Assert.False(be.TryDeposit(slot, wholeStack: true));
  }

  [Fact]
  public void A_different_grade_is_refused_once_the_tank_is_loaded()
  {
    var (_, be, burden) = NewHopper();

    var first = new ItemStack(burden, 20);
    Burden.Write(first, new BurdenMix(80f, 10f, 10f));
    Assert.True(be.TryDeposit(new DummySlot(first), wholeStack: true));

    var other = new ItemStack(burden, 15);
    Burden.Write(other, new BurdenMix(60f, 10f, 30f));
    var otherSlot = new DummySlot(other);
    Assert.False(be.TryDeposit(otherSlot, wholeStack: true));

    Assert.Equal(20, be.TankCount);
    Assert.Equal(15, otherSlot.StackSize);
  }

  [Fact]
  public void Withdraw_hands_back_the_whole_tank()
  {
    var (_, be, burden) = NewHopper();
    Deposit(be, burden, 33);

    ItemStack? taken = be.TryWithdraw();

    Assert.Equal(33, taken?.StackSize);
    Assert.Equal(0, be.TankCount);
    Assert.Null(be.TryWithdraw());
  }

  #endregion

  #region Bell feed / toggle

  [Fact]
  public void DrawBurden_pulls_up_to_the_requested_units_then_empties()
  {
    var (_, be, burden) = NewHopper();
    Deposit(be, burden, 40);

    Assert.Equal(10, be.DrawBurden(10)?.StackSize);
    Assert.Equal(30, be.TankCount);

    // Asking for more than remains clamps to what is there, and drains the tank.
    Assert.Equal(30, be.DrawBurden(100)?.StackSize);
    Assert.Equal(0, be.TankCount);
    Assert.Null(be.DrawBurden(5));
  }

  [Fact]
  public void Ctrl_toggle_flips_the_bell_hoppers_dropping()
  {
    var (world, be, _) = NewHopper();
    var bell = new BlockEntityHopperBell
    {
      Pos = HopperPos.DownCopy(),
      Block = TestBlocks.Configure(new Block(), "iwex:hopperbell", 90),
    };
    world.Place(bell.Pos, bell.Block, bell);
    world.Attach(bell);
    Assert.True(bell.IsDropping); // on by default

    be.ToggleBellDropping();
    Assert.False(bell.IsDropping);

    be.ToggleBellDropping();
    Assert.True(bell.IsDropping);
  }

  #endregion

  #region Persistence

  [Fact]
  public void The_tank_round_trips_through_the_tree()
  {
    var (world, be, burden) = NewHopper();
    Deposit(be, burden, 42);

    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);

    var dst = new BlockEntityHopperReinforced { Pos = HopperPos, Block = be.Block };
    world.Attach(dst);
    dst.FromTreeAttributes(tree, world.World);

    Assert.Equal(42, dst.TankCount);
  }

  #endregion
}
