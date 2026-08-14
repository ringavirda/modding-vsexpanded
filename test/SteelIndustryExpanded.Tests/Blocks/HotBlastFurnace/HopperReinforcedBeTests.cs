using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using IronIndustryExpanded.Tests;
using NSubstitute;
using SteelIndustryExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelIndustryExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The reinforced hopper is the hot blast furnace's only charging cell: the tank the player tops up,
/// which the bell hopper below drains into the shaft. Pins the tank fill/cap/grade rules, the withdraw,
/// the <see cref="BlockEntityHopperReinforced.DrawBurden"/> feed the bell pulls through, the bell-drop
/// toggle, persistence, and that the hopper takes the furnace's fuel as well as its burden.
/// <para>
/// Every case stands a real furnace because the tank keeps no list of what is chargeable - it asks the
/// anchored core (<c>IsChargeItem</c>). Over nothing it accepts nothing, and the assertions would pass
/// vacuously.
/// </para>
/// </summary>
public class HopperReinforcedBeTests {
  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>
  /// A standing hot blast furnace with a real reinforced hopper and bell hopper in the two cells its
  /// drawing puts them in (local <c>(0,8,0)</c> and <c>(0,7,0)</c>). Both cells and the codes they want
  /// come off the raised structure rather than being hand-written, so a redrawn layout moves the fixture
  /// with it. A structure that never completes resolves no anchor and the delegation goes with it.
  /// </summary>
  private static (
    BlockEntityBlastFurnaceHot core,
    StructureRig rig,
    BlockEntityHopperReinforced hopper,
    BlockEntityHopperBell bell
  ) Standing() {
    var core = new BlockEntityBlastFurnaceHot();
    StructureRig rig = FurnaceLayoutRig.Stand(
      core,
      BlockBlastFurnaceCoreHot.Definitions("siex").Single(),
      Anchor,
      "siex:blastfurnacecore",
      "north"
    );

    rig.World.RegisterItem("iiex:burden");
    rig.World.RegisterItem(CokeCode);
    rig.World.RegisterItem(CharcoalCode);
    rig.World.RegisterItem(FluxCode);

    // The charge pile and its entity class, so the core's SyncChargeBlocks materialises real windows onto
    // the columns the bell fills.
    rig.World.RegisterBlockEntityFactory(
      "iiex.BlockEntityChargePile",
      () => new BlockEntityChargePile()
    );
    Block pile = TestBlocks.Configure(
      new Block(),
      BlockChargePile.PileCode.ToShortString(),
      950,
      ("type", "chargepile")
    );
    pile.EntityClass = "iiex.BlockEntityChargePile";
    rig.World.Register(pile);

    var (bellPos, bellWanted) = rig.Cells.Single(c =>
      c.Wanted.Contains("hopperbell", StringComparison.Ordinal)
    );
    var bell = new BlockEntityHopperBell {
      Pos = bellPos.Copy(),
      Block = TestBlocks.Configure(new BlockHopperBell(), bellWanted, 90),
    };
    rig.World.Place(bellPos, bell.Block, bell);

    var (hopperPos, hopperWanted) = rig.Cells.Single(c =>
      c.Wanted.Contains("hopperreinforced", StringComparison.Ordinal)
    );
    var hopper = new BlockEntityHopperReinforced {
      Pos = hopperPos.Copy(),
      Block = TestBlocks.Configure(
        new BlockHopperReinforced(),
        hopperWanted,
        91
      ),
    };
    rig.World.Place(hopperPos, hopper.Block, hopper);

    // Initialize, not Attach: the bell's drip cadence is a listener its own Initialize registers, so
    // AdvanceBlockEntityTime drives the whole charging chain (click -> tank -> magazine -> column) with
    // nothing invoked by reflection.
    rig.World.Initialize(hopper);
    rig.World.Initialize(bell);

    return (core, rig, hopper, bell);
  }

