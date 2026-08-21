using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// What a firebox hearth does with the contents of its firebox: how much charge it reads, what it
/// refuses, and how much it wants before it lights. Asserted on both
/// <see cref="BlockEntityPuddlingFurnace"/> and <see cref="BlockEntityHeatingFurnace"/>, since the
/// answers belong to <see cref="BlockEntityFireboxFurnace"/>. See
/// docs/design/machines/puddling-furnace.md.
/// The charge hooks are invoked directly rather than through a lit furnace, because the puddling layout
/// has filler cells no part produces and its structure never completes in game, so
/// <c>OnProductionTick</c> never runs. <see cref="FireboxTickTests"/> covers the real tick.
/// </summary>
public class FireboxChargeTests {
  #region Harness

  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>The two hearths, each pointed at a real oriented block code so the block entity derives
  /// its own structure angle.</summary>
  private static IEnumerable<BlockEntityFireboxFurnace> Hearths() {
    var puddling = new BlockEntityPuddlingFurnace { Pos = Anchor.Copy() };
    OrientWithLayout(
      puddling,
      BlockPuddlingFurnaceCore.Definitions("iiex").Single(),
      "iiex:furnace-puddlingcore-tier1-n",
      "north"
    );
    yield return puddling;

    var heating = new BlockEntityHeatingFurnace { Pos = Anchor.Copy() };
    OrientWithLayout(
      heating,
      BlockHeatingFurnaceCore.Definitions("iiex").Single(),
      "iiex:furnace-heatingcore-tier1-n",
      "north"
    );
    yield return heating;
  }

  /// <summary>
  /// A firebox holding <paramref name="stacks"/>, in the shape the charge walk hands the hooks: one
  /// <see cref="BEBehaviorFirebox"/> bed per cell, each charged through its own API so a fuel the
  /// firebox would refuse cannot enter a premise. Built without a standing structure, since the hooks
  /// take an opaque handle and never touch the world.
  /// </summary>
  private static object Firebox(TestWorld world, params ItemStack[] stacks) {
    var beds = new List<(BlockPos pos, BEBehaviorFirebox bed)>();
    for (int i = 0; i < stacks.Length; i++) {
      var be = new BlockEntityFirebox { Pos = Anchor.AddCopy(0, 1, i) };
      var bed = new BEBehaviorFirebox(be);
      bed.TryAdd(stacks[i], stacks[i].StackSize);
      beds.Add((be.Pos, bed));
    }
    return beds;
  }

  /// <summary>Runs the furnace's own <c>ReadChargeMix</c> and hands back everything it reported.</summary>
  private static (
    int count,
    bool isFull,
    BurdenMix mix,
    int rejectedCount
  ) Read(BlockEntityFurnaceCore be, object firebox) {
    object?[] args = [firebox, null, null, null];
    int count = (int)ReflectionHelpers.Invoke(be, "ReadChargeMix", args)!;
    return (count, (bool)args[1]!, (BurdenMix)args[2]!, (int)args[3]!);
  }

  private static int Threshold(BlockEntityFurnaceCore be) =>
    (int)ReflectionHelpers.GetProperty(be, "ChargeCapacityUnits")!;

  private static int Cells(BlockEntityFireboxFurnace be) =>
    (int)ReflectionHelpers.GetProperty(be, "FireboxCellCount")!;

  #endregion

  #region B8, third cause - a firebox of plain fuel must not read as zero

  /// <summary>
  /// A firebox reads its beds directly instead of filtering on <c>IsChargeItem</c>, which on the core
  /// admits prepared burden only, so plain fuel counts. The shaft branch overrides <c>IsChargeItem</c>
  /// to admit the <c>fuel</c> role because coke is charged as its own bands there; a firebox does not.
  /// </summary>
  [Fact]
  public void A_firebox_of_plain_coke_is_counted_rather_than_scored_at_zero() {
    var world = new TestWorld();
    Item coke = world.RegisterItem("game:coke");

    foreach (BlockEntityFireboxFurnace hearth in Hearths()) {
      var read = Read(
        hearth,
        Firebox(world, new ItemStack(coke, 12), new ItemStack(coke, 4))
      );

      Assert.Equal(16, read.count);
      // Reads as pure fuel: nothing in a firebox is being reduced, so there is no ore or flux fraction.
      Assert.Equal(1f, read.mix.FuelFrac);
      Assert.Equal(0f, read.mix.IronFrac);
    }
  }

  /// <summary>An empty firebox reads zero: the unfiltered stack-size read must not turn no fuel into a
  /// count.</summary>
  [Fact]
  public void An_empty_firebox_still_reads_zero() {
    var world = new TestWorld();

    foreach (BlockEntityFireboxFurnace hearth in Hearths()) {
      Assert.Equal(0, Read(hearth, Firebox(world)).count);
      Assert.False(Read(hearth, Firebox(world)).isFull);
    }
  }

