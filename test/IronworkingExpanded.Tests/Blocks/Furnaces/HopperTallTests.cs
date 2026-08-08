using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.Items;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The tall hopper: a one-stack burden tank that drips into the furnace shaft below, continuously and
/// with no toggle. Covers the tank fill and cap, the drip into the furnace's charge columns, the deposit
/// and withdraw routed from the top filler cell, the charging readout, and persistence.
/// </summary>
/// <remarks>
/// In <see cref="FurnaceConfigCollection"/> because the charge-verdict cases read a computed heat
/// balance: <c>HeatBalanceTests</c> moves <c>BfCombustionBaseTemp</c> and <c>BfCokeSensitivity</c>
/// mid-run, and a verdict computed inside that window flips for a reason unrelated to the charge.
/// </remarks>
[Collection(FurnaceConfigCollection.Name)]
public class HopperTallTests {
  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>
  /// A standing cold blast furnace with a real tall hopper in the cell its drawing puts one in. The core
  /// is half the subject: the hopper delegates what is chargeable (<c>IsChargeItem</c>) and where a load
  /// lands (<c>NextChargeColumn</c>) to it, so a hopper standing over nothing passes vacuously.
  /// </summary>
  private static (
    BlockEntityBlastFurnaceCold core,
    StructureRig rig,
    BlockEntityHopperTall hopper
  ) Standing(string side = "north") {
    var core = new BlockEntityBlastFurnaceCold();
    StructureRig rig = Stand(
      core,
      BlockBlastFurnaceCoreCold.Definitions("iwex").Single(),
      Anchor,
      "iwex:furnace-blastcore-tier1",
      side
    );

    rig.World.RegisterItem("iwex:burden");
    rig.World.RegisterItem("iwex:remeltburden");
    rig.World.RegisterItem("game:coke");
    // The shaft's second fuel. Registered because the course readout names the fuel off the resolved item,
    // and an unregistered code falls back to a generic word.
    rig.World.RegisterItem("game:charcoal");

    // The charge pile + its entity class, so the core's SyncChargeBlocks materialises real windows.
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

    // The hopper cell and the code it wants come off the raised structure: the layout marks the hopper as
    // an oriented part, so the demanded variant rotates with the furnace. A hard-coded "-e" leaves three
    // of the four facings unsatisfied, and a structure that never completes resolves no anchor.
    var (hopperPos, wanted) = rig.Cells.Single(c =>
      c.Wanted.StartsWith("iwex:hopper-tall", StringComparison.Ordinal)
    );
    var block = TestBlocks.Configure(
      new BlockHopperTall(),
      wanted,
      80,
      ("side", wanted[(wanted.LastIndexOf('-') + 1)..])
    );
    var hopper = new BlockEntityHopperTall {
      Pos = hopperPos.Copy(),
      Block = block,
    };
    rig.World.Place(hopperPos, block, hopper);
    rig.World.Attach(hopper);

    return (core, rig, hopper);
  }

  private static Item BurdenItem(StructureRig rig) =>
    rig.World.World.GetItem(new AssetLocation("iwex:burden"))!;

  private static void Deposit(
    BlockEntityHopperTall be,
    Item burden,
    int units
  ) =>
    be.TryDeposit(
      new DummySlot(new ItemStack(burden, units)),
      wholeStack: true
    );

  private static void Tick(BlockEntityHopperTall be) =>
    ReflectionHelpers.Invoke(be, "OnServerTick", 1f);

  /// <summary>Every column of the furnace's shaft, in the deterministic ascending-<c>(x, z)</c> order the
  /// selection rule breaks its ties on.</summary>
  private static List<ChargeColumn> Columns(BlockEntityBlastFurnaceCold core) =>
    [
      .. core
        .ShaftColumns.OrderBy(kv => kv.Key.X)
        .ThenBy(kv => kv.Key.Z)
        .Select(kv => kv.Value),
    ];

  private static int ShaftUnits(BlockEntityBlastFurnaceCold core) =>
    core.ShaftChargeUnits;

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

  #region Tank fill / cap