  /// <summary>The two fuels the shaft burns, as the codes a stack carries. Named once so the deposit
  /// cases and the band assertions cannot disagree about what fuel is.</summary>
  private const string CokeCode = "game:coke";
  private const string CharcoalCode = "game:charcoal";

  /// <summary>Flux, which no furnace in the line charges as a band. The "not charge" control.</summary>
  private const string FluxCode = "game:lime";

  private static Item Item(StructureRig rig, string code) =>
    rig.World.World.GetItem(new AssetLocation(code))!;

  private static Item BurdenItem(StructureRig rig) => Item(rig, "iiex:burden");

  private static void Deposit(
    BlockEntityHopperReinforced be,
    Item burden,
    int units,
    bool wholeStack = true
  ) => be.TryDeposit(new DummySlot(new ItemStack(burden, units)), wholeStack);

  /// <summary>Every column of the shaft, in the ascending-<c>(x, z)</c> order the selection rule breaks
  /// its ties on.</summary>
  private static List<ChargeColumn> Columns(BlockEntityBlastFurnaceHot core) =>
    [
      .. core
        .ShaftColumns.OrderBy(kv => kv.Key.X)
        .ThenBy(kv => kv.Key.Z)
        .Select(kv => kv.Value),
    ];

  private static IPlayer PlayerHolding(ItemSlot active, bool ctrl) {
    var player = Substitute.For<IPlayer>();
    var entity = Substitute.For<EntityPlayer>();
    entity.Controls.CtrlKey = ctrl; // Controls is a real field on the proxy
    player.Entity.Returns(entity);
    var inv = Substitute.For<IPlayerInventoryManager>();
    inv.ActiveHotbarSlot.Returns(active);
    player.InventoryManager.Returns(inv);
    return player;
  }

  /// <summary>
  /// One player gesture on the hopper, through the block's own interaction handler: Ctrl + right-click
  /// with <paramref name="units"/> of <paramref name="code"/> in hand. Returns the slot so a case can see
  /// what stayed in hand. The click goes through the block rather than straight to <c>TryDeposit</c>
  /// because the block carries its own chargeable check, which a direct call would skip.
  /// </summary>
  private static ItemSlot Click(
    StructureRig rig,
    BlockEntityHopperReinforced hopper,
    string code,
    int units,
    bool ctrl = true
  ) {
    var slot = new DummySlot(new ItemStack(Item(rig, code), units));
    rig.World.GetBlock(hopper.Pos)
      .OnBlockInteractStart(
        rig.World.World,
        PlayerHolding(slot, ctrl),
        new BlockSelection {
          Position = hopper.Pos.Copy(),
          Face = BlockFacing.UP,
        }
      );
    return slot;
  }

  #region Tank fill / cap / grade

  [Fact]
  public void Deposits_burden_into_the_tank() {
    var (_, rig, be, _) = Standing();
    var slot = new DummySlot(new ItemStack(BurdenItem(rig), 20));

    Assert.True(be.TryDeposit(slot, wholeStack: true));

    Assert.Equal(20, be.TankCount);
    Assert.True(slot.Empty);
  }

  [Fact]
  public void A_plain_deposit_takes_one_unit() {
    var (_, rig, be, _) = Standing();
    var slot = new DummySlot(new ItemStack(BurdenItem(rig), 20));

    Assert.True(be.TryDeposit(slot, wholeStack: false));

    Assert.Equal(1, be.TankCount);
    Assert.Equal(19, slot.StackSize);
  }

  [Fact]
  public void The_tank_fills_to_capacity_and_refuses_the_overflow() {
    var (_, rig, be, _) = Standing();
    int cap = SiexValues.HopperReinforcedCapacity;

    var slot = new DummySlot(new ItemStack(BurdenItem(rig), cap + 10));
    Assert.True(be.TryDeposit(slot, wholeStack: true));

    Assert.Equal(cap, be.TankCount);
    Assert.Equal(10, slot.StackSize); // the overflow stays in hand
    Assert.True(be.IsFull);
    Assert.False(be.TryDeposit(slot, wholeStack: true));
  }