  #endregion

  #region B8, first cause - the threshold is the firebox's own capacity

  /// <summary>
  /// The firebox cell count comes off the layout's <c>CellRole.Firebox</c> marks rather than being
  /// hand-set on the block entity, so the count and the cells a player can load cannot disagree. The
  /// hearth therefore has to be stood on its own blocktype for the layout to be read.
  /// </summary>
  [Fact]
  public void The_firebox_cell_count_comes_from_the_geometry_the_machine_declares() {
    // The puddling drawing marks one Firebox cell; the heating drawing marks two.
    List<BlockEntityFireboxFurnace> hearths = Hearths().ToList();
    Assert.Equal(1, Cells(hearths[0]));
    Assert.Equal(2, Cells(hearths[1]));
  }

  /// <summary>
  /// A firebox fires on what its own cells hold: the threshold is the cell count times the per-cell
  /// constant, not a fixed total sized for a shaft.
  /// </summary>
  [Fact]
  public void Each_hearth_fires_on_its_own_fireboxs_capacity_not_a_shared_constant() {
    var thresholds = new List<int>();

    foreach (BlockEntityFireboxFurnace hearth in Hearths()) {
      // Cells times the per-cell constant, with no offset and no floor. The amount itself is pinned by
      // the literals below.
      Assert.Equal(
        Cells(hearth) * IiexValues.FireboxMixPerCell,
        Threshold(hearth)
      );
      thresholds.Add(Threshold(hearth));
    }

    // The one-cell hearth and the two-cell one ask for different amounts, which no shared constant can
    // satisfy.
    Assert.Equal(2, thresholds.Count);
    Assert.NotEqual(thresholds[0], thresholds[1]);

    // Literals: every relation above is the same expression over the same sources on both sides and
    // holds for any per-cell value. The branch guard bounds the value at cells * 16 above and "> 0"
    // below, so it does not pin these either.
    Assert.Equal(12, IiexValues.FireboxMixPerCell);
    Assert.Equal(new[] { 12, 24 }, thresholds);
  }

  /// <summary>
  /// The threshold as the furnace applies it rather than as a number it exposes: one unit under it the
  /// firebox is not full, and at it, it is. Both fireboxes here are states the game can produce, one
  /// bed per cell within its own capacity.
  /// </summary>
  [Fact]
  public void The_firebox_reads_full_at_its_own_threshold_and_not_one_unit_under() {
    var world = new TestWorld();
    Item coke = world.RegisterItem("game:coke");

    foreach (BlockEntityFireboxFurnace hearth in Hearths()) {
      int perCell = IiexValues.FireboxMixPerCell;
      int cells = Cells(hearth);

      var brimmed = new ItemStack[cells];
      for (int i = 0; i < cells; i++)
        brimmed[i] = new ItemStack(coke, perCell);
      Assert.True(Read(hearth, Firebox(world, brimmed)).isFull);

      // One unit out of the last cell, everything else unchanged.
      brimmed[cells - 1] = new ItemStack(coke, perCell - 1);
      Assert.False(Read(hearth, Firebox(world, brimmed)).isFull);
    }
  }

  /// <summary>
  /// A hearth whose drawing marks no firebox counts zero cells, not one. The count multiplies straight
  /// into <see cref="BlockEntityFireboxFurnace.ChargeCapacityUnits"/>, so a fallback of 1 would invent a
  /// threshold for a machine with nowhere to put fuel. Every shipped hearth draws a firebox, so
  /// <c>FireboxCellCount</c>'s <c>ShaftBox is null</c> arm is reachable only from a drawing built here.
  /// </summary>
  [Fact]
  public void A_hearth_whose_drawing_marks_no_firebox_counts_no_cells() {
    BlockEntityFireboxFurnace hearth = NoFireboxHearth(out _);

    Assert.Null(ShaftBoxOf(hearth));
    Assert.Equal(0, Cells(hearth));
  }

  /// <summary>
  /// A zero cell count is inert: the threshold goes to zero, so "full" is trivially true, but the
  /// furnace still cannot light because ignition needs beds and a box-less hearth collects none.
  /// </summary>
  [Fact]
  public void A_hearth_with_no_firebox_cannot_light_however_low_its_threshold_goes() {
    BlockEntityFireboxFurnace hearth = NoFireboxHearth(out StructureRig rig);

    // A full bed in the cell the drawing draws as fuel bed. It sits directly over the anchor, so a
    // walk that invented a box there rather than skipping would collect it and light the hearth.
    BlockPos cell = rig.Cell(0, 1, 0);
    var firebox = new BlockEntityFirebox { Pos = cell.Copy() };
    var bed = new BEBehaviorFirebox(firebox);
    bed.TryAdd(
      new ItemStack(new TestWorld().RegisterItem("game:coke"), 99),
      99
    );
    ReflectionHelpers.SetField(
      firebox,
      "Behaviors",
      new List<BlockEntityBehavior> { bed }
    );
    rig.Occupy(
      cell,
      TestBlocks.Configure(
        new Block(),
        "iiex:furnace-firebox-tier1-n",
        900,
        ("side", "north")
      ),
      firebox
    );

    Assert.Equal(0, Threshold(hearth));
    Assert.Empty(Beds(hearth));
    Assert.False(Ignites(hearth, Beds(hearth)));
  }

