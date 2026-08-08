using System.Collections.Generic;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.OreProcessing.BlockEntities;
using IronworkingExpanded.BlockStructures.OreProcessing.Blocks;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The burdenmaker's two hoppers, its shared basin and the one gate between them.
/// <para>
/// <b>Crate semantics</b>: materials go in and come out freely, with no batch state to get stuck in. The
/// ore mixer's <c>drainfirst</c> / <c>nothingmixed</c> / <c>wrongfamily</c> refusals are all gone; what is
/// left to assert is that each hopper takes only its own material, that the gate makes one honest batch, and
/// that <b>everything comes back on break</b>.
/// </para>
/// </summary>
public class BurdenmakerTests
{
  private static (
    TestWorld world,
    BlockEntityBurdenmaker be,
    Item ore,
    Item lime
  ) NewMachine()
  {
    var world = new TestWorld();
    world.RegisterItem("iwex:burden");
    world.RegisterItem("game:crushed-iron");
    world.RegisterItem("game:lime");
    world.RegisterItem("game:clay-fire"); // an unrelated item, for the refusal cases

    // The real block type, not `new Block()`. A stub carries no variants, so `Variant["side"]` is null
    // and the animator's cache key throws - and more importantly a stub block is how a fixture silently
    // stops exercising production code (see the charge-pile and heat-balance scars).
    var block = TestBlocks.Configure(
      new BlockBurdenmaker(),
      "iwex:burdenmaker-red-n",
      140,
      ("brick", "red"),
      ("side", "n")
    );
    var pos = new BlockPos(0, 0, 0);
    var be = new BlockEntityBurdenmaker { Pos = pos, Block = block };
    world.Place(pos, block, be);
    world.Initialize(be); // runs Initialize → the inventory's LateInitialize (Api wiring)

    return (
      world,
      be,
      world.World.GetItem(new AssetLocation("game:crushed-iron"))!,
      world.World.GetItem(new AssetLocation("game:lime"))!
    );
  }

  private static DummySlot Slot(Item item, int size) =>
    new(new ItemStack(item, size));

  #region Each hopper takes only its own material

  [Fact]
  public void The_wide_hopper_takes_ore_and_refuses_flux()
  {
    var (_, be, ore, lime) = NewMachine();

    Assert.True(be.TryLoadOre(Slot(ore, 64), wholeStack: true));
    Assert.Equal(64, be.OreUnits);

    Assert.False(be.TryLoadOre(Slot(lime, 64), wholeStack: true));
    Assert.Equal(64, be.OreUnits);
    Assert.Equal(0, be.FluxUnits);
  }

  [Fact]
  public void The_narrow_hopper_takes_flux_and_refuses_ore()
  {
    var (_, be, ore, lime) = NewMachine();

    Assert.True(be.TryLoadFlux(Slot(lime, 32), wholeStack: true));
    Assert.Equal(32, be.FluxUnits);

    Assert.False(be.TryLoadFlux(Slot(ore, 32), wholeStack: true));
    Assert.Equal(32, be.FluxUnits);
    Assert.Equal(0, be.OreUnits);
  }

  [Fact]
  public void An_unrelated_item_goes_in_neither_hopper()
  {
    var (world, be, _, _) = NewMachine();
    Item clay = world.World.GetItem(new AssetLocation("game:clay-fire"))!;

    Assert.False(be.TryLoadOre(Slot(clay, 16), wholeStack: true));
    Assert.False(be.TryLoadFlux(Slot(clay, 16), wholeStack: true));
    Assert.Equal(0, be.OreUnits);
    Assert.Equal(0, be.FluxUnits);
  }

  [Fact]
  public void A_plain_deposit_takes_one_unit_and_ctrl_takes_the_stack()
  {
    var (_, be, ore, _) = NewMachine();

    DummySlot held = Slot(ore, 40);
    Assert.True(be.TryLoadOre(held));
    Assert.Equal(1, be.OreUnits);
    Assert.Equal(39, held.Itemstack!.StackSize);

    Assert.True(be.TryLoadOre(held, wholeStack: true));
    Assert.Equal(40, be.OreUnits);
    Assert.True(held.Empty);
  }

