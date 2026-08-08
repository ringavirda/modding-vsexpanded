using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace IronworkingExpanded.Tests;

/// <summary>
/// What a <b>firebox</b> hearth does with what is in its firebox: how much charge it reads, what it
/// refuses, and how much it wants before it will light. The reverberatory pair -
/// <see cref="BlockEntityPuddlingFurnace"/> and <see cref="BlockEntityHeatingFurnace"/> - had no
/// behavioural coverage of any kind before this file; everything either of them had was structural
/// (which branch it hangs off, which cells it claims, what it writes to the save tree).
/// <para>
/// These are the three shipped defects of <b>B8</b> (<c>docs/design/machines/puddling-furnace.md</c>
/// § Gotchas), asserted on <b>both</b> hearths rather than only on the puddling one. That is the point of
/// the split: the answers belong to <see cref="BlockEntityFireboxFurnace"/>, so a regression that reaches
/// one hearth reaches the other, and a test that named only the broken machine would go quiet the moment
/// someone "fixed" it by re-overriding on the leaf.
/// </para>
/// <para>
/// <b>Neither hearth is reachable in game.</b> The puddling layout has five filler cells no part
/// produces, so its structure never completes and <c>OnProductionTick</c> never runs (Open #1). The
/// charge hooks are therefore driven directly here rather than through a lit furnace, which is why these
/// read as unit tests of a seam rather than as scenarios.
/// </para>
/// <para>
/// Driving the hooks directly is a choice, not the only option: <c>StructureRig.Raise</c> synthesises a
/// stand-in for every empty footprint cell, so in the <em>harness</em> a hearth completes and its real
/// tick can be driven — see <see cref="FireboxTickTests"/>, which is what actually proves this branch's
/// tick reaches these hooks. The in-game unreachability does not bound what a test can stage.
/// </para>
/// </summary>
public class FireboxChargeTests
{
  #region Harness

  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>The two hearths, each stood up the way <c>ShaftColumnsTests</c> does - pointed at a real
  /// oriented block code so the block entity derives its own structure angle.</summary>
  private static IEnumerable<BlockEntityFireboxFurnace> Hearths()
  {
    var puddling = new BlockEntityPuddlingFurnace { Pos = Anchor.Copy() };
    OrientWithLayout(
      puddling,
      BlockPuddlingFurnaceCore.Definitions("iwex").Single(),
      "iwex:furnace-puddlingcore-tier1-n",
      "north"
    );
    yield return puddling;

    var heating = new BlockEntityHeatingFurnace { Pos = Anchor.Copy() };
    OrientWithLayout(
      heating,
      BlockHeatingFurnaceCore.Definitions("iwex").Single(),
      "iwex:furnace-heatingcore-tier1-n",
      "north"
    );
    yield return heating;
  }

