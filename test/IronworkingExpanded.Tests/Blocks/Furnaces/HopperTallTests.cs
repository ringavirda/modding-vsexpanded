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
/// The tall hopper: a one-stack burden tank that drips its contents into the furnace shaft below,
/// continuously and with no toggle. It fills either burden family (the furnace it feeds gates
/// acceptance), caps at one stack, and takes every interaction from its top filler cell (routed to the
/// block entity), not its base. Covers the tank fill/cap, the continuous drip into a shaft pile, the
/// filler-routed deposit/withdraw, and persistence.
/// </summary>
/// <remarks>
/// <b>In <see cref="FurnaceConfigCollection"/> because the charge-verdict cases read a computed heat
/// balance</b>, which that collection's own doc says is the joining condition. It was outside it while
/// <c>The_verdict_flips_between_a_charge_that_melts_and_one_that_chills</c> already read one - a live
/// race, since <c>HeatBalanceTests</c> turns <c>BfCombustionBaseTemp</c> down to 0 and
/// <c>BfCokeSensitivity</c> up to 4 mid-run, and a verdict computed inside that window flips for a reason
/// that has nothing to do with the charge. The coke/charcoal pair below doubles the exposure, because its
/// whole claim is that two furnaces differing only in fuel land on <em>different sides</em> of the melt
/// line - the narrowest margin anything in this file asserts on.
/// </remarks>
[Collection(FurnaceConfigCollection.Name)]
public class HopperTallTests
{
  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>
  /// A standing cold blast furnace with a <b>real</b> tall hopper in the cell its own drawing puts one in.
  /// <para>
  /// <b>The furnace is not scenery here - it is half the subject.</b> Since the column cutover the
  /// hopper has no opinion of its own about what is chargeable or about where a load lands: it asks the
  /// anchored core (<c>IsChargeItem</c>) and the core's own selection rule (<c>NextChargeColumn</c>). A
  /// bare hopper over nothing, which is what this fixture used to be, can therefore no longer express any
  /// of it - it would accept nothing and drip nowhere, and every assertion would pass vacuously.
  /// </para>
  /// </summary>
  private static (
    BlockEntityBlastFurnaceCold core,
    StructureRig rig,
    BlockEntityHopperTall hopper
  ) Standing(string side = "north")
  {
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
    // The shaft's second fuel. Registered because the course readout names the fuel off the resolved
    // item rather than off a lang key of iwex's own - an unregistered code falls back to the generic
    // word, which would let a naming bug pass as "vague on purpose".
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

    // The hopper cell and the code it wants both come off the raised structure rather than being
    // hand-written: the layout marks the hopper as an oriented part, so the demanded variant rotates with
    // the furnace. Hard-coding "-e" would leave three of the four facings holding a block that satisfies
    // nothing, and a structure that never completes resolves no anchor.
    var (hopperPos, wanted) = rig.Cells.Single(c =>
      c.Wanted.StartsWith("iwex:hopper-tall", StringComparison.Ordinal)
    );
    var block = TestBlocks.Configure(
      new BlockHopperTall(),
      wanted,
      80,
      ("side", wanted[(wanted.LastIndexOf('-') + 1)..])
    );
    var hopper = new BlockEntityHopperTall { Pos = hopperPos.Copy(), Block = block };
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
    var (_, rig, be) = Standing();
    var slot = new DummySlot(new ItemStack(BurdenItem(rig), 50));

    Assert.True(be.TryDeposit(slot, wholeStack: true));

    Assert.Equal(50, be.TankCount);
    Assert.True(slot.Empty);
  }

  [Fact]
  public void A_plain_deposit_takes_one_unit()
  {
    var (_, rig, be) = Standing();
    var slot = new DummySlot(new ItemStack(BurdenItem(rig), 50));

    Assert.True(be.TryDeposit(slot, wholeStack: false));

    Assert.Equal(1, be.TankCount);
    Assert.Equal(49, slot.StackSize);
  }

  [Fact]
  public void Same_burden_stacks_into_the_tank()
  {
    var (_, rig, be) = Standing();

    Deposit(be, BurdenItem(rig), 30);
    Deposit(be, BurdenItem(rig), 20);

    Assert.Equal(50, be.TankCount);
  }

