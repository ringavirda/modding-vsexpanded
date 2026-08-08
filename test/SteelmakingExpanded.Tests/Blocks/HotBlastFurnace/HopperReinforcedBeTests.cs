using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.Items;
using IronworkingExpanded.Tests;
using NSubstitute;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The reinforced hopper is the hot blast furnace's <b>only</b> charging cell: the tank the player tops up,
/// which the bell hopper below drains into the shaft. This pins the tank fill/cap/grade rules, the withdraw,
/// the <see cref="BlockEntityHopperReinforced.DrawBurden"/> feed the bell pulls through, the bell-drop
/// toggle, persistence - and the thing whose absence let a shipped furnace go unlightable:
/// that the hopper takes the furnace's own <b>fuel</b> as well as its burden.
/// <para>
/// <b>The furnace is half the subject, not scenery.</b> The tank has no opinion of its own about what is
/// chargeable any more - it asks the anchored core (<c>IsChargeItem</c>), exactly as the tall hopper does.
/// A bare hopper over nothing, which is what this fixture used to be, can no longer express any of that: it
/// would accept nothing, and every assertion here would pass vacuously against a machine-agnostic tank that
/// had quietly stopped being one.
/// </para>
/// </summary>
public class HopperReinforcedBeTests
{
  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>
  /// A standing hot blast furnace with a <b>real</b> reinforced hopper and bell hopper in the two cells its
  /// own drawing puts them in (local <c>(0,8,0)</c> and <c>(0,7,0)</c>).
  /// <para>
  /// Both cells and the codes they want come off the raised structure rather than being hand-written, so a
  /// redrawn layout moves the fixture with it instead of leaving a hopper in a cell that satisfies nothing -
  /// and a structure that never completes resolves no anchor, which would take the delegation with it.
  /// </para>
  /// </summary>
  private static (
    BlockEntityBlastFurnaceHot core,
    StructureRig rig,
    BlockEntityHopperReinforced hopper,
    BlockEntityHopperBell bell
  ) Standing()
  {
    var core = new BlockEntityBlastFurnaceHot();
    StructureRig rig = FurnaceLayoutRig.Stand(
      core,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      Anchor,
      "smex:blastfurnacecore",
      "north"
    );

    rig.World.RegisterItem("iwex:burden");
    rig.World.RegisterItem(CokeCode);
    rig.World.RegisterItem(CharcoalCode);
    rig.World.RegisterItem(FluxCode);

    // The charge pile + its entity class, so the core's own SyncChargeBlocks materialises real windows
    // onto the columns the bell fills.
    rig.World.RegisterBlockEntityFactory(
      "iwex.BlockEntityChargePile",
      () => new BlockEntityChargePile()
    );
    Block pile = TestBlocks.Configure(
      new Block(),
      BlockChargePile.PileCode.ToShortString(),
      950,
      ("type", "chargepile")
    );
    pile.EntityClass = "iwex.BlockEntityChargePile";
    rig.World.Register(pile);

    var (bellPos, bellWanted) = rig.Cells.Single(c =>
      c.Wanted.Contains("hopperbell", StringComparison.Ordinal)
    );
    var bell = new BlockEntityHopperBell
    {
      Pos = bellPos.Copy(),
      Block = TestBlocks.Configure(new BlockHopperBell(), bellWanted, 90),
    };
    rig.World.Place(bellPos, bell.Block, bell);

    var (hopperPos, hopperWanted) = rig.Cells.Single(c =>
      c.Wanted.Contains("hopperreinforced", StringComparison.Ordinal)
    );
    var hopper = new BlockEntityHopperReinforced
    {
      Pos = hopperPos.Copy(),
      Block = TestBlocks.Configure(
        new BlockHopperReinforced(),
        hopperWanted,
        91
      ),
    };
    rig.World.Place(hopperPos, hopper.Block, hopper);

    // Initialize, not Attach: the bell's drip cadence is a listener its own Initialize registers, so
    // AdvanceBlockEntityTime drives the shipped charging chain (player click -> tank -> magazine -> column)
    // with nothing invoked by reflection.
    rig.World.Initialize(hopper);
    rig.World.Initialize(bell);

    return (core, rig, hopper, bell);
  }

  /// <summary>The two fuels the shaft burns, as the codes a stack actually carries. Named once so the
  /// deposit cases and the band assertions cannot come to disagree about what fuel is.</summary>
  private const string CokeCode = "game:coke";
  private const string CharcoalCode = "game:charcoal";

  /// <summary>Flux - a material role the mixer reads and no furnace charges as a band. The "not charge"
  /// control.</summary>
  private const string FluxCode = "game:lime";

  private static Item Item(StructureRig rig, string code) =>
    rig.World.World.GetItem(new AssetLocation(code))!;