  /// <summary>
  /// A firebox holding <paramref name="stacks"/>, in the shape the charge walk hands the hooks: one
  /// <see cref="BEBehaviorFirebox"/> bed per cell.
  /// <para>
  /// This was a list of vanilla <c>BlockEntityCoalPile</c>s until the firebox became a block of its own.
  /// The fixture is now built by <b>charging the bed through its own API</b> rather than by
  /// reflecting a stack into an inventory - so a fuel the firebox would refuse cannot be smuggled into a
  /// test's premise, which is the property the old fixture had no way to express.
  /// </para>
  /// <para>
  /// Built directly rather than through a standing structure because the hooks take an opaque handle and
  /// never touch the world - and because neither hearth can complete a structure to walk (see the class
  /// remarks).
  /// </para>
  /// </summary>
  private static object Firebox(TestWorld world, params ItemStack[] stacks)
  {
    var beds = new List<(BlockPos pos, BEBehaviorFirebox bed)>();
    for (int i = 0; i < stacks.Length; i++)
    {
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
  ) Read(BlockEntityFurnaceCore be, object firebox)
  {
    // Four args: the old fifth, `out string? rejectedFamily`, went with the burden family model.
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
  /// The defect this replaces: a firebox read the shaft's way counts only items that pass
  /// <c>IsChargeItem</c>, which on the core is prepared <b>burden</b> and nothing else. Vanilla coke is
  /// not burden, so a firebox full of coke scored <b>zero</b> and the furnace read as empty however much
  /// fuel was in it. A firebox reads its beds directly now, so there is nothing left to filter.
  /// <para>
  /// <c>IsChargeItem</c> also once admitted the <c>charge</c> material role, whose single grant
  /// was <c>iwex:blastmix</c> - both the role grant and the item are gone. The <b>shaft</b> branch does
  /// override <c>IsChargeItem</c> to admit the <c>fuel</c> role, because coke is charged as its own bands
  /// there; a firebox needs no such override, which is the point of this case.
  /// </para>
  /// </summary>
  [Fact]
  public void A_firebox_of_plain_coke_is_counted_rather_than_scored_at_zero()
  {
    var world = new TestWorld();
    Item coke = world.RegisterItem("game:coke");

    foreach (BlockEntityFireboxFurnace hearth in Hearths())
    {
      var read = Read(
        hearth,
        Firebox(world, new ItemStack(coke, 12), new ItemStack(coke, 4))
      );

      Assert.Equal(16, read.count);
      // ...and it reads as pure fuel, which is the honest input to the heat balance: nothing in a
      // firebox is being reduced, so there is no ore or flux fraction to report.
      Assert.Equal(1f, read.mix.FuelFrac);
      Assert.Equal(0f, read.mix.IronFrac);
    }
  }

  /// <summary>An empty firebox still reads zero - the raw-stack-size read must not turn "no fuel" into
  /// a count just because it stopped filtering.</summary>
  [Fact]
  public void An_empty_firebox_still_reads_zero()
  {
    var world = new TestWorld();

    foreach (BlockEntityFireboxFurnace hearth in Hearths())
    {
      Assert.Equal(0, Read(hearth, Firebox(world)).count);
      Assert.False(Read(hearth, Firebox(world)).isFull);
    }
  }

  #endregion

  #region B8, first cause - the threshold is the firebox's own capacity

  /// <summary>
  /// The firebox count is <b>derived from the drawing the machine is built from</b>, not hand-set beside
  /// it. A hand-set count is free to drift away from the cells a player can actually load, and a threshold
  /// drifting out of physical reach is the whole reason B8 existed.
  /// <para>
  /// The geometry was once two hand-declared <c>ShaftMin</c>/<c>ShaftMax</c> corners on the block
  /// entity, so a bare <c>new BlockEntityPuddlingFurnace()</c> answered this. It now comes off the layout's
  /// <c>CellRole.Firebox</c> marks, so the hearth has to be stood on its own blocktype - which is the point:
  /// the count and the cells are one statement rather than two that can disagree.
  /// </para>
  /// </summary>
  [Fact]
  public void The_firebox_cell_count_comes_from_the_geometry_the_machine_declares()
  {
    // The puddling drawing marks one `c` cell at (-5,1,0); the heating one marks two, (-5,1,0)/(-5,1,1).
    // Those are exactly the values the deleted `_fireboxMin`/`_fireboxMax` pairs held. Add a cell to
    // either layout and the threshold below moves with it, without anyone remembering to.
    List<BlockEntityFireboxFurnace> hearths = Hearths().ToList();
    Assert.Equal(1, Cells(hearths[0]));
    Assert.Equal(2, Cells(hearths[1]));
  }

  /// <summary>
  /// A firebox fires on what its <b>own</b> cells hold. The puddling furnace inherited the blast
  /// furnace's full-stack 320 and could not light at any temperature; handing it the cupola's 160
  /// instead only moved it from ten times out of reach to five, because a fixed constant is the wrong
  /// shape - it was sized for a shaft, inherited by a two-cell firebox and then by a one-cell one.
  /// </summary>
  [Fact]
  public void Each_hearth_fires_on_its_own_fireboxs_capacity_not_a_shared_constant()
  {
    var thresholds = new List<int>();

    foreach (BlockEntityFireboxFurnace hearth in Hearths())
    {
      // The relation: cells times the per-cell constant, with no offset and no floor. On its own this
      // states nothing about the amount - see the numbers below.
      Assert.Equal(
        Cells(hearth) * IwexValues.FireboxMixPerCell,
        Threshold(hearth)
      );
      // The two shaft constants this used to be compared against - `BlastMixRequiredToFire` (320) and
      // `CupolaMixRequiredToFire` (160), the one a firebox inherited twice - are deleted. There
      // is no longer a hand-picked total anywhere in the mod for a firebox to inherit, which is a stronger
      // guarantee than the inequality was: the defect is now unrepresentable rather than merely absent.
      // The relation above and the differing counts below are what carry the claim.
      thresholds.Add(Threshold(hearth));
    }

    // The assertion no constant can satisfy: the one-cell hearth and the two-cell one ask for
    // different amounts. Without it, `=> 24` on the branch would pass every line above for the reheat
    // furnace and put the puddling one back out of reach.
    Assert.Equal(2, thresholds.Count);
    Assert.NotEqual(thresholds[0], thresholds[1]);

    // The numbers, which nothing in this suite pinned. The relation asserted in the loop is the same
    // expression over the same two sources on both sides, so it holds for any per-cell value; and
    // FireboxMixPerCell appears elsewhere only as `int perCell = IwexValues.FireboxMixPerCell`, which
    // likewise builds its fixture out of the value under test.
    // FurnaceBranchGuards.NoFireboxAsksForMoreThanItsCellsCanHold bounds it above at cells * 16 (the
    // hard BlockEntityCoalPile.MaxStackSize, and the defect that guard exists for) and below only at
    // "> 0". So a default of 20 was caught and a default of 1 was not - a reverberatory hearth lighting
    // on a single lump of coke, with the whole suite green. These two literals are what closes that.
    Assert.Equal(12, IwexValues.FireboxMixPerCell);
    Assert.Equal(new[] { 12, 24 }, thresholds);
  }

  /// <summary>
  /// The threshold as the furnace actually applies it, rather than as a number it exposes: one unit
  /// under it the firebox is not full, and at it, it is.
  /// <para>
  /// Both fireboxes here are states the game can actually produce - one vanilla coal pile per cell,
  /// each inside <c>MaxStackSize</c>. The version of this test that ran against a fixed 160 had to build
  /// a single 160-unit pile, ten times what one cell can hold, so it read as reassurance about a state
  /// that could not exist.
  /// </para>
  /// </summary>
  [Fact]
  public void The_firebox_reads_full_at_its_own_threshold_and_not_one_unit_under()
  {
    var world = new TestWorld();
    Item coke = world.RegisterItem("game:coke");

    foreach (BlockEntityFireboxFurnace hearth in Hearths())
    {
      int perCell = IwexValues.FireboxMixPerCell;
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
  /// A hearth whose drawing marks <b>no</b> firebox at all counts zero cells - not one, and not the
  /// core's box.
  /// <para>
  /// This branch shipped with <b>no</b> test reader: every hearth in the mod draws a firebox, so
  /// <c>FireboxCellCount</c>'s <c>ShaftBox is null</c> arm was reachable only from a drawing nobody had
  /// written. Changing its answer from 0 to 1 left all 2287 tests green. The value matters because it
  /// multiplies straight into <see cref="BlockEntityFireboxFurnace.ChargeCapacityUnits"/>: a fallback of 1
  /// would invent a threshold for a machine with nowhere to put fuel.
  /// </para>
  /// </summary>
  [Fact]
  public void A_hearth_whose_drawing_marks_no_firebox_counts_no_cells()
  {
    BlockEntityFireboxFurnace hearth = NoFireboxHearth(out _);

    Assert.Null(ShaftBoxOf(hearth));
    Assert.Equal(0, Cells(hearth));
  }

  /// <summary>
  /// ...and a zero count is <b>inert rather than dangerous</b>, which is the claim
  /// <c>FireboxCellCount</c>'s own remark makes and the reason 0 is the honest answer. The threshold goes
  /// to zero, so "full" is trivially true - but the furnace still cannot light, because ignition needs
  /// beds and a box-less hearth collects none.
  /// <para>
  /// Deliberately a second test rather than three more lines on the one above. The two state different
  /// things - what the geometry counts, and that counting nothing is safe - so deleting either leaves the
  /// no-firebox arm pinned by the other.
  /// </para>
  /// </summary>
  [Fact]
  public void A_hearth_with_no_firebox_cannot_light_however_low_its_threshold_goes()
  {
    BlockEntityFireboxFurnace hearth = NoFireboxHearth(out StructureRig rig);

    // A real, full bed in the cell the drawing draws as fuel bed - the state that makes the claim worth
    // anything. It sits directly over the anchor, so a walk that invented a box there instead of skipping
    // would collect it, light the hearth and reach a threshold of 0 as "full".
    BlockPos cell = rig.Cell(0, 1, 0);
    var firebox = new BlockEntityFirebox { Pos = cell.Copy() };
    var bed = new BEBehaviorFirebox(firebox);
    bed.TryAdd(new ItemStack(new TestWorld().RegisterItem("game:coke"), 99), 99);
    ReflectionHelpers.SetField(firebox, "Behaviors", new List<BlockEntityBehavior> { bed });
    rig.Occupy(
      cell,
      TestBlocks.Configure(new Block(), "iwex:furnace-firebox-tier1-n", 900, ("side", "north")),
      firebox
    );

    Assert.Equal(0, Threshold(hearth));
    Assert.Empty(Beds(hearth));
    Assert.False(Ignites(hearth, Beds(hearth)));
  }

  /// <summary>
  /// A heating hearth stood on a drawing that marks its fuel bed with no <c>Firebox</c> role. The legend
  /// is the real firebox glyph, so the cells are genuinely fuel-shaped; what is missing is the
  /// <c>Role</c> call - the same construction <c>FurnaceRoleCellsTests</c> uses on the shaft branch.
  /// </summary>
  private static BlockEntityHeatingFurnace NoFireboxHearth(out StructureRig rig)
  {
    ExBlockDef def = ExBlockDef
      .Create("iwex", "furnace")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "iwex:furnace-heatingcore-*")
          .Legend('F', FireboxGlyph)
          .Layer(0, "C")
          .Layer(1, "F")
      );

    var hearth = new BlockEntityHeatingFurnace();
    rig = Stand(hearth, def, Anchor, "iwex:furnace-heatingcore-tier1", "north");
    return hearth;
  }

  /// <summary>The furnace's own charge walk - <c>CollectCharge</c> is <c>protected</c>. On this branch it
  /// returns the firebox beds, not vanilla coal piles.</summary>
  private static List<(BlockPos pos, BEBehaviorFirebox bed)> Beds(
    BlockEntityFurnaceCore be
  ) =>
    (List<(BlockPos, BEBehaviorFirebox)>)ReflectionHelpers.Invoke(be, "CollectCharge")!;

  private static bool Ignites(BlockEntityFurnaceCore be, object firebox) =>
    (bool)ReflectionHelpers.Invoke(be, "TryIgniteCharge", firebox)!;

  /// <summary>
  /// The assertion whose absence let a shaft's threshold ship on a hearth twice: a firebox may never
  /// ask for more fuel than its cells can physically hold. Delegated to
  /// <see cref="FurnaceBranchGuards"/> so it covers every firebox in every loaded assembly rather than
  /// the two named here - a third hearth arriving with a hand-picked number is exactly the failure mode.
  /// </summary>
  [Fact]
  public void No_firebox_asks_for_more_fuel_than_its_cells_can_hold() =>
    FurnaceBranchGuards.NoFireboxAsksForMoreThanItsCellsCanHold();

  #endregion

  #region B8's companion - a firebox refuses nothing

  /// <summary>
  /// The family gate is a burden-column idea: a shaft furnace converts one family and rejects the other,
  /// because what it burns is what it is reducing. A firebox is only ever making flame, so it declares no
  /// families at all and refuses nothing.
  /// <para>
  /// Asserted through <c>AcceptsCharge</c> with <b>remelt</b> burden - the cupola's family, the one thing
  /// an ore-gated furnace demonstrably turns down. Gating the puddling firebox to ore burden meant the
  /// only charge it would have accepted was blast-furnace burden, which is nonsense for a fuel bed.
  /// </para>
  /// </summary>
  // `A_firebox_declares_no_burden_family_so_it_refuses_none` was retired here, not rewritten -
  // and the first attempt to rewrite it is why this note exists.
  //
  // Its oracle was `AcceptedFamilies is null` plus "a firebox accepts burden of either family". The gate
  // is deleted and the second family with it, so the assertion had no subject. Re-expressing it as "a
  // firebox declares fuel chargeable" failed, and correctly: a firebox never consults `IsChargeItem` at
  // all - it reads its beds directly (see the case at the top of this file). Asserting a predicate the
  // branch does not use would have been a green test over a code path nothing reaches.
  //
  // What the case actually protected - a firebox refuses nothing - is asserted where the branch really
  // answers it, in `A_firebox_reports_no_rejected_charge_whatever_is_in_it` below.

  /// <summary>And the read reports no rejection either, whatever is in the box - the count, the
  /// rejected count and the family token all agree that a firebox has no wrong charge.
  /// <para>
  /// The fixture used to be <b>remelt burden</b>, the cupola's family and the one thing an ore-gated
  /// furnace demonstrably turns down. A bed refuses that outright now (it is not a fuel), so the premise
  /// moved to a real fuel and the claim moved with it: what is asserted is that the <em>read</em> never
  /// reports a family rejection, which has to hold or the furnace turns charge away at
  /// <c>AcceptsCharge</c> while the HUD says nothing was refused.
  /// </para></summary>
  [Fact]
  public void A_firebox_reports_no_rejected_charge_whatever_is_in_it()
  {
    var world = new TestWorld();
    var charcoal = new ItemStack(world.RegisterItem("game:charcoal"), 8);

    foreach (BlockEntityFireboxFurnace hearth in Hearths())
    {
      var read = Read(hearth, Firebox(world, charcoal));

      Assert.Equal(8, read.count);
      Assert.Equal(0, read.rejectedCount);
    }
  }

  /// <summary>
  /// <b>Where acceptance moved to.</b> A bed decides what it holds, so the four metallurgical fuels go
  /// in and lignite does not - and neither does anything that is not fuel at all. That is a property the
  /// coal-pile firebox could not have: vanilla's pile takes what vanilla's pile takes.
  /// </summary>
  [Theory]
  [InlineData("game:coke", true)]
  [InlineData("game:charcoal", true)]
  [InlineData("game:ore-bituminouscoal", true)]
  [InlineData("game:ore-anthracite", true)]
  // Low-rank, high-moisture, high-ash: it will not carry a metallurgical heat.
  [InlineData("game:ore-lignite", false)]
  [InlineData("iwex:remeltburden", false)]
  [InlineData("game:ingot-iron", false)]
  public void A_bed_takes_the_four_metallurgical_fuels_and_nothing_else(
    string code,
    bool accepted
  )
  {
    var world = new TestWorld();
    var stack = new ItemStack(world.RegisterItem(code), 4);

    var bed = new BEBehaviorFirebox(new BlockEntityFirebox { Pos = Anchor.Copy() });

    Assert.Equal(accepted, BEBehaviorFirebox.IsFuel(stack));
    Assert.Equal(accepted ? 4 : 0, bed.TryAdd(stack, 4));
  }

  /// <summary>
  /// <b>One fuel per bed.</b> The layer texture depicts what the player charged, so a second substance is
  /// refused until the first has burned down or been taken out - the same rule the tall hopper's tank
  /// enforces.
  /// </summary>
  [Fact]
  public void A_bed_holding_one_fuel_refuses_a_second_until_it_is_emptied()
  {
    var world = new TestWorld();
    var coke = new ItemStack(world.RegisterItem("game:coke"), 4);
    var charcoal = new ItemStack(world.RegisterItem("game:charcoal"), 4);

    var bed = new BEBehaviorFirebox(new BlockEntityFirebox { Pos = Anchor.Copy() });

    Assert.Equal(4, bed.TryAdd(coke, 4));
    Assert.Equal(0, bed.TryAdd(charcoal, 4));

    // ...and it is the fuel being gone that lifts the refusal, not the block being replaced: burning the
    // bed down clears the substance and the next charge may be anything.
    Assert.Equal(4, bed.Consume(4));
    Assert.Equal(4, bed.TryAdd(charcoal, 4));
  }

  /// <summary>
  /// A bed fills to its own six drawn courses and no further - the ceiling that replaced
  /// <c>BlockEntityCoalPile.MaxStackSize</c>, and the one the ignition threshold is now derived from.
  /// </summary>
  [Fact]
  public void A_bed_fills_to_its_drawn_courses_and_stops()
  {
    var world = new TestWorld();
    var coke = new ItemStack(world.RegisterItem("game:coke"), 999);

    var bed = new BEBehaviorFirebox(new BlockEntityFirebox { Pos = Anchor.Copy() });

    Assert.Equal(BEBehaviorFirebox.CellCapacity, bed.TryAdd(coke, 999));
    Assert.True(bed.IsFull);
    Assert.Equal(BEBehaviorFirebox.LayersPerCell, bed.LayerCount);
    Assert.Equal(0, bed.TryAdd(coke, 999));

    // The number the shape pins: six courses of two. Asserted as literals because every relation above
    // is the same expression over the same source on both sides and would hold for any capacity.
    Assert.Equal(6, BEBehaviorFirebox.LayersPerCell);
    Assert.Equal(2, BEBehaviorFirebox.UnitsPerLayer);
    Assert.Equal(12, BEBehaviorFirebox.CellCapacity);
  }

  /// <summary>
  /// A firebox is <b>not a one-way sink</b> - fuel comes back out a course at a time, as it does from a
  /// vanilla coal pile, and a part-emptied bed draws one course fewer.
  /// </summary>
  [Fact]
  public void Fuel_can_be_taken_back_out_a_course_at_a_time()
  {
    var world = new TestWorld();
    var coke = new ItemStack(world.RegisterItem("game:coke"), 999);

    var be = new BlockEntityFirebox { Pos = Anchor.Copy() };
    var bed = new BEBehaviorFirebox(be);
    bed.TryAdd(coke, 999);

    // Needs an Api to build the returned stack - the bed stores a code, not a resolved collectible, so
    // that it keeps drawing after the mod that owned its fuel is removed.
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
  /// A hearth pours nothing, so it inherits the core's product defaults rather than a shaft's pool. The
  /// puddling furnace carried a copy of the blast furnace's pool until this task: always 0, undrainable
  /// (its layout has no metal tap), and a furnace that ever did reach Melting would have filled it,
  /// tripped <c>LiquidCapacityReached</c> and snuffed itself.
  /// </summary>
  [Fact]
  public void Neither_hearth_pools_anything_so_neither_can_back_up_on_its_own_output()
  {
    foreach (BlockEntityFireboxFurnace hearth in Hearths())
    {
      Assert.False(
        (bool)ReflectionHelpers.GetProperty(hearth, "LiquidCapacityReached")!
      );
      Assert.Equal(
        0f,
        (float)ReflectionHelpers.GetProperty(hearth, "DrainedMetalUnits")!
      );
      Assert.Null(ReflectionHelpers.GetProperty(hearth, "SolidProductBlock"));
    }
  }

  #endregion
}