  [Fact]
  public void Taking_from_a_hopper_returns_the_material_not_burden()
  {
    var (_, be, ore, lime) = NewMachine();
    be.TryLoadOre(Slot(ore, 20), wholeStack: true);
    be.TryLoadFlux(Slot(lime, 10), wholeStack: true);

    ItemStack? backOre = be.TryTakeOre();
    ItemStack? backFlux = be.TryTakeFlux();

    Assert.Equal("crushed-iron", backOre?.Collectible.Code.Path);
    Assert.Equal("lime", backFlux?.Collectible.Code.Path);
    Assert.Equal(0, be.OreUnits);
    Assert.Equal(0, be.FluxUnits);
  }

  [Fact]
  public void A_full_hopper_takes_no_more()
  {
    // This case earns its keep by having already failed: with the slot ranges first written as 4/2/9,
    // a 64-stack ore filled the wide hopper at **256** units against a configured capacity of **512**, so
    // the config key promised twice what the machine could hold. The slot count must always leave the
    // unit cap binding first - see the constants on the block entity. If this goes red after a capacity
    // change, the slot ranges are what to move, not this number.
    var (_, be, ore, _) = NewMachine();
    int cap = IwexValues.BurdenmakerOreCapacity;

    for (int i = 0; i < 64 && be.OreUnits < cap; i++)
      be.TryLoadOre(Slot(ore, cap), wholeStack: true);

    Assert.Equal(cap, be.OreUnits);
    Assert.False(be.TryLoadOre(Slot(ore, 64), wholeStack: true));
    Assert.Equal(cap, be.OreUnits);
  }

  [Fact]
  public void Every_tank_can_actually_reach_its_configured_capacity()
  {
    // The general form of the case above, for the other two tanks - the flux hopper and the basin have the
    // same failure mode and no scenario would otherwise fill them to the brim.
    var (_, be, ore, lime) = NewMachine();

    for (int i = 0; i < 64 && be.FluxUnits < IwexValues.BurdenmakerFluxCapacity; i++)
      be.TryLoadFlux(Slot(lime, IwexValues.BurdenmakerFluxCapacity), wholeStack: true);
    Assert.Equal(IwexValues.BurdenmakerFluxCapacity, be.FluxUnits);

    for (int i = 0; i < 64 && be.OreUnits < IwexValues.BurdenmakerOreCapacity; i++)
      be.TryLoadOre(Slot(ore, IwexValues.BurdenmakerOreCapacity), wholeStack: true);

    // Gate the lot through: ore + flux must fit the basin, or a legal full load would be destroyed.
    Assert.True(be.ToggleGate(out _));
    Assert.Equal(
      IwexValues.BurdenmakerOreCapacity + IwexValues.BurdenmakerFluxCapacity,
      be.BurdenUnits
    );
    Assert.True(be.BurdenUnits <= IwexValues.BurdenmakerBunkerCapacity);
  }

  #endregion

  #region The gate

  [Fact]
  public void Opening_the_gate_empties_both_hoppers_into_one_stamped_batch()
  {
    var (_, be, ore, lime) = NewMachine();
    be.TryLoadOre(Slot(ore, 90), wholeStack: true);
    be.TryLoadFlux(Slot(lime, 10), wholeStack: true);

    Assert.True(be.ToggleGate(out string? error));
    Assert.Null(error);

    Assert.Equal(0, be.OreUnits);
    Assert.Equal(0, be.FluxUnits);
    Assert.Equal(100, be.BurdenUnits);
    Assert.True(be.GateOpen);

    BurdenMix mix = Burden.Read(be.TryWithdrawBurden());
    Assert.Equal(0.90f, mix.Iron, 3);
    Assert.Equal(0.10f, mix.Flux, 3);
    // Coke left the burden when charging became layered. A burden carrying a fuel fraction would be a
    // second, disagreeing answer to "how much carbon is at the raceway".
    Assert.Equal(0f, mix.Fuel, 5);
  }