  private static Item BurdenItem(StructureRig rig) => Item(rig, "iwex:burden");

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

  /// <summary>
  /// One player gesture on the hopper, through the block's <b>own</b> interaction handler: Ctrl +
  /// right-click with <paramref name="units"/> of <paramref name="code"/> in hand. Returns the slot, so a
  /// case can see what stayed in hand.
  /// <para>
  /// The click goes through the block, not straight to <c>TryDeposit</c>. The block carried its own copy
  /// of "is this chargeable" and it was wrong in the same way the tank's was, so a case that called the
  /// block entity directly would have proved half a fix.
  /// </para>
  /// </summary>
  private static ItemSlot Click(
    StructureRig rig,
    BlockEntityHopperReinforced hopper,
    string code,
    int units,
    bool ctrl = true
  )
  {
    var slot = new DummySlot(new ItemStack(Item(rig, code), units));
    rig.World.GetBlock(hopper.Pos)
      .OnBlockInteractStart(
        rig.World.World,
        PlayerHolding(slot, ctrl),
        new BlockSelection { Position = hopper.Pos.Copy(), Face = BlockFacing.UP }
      );
    return slot;
  }

  #region Tank fill / cap / grade

  [Fact]
  public void Deposits_burden_into_the_tank()
  {
    var (_, rig, be, _) = Standing();
    var slot = new DummySlot(new ItemStack(BurdenItem(rig), 20));

    Assert.True(be.TryDeposit(slot, wholeStack: true));

    Assert.Equal(20, be.TankCount);
    Assert.True(slot.Empty);
  }

  [Fact]
  public void A_plain_deposit_takes_one_unit()
  {
    var (_, rig, be, _) = Standing();
    var slot = new DummySlot(new ItemStack(BurdenItem(rig), 20));

    Assert.True(be.TryDeposit(slot, wholeStack: false));

    Assert.Equal(1, be.TankCount);
    Assert.Equal(19, slot.StackSize);
  }

  [Fact]
  public void The_tank_fills_to_capacity_and_refuses_the_overflow()
  {
    var (_, rig, be, _) = Standing();
    int cap = SmexValues.HopperReinforcedCapacity;

    var slot = new DummySlot(new ItemStack(BurdenItem(rig), cap + 10));
    Assert.True(be.TryDeposit(slot, wholeStack: true));

    Assert.Equal(cap, be.TankCount);
    Assert.Equal(10, slot.StackSize); // the overflow stays in hand
    Assert.True(be.IsFull);
    Assert.False(be.TryDeposit(slot, wholeStack: true));
  }