  [Fact]
  public void Deposits_burden_into_the_tank() {
    var (_, rig, be) = Standing();
    var slot = new DummySlot(new ItemStack(BurdenItem(rig), 50));

    Assert.True(be.TryDeposit(slot, wholeStack: true));

    Assert.Equal(50, be.TankCount);
    Assert.True(slot.Empty);
  }

  [Fact]
  public void A_plain_deposit_takes_one_unit() {
    var (_, rig, be) = Standing();
    var slot = new DummySlot(new ItemStack(BurdenItem(rig), 50));

    Assert.True(be.TryDeposit(slot, wholeStack: false));

    Assert.Equal(1, be.TankCount);
    Assert.Equal(49, slot.StackSize);
  }

  [Fact]
  public void Same_burden_stacks_into_the_tank() {
    var (_, rig, be) = Standing();

    Deposit(be, BurdenItem(rig), 30);
    Deposit(be, BurdenItem(rig), 20);

    Assert.Equal(50, be.TankCount);
  }

  [Fact]
  public void The_tank_fills_to_capacity_and_refuses_the_overflow() {
    var (_, rig, be) = Standing();
    Item burden = BurdenItem(rig);
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
  public void A_different_grade_is_refused_once_the_tank_is_loaded() {
    var (_, rig, be) = Standing();
    Item burden = BurdenItem(rig);

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
  public void Drips_burden_onto_the_shafts_lowest_column() {
    var (core, rig, be) = Standing();
    Deposit(be, BurdenItem(rig), 50);

    Tick(be);

    int per = IwexValues.HopperTallDropPerSecond;
    Assert.Equal(per, ShaftUnits(core)); // one drip landed in the shaft
    Assert.Equal(50 - per, be.TankCount); // and the tank fell by exactly the same

    // On an empty shaft every column is equally low, so the tie breaks on ascending (x, z) and the drip
    // lands on the first key. A rule that dropped straight down lands in the hopper's own column.
    List<ChargeColumn> columns = Columns(core);
    Assert.Equal(per, columns[0].TotalUnits);
    Assert.All(columns.Skip(1), c => Assert.Equal(0, c.TotalUnits));
  }

  [Fact]
  public void The_shaft_fills_in_level_courses_rather_than_one_tower() {
    // Selection is lowest-column-first because the raceway course has to be complete before a furnace will
    // light: filling one column to the roof leaves eight tuyeres blowing into empty air.
    var (core, rig, be) = Standing();
    int columnCount = core.ShaftColumns.Count;
    int per = IwexValues.HopperTallDropPerSecond;
    Deposit(be, BurdenItem(rig), per * columnCount);

    for (int i = 0; i < columnCount; i++)
      Tick(be);

    Assert.Equal(0, be.TankCount);
    Assert.All(Columns(core), c => Assert.Equal(per, c.TotalUnits));
  }

  [Fact]
  public void A_load_spread_over_many_ticks_stays_ONE_band_per_column() {
    // Temperature quantisation. ChargeColumn.Push merges only within 1 C, so a hopper dripping at raw
    // per-tick ambient starts a fresh segment on almost every drip. Asserted on the segment count, since
    // unit totals are identical either way.
    var (core, rig, be) = Standing();
    int columnCount = core.ShaftColumns.Count;
    Deposit(be, BurdenItem(rig), IwexValues.HopperTallCapacity);

    for (int i = 0; i < columnCount * 3; i++)
      Tick(be);

    Assert.All(
      Columns(core),
      c =>
        Assert.True(
          c.Segments.Count <= 1,
          $"a column took {c.Segments.Count} segments from one grade of one tank"
        )
    );
  }

  [Fact]
  public void Drips_what_the_furnace_below_declares_chargeable() {
    // The hopper lays whatever the machine below declares chargeable and holds no opinion of its own, so
    // one hopper block serves the blast furnace, the cupola, the heating furnace and the coke oven.
    var (core, rig, be) = Standing();
    Item burden = BurdenItem(rig);

    Deposit(be, burden, 20);
    Tick(be);

    Assert.True(be.TankCount < 20);
    Assert.Equal("iwex:burden", Columns(core)[0].Segments[0].Material);
  }

  [Fact]
  public void The_hopper_takes_the_furnaces_own_FUEL_as_well_as_its_burden() {
    // A shaft furnace charges coke as its own bands, so the tank takes the furnace's fuel as well as its
    // burden. A hopper answering a burden predicate of its own could never charge a round.
    var (core, rig, be) = Standing();
    Item coke = rig.World.World.GetItem(new AssetLocation("game:coke"))!;

    Assert.True(
      be.Accepts(new ItemStack(coke, 4)),
      "a shaft furnace charges coke"
    );
    Deposit(be, coke, 16);
    Tick(be);

    // `"coke"`, not `"game:coke"`: a segment stores `Code.ToShortString()`, which drops the implicit
    // `game:` domain. Code-string gates read it back through AssetLocation's domainless constructor, which
    // puts `game:` back, so the two forms name the same material.
    Assert.Equal("coke", Columns(core)[0].Segments[0].Material);
  }

  [Fact]
  public void Fuel_goes_onto_burden_but_never_onto_more_fuel() {
    // Band order, the one rule the selection applies beyond "lowest first": a round is coke then burden,
    // so coke is never laid straight onto coke.
    var (core, rig, be) = Standing();
    Item coke = rig.World.World.GetItem(new AssetLocation("game:coke"))!;
    int columnCount = core.ShaftColumns.Count;

    // A course of coke across every column - the bottom of a proper round.
    Deposit(be, coke, IwexValues.HopperTallDropPerSecond * columnCount);
    for (int i = 0; i < columnCount; i++)
      Tick(be);
    Assert.All(Columns(core), c => Assert.Equal("coke", c.TopMaterial));

    // Every column is topped with coke, so a second coke load has nowhere to go and the tank holds.
    be.TryWithdraw();
    Deposit(be, coke, 32);
    int before = ShaftUnits(core);
    Tick(be);
    Assert.Equal(before, ShaftUnits(core));
    Assert.Equal(32, be.TankCount);

    // Burden goes in at once, which makes the refusal above a band-order rule rather than a full shaft.
    be.TryWithdraw();
    Deposit(be, BurdenItem(rig), 32);
    Tick(be);
    Assert.True(ShaftUnits(core) > before);
  }

  [Fact]
  public void Charcoal_will_not_go_onto_a_column_topped_with_COKE() {
    // The band-order rule asks `IsFuelCode(top)`, never `top == material`, and only a second fuel tells
    // those apart: coke onto coke is refused by both, charcoal onto a coke-topped column by the role test
    // alone. An equality in `NextChargeColumn` lays a second fuel course onto the first without throwing.
    var (core, rig, be) = Standing();
    Item coke = rig.World.World.GetItem(new AssetLocation("game:coke"))!;
    Item charcoal = rig.World.World.GetItem(
      new AssetLocation("game:charcoal")
    )!;
    int columnCount = core.ShaftColumns.Count;

    // A complete course of coke, laid the way a player lays one - through the tank, not by hand.
    Deposit(be, coke, IwexValues.HopperTallDropPerSecond * columnCount);
    for (int i = 0; i < columnCount; i++)
      Tick(be);
    Assert.All(Columns(core), c => Assert.Equal("coke", c.TopMaterial));

    // The hopper still accepts charcoal: the refusal is about where it may go, not what it is. Asserted
    // first, so the case cannot pass on a shaft that never recognised charcoal as charge.
    be.TryWithdraw();
    Assert.True(
      be.Accepts(new ItemStack(charcoal, 4)),
      "a shaft furnace charges charcoal - it is a fuel-role grant like coke"
    );
    Deposit(be, charcoal, 32);

    int before = ShaftUnits(core);
    Tick(be);
    Assert.Equal(before, ShaftUnits(core));
    Assert.Equal(32, be.TankCount);
    // Per column, not on the total: a drip that landed charcoal in one column while another shed the
    // same volume would balance the total exactly.
    Assert.All(Columns(core), c => Assert.Equal("coke", c.TopMaterial));

    // The control: burden onto the same coke course goes in at once, so the hold above is the band order,
    // not a full shaft, a jammed tank or an unregistered item.
    be.TryWithdraw();
    Deposit(be, BurdenItem(rig), 32);
    Tick(be);
    Assert.True(ShaftUnits(core) > before);
  }

  [Fact]
  public void Holds_the_load_when_the_shaft_is_full() {
    var (core, rig, be) = Standing();

    // Fill every column to its own ceiling by hand - the hopper's own drip would take a thousand ticks.
    foreach (var (x, z) in core.ShaftColumns.Keys)
      core.ChargeColumnAt(x, z)!
        .Push("iwex:burden", core.ColumnCapacity(x, z), 20f, default);
    int full = ShaftUnits(core);

    Deposit(be, BurdenItem(rig), 20);
    Tick(be);

    // Nowhere to put it, so the tank keeps it. The shaft total is asserted too: a hopper pushing above a
    // column's own roof leaves the tank untouched as well, while inventing charge no block can draw.
    Assert.Equal(20, be.TankCount);
    Assert.Equal(full, ShaftUnits(core));
  }

  #endregion

  #region Rotated structure

  /// <summary>
  /// A hopper at any facing charges its own furnace's columns - the same set, in the same order. Asserted
  /// on the structure-local key set, since a world-frame assertion moves with the facing.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void A_hopper_charges_the_same_column_set_at_every_facing(string side) {
    var (core, rig, be) = Standing(side);
    int columnCount = core.ShaftColumns.Count;
    int per = IwexValues.HopperTallDropPerSecond;
    Deposit(be, BurdenItem(rig), per * columnCount);

    for (int i = 0; i < columnCount; i++)
      Tick(be);

    // Nine columns, every one of them charged, whichever way the furnace faces.
    Assert.Equal(9, columnCount);
    Assert.All(Columns(core), c => Assert.Equal(per, c.TotalUnits));

    // The same nine local keys at all four facings.
    Assert.Equal(
      "(-1,-1) (-1,0) (-1,1) (0,-1) (0,0) (0,1) (1,-1) (1,0) (1,1)",
      string.Join(
        " ",
        core.ShaftColumns.Keys.OrderBy(k => k.X)
          .ThenBy(k => k.Z)
          .Select(k => $"({k.X},{k.Z})")
      )
    );
  }

  #endregion

  #region The charging readout

  // The four lines the hopper surfaces are all derived: no field is persisted for them.
  //
  // The headless lang service echoes a key and drops its arguments, so "the line is present" and "its
  // numbers are right" are two different assertions. Course numbers come off `Core.TopCourse`; the verdict
  // is read off which key appears.

  private static string Hud(BlockEntityHopperTall be) {
    var sb = new System.Text.StringBuilder();
    be.GetBlockInfo(null!, sb);
    return sb.ToString();
  }

  /// <summary>Lays <paramref name="bands"/> bands of <paramref name="material"/> onto every column - a
  /// level course across the whole shaft, which is how a shaft fills.</summary>
  private static void Course(
    BlockEntityBlastFurnaceCold core,
    string material,
    int bands,
    BurdenMix mix
  ) {
    int perBand = core.ChargeUnitsPerBlock / ChargeColumn.BandsPerBlock;
    foreach (ChargeColumn column in Columns(core))
      column.Push(material, bands * perBand, 20f, mix);
  }

  [Fact]
  public void The_course_line_reports_the_top_course_of_the_column_being_filled() {
    var (core, _, be) = Standing();

    // A proper round, laid level across the shaft: 4 bands of coke, then 3 of burden on top.
    Course(core, "coke", 4, default);
    Course(core, "iwex:burden", 3, new BurdenMix(75f, 5f, 20f));

    var course = core.TopCourse;
    Assert.Equal(4, course.FuelBands);
    Assert.Equal(3, course.BurdenBands);
    Assert.Equal(ChargeColumn.BandsPerBlock, course.Bands);
    Assert.Contains("iwex:bf-info-course", Hud(be));

    // It follows the lowest column - the one the hopper fills next - so running one column far past the
    // rest must not move the readout.
    int perBand = core.ChargeUnitsPerBlock / ChargeColumn.BandsPerBlock;
    Columns(core)[^1].Push("coke", 20 * perBand, 20f, default);

    Assert.Equal(4, core.TopCourse.FuelBands);
    Assert.Equal(3, core.TopCourse.BurdenBands);
  }

  [Fact]
  public void The_course_line_names_the_fuel_laid_and_refuses_to_name_a_mixed_course() {
    // A charcoal band is half the carbon of a coke one (`BlockEntityFurnaceCore.CarbonPerUnit`), so the
    // course carries the fuel's own code rather than bucketing every fuel band as coke.
    var (burnt, _, burntBe) = Standing();
    Course(burnt, "charcoal", 4, default);
    Course(burnt, "iwex:burden", 3, new BurdenMix(75f, 5f, 20f));

    Assert.Equal(4, burnt.TopCourse.FuelBands);
    Assert.Equal("charcoal", burnt.TopCourse.FuelMaterial);
    Assert.Contains("iwex:bf-info-course", Hud(burntBe));

    // A course holding both fuels has no single name, so the material goes null and the line reads "mixed
    // fuel". FuelBands still counts every band.
    var (mixed, _, _) = Standing();
    Course(mixed, "coke", 2, default);
    Course(mixed, "charcoal", 2, default);

    Assert.Equal(4, mixed.TopCourse.FuelBands);
    Assert.Null(mixed.TopCourse.FuelMaterial);

    // The null reads as "mixed" only alongside a non-zero FuelBands: a burden-only course is null too, so
    // the material alone calls an empty course mixed.
    var (bare, _, _) = Standing();
    Course(bare, "iwex:burden", 3, new BurdenMix(75f, 5f, 20f));

    Assert.Equal(0, bare.TopCourse.FuelBands);
    Assert.Null(bare.TopCourse.FuelMaterial);
  }

  /// <summary>What the course line prints for its fuel - the string <c>iwex:bf-info-course</c>'s second
  /// argument is filled with.</summary>
  /// <remarks>
  /// Reached by reflection: the headless lang service echoes a key and drops its arguments, so the
  /// rendered line is identical for a coke round and a charcoal one.
  /// </remarks>
  private static string FuelName(BlockEntityBlastFurnaceCold core) =>
    (string)ReflectionHelpers.Invoke(core, "CourseFuelName", core.TopCourse)!;

  [Fact]
  public void TopCourse_tells_a_charcoal_course_from_a_coke_course() {
    // Two furnaces with the same round - four bands of fuel under three of burden - differing only in
    // which fuel. This line is the only place a player sees that difference before lighting.
    var grade = new BurdenMix(75f, 5f, 20f);

    var (coked, _, cokedBe) = Standing();
    Course(coked, "coke", 4, default);
    Course(coked, "iwex:burden", 3, grade);

    var (charred, _, charredBe) = Standing();
    Course(charred, "charcoal", 4, default);
    Course(charred, "iwex:burden", 3, grade);

    // A course reader that knows only coke counts the charcoal bands as burden and reports 0 of 4 fuel
    // with 7 of burden. Asserted as the pair and against the coke twin, so a reader falling back to
    // another constant cannot slip through.
    Assert.Equal(coked.TopCourse.FuelBands, charred.TopCourse.FuelBands);
    Assert.Equal(coked.TopCourse.BurdenBands, charred.TopCourse.BurdenBands);
    Assert.Equal(4, charred.TopCourse.FuelBands);
    Assert.Equal(3, charred.TopCourse.BurdenBands);

    // Counting the bands but naming them coke makes the round look worth twice what it is, so the two
    // courses must not answer with the same name.
    Assert.Equal("coke", coked.TopCourse.FuelMaterial);
    Assert.Equal("charcoal", charred.TopCourse.FuelMaterial);

    // The difference reaches the line itself, not just the model behind it. Both HUDs carry the course
    // line; the discriminator is the fuel name inside it, which the echoing lang service cannot show
    // (see FuelName).
    Assert.Contains("iwex:bf-info-course", Hud(cokedBe));
    Assert.Contains("iwex:bf-info-course", Hud(charredBe));
    Assert.NotEqual(FuelName(coked), FuelName(charred));
  }

  [Fact]
  public void The_verdict_flips_between_a_charge_that_melts_and_one_that_chills() {
    // The verdict is the projection "on full blast, at ambient" - the cold reading, so it never promises a
    // melt the furnace cannot deliver. It reads ComputeHeatBalance rather than re-deriving a coke
    // threshold of its own.
    //
    // The shaft is charged full because the heat balance's cold-charge loss scales with the charge
    // standing in it: a thin shaft of the same burden runs hotter and does read as melting. The grade is
    // varied by how much coke is laid, not by the burden's stamp - a stamped fuel fraction is not carbon,
    // so flipping the stamp charges two identical furnaces.
    var (core, _, be) = Standing();
    int bands =
      core.ColumnCapacity(0, 0)
      / (core.ChargeUnitsPerBlock / ChargeColumn.BandsPerBlock);
    var grade = new BurdenMix(75f, 5f, 20f);

    // The standard grade - 20 % of the round laid as coke - settles below iron's melt line on cold blast.
    Course(core, "coke", bands / 5, default);
    Course(core, "iwex:burden", bands - (bands / 5), grade);
    ReflectionHelpers.Invoke(core, "OnProductionTick", 1f);

    Assert.Contains("iwex:bf-info-chargechills", Hud(be));
    Assert.DoesNotContain("iwex:bf-info-chargemelts", Hud(be));

    // The control: the same furnace, the same number of bands, more of them coke. Without it the case
    // passes on a verdict hard-wired to "chills".
    var (rich, _, richBe) = Standing();
    Course(rich, "coke", bands * 2 / 5, default);
    Course(rich, "iwex:burden", bands - (bands * 2 / 5), grade);
    ReflectionHelpers.Invoke(rich, "OnProductionTick", 1f);

    Assert.Contains("iwex:bf-info-chargemelts", Hud(richBe));
    Assert.DoesNotContain("iwex:bf-info-chargechills", Hud(richBe));
  }

  [Fact]
  public void The_charge_verdict_tells_a_coke_round_from_a_charcoal_round_at_the_same_band_count() {
    // Two shafts loaded to the same height, with the same number of fuel bands under the same grade of
    // burden, differing only in the fuel: coke settles above iron's melt line and charcoal below it.
    //
    // Equal height is load-bearing: the heat balance's cold-charge loss scales with the charge standing in
    // the furnace, so a thinner charcoal twin reads cooler for a reason unrelated to its carbon.
    var grade = new BurdenMix(75f, 5f, 20f);

    var (coked, _, cokedBe) = Standing();
    int bands =
      coked.ColumnCapacity(0, 0)
      / (coked.ChargeUnitsPerBlock / ChargeColumn.BandsPerBlock);
    // A quarter of the round as fuel: the 20 % round chills on either fuel (coke lands ~0.238 carbon
    // against a ~0.239 melt threshold) and the 40 % round melts on either. Only between those does
    // swapping coke for charcoal move a charge across the line.
    int fuelBands = bands / 4;
    Course(coked, "coke", fuelBands, default);
    Course(coked, "iwex:burden", bands - fuelBands, grade);
    ReflectionHelpers.Invoke(coked, "OnProductionTick", 1f);

    var (charred, _, charredBe) = Standing();
    Course(charred, "charcoal", fuelBands, default);
    Course(charred, "iwex:burden", bands - fuelBands, grade);
    ReflectionHelpers.Invoke(charred, "OnProductionTick", 1f);

    // Stated first, because it is what makes the verdicts below mean anything: the two rounds are the same
    // volume, height and fuel-band count, so the only difference the model may use is CarbonPerUnit.
    Assert.Equal(coked.ShaftChargeUnits, charred.ShaftChargeUnits);
    Assert.Equal(coked.TopCourse.FuelBands, charred.TopCourse.FuelBands);

    Assert.Contains("iwex:bf-info-chargemelts", Hud(cokedBe));
    Assert.DoesNotContain("iwex:bf-info-chargechills", Hud(cokedBe));

    Assert.Contains("iwex:bf-info-chargechills", Hud(charredBe));
    Assert.DoesNotContain("iwex:bf-info-chargemelts", Hud(charredBe));
  }

  [Fact]
  public void The_readout_is_derived_so_it_survives_a_save_round_trip_unchanged() {
    var (core, rig, be) = Standing();
    int perBand = core.ChargeUnitsPerBlock / ChargeColumn.BandsPerBlock;
    Columns(core)[0].Push("coke", 5 * perBand, 20f, default);
    ReflectionHelpers.Invoke(core, "OnProductionTick", 1f);
    string before = Hud(be);

    // Round-trip the core, since the readout is entirely its state, and read the hopper's line off the copy.
    var tree = new TreeAttribute();
    core.ToTreeAttributes(tree);
    var reloaded = new BlockEntityBlastFurnaceCold {
      Pos = core.Pos.Copy(),
      Block = core.Block,
    };
    rig.World.Attach(reloaded);
    ReflectionHelpers.Invoke(reloaded, "UpdateStructureRotation");
    reloaded.FromTreeAttributes(tree, rig.World.World);

    Assert.Equal(core.TopCourse, reloaded.TopCourse);
    Assert.Equal(core.ShaftChargeUnits, reloaded.ShaftChargeUnits);
    Assert.Contains("iwex:bf-info-shaftfull", before);
  }

  [Fact]
  public void An_incomplete_structure_says_nothing_about_the_shaft() {
    var (core, _, be) = Standing();
    ReflectionHelpers.SetProperty(core, nameof(core.StructureComplete), false);

    string hud = Hud(be);

    // The tank line is the hopper's own and stays; every shaft line is the furnace's and goes silent.
    Assert.Contains("iwex:hoppertall-empty", hud);
    foreach (
      string key in new[]
      {
        "iwex:bf-info-course",
        "iwex:bf-info-chargemelts",
        "iwex:bf-info-chargechills",
        "iwex:bf-info-shaftfull",
      }
    )
      Assert.DoesNotContain(key, hud);
  }

  #endregion

  #region Interaction routed from the top filler cell

  private BlockStructureFiller PlaceFiller(
    TestWorld world,
    BlockPos principalPos
  ) {
    var fillerPos = principalPos.UpCopy();
    var filler = TestBlocks.Configure(
      new BlockStructureFiller(),
      "exlib:structurefiller",
      81
    );
    var fillerBe = new BlockEntityStructureFiller {
      Pos = fillerPos,
      Principal = principalPos,
    };
    world.Place(fillerPos, filler, fillerBe);
    world.Attach(fillerBe);
    return filler;
  }

  [Fact]
  public void A_click_on_the_top_filler_cell_deposits_into_the_hopper() {
    var (_, rig, be) = Standing();
    TestWorld world = rig.World;
    world.World.Side.Returns(EnumAppSide.Server);
    BlockStructureFiller filler = PlaceFiller(world, be.Pos);

    var player = PlayerHolding(
      new DummySlot(new ItemStack(BurdenItem(rig), 30)),
      ctrl: true
    );
    var sel = new BlockSelection {
      Position = be.Pos.UpCopy(),
      Face = BlockFacing.UP,
    };

    bool handled = filler.OnBlockInteractStart(world.World, player, sel);

    // The filler forwarded the click to the hopper base's block entity, not its own inert cell.
    Assert.True(handled);
    Assert.Equal(30, be.TankCount);
  }

  [Fact]
  public void An_empty_handed_click_on_the_filler_withdraws_from_the_hopper() {
    var (_, rig, be) = Standing();
    TestWorld world = rig.World;
    world.World.Side.Returns(EnumAppSide.Server);
    Deposit(be, BurdenItem(rig), 40);
    BlockStructureFiller filler = PlaceFiller(world, be.Pos);

    var player = PlayerHolding(new DummySlot(), ctrl: false);
    player
      .InventoryManager.TryGiveItemstack(Arg.Any<ItemStack>())
      .Returns(true);
    var sel = new BlockSelection {
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
  public void The_tank_round_trips_through_the_tree() {
    var (_, rig, be) = Standing();
    Deposit(be, BurdenItem(rig), 77);

    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);

    var dst = new BlockEntityHopperTall {
      Pos = be.Pos.Copy(),
      Block = be.Block,
    };
    rig.World.Attach(dst);
    dst.FromTreeAttributes(tree, rig.World.World);

    Assert.Equal(77, dst.TankCount);
  }

  [Fact]
  public void An_empty_tank_serializes_as_empty() {
    var (_, rig, be) = Standing();

    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);

    var dst = new BlockEntityHopperTall {
      Pos = be.Pos.Copy(),
      Block = be.Block,
    };
    rig.World.Attach(dst);
    dst.FromTreeAttributes(tree, rig.World.World);

    Assert.Equal(0, dst.TankCount);
  }

  #endregion
}