  [Fact]
  public void A_different_grade_is_refused_once_the_tank_is_loaded() {
    var (_, rig, be, _) = Standing();

    var first = new ItemStack(BurdenItem(rig), 20);
    Burden.Write(first, new BurdenMix(80f, 10f, 10f));
    Assert.True(be.TryDeposit(new DummySlot(first), wholeStack: true));

    var other = new ItemStack(BurdenItem(rig), 15);
    Burden.Write(other, new BurdenMix(60f, 10f, 30f));
    var otherSlot = new DummySlot(other);
    Assert.False(be.TryDeposit(otherSlot, wholeStack: true));

    Assert.Equal(20, be.TankCount);
    Assert.Equal(15, otherSlot.StackSize);
  }

  [Fact]
  public void Withdraw_hands_back_the_whole_tank() {
    var (_, rig, be, _) = Standing();
    Deposit(be, BurdenItem(rig), 33);

    ItemStack? taken = be.TryWithdraw();

    Assert.Equal(33, taken?.StackSize);
    Assert.Equal(0, be.TankCount);
    Assert.Null(be.TryWithdraw());
  }

  #endregion

  #region Fuel - the charging route the hot furnace has and nothing else

  // The hopper is the hot blast furnace's only charging cell, so `Accepts` must admit fuel as well as
  // burden or no carbon reaches the raceway and the furnace cannot be lit. These cases go through the
  // player gesture end to end; pushing charge straight into the columns drives past the gate.

  [Fact]
  public void The_tank_takes_the_furnaces_own_FUEL_as_well_as_its_burden() {
    var (_, rig, be, _) = Standing();

    // Both fuels: both are shaft charge and they carry different carbon per unit, so a gate that admitted
    // only one of them still leaves a fuel the furnace cannot be charged with.
    Assert.True(be.Accepts(new ItemStack(Item(rig, CokeCode), 4)));
    Assert.True(be.Accepts(new ItemStack(Item(rig, CharcoalCode), 4)));
    Assert.True(be.Accepts(new ItemStack(BurdenItem(rig), 4)));
  }

  [Fact]
  public void A_tank_holding_coke_refuses_charcoal_into_the_same_stack() {
    // A band is stored as one material code and two fuels are not the same carbon, so a tank that pooled
    // them would lay a band under a code true of neither half.
    var (_, rig, be, _) = Standing();
    Deposit(be, Item(rig, CokeCode), 10);

    var charcoal = new DummySlot(new ItemStack(Item(rig, CharcoalCode), 10));
    Assert.False(be.TryDeposit(charcoal, wholeStack: true));

    Assert.Equal(10, be.TankCount);
    Assert.Equal(10, charcoal.StackSize);
  }

  [Fact]
  public void The_hopper_has_no_opinion_of_its_own_about_what_is_chargeable() {
    // The delegation as behaviour: a tank with no machine under it declares nothing chargeable, which is
    // what makes the two cases above statements about the furnace rather than about a list this block
    // keeps.
    var world = new TestWorld();
    Item coke = world.RegisterItem(CokeCode);
    Item burden = world.RegisterItem("iiex:burden");
    var orphan = new BlockEntityHopperReinforced {
      Pos = Anchor,
      Block = TestBlocks.Configure(new Block(), "siex:hopperreinforced", 91),
    };
    world.Place(Anchor, orphan.Block, orphan);
    world.Attach(orphan);

    Assert.False(orphan.Accepts(new ItemStack(coke, 4)));
    Assert.False(orphan.Accepts(new ItemStack(burden, 4)));
  }