  [Fact]
  public void A_different_grade_is_refused_once_the_tank_is_loaded()
  {
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
  public void Withdraw_hands_back_the_whole_tank()
  {
    var (_, rig, be, _) = Standing();
    Deposit(be, BurdenItem(rig), 33);

    ItemStack? taken = be.TryWithdraw();

    Assert.Equal(33, taken?.StackSize);
    Assert.Equal(0, be.TankCount);
    Assert.Null(be.TryWithdraw());
  }

  #endregion

  #region Fuel - the charging route the hot furnace has and nothing else

  // This region is the one whose absence let an unlightable furnace ship (found by the
  // charcoal survey). `Accepts` answered `Burden.IsAny`, which is true for prepared burden and nothing
  // else, so the reinforced hopper refused coke and charcoal alike - and it is the only charging cell the hot
  // blast furnace's drawing carries. No fuel could reach the shaft, no raceway could hold carbon, and the
  // machine could not be lit at all. Every smex case pushed charge straight into the columns and so drove
  // past the gate; the cure is a case that goes through the shipped gesture end to end.

  [Fact]
  public void The_tank_takes_the_furnaces_own_FUEL_as_well_as_its_burden()
  {
    var (_, rig, be, _) = Standing();

    // Both fuels, because both are shaft charge and they are priced differently (CarbonPerUnit 1.0 vs
    // 0.5). A gate that admitted only the one the fixtures happen to use would be the same bug narrowed.
    Assert.True(be.Accepts(new ItemStack(Item(rig, CokeCode), 4)));
    Assert.True(be.Accepts(new ItemStack(Item(rig, CharcoalCode), 4)));
    Assert.True(be.Accepts(new ItemStack(BurdenItem(rig), 4)));
  }

  [Fact]
  public void A_tank_holding_coke_refuses_charcoal_into_the_same_stack()
  {
    // The single-stack rule doing its real work now that fuel can enter here. A band is stored as one
    // material code, and two fuels are not the same carbon, so a tank that pooled them would have to lay a
    // band under a code that was true of neither half of it.
    var (_, rig, be, _) = Standing();
    Deposit(be, Item(rig, CokeCode), 10);

    var charcoal = new DummySlot(new ItemStack(Item(rig, CharcoalCode), 10));
    Assert.False(be.TryDeposit(charcoal, wholeStack: true));

    Assert.Equal(10, be.TankCount);
    Assert.Equal(10, charcoal.StackSize);
  }

  [Fact]
  public void The_hopper_has_no_opinion_of_its_own_about_what_is_chargeable()
  {
    // The delegation stated as a behaviour: a tank with no machine under it declares no charge. That is
    // the honest answer - there is no furnace to say what it burns - and it is also what makes the two
    // cases above statements about the furnace rather than about a list this block keeps.
    var world = new TestWorld();
    Item coke = world.RegisterItem(CokeCode);
    Item burden = world.RegisterItem("iwex:burden");
    var orphan = new BlockEntityHopperReinforced
    {
      Pos = Anchor,
      Block = TestBlocks.Configure(new Block(), "smex:hopperreinforced", 91),
    };
    world.Place(Anchor, orphan.Block, orphan);
    world.Attach(orphan);

    Assert.False(orphan.Accepts(new ItemStack(coke, 4)));
    Assert.False(orphan.Accepts(new ItemStack(burden, 4)));
  }

  [Fact]
  public void A_coke_load_reaches_the_shaft_as_a_FUEL_band()
  {
    var (core, rig, hopper, _) = Standing();

    Click(rig, hopper, CokeCode, 20);
    Assert.Equal(20, hopper.TankCount); // the click landed at all - the gate the block used to fail

    rig.World.AdvanceBlockEntityTime(1000); // one bell drop, on the bell's own registered cadence

    // `"coke"`, not `"game:coke"`: a segment stores `Code.ToShortString()`, and vanilla's short form
    // drops the implicit `game:` domain. Every code-string gate reads it back through an AssetLocation,
    // whose domainless constructor puts `game:` back - so the two spellings are the same material, and the
    // one that lands in a save is this one.
    Assert.Equal(
      SmexValues.HopperDropAmount,
      Columns(core)[0].TotalUnits
    );
    Assert.Equal("coke", Columns(core)[0].Segments[0].Material);
  }

  [Fact]
  public void A_hot_blast_furnace_can_be_FUELLED_and_LIT_through_its_own_hopper()
  {
    // The headline, and the case whose absence is the whole story: build the shipped furnace, load the
    // shipped hopper with the shipped gesture, and let the shipped ticks run. Nothing is pushed into a
    // column and nothing is invoked by reflection.
    var (core, rig, hopper, _) = Standing();
    Assert.Equal(FurnaceState.Idle, core.State); // premise: a built furnace starts dark

    // A raceway course: one bell drop per column, laid lowest-first, which is exactly what the positional
    // ignition gate wants - carbon in front of every tuyere, not N units somewhere in the shaft.
    int course = core.ShaftColumns.Count * SmexValues.HopperDropAmount;
    Click(rig, hopper, CokeCode, course);
    Assert.Equal(course, hopper.TankCount);

    bool lit = false;
    for (int i = 0; i < 30 && !lit; i++)
    {
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
  public void Something_the_furnace_does_not_charge_still_toggles_the_bell_on_ctrl()
  {
    // The fallback the deposit gate must not eat. Ctrl + right-click holding anything the furnace does
    // not charge is the bell's stop switch, and widening the deposit branch from "burden" to "whatever the
    // core charges" must not widen it to "anything at all".
    //
    // Flux, not remelt burden: `IsChargeItem` is deliberately family-blind (the tank is dumb and the
    // furnace refuses the wrong family later, at the melt), so remelt burden would load here quite
    // correctly. Lime is charge to no furnace in the line.
    var (_, rig, hopper, bell) = Standing();
    Assert.True(bell.IsDropping);

    Click(rig, hopper, FluxCode, 8);

    Assert.Equal(0, hopper.TankCount);
    Assert.False(bell.IsDropping); // the gesture fell through to the toggle
  }

  #endregion

  #region Bell feed / toggle

  [Fact]
  public void DrawBurden_pulls_up_to_the_requested_units_then_empties()
  {
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
  public void Ctrl_toggle_flips_the_bell_hoppers_dropping()
  {
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
  public void The_tank_round_trips_through_the_tree()
  {
    var (_, rig, be, _) = Standing();
    Deposit(be, BurdenItem(rig), 42);

    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);

    var dst = new BlockEntityHopperReinforced { Pos = be.Pos.Copy(), Block = be.Block };
    rig.World.Attach(dst);
    dst.FromTreeAttributes(tree, rig.World.World);

    Assert.Equal(42, dst.TankCount);
  }

  #endregion
}