  [Fact]
  public void The_gate_refuses_when_nothing_is_loaded()
  {
    var (_, be, _, _) = NewMachine();

    Assert.False(be.ToggleGate(out string? error));
    Assert.Equal("iwex-burdenmaker-nothingloaded", error);
    Assert.False(be.GateOpen);
  }

  [Fact]
  public void The_gate_refuses_while_the_basin_still_holds_a_batch()
  {
    var (_, be, ore, lime) = NewMachine();
    be.TryLoadOre(Slot(ore, 50), wholeStack: true);
    be.TryLoadFlux(Slot(lime, 50), wholeStack: true);
    be.ToggleGate(out _);
    be.ToggleGate(out _); // shut it again; the batch stays in the basin

    be.TryLoadOre(Slot(ore, 30), wholeStack: true);

    Assert.False(be.ToggleGate(out string? error));
    Assert.Equal("iwex-burdenmaker-emptybunker", error);
    // The refused batch is still in the hoppers - a refusal must not consume anything.
    Assert.Equal(30, be.OreUnits);
  }

  [Fact]
  public void A_second_batch_carries_only_its_own_stamp()
  {
    // The dirty-precondition rule. The first batch is 50:50; the second is 90:10. If the basin were
    // allowed to pool, the second read would come back as an average of the two and the player would be
    // charging a grade they never made.
    var (_, be, ore, lime) = NewMachine();

    be.TryLoadOre(Slot(ore, 50), wholeStack: true);
    be.TryLoadFlux(Slot(lime, 50), wholeStack: true);
    be.ToggleGate(out _);
    BurdenMix first = Burden.Read(be.TryWithdrawBurden());
    while (be.BurdenUnits > 0)
      be.TryWithdrawBurden();
    be.ToggleGate(out _); // shut the lid

    be.TryLoadOre(Slot(ore, 90), wholeStack: true);
    be.TryLoadFlux(Slot(lime, 10), wholeStack: true);
    Assert.True(be.ToggleGate(out _));
    BurdenMix second = Burden.Read(be.TryWithdrawBurden());

    Assert.Equal(0.50f, first.Iron, 3);
    Assert.Equal(0.90f, second.Iron, 3);
    Assert.Equal(0.10f, second.Flux, 3);
  }

  #endregion

  #region Drops — the whole point of the block

  [Fact]
  public void Breaking_it_returns_the_ore_the_flux_AND_the_burden()
  {
    // The ore mixer returned nothing and could silently destroy up to 512 units of raw charge. Under R2
    // that was always wrong, and with no batch state there is not even an excuse for it. Asserted, not
    // assumed - and asserted through the inventory the container base spills, so it cannot pass by virtue
    // of a custom GetDrops that a later edit removes.
    var (_, be, ore, lime) = NewMachine();

    be.TryLoadOre(Slot(ore, 60), wholeStack: true);
    be.TryLoadFlux(Slot(lime, 20), wholeStack: true);
    be.ToggleGate(out _);
    be.ToggleGate(out _);
    be.TryLoadOre(Slot(ore, 45), wholeStack: true);
    be.TryLoadFlux(Slot(lime, 15), wholeStack: true);

    // The premise: all three tanks really are loaded, or the assertion below is vacuous.
    Assert.Equal(45, be.OreUnits);
    Assert.Equal(15, be.FluxUnits);
    Assert.Equal(80, be.BurdenUnits);

    var found = new Dictionary<string, int>();
    foreach (ItemSlot slot in be.Inventory)
      if (!slot.Empty)
      {
        string path = slot.Itemstack.Collectible.Code.Path;
        found[path] = found.GetValueOrDefault(path) + slot.Itemstack.StackSize;
      }

    Assert.Equal(45, found["crushed-iron"]);
    Assert.Equal(15, found["lime"]);
    Assert.Equal(80, found["burden"]);
  }

  #endregion
}