  [Fact]
  public void A_coke_load_reaches_the_shaft_as_a_FUEL_band() {
    var (core, rig, hopper, _) = Standing();

    Click(rig, hopper, CokeCode, 20);
    Assert.Equal(20, hopper.TankCount); // the click cleared the block's own chargeable gate

    rig.World.AdvanceBlockEntityTime(1000); // one bell drop, on the bell's own registered cadence

    // `"coke"`, not `"game:coke"`: a segment stores `Code.ToShortString()`, and vanilla's short form drops
    // the implicit `game:` domain. Code-string gates read it back through an AssetLocation, whose
    // domainless constructor puts `game:` back, so both spellings are the same material.
    Assert.Equal(SiexValues.HopperDropAmount, Columns(core)[0].TotalUnits);
    Assert.Equal("coke", Columns(core)[0].Segments[0].Material);
  }

  [Fact]
  public void A_hot_blast_furnace_can_be_FUELLED_and_LIT_through_its_own_hopper() {
    // End to end: build the furnace, load the hopper through the player gesture, and let the registered
    // ticks run. Nothing is pushed into a column and nothing is invoked by reflection.
    var (core, rig, hopper, _) = Standing();
    Assert.Equal(FurnaceState.Idle, core.State); // premise: a built furnace starts dark

    // A raceway course: one bell drop per column, laid lowest-first, which is what the positional ignition
    // gate wants - carbon in front of every tuyere, not N units somewhere in the shaft.
    int course = core.ShaftColumns.Count * SiexValues.HopperDropAmount;
    Click(rig, hopper, CokeCode, course);
    Assert.Equal(course, hopper.TankCount);

    bool lit = false;
    for (int i = 0; i < 30 && !lit; i++) {
      rig.World.AdvanceBlockEntityTime(1000);
      lit = core.State != FurnaceState.Idle;
    }

    Assert.True(
      lit,
      $"the furnace should light off the fuel its own hopper laid; it was {core.State} with "
        + $"{core.ShaftChargeUnits} u in the shaft and {hopper.TankCount} u still in the tank"
    );
  }

  [Fact]
  public void Something_the_furnace_does_not_charge_still_toggles_the_bell_on_ctrl() {
    // Ctrl + right-click holding anything the furnace does not charge is the bell's stop switch, and the
    // deposit branch must not eat it.
    //
    // Flux, not remelt burden: `IsChargeItem` is family-blind (the tank is dumb and the furnace refuses
    // the wrong family later, at the melt), so remelt burden would load here correctly. Lime is charge to
    // no furnace in the line.
    var (_, rig, hopper, bell) = Standing();
    Assert.True(bell.IsDropping);

    Click(rig, hopper, FluxCode, 8);

    Assert.Equal(0, hopper.TankCount);
    Assert.False(bell.IsDropping); // the gesture fell through to the toggle
  }

  #endregion

  #region Bell feed / toggle

  [Fact]
  public void DrawBurden_pulls_up_to_the_requested_units_then_empties() {
    var (_, rig, be, _) = Standing();
    Deposit(be, BurdenItem(rig), 40);

    Assert.Equal(10, be.DrawBurden(10)?.StackSize);
    Assert.Equal(30, be.TankCount);

    // Asking for more than remains clamps to what is there, and drains the tank.
    Assert.Equal(30, be.DrawBurden(100)?.StackSize);
    Assert.Equal(0, be.TankCount);
    Assert.Null(be.DrawBurden(5));
  }

  [Fact]
  public void Ctrl_toggle_flips_the_bell_hoppers_dropping() {
    var (_, _, be, bell) = Standing();
    Assert.True(bell.IsDropping); // on by default

    be.ToggleBellDropping();
    Assert.False(bell.IsDropping);

    be.ToggleBellDropping();
    Assert.True(bell.IsDropping);
  }

  #endregion

  #region Persistence

  [Fact]
  public void The_tank_round_trips_through_the_tree() {
    var (_, rig, be, _) = Standing();
    Deposit(be, BurdenItem(rig), 42);

    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);

    var dst = new BlockEntityHopperReinforced {
      Pos = be.Pos.Copy(),
      Block = be.Block,
    };
    rig.World.Attach(dst);
    dst.FromTreeAttributes(tree, rig.World.World);

    Assert.Equal(42, dst.TankCount);
  }

  #endregion
}