  [Fact]
  public void The_tank_fills_to_capacity_and_refuses_the_overflow()
  {
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
  public void A_different_grade_is_refused_once_the_tank_is_loaded()
  {
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
  public void Drips_burden_onto_the_shafts_lowest_column()
  {
    var (core, rig, be) = Standing();
    Deposit(be, BurdenItem(rig), 50);

    Tick(be);

    int per = IwexValues.HopperTallDropPerSecond;
    Assert.Equal(per, ShaftUnits(core)); // one drip landed in the shaft
    Assert.Equal(50 - per, be.TankCount); // and the tank fell by exactly the same

    // On an empty shaft every column is equally low, so the tie breaks on ascending (x, z) - the first
    // key, and nothing else. A rule that dropped straight down would land in the hopper's own column.
    List<ChargeColumn> columns = Columns(core);
    Assert.Equal(per, columns[0].TotalUnits);
    Assert.All(columns.Skip(1), c => Assert.Equal(0, c.TotalUnits));
  }

  [Fact]
  public void The_shaft_fills_in_level_courses_rather_than_one_tower()
  {
    // The whole reason the selection is "lowest column first": the raceway course has to be complete
    // before a furnace will light, so a hopper that filled one column to the roof would leave eight
    // tuyeres blowing into empty air however much burden went in.
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
  public void A_load_spread_over_many_ticks_stays_ONE_band_per_column()
  {
    // The temperature quantisation, and it is the only thing standing between a hand-charged course
    // and a save tree with thousands of segments. ChargeColumn.Push merges only within 1 °C, so a hopper
    // dripping at raw per-tick ambient starts a fresh segment on almost every drip - nothing throws, the
    // totals stay right, the bands still draw, and the column quietly becomes unreadable.
    //
    // Asserted on the segment count, which nothing else in the suite looks at. A unit total cannot see
    // this at all: it is identical either way.
    var (core, rig, be) = Standing();
    int columnCount = core.ShaftColumns.Count;
    Deposit(be, BurdenItem(rig), IwexValues.HopperTallCapacity);

    for (int i = 0; i < columnCount * 3; i++)
      Tick(be);

    Assert.All(
      Columns(core),
      c => Assert.True(
        c.Segments.Count <= 1,
        $"a column took {c.Segments.Count} segments from one grade of one tank"
      )
    );
  }

  [Fact]
  public void Drips_what_the_furnace_below_declares_chargeable()
  {
    // This was `Drips_either_burden_family` and laid `iwex:remeltburden` - the
    // second burden, deleted with the family model. The claim it made is unchanged and is the reason the
    // case survives its fixture: the hopper is a dumb tank. It lays whatever the machine below declares
    // chargeable and holds no opinion of its own - which is why one hopper block can serve the blast
    // furnace, the cupola, the heating furnace and the coke oven without knowing what any of them burns.
    var (core, rig, be) = Standing();
    Item burden = BurdenItem(rig);

    Deposit(be, burden, 20);
    Tick(be);

    Assert.True(be.TankCount < 20);
    Assert.Equal("iwex:burden", Columns(core)[0].Segments[0].Material);
  }

  [Fact]
  public void The_hopper_takes_the_furnaces_own_FUEL_as_well_as_its_burden()
  {
    // The delegation, stated as a behaviour rather than as a call. The hopper used to answer
    // `Burden.IsAny` - a fourth copy of a predicate three cores already carried - which is why one hopper
    // block could not serve four machines: a shaft furnace burns coke as its own bands, and a tank that
    // knew only about burden could never charge a round.
    var (core, rig, be) = Standing();
    Item coke = rig.World.World.GetItem(new AssetLocation("game:coke"))!;

    Assert.True(be.Accepts(new ItemStack(coke, 4)), "a shaft furnace charges coke");
    Deposit(be, coke, 16);
    Tick(be);

    // `"coke"`, not `"game:coke"`: a segment stores `Code.ToShortString()`, and vanilla's short form
    // drops the implicit `game:` domain. Every code-string gate reads it back through an AssetLocation,
    // whose domainless constructor puts `game:` back - so the two forms are the same material, and the
    // one that lands in a save is this one.
    Assert.Equal("coke", Columns(core)[0].Segments[0].Material);
  }

  [Fact]
  public void Fuel_goes_onto_burden_but_never_onto_more_fuel()
  {
    // The band order, and the one rule the selection applies beyond "lowest first". A round is coke
    // then burden; coke laid straight onto coke is not a round, it is a fire with no ore in it.
    var (core, rig, be) = Standing();
    Item coke = rig.World.World.GetItem(new AssetLocation("game:coke"))!;
    int columnCount = core.ShaftColumns.Count;

    // A course of coke across every column - the bottom of a proper round.
    Deposit(be, coke, IwexValues.HopperTallDropPerSecond * columnCount);
    for (int i = 0; i < columnCount; i++)
      Tick(be);
    Assert.All(Columns(core), c => Assert.Equal("coke", c.TopMaterial));

    // A second coke load has nowhere to go: every column is topped with coke, so the tank holds.
    be.TryWithdraw();
    Deposit(be, coke, 32);
    int before = ShaftUnits(core);
    Tick(be);
    Assert.Equal(before, ShaftUnits(core));
    Assert.Equal(32, be.TankCount);

    // ...and burden is taken straight away, which is what makes the refusal above a band-order rule and
    // not simply a full shaft.
    be.TryWithdraw();
    Deposit(be, BurdenItem(rig), 32);
    Tick(be);
    Assert.True(ShaftUnits(core) > before);
  }

  [Fact]
  public void Charcoal_will_not_go_onto_a_column_topped_with_COKE()
  {
    // <b>The band-order rule asks <c>IsFuelCode(top)</c>, never <c>top == material</c></b>, and a
    // second fuel is the only thing in the game that can tell those two spellings apart. Coke onto coke -
    // the case directly above - is refused by both, so it passes whichever one is in the source. Charcoal
    // onto a coke-topped column is refused by the role test alone: simplify `NextChargeColumn` to an
    // equality and a hopper of charcoal quietly lays a second fuel course onto the first, which is not a
    // round but a fire with no ore in it. Nothing throws, the units all add up, and the furnace lights on a
    // shaft that can never make iron.
    var (core, rig, be) = Standing();
    Item coke = rig.World.World.GetItem(new AssetLocation("game:coke"))!;
    Item charcoal = rig.World.World.GetItem(new AssetLocation("game:charcoal"))!;
    int columnCount = core.ShaftColumns.Count;

    // A complete course of coke, laid the way a player lays one - through the tank, not by hand.
    Deposit(be, coke, IwexValues.HopperTallDropPerSecond * columnCount);
    for (int i = 0; i < columnCount; i++)
      Tick(be);
    Assert.All(Columns(core), c => Assert.Equal("coke", c.TopMaterial));

    // The hopper still accepts charcoal - the refusal is about where it may go, never about what it is.
    // Asserting that first is what stops this case passing for the wrong reason: a shaft that had simply
    // never recognised charcoal as charge would hold its tank here too, and read identically.
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
    // And no column grew a charcoal band anywhere, not merely "no units in total". A drip that landed
    // charcoal in one column while another shed the same volume would balance the total exactly.
    Assert.All(Columns(core), c => Assert.Equal("coke", c.TopMaterial));

    // The control: burden onto the very same coke course goes in at once, so the hold above is the band
    // order and not a full shaft, a jammed tank or an unregistered item.
    be.TryWithdraw();
    Deposit(be, BurdenItem(rig), 32);
    Tick(be);
    Assert.True(ShaftUnits(core) > before);
  }

  [Fact]
  public void Holds_the_load_when_the_shaft_is_full()
  {
    var (core, rig, be) = Standing();

    // Fill every column to its own ceiling by hand - the hopper's own drip would take a thousand ticks.
    foreach (var (x, z) in core.ShaftColumns.Keys)
      core.ChargeColumnAt(x, z)!.Push("iwex:burden", core.ColumnCapacity(x, z), 20f, default);
    int full = ShaftUnits(core);

    Deposit(be, BurdenItem(rig), 20);
    Tick(be);

    // Nowhere to put it, so the tank keeps it. The shaft total is asserted too: a hopper that "held"
    // by pushing into a column above its own roof would leave the tank untouched here as well, while
    // inventing charge no block can draw and no player can dig out.
    Assert.Equal(20, be.TankCount);
    Assert.Equal(full, ShaftUnits(core));
  }

  #endregion

  #region Rotated structure

  /// <summary>
  /// <b>The rotation bug this replaces cannot exist any more, and that is the assertion.</b> B16 was a
  /// nine-offset table of drip candidates the hopper rotated by its own <c>side</c> variant and walked
  /// downward through the world - so a south- or west-facing cupola charged the cell <em>outside</em>
  /// itself. The table is gone: columns are keyed <b>structure-local</b> on the core, and the hopper asks
  /// the core which one to lay on, so there is no offset left to rotate wrongly.
  /// <para>
  /// What survives as a real statement is the invariant the old fix was trying to buy - a hopper at any
  /// facing charges its own furnace's own columns, the same set, in the same order. Asserted as the
  /// <b>structure-local</b> key set, because that is exactly the frame the rotation used to be applied in:
  /// a world-frame assertion would move with the facing and could not tell "the same columns" from "some
  /// columns".
  /// </para>
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void A_hopper_charges_the_same_column_set_at_every_facing(string side)
  {
    var (core, rig, be) = Standing(side);
    int columnCount = core.ShaftColumns.Count;
    int per = IwexValues.HopperTallDropPerSecond;
    Deposit(be, BurdenItem(rig), per * columnCount);

    for (int i = 0; i < columnCount; i++)
      Tick(be);

    // Nine columns, every one of them charged, whichever way the furnace faces.
    Assert.Equal(9, columnCount);
    Assert.All(Columns(core), c => Assert.Equal(per, c.TotalUnits));

    // ...and they are the same nine local keys at all four facings - the frame the deleted offset table
    // used to be rotated into.
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

  // Charging is a skill now - a round is coke then burden, laid in level courses - so a player who
  // cannot see what they just laid is charging blind. These pin the four lines the hopper surfaces, all
  // of them derived: not one field is persisted for them.
  //
  // The headless lang service echoes a key and drops its arguments, so "the line is present" and "its
  // numbers are right" are two different assertions. The course numbers are read off `Core.TopCourse`,
  // which is public precisely so that seam can be pinned; the verdict is read off which key appears,
  // which is the whole discriminator it carries.

  private static string Hud(BlockEntityHopperTall be)
  {
    var sb = new System.Text.StringBuilder();
    be.GetBlockInfo(null!, sb);
    return sb.ToString();
  }

  /// <summary>Lays <paramref name="bands"/> bands of <paramref name="material"/> onto every column - a
  /// level course across the whole shaft, which is how a shaft actually fills.</summary>
  private static void Course(
    BlockEntityBlastFurnaceCold core,
    string material,
    int bands,
    BurdenMix mix
  )
  {
    int perBand = core.ChargeUnitsPerBlock / ChargeColumn.BandsPerBlock;
    foreach (ChargeColumn column in Columns(core))
      column.Push(material, bands * perBand, 20f, mix);
  }

  [Fact]
  public void The_course_line_reports_the_top_course_of_the_column_being_filled()
  {
    var (core, _, be) = Standing();

    // A proper round, laid level across the shaft: 4 bands of coke, then 3 of burden on top.
    Course(core, "coke", 4, default);
    Course(core, "iwex:burden", 3, new BurdenMix(75f, 5f, 20f));

    var course = core.TopCourse;
    Assert.Equal(4, course.FuelBands);
    Assert.Equal(3, course.BurdenBands);
    Assert.Equal(ChargeColumn.BandsPerBlock, course.Bands);
    Assert.Contains("iwex:bf-info-course", Hud(be));

    // It follows the column the hopper will fill next - the lowest - not a fixed one and not the
    // tallest. Running one column far past the rest must not move the readout: the round being laid is
    // still the one at the bottom of the stockline, which is where the next drip lands.
    int perBand = core.ChargeUnitsPerBlock / ChargeColumn.BandsPerBlock;
    Columns(core)[^1].Push("coke", 20 * perBand, 20f, default);

    Assert.Equal(4, core.TopCourse.FuelBands);
    Assert.Equal(3, core.TopCourse.BurdenBands);
  }

  [Fact]
  public void The_course_line_names_the_fuel_laid_and_refuses_to_name_a_mixed_course()
  {
    // The readout used to bucket every fuel band together and call it coke. A charcoal band is half
    // the carbon of a coke one (`BlockEntityFurnaceCore.CarbonPerUnit`), so that line told a player who
    // charged charcoal their round was worth twice what it is - on the very readout charging-as-a-skill
    // is judged from. The course now carries the fuel's own code, which is also why a third fuel needs
    // no new counter here.
    var (burnt, _, burntBe) = Standing();
    Course(burnt, "charcoal", 4, default);
    Course(burnt, "iwex:burden", 3, new BurdenMix(75f, 5f, 20f));

    Assert.Equal(4, burnt.TopCourse.FuelBands);
    Assert.Equal("charcoal", burnt.TopCourse.FuelMaterial);
    Assert.Contains("iwex:bf-info-course", Hud(burntBe));

    // A course holding both fuels has no honest single name, so the material goes null and the line
    // says "mixed fuel". FuelBands still counts every band - the volume of the course is not in doubt,
    // only what to call it. Naming the first fuel found would be the same lie in a smaller font.
    var (mixed, _, _) = Standing();
    Course(mixed, "coke", 2, default);
    Course(mixed, "charcoal", 2, default);

    Assert.Equal(4, mixed.TopCourse.FuelBands);
    Assert.Null(mixed.TopCourse.FuelMaterial);

    // That null reads as "mixed" only because bands were laid: a burden-only course is null too, and
    // the pair is what tells the two apart. Reading the material alone would call an empty course mixed.
    var (bare, _, _) = Standing();
    Course(bare, "iwex:burden", 3, new BurdenMix(75f, 5f, 20f));

    Assert.Equal(0, bare.TopCourse.FuelBands);
    Assert.Null(bare.TopCourse.FuelMaterial);
  }

  /// <summary>What the course line would actually print for its fuel - the string
  /// <c>iwex:bf-info-course</c>'s second argument is filled with.</summary>
  /// <remarks>
  /// <b>Reached by reflection, and there is no public alternative.</b> The headless lang service echoes
  /// a key and drops its arguments, so the rendered line is the byte-identical string
  /// <c>"iwex:bf-info-course"</c> for a coke round and a charcoal one. Reading the HUD text is therefore
  /// blind to the exact bug this pins - a readout that names every fuel "coke" - and the name is only
  /// observable where it is produced.
  /// </remarks>
  private static string FuelName(BlockEntityBlastFurnaceCold core) =>
    (string)ReflectionHelpers.Invoke(core, "CourseFuelName", core.TopCourse)!;

  [Fact]
  public void TopCourse_tells_a_charcoal_course_from_a_coke_course()
  {
    // Two furnaces, the same round in both - four bands of fuel under three of burden - differing in
    // nothing but which fuel. A charcoal band is half the carbon of a coke one, so this line is the only
    // place a player can see the difference before lighting, and it has two ways to lie.
    var grade = new BurdenMix(75f, 5f, 20f);

    var (coked, _, cokedBe) = Standing();
    Course(coked, "coke", 4, default);
    Course(coked, "iwex:burden", 3, grade);

    var (charred, _, charredBe) = Standing();
    Course(charred, "charcoal", 4, default);
    Course(charred, "iwex:burden", 3, grade);

    // Lie one - blindness. A course reader that knows only coke counts the charcoal bands as burden and
    // reports 0 of 4 fuel with 7 of burden: the player is told a round with four bands of fuel in it has
    // none, and the obvious "fix" is to charge more. Asserted as the pair and against the coke twin, so a
    // reader that fell back to some other constant cannot slip through either.
    Assert.Equal(coked.TopCourse.FuelBands, charred.TopCourse.FuelBands);
    Assert.Equal(coked.TopCourse.BurdenBands, charred.TopCourse.BurdenBands);
    Assert.Equal(4, charred.TopCourse.FuelBands);
    Assert.Equal(3, charred.TopCourse.BurdenBands);

    // Lie two - aliasing. Counting the bands but calling them coke is the worse of the two, because it
    // reads as a correct line: the round looks worth twice what it is, on the very readout charging-as-a-
    // skill is judged from. The two courses must not answer with the same name.
    Assert.Equal("coke", coked.TopCourse.FuelMaterial);
    Assert.Equal("charcoal", charred.TopCourse.FuelMaterial);

    // ...and the difference survives all the way to the words on the line, not just to the model behind
    // it. Both huDs still carry the course line - the discriminator is the fuel name inside it, which
    // the echoing headless lang service cannot show (see FuelName).
    Assert.Contains("iwex:bf-info-course", Hud(cokedBe));
    Assert.Contains("iwex:bf-info-course", Hud(charredBe));
    Assert.NotEqual(FuelName(coked), FuelName(charred));
  }

  [Fact]
  public void The_verdict_flips_between_a_charge_that_melts_and_one_that_chills()
  {
    // The verdict is the projection "on full blast, at ambient" - the cold reading, so it can never
    // promise a melt the furnace cannot deliver. It reads ComputeHeatBalance rather than re-deriving a
    // coke threshold of its own, so it cannot drift from the model.
    //
    // The shaft is charged full, and that is not incidental: the heat balance's cold-charge loss
    // scales with how much charge is in there, so a thin shaft of the very same burden genuinely runs
    // hotter and genuinely does read as melting. Charging thin here would have "passed" the melts case
    // for a reason that has nothing to do with the grade.
    // The grade is varied by how much coke the player lays, not by the burden's stamp. A burden band's
    // stamped fuel no longer counts as carbon - it was a second, disagreeing answer to "how much
    // is at the raceway" once coke got its own bands - so a test that flipped the stamp would now be
    // charging two identical furnaces and asserting they differ.
    var (core, _, be) = Standing();
    int bands = core.ColumnCapacity(0, 0) / (core.ChargeUnitsPerBlock / ChargeColumn.BandsPerBlock);
    var grade = new BurdenMix(75f, 5f, 20f);

    // The standard grade - 20 % of the round laid as coke - settles below iron's melt line on cold blast.
    Course(core, "coke", bands / 5, default);
    Course(core, "iwex:burden", bands - (bands / 5), grade);
    ReflectionHelpers.Invoke(core, "OnProductionTick", 1f);

    Assert.Contains("iwex:bf-info-chargechills", Hud(be));
    Assert.DoesNotContain("iwex:bf-info-chargemelts", Hud(be));

    // The control: the same furnace, the same number of bands, more of them coke. Without this the case
    // passes on a verdict hard-wired to "chills".
    var (rich, _, richBe) = Standing();
    Course(rich, "coke", bands * 2 / 5, default);
    Course(rich, "iwex:burden", bands - (bands * 2 / 5), grade);
    ReflectionHelpers.Invoke(rich, "OnProductionTick", 1f);

    Assert.Contains("iwex:bf-info-chargemelts", Hud(richBe));
    Assert.DoesNotContain("iwex:bf-info-chargechills", Hud(richBe));
  }

  [Fact]
  public void The_charge_verdict_tells_a_coke_round_from_a_charcoal_round_at_the_same_band_count()
  {
    // <b>The player-facing proof that the shaft prices its fuel rather than merely accepting it.</b>
    // Charcoal has always been chargeable, always lit, always burned, always yielded iron - at coke's
    // exact rate. Two shafts loaded to the same height, with the same number of fuel bands under the same
    // grade of burden, differing in nothing but the fuel: coke settles above iron's melt line and charcoal
    // below it. If a future change ever makes charcoal a free swap for coke again, this is the case that
    // says so in the words the player reads.
    //
    // The shafts are charged full and to the same height, both load-bearing: the heat balance's
    // cold-charge loss scales with how much charge is standing in the furnace, so a charcoal twin charged
    // any thinner would read cooler for a reason that has nothing to do with its carbon.
    var grade = new BurdenMix(75f, 5f, 20f);

    var (coked, _, cokedBe) = Standing();
    int bands =
      coked.ColumnCapacity(0, 0) / (coked.ChargeUnitsPerBlock / ChargeColumn.BandsPerBlock);
    // A quarter of the round as fuel, and the fraction is chosen rather than arbitrary: it is the
    // window in which the fuel's identity is what decides the verdict. The standard 20 % round chills on
    // either fuel (coke lands ~0.238 carbon against a ~0.239 melt threshold - the case above rides that
    // hair deliberately), and the 40 % round melts on either. Only between those does swapping coke for
    // charcoal move a charge across the line, so this is the one split that can fail for the right reason.
    int fuelBands = bands / 4;
    Course(coked, "coke", fuelBands, default);
    Course(coked, "iwex:burden", bands - fuelBands, grade);
    ReflectionHelpers.Invoke(coked, "OnProductionTick", 1f);

    var (charred, _, charredBe) = Standing();
    Course(charred, "charcoal", fuelBands, default);
    Course(charred, "iwex:burden", bands - fuelBands, grade);
    ReflectionHelpers.Invoke(charred, "OnProductionTick", 1f);

    // Stated first, because it is what makes the verdicts below mean anything: the two rounds are the
    // same volume, the same height and the same number of fuel bands. The only difference the model may
    // use is CarbonPerUnit. A test that let the charcoal shaft be shorter or leaner would prove nothing
    // except that less fuel is worse.
    Assert.Equal(coked.ShaftChargeUnits, charred.ShaftChargeUnits);
    Assert.Equal(coked.TopCourse.FuelBands, charred.TopCourse.FuelBands);

    Assert.Contains("iwex:bf-info-chargemelts", Hud(cokedBe));
    Assert.DoesNotContain("iwex:bf-info-chargechills", Hud(cokedBe));

    Assert.Contains("iwex:bf-info-chargechills", Hud(charredBe));
    Assert.DoesNotContain("iwex:bf-info-chargemelts", Hud(charredBe));
  }

  [Fact]
  public void The_readout_is_derived_so_it_survives_a_save_round_trip_unchanged()
  {
    var (core, rig, be) = Standing();
    int perBand = core.ChargeUnitsPerBlock / ChargeColumn.BandsPerBlock;
    Columns(core)[0].Push("coke", 5 * perBand, 20f, default);
    ReflectionHelpers.Invoke(core, "OnProductionTick", 1f);
    string before = Hud(be);

    // Round-trip the core - the readout is entirely its state - and read the hopper's line off the copy.
    var tree = new TreeAttribute();
    core.ToTreeAttributes(tree);
    var reloaded = new BlockEntityBlastFurnaceCold
    {
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
  public void An_incomplete_structure_says_nothing_about_the_shaft()
  {
    var (core, _, be) = Standing();
    ReflectionHelpers.SetProperty(core, nameof(core.StructureComplete), false);

    string hud = Hud(be);

    // The tank line is the hopper's own and stays; every shaft line is the furnace's and goes silent.
    Assert.Contains("iwex:hoppertall-empty", hud);
    foreach (
      string key in new[]
      {
        // `bf-info-mixloaded` was listed here and is deleted - it printed the same pair of
        // numbers as `bf-info-shaftfull` once the denominator became the furnace's capacity.
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
    var (_, rig, be) = Standing();
    TestWorld world = rig.World;
    world.World.Side.Returns(EnumAppSide.Server);
    BlockStructureFiller filler = PlaceFiller(world, be.Pos);

    var player = PlayerHolding(
      new DummySlot(new ItemStack(BurdenItem(rig), 30)),
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
    var (_, rig, be) = Standing();
    TestWorld world = rig.World;
    world.World.Side.Returns(EnumAppSide.Server);
    Deposit(be, BurdenItem(rig), 40);
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
    var (_, rig, be) = Standing();
    Deposit(be, BurdenItem(rig), 77);

    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);

    var dst = new BlockEntityHopperTall { Pos = be.Pos.Copy(), Block = be.Block };
    rig.World.Attach(dst);
    dst.FromTreeAttributes(tree, rig.World.World);

    Assert.Equal(77, dst.TankCount);
  }

  [Fact]
  public void An_empty_tank_serializes_as_empty()
  {
    var (_, rig, be) = Standing();

    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);

    var dst = new BlockEntityHopperTall { Pos = be.Pos.Copy(), Block = be.Block };
    rig.World.Attach(dst);
    dst.FromTreeAttributes(tree, rig.World.World);

    Assert.Equal(0, dst.TankCount);
  }

  #endregion
}
