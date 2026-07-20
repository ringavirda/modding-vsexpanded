using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.OreProcessing.BlockEntities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The ore bunker's crate-style storage: it holds ONE feedstock at a time - either a single crushed-ore
/// type, or burden of a single grade. Burden of the same grade pools to a weighted-average composition;
/// a mismatched grade, the other category, or a second ore type is declined.
/// </summary>
public class OreBunkerTests
{
  private static (
    TestWorld world,
    BlockEntityOreBunker be,
    Item burden
  ) NewBunker()
  {
    var world = new TestWorld();
    world.RegisterItem("iwex:burden");
    var burden = world.World.GetItem(new AssetLocation("iwex:burden"))!;

    var block = TestBlocks.Configure(new Block(), "iwex:bunker-red-north", 70);
    var pos = new BlockPos(0, 0, 0);
    var be = new BlockEntityOreBunker { Pos = pos, Block = block };
    world.Place(pos, block, be);
    world.Initialize(be); // runs Initialize → the inventory's LateInitialize (Api wiring)
    return (world, be, burden);
  }

  #region Deposit / withdraw

  [Fact]
  public void Deposits_burden_and_reports_the_total()
  {
    var (_, be, burden) = NewBunker();
    var slot = new DummySlot(new ItemStack(burden, 100));

    Assert.True(be.TryDeposit(slot));
    Assert.Equal(100, be.TotalContents);
    Assert.True(slot.Empty);
  }

  [Fact]
  public void A_plain_right_click_deposits_a_single_unit()
  {
    var (_, be, burden) = NewBunker();
    var slot = new DummySlot(new ItemStack(burden, 100));

    Assert.True(be.TryDeposit(slot, wholeStack: false));
    Assert.Equal(1, be.TotalContents);
    Assert.Equal(99, slot.StackSize); // the rest stays in hand
  }

  [Fact]
  public void Rejects_items_that_are_neither_burden_nor_crushed_ore()
  {
    var (world, be, _) = NewBunker();
    var stick = world.RegisterItem("game:stick");
    var slot = new DummySlot(new ItemStack(stick, 5));

    Assert.False(be.TryDeposit(slot));
    Assert.Equal(0, be.TotalContents);
    Assert.Equal(5, slot.StackSize);
  }

  [Fact]
  public void Withdraw_returns_a_stack_and_lowers_the_total()
  {
    var (_, be, burden) = NewBunker();
    be.TryDeposit(new DummySlot(new ItemStack(burden, 64)));

    ItemStack? taken = be.TryWithdraw();

    Assert.NotNull(taken);
    Assert.Equal(64, taken!.StackSize);
    Assert.Equal(0, be.TotalContents);
    Assert.Null(be.TryWithdraw()); // now empty
  }

  #endregion

  #region Burden grades

  [Fact]
  public void Same_grade_burden_pools_to_a_weighted_average()
  {
    var (_, be, burden) = NewBunker();

    // Both classify as the same grade (low fuel fraction), but at different proportions and scales.
    var first = new ItemStack(burden, 50);
    Burden.Write(first, new BurdenMix(80f, 10f, 10f)); // fracs .80 / .10 / .10
    var second = new ItemStack(burden, 30);
    Burden.Write(second, new BurdenMix(60f, 20f, 10f)); // fracs ~.667 / .222 / .111

    Assert.True(be.TryDeposit(new DummySlot(first)));
    Assert.True(be.TryDeposit(new DummySlot(second)));

    // Same grade merges (one feedstock), so the total is their sum...
    Assert.Equal(80, be.TotalContents);

    // ...and the stored composition is the unit-weighted average of the two fractions.
    BurdenMix pooled = Burden.Read(be.Inventory[0].Itemstack);
    float expIron = (0.80f * 50 + (60f / 90f) * 30) / 80;
    float expFuel = (0.10f * 50 + (10f / 90f) * 30) / 80;
    Assert.Equal(expIron, pooled.IronFrac, 3);
    Assert.Equal(expFuel, pooled.FuelFrac, 3);
  }

  [Fact]
  public void Burden_of_a_different_grade_is_declined()
  {
    var (_, be, burden) = NewBunker();

    var lowCoke = new ItemStack(burden, 50);
    Burden.Write(lowCoke, new BurdenMix(80f, 10f, 10f)); // lowcoke (fuel .10)
    var highCoke = new ItemStack(burden, 30);
    Burden.Write(highCoke, new BurdenMix(60f, 10f, 30f)); // highcoke (fuel .30)

    Assert.True(be.TryDeposit(new DummySlot(lowCoke)));
    var second = new DummySlot(highCoke);
    Assert.False(be.TryDeposit(second)); // a different grade does not align

    Assert.Equal(50, be.TotalContents);
    Assert.Equal(30, second.StackSize); // stays in hand
  }

  #endregion

  #region Crushed ore

  [Fact]
  public void Accepts_and_stacks_crushed_ore()
  {
    var (world, be, _) = NewBunker();
    var ore = world.RegisterItem("game:crushed-iron");

    Assert.True(be.TryDeposit(new DummySlot(new ItemStack(ore, 40))));
    Assert.True(be.TryDeposit(new DummySlot(new ItemStack(ore, 10))));
    Assert.Equal(50, be.TotalContents);
  }

  [Fact]
  public void Ore_and_burden_do_not_share_a_bunker()
  {
    var (world, be, burden) = NewBunker();
    var ore = world.RegisterItem("game:crushed-iron");

    Assert.True(be.TryDeposit(new DummySlot(new ItemStack(ore, 40))));

    var rejectedBurden = new DummySlot(new ItemStack(burden, 20));
    Assert.False(be.TryDeposit(rejectedBurden)); // burden onto stored ore is declined
    Assert.Equal(20, rejectedBurden.StackSize);
    Assert.Equal(40, be.TotalContents);
  }

  [Fact]
  public void A_second_ore_type_is_declined()
  {
    var (world, be, _) = NewBunker();
    var iron = world.RegisterItem("game:crushed-iron");
    var copper = world.RegisterItem("game:crushed-copper");

    Assert.True(be.TryDeposit(new DummySlot(new ItemStack(iron, 40))));

    var other = new DummySlot(new ItemStack(copper, 20));
    Assert.False(be.TryDeposit(other));
    Assert.Equal(20, other.StackSize);
    Assert.Equal(40, be.TotalContents);
  }

  #endregion
}