  /// <summary>
  /// A heating hearth stood on a drawing that marks its fuel bed with no <c>Firebox</c> role. The legend
  /// is the real firebox glyph, so the cells are genuinely fuel-shaped; only the <c>Role</c> call is
  /// missing.
  /// </summary>
  private static BlockEntityHeatingFurnace NoFireboxHearth(out StructureRig rig) {
    ExBlockDef def = ExBlockDef
      .Create("iiex", "furnace")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "iiex:furnace-heatingcore-*")
          .Legend('F', FireboxGlyph)
          .Layer(0, "C")
          .Layer(1, "F")
      );

    var hearth = new BlockEntityHeatingFurnace();
    rig = Stand(hearth, def, Anchor, "iiex:furnace-heatingcore-tier1", "north");
    return hearth;
  }

  /// <summary>The furnace's own charge walk; <c>CollectCharge</c> is protected. On this branch it
  /// returns the firebox beds.</summary>
  private static List<(BlockPos pos, BEBehaviorFirebox bed)> Beds(
    BlockEntityFurnaceCore be
  ) =>
    (List<(BlockPos, BEBehaviorFirebox)>)
      ReflectionHelpers.Invoke(be, "CollectCharge")!;

  private static bool Ignites(BlockEntityFurnaceCore be, object firebox) =>
    (bool)ReflectionHelpers.Invoke(be, "TryIgniteCharge", firebox)!;

  /// <summary>
  /// A firebox may never ask for more fuel than its cells can hold. Delegated to
  /// <see cref="FurnaceBranchGuards"/> so it covers every firebox in every loaded assembly, not only
  /// the two named here.
  /// </summary>
  [Fact]
  public void No_firebox_asks_for_more_fuel_than_its_cells_can_hold() =>
    FurnaceBranchGuards.NoFireboxAsksForMoreThanItsCellsCanHold();

  /// <summary>
  /// B8's fifth cause: the same ceiling read from the other end. A firebox that cannot hold its own
  /// disruption floor lights and then snuffs itself on the extinguish grace, which is a furnace that has
  /// never worked rather than one that is hard to keep lit.
  /// </summary>
  [Fact]
  public void No_firebox_carries_a_disruption_floor_above_its_own_capacity() =>
    FurnaceBranchGuards.NoFireboxCarriesAFloorAboveItsOwnCapacity();

  /// <summary>
  /// And it must be able to reach the temperature it works at, on the chimney its own drawing gives it.
  /// A hearth that lights, holds and never crosses its process temperature is B8's other half.
  /// </summary>
  [Fact]
  public void Every_firebox_reaches_its_own_process_temperature() =>
    FurnaceBranchGuards.EveryFireboxReachesItsOwnProcessTemperature();

  #endregion

  #region B8's companion - a firebox refuses nothing

  /// <summary>
  /// The read reports no rejection, whatever is in the box: a firebox only makes flame, declares no
  /// burden family and refuses nothing. The count and the rejected count have to agree, or the furnace
  /// turns charge away at <c>AcceptsCharge</c> while the HUD reports nothing refused.
  /// </summary>
  [Fact]
  public void A_firebox_reports_no_rejected_charge_whatever_is_in_it() {
    var world = new TestWorld();
    var charcoal = new ItemStack(world.RegisterItem("game:charcoal"), 8);

    foreach (BlockEntityFireboxFurnace hearth in Hearths()) {
      var read = Read(hearth, Firebox(world, charcoal));

      Assert.Equal(8, read.count);
      Assert.Equal(0, read.rejectedCount);
    }
  }

  /// <summary>
  /// A bed decides what it holds: the four metallurgical fuels go in, lignite does not, and neither does
  /// anything that is not a fuel.
  /// </summary>
  [Theory]
  [InlineData("game:coke", true)]
  [InlineData("game:charcoal", true)]
  [InlineData("game:ore-bituminouscoal", true)]
  [InlineData("game:ore-anthracite", true)]
  // Low-rank, high-moisture, high-ash: it will not carry a metallurgical heat.
  [InlineData("game:ore-lignite", false)]
  [InlineData("iiex:remeltburden", false)]
  [InlineData("game:ingot-iron", false)]
  public void A_bed_takes_the_four_metallurgical_fuels_and_nothing_else(
    string code,
    bool accepted
  ) {
    var world = new TestWorld();
    var stack = new ItemStack(world.RegisterItem(code), 4);

    var bed = new BEBehaviorFirebox(
      new BlockEntityFirebox { Pos = Anchor.Copy() }
    );

    Assert.Equal(accepted, BEBehaviorFirebox.IsFuel(stack));
    Assert.Equal(accepted ? 4 : 0, bed.TryAdd(stack, 4));
  }

  /// <summary>
  /// One fuel per bed: the layer texture depicts what the player charged, so a second substance is
  /// refused until the first has burned down or been taken out. Same rule as the tall hopper's tank.
  /// </summary>
  [Fact]
  public void A_bed_holding_one_fuel_refuses_a_second_until_it_is_emptied() {
    var world = new TestWorld();
    var coke = new ItemStack(world.RegisterItem("game:coke"), 4);
    var charcoal = new ItemStack(world.RegisterItem("game:charcoal"), 4);

    var bed = new BEBehaviorFirebox(
      new BlockEntityFirebox { Pos = Anchor.Copy() }
    );

    Assert.Equal(4, bed.TryAdd(coke, 4));
    Assert.Equal(0, bed.TryAdd(charcoal, 4));

    // The refusal is lifted by the fuel being gone: burning the bed down clears the substance and the
    // next charge may be anything.
    Assert.Equal(4, bed.Consume(4));
    Assert.Equal(4, bed.TryAdd(charcoal, 4));
  }

  /// <summary>
  /// A bed fills to its six drawn courses and no further. That ceiling is what the ignition threshold is
  /// derived from.
  /// </summary>
  [Fact]
  public void A_bed_fills_to_its_drawn_courses_and_stops() {
    var world = new TestWorld();
    var coke = new ItemStack(world.RegisterItem("game:coke"), 999);

    var bed = new BEBehaviorFirebox(
      new BlockEntityFirebox { Pos = Anchor.Copy() }
    );

    Assert.Equal(BEBehaviorFirebox.CellCapacity, bed.TryAdd(coke, 999));
    Assert.True(bed.IsFull);
    Assert.Equal(BEBehaviorFirebox.LayersPerCell, bed.LayerCount);
    Assert.Equal(0, bed.TryAdd(coke, 999));

    // Six courses of two, as the shape draws them. Literals: every relation above is the same
    // expression over the same source on both sides and holds for any capacity.
    Assert.Equal(6, BEBehaviorFirebox.LayersPerCell);
    Assert.Equal(2, BEBehaviorFirebox.UnitsPerLayer);
    Assert.Equal(12, BEBehaviorFirebox.CellCapacity);
  }

  /// <summary>
  /// Fuel comes back out of a bed a course at a time, as it does from a vanilla coal pile, and a
  /// part-emptied bed draws one course fewer.
  /// </summary>
  [Fact]
  public void Fuel_can_be_taken_back_out_a_course_at_a_time() {
    var world = new TestWorld();
    var coke = new ItemStack(world.RegisterItem("game:coke"), 999);

    var be = new BlockEntityFirebox { Pos = Anchor.Copy() };
    var bed = new BEBehaviorFirebox(be);
    bed.TryAdd(coke, 999);

    // Needs an Api to build the returned stack: the bed stores a code, not a resolved collectible, so
    // it keeps drawing after the mod that owned its fuel is removed.
    ReflectionHelpers.SetField(bed, "Api", world.Api);

    ItemStack? course = bed.TryTakeLayer();
    Assert.NotNull(course);
    Assert.Equal(BEBehaviorFirebox.UnitsPerLayer, course!.StackSize);
    Assert.Equal(BEBehaviorFirebox.LayersPerCell - 1, bed.LayerCount);
    Assert.False(bed.IsFull);
  }

  #endregion

  #region The pool that is gone

  /// <summary>
  /// A hearth pours nothing, so it inherits the core's product defaults rather than a shaft's pool. Its
  /// layout has no metal tap, so a pool it ever filled would be undrainable and would trip
  /// <c>LiquidCapacityReached</c>, snuffing the furnace.
  /// </summary>
  [Fact]
  public void Neither_hearth_pools_anything_so_neither_can_back_up_on_its_own_output() {
    foreach (BlockEntityFireboxFurnace hearth in Hearths()) {
      Assert.False(
        (bool)ReflectionHelpers.GetProperty(hearth, "LiquidCapacityReached")!
      );
      // The pool is the crucible floor's own cells now, so "pools nothing" is "marks no pool cell" -
      // which is also what keeps the empty-pool read (0 of 0) from presenting as a full one.
      Assert.Empty(hearth.PoolCells);
      Assert.Null(ReflectionHelpers.GetProperty(hearth, "SolidProductBlock"));
    }
  }

  #endregion
}
