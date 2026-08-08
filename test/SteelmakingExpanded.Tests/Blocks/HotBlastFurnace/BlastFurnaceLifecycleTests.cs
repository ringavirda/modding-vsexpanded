using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using IronworkingExpanded.Items;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The blast furnace's melting math, driven directly: the per-cycle conversion of hearth charge into
/// molten iron and slag (capacity-clamped) and the hearth charge accounting. These are the numbers the
/// firing/melting tick relies on and the gated <c>OnProductionTick</c> does not expose.
/// </summary>
public class BlastFurnaceLifecycleTests {
  private static TestWorld NewWorld() {
    var world = new TestWorld();
    world.RegisterItem("game:ingot-iron", 1500f);
    world.RegisterItem("iwex:slag");
    return world;
  }

  private static BlockEntityBlastFurnaceHot Furnace(TestWorld world) {
    var be = new BlockEntityBlastFurnaceHot {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "smex:blastfurnacecore-n",
        1,
        ("side", "north")
      ),
    };
    world.Attach(be);
    // Attach the shipped layout to the placed block before the rotation update reads it: the crucible
    // floor is the drawing's own CellRole.Pool, so a furnace with no attributes has no crucible to freeze
    // onto. The structure is not raised - these tests drive the melt directly and never need it complete.
    StructureRig.Around(
      world,
      be,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single()
    );
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
    ReflectionHelpers.Invoke(be, "CacheAttributes");
    return be;
  }

  // Charge is held in ChargeColumns as pure data, so the helpers below charge a column and read it back
  // rather than constructing piles. The charge temperature is a single fixed value because
  // ChargeColumn.Push merges only within 1 °C: a varying one shatters a load into slivers.

  private const float ChargeTemp = 20f;

  /// <summary>Lays <paramref name="units"/> of unstamped burden into the column at structure-local
  /// <c>(x, z)</c> - charge carrying no composition, which the furnace burns as the standard grade. This is
  /// the legacy <c>iwex:blastmix</c> shape, whose reading is pinned by
  /// <c>Legacy_blast_mix_reads_as_a_standard_grade_burden</c>.</summary>
  private static ChargeColumn Blastmix(
    BlockEntityBlastFurnaceHot be,
    int localX,
    int localZ,
    int units
  ) => Charge(be, localX, localZ, "burden", units, null);

  /// <summary>Lays prepared burden of a known composition into the column at <c>(x, z)</c>.</summary>
  private static ChargeColumn BurdenIn(
    BlockEntityBlastFurnaceHot be,
    int localX,
    int localZ,
    int units,
    BurdenMix mix
  ) => Charge(be, localX, localZ, "burden", units, mix);

  /// <summary>The cupola's remelt burden - the wrong family for a blast furnace.</summary>
  private static ChargeColumn RemeltIn(
    BlockEntityBlastFurnaceHot be,
    int localX,
    int localZ,
    int units,
    BurdenMix mix
  ) => Charge(be, localX, localZ, "remeltburden", units, mix);

  private static ChargeColumn Charge(
    BlockEntityBlastFurnaceHot be,
    int localX,
    int localZ,
    string itemPath,
    int units,
    BurdenMix? mix
  ) {
    ChargeColumn column = be.ChargeColumnAt(localX, localZ)!;
    column.Push($"iwex:{itemPath}", units, ChargeTemp, mix ?? default);
    return column;
  }

  /// <summary>The handle the three charge hooks take - the furnace's own column set.</summary>
  private static object Handle(BlockEntityBlastFurnaceHot be) =>
    be.ShaftColumns;

  /// <summary>
  /// Lays burden that has already arrived at the raceway hot - above iron's melt line, the only burden the
  /// melt will take. The melt condition is per band and reads <c>ChargeSegment.Temperature</c>, the heat the
  /// band carried down the shaft rather than the furnace's own, so a direct melt test must charge hot.
  /// </summary>
  private static ChargeColumn HotBurdenIn(
    BlockEntityBlastFurnaceHot be,
    int localX,
    int localZ,
    int units,
    BurdenMix? mix = null
  ) {
    ChargeColumn column = be.ChargeColumnAt(localX, localZ)!;
    column.Push(
      "iwex:burden",
      units,
      IwexValues.BfIronMeltingPoint + 100f,
      mix ?? default
    );
    return column;
  }

  /// <summary>
  /// Lays a round into the column at <c>(x, z)</c> - a coke course, then a burden course - at the given
  /// coke fraction. Carbon comes from fuel bands and nowhere else, so the coke fraction is a fact about the
  /// geometry charged; a burden's <c>Mix.Fuel</c> is a grade and display field with no mechanical effect.
  /// </summary>
  private static ChargeColumn RoundIn(
    BlockEntityBlastFurnaceHot be,
    int localX,
    int localZ,
    int units,
    float cokeFrac
  ) {
    ChargeColumn column = be.ChargeColumnAt(localX, localZ)!;
    int coke = (int)System.Math.Round(units * cokeFrac);
    column.Push("game:coke", coke, ChargeTemp, default);
    column.Push(
      "iwex:burden",
      units - coke,
      ChargeTemp,
      new BurdenMix(
        1f - IwexValues.BfDefaultFluxFrac,
        IwexValues.BfDefaultFluxFrac,
        0f
      )
    );
    return column;
  }

  /// <summary>Drives one melt directly - <c>ConsumeForMelting</c> on the furnace's own columns.</summary>
  private static void Melt(BlockEntityBlastFurnaceHot be, int units) =>
    ReflectionHelpers.Invoke(be, "ConsumeForMelting", Handle(be), units);

  /// <summary>Everything going out does, minus deciding that it went out - the residue path a derived
  /// branch reaches when <c>DeriveState</c> reads <c>Idle</c>. Used instead of <c>Extinguish()</c>, which
  /// belongs to the firebox branch and assigns a state; a shaft's state is a read.</summary>
  private static void Shutdown(BlockEntityBlastFurnaceHot be) =>
    ReflectionHelpers.Invoke(be, "Shutdown");

  private static int Units(ChargeColumn column) => column.TotalUnits;

  /// <summary>What stands in the <paramref name="blockIndex"/>-th charge block of
  /// <paramref name="column"/> - the band covering that block's base, which is the granularity burn-out
  /// decides salvage at. <c>default</c> when the column does not reach that block.</summary>
  private static ChargeSegment BandIn(
    BlockEntityBlastFurnaceHot be,
    ChargeColumn column,
    int blockIndex
  ) {
    int low = blockIndex * be.ChargeUnitsPerBlock;
    int at = 0;
    foreach (ChargeSegment segment in column.Segments) {
      int end = at + segment.Units;
      if (end > low)
        return segment;
      at = end;
    }
    return default;
  }

  private static float Iron(BlockEntityBlastFurnaceHot be) =>
    (float)ReflectionHelpers.GetField(be, "_moltenIron")!;

  private static float Slag(BlockEntityBlastFurnaceHot be) =>
    (float)ReflectionHelpers.GetField(be, "_moltenSlag")!;

  #region ConsumeForMelting

  /// <summary>
  /// Yield is a property of the band: <c>BfIronPerOreUnit</c> against the burden's own ore share, so a rich
  /// burden renders more iron than a lean one and roasting changes what a charge yields without changing
  /// what a pile holds.
  /// </summary>
  [Fact]
  public void A_melt_cycle_renders_burden_into_molten_iron_and_slag() {
    var world = NewWorld();
    var be = Furnace(world);
    var mix = new BurdenMix(65f, 5f, 30f);
    ChargeColumn column = HotBurdenIn(be, 0, 0, 100, mix);

    Melt(be, 16);

    Assert.Equal(84, Units(column)); // 16 units of burden consumed
    Assert.Equal(16 * IwexValues.BfIronPerOreUnit * mix.IronFrac, Iron(be), 2);
    Assert.Equal(16 * IwexValues.BfSlagPerOreUnit * mix.IronFrac, Slag(be), 2);
  }

  /// <summary>
  /// The melt takes burden and steps over coke: coke leaves a column by burning and by nothing else. A melt
  /// that took the lowest N units whatever they were made of would destroy coke at the melt rate on top of
  /// the carbon the raceway burns, and campaign length would stop tracking how much coke was charged.
  /// </summary>
  [Fact]
  public void A_melt_steps_over_the_coke_at_the_raceway_and_takes_only_burden() {
    var world = NewWorld();
    var be = Furnace(world);
    ChargeColumn column = be.ChargeColumnAt(0, 0)!;
    float hot = IwexValues.BfIronMeltingPoint + 100f;
    column.Push("game:coke", 10, hot, default); // the round's coke course, at the raceway
    column.Push("iwex:burden", 90, hot, new BurdenMix(65f, 5f, 30f));

    Melt(be, 16);

    Assert.Equal(84, Units(column)); // 16 gone, all of it burden
    Assert.Equal("game:coke", column.Segments[0].Material);
    Assert.Equal(10, column.Segments[0].Units); // every unit of it still there
    Assert.Equal(74, column.Segments[1].Units);
  }

  /// <summary>
  /// The charcoal counterpart of <c>A_melt_steps_over_the_coke_at_the_raceway_and_takes_only_burden</c>,
  /// whose body lays coke only and so passes against a melt whose skip is
  /// <c>segment.Material == "game:coke"</c>. Charcoal already lays twice the volume for the same heat
  /// (<c>BlockEntityFurnaceCore.CarbonPerUnit</c>), so a melt that ate the fuel course would destroy it at
  /// the melt rate on top of the burn.
  /// </summary>
  [Fact]
  public void A_melt_steps_over_a_CHARCOAL_band_exactly_as_it_steps_over_coke() {
    var world = NewWorld();
    var be = Furnace(world);
    var mix = new BurdenMix(65f, 5f, 30f);
    ChargeColumn column = be.ChargeColumnAt(0, 0)!;
    float hot = IwexValues.BfIronMeltingPoint + 100f;
    column.Push("game:charcoal", 10, hot, default); // the round's fuel course, at the raceway
    column.Push("iwex:burden", 90, hot, mix);

    Melt(be, 16);

    Assert.Equal(84, Units(column)); // 16 gone, all of it burden
    Assert.Equal("game:charcoal", column.Segments[0].Material);
    Assert.Equal(10, column.Segments[0].Units); // every unit of it still there
    Assert.Equal(74, column.Segments[1].Units);

    // The same claim stated in metal rather than in bands: a fuel band carries `default` mix, so a melt
    // that consumed the ten charcoal units would render nothing for them and the pool would come up
    // exactly ten units of burden short.
    Assert.Equal(16 * IwexValues.BfIronPerOreUnit * mix.IronFrac, Iron(be), 2);
  }

  [Fact]
  public void Molten_output_is_clamped_at_the_furnace_capacity() {
    var world = NewWorld();
    var be = Furnace(world);
    HotBurdenIn(be, 0, 0, 100, new BurdenMix(65f, 5f, 30f));
    // Already near the iron ceiling (2400 default).
    ReflectionHelpers.SetField(
      be,
      "_moltenIron",
      IwexValues.BfMaxMoltenIron - 10f
    );

    Melt(be, 16);

    Assert.Equal(IwexValues.BfMaxMoltenIron, Iron(be), 1); // capped, not 2400 + a cycle
  }

  /// <summary>
  /// A charge column has no collapse in it. The raceway is at the bottom, so that is where descent takes
  /// from, and the empty space appears at the top, where the hopper drips.
  /// </summary>
  [Fact]
  public void Melting_draws_from_the_BOTTOM_of_a_column_and_the_gap_appears_on_top() {
    var world = NewWorld();
    var be = Furnace(world);
    var lean = new BurdenMix(90f, 5f, 5f);
    var rich = new BurdenMix(60f, 5f, 35f);

    // Two courses in one column: lean at the raceway, rich laid on top of it.
    ChargeColumn column = HotBurdenIn(be, 0, 0, 50, lean);
    column.Push("iwex:burden", 50, IwexValues.BfIronMeltingPoint + 100f, rich);

    Melt(be, 16);

    Assert.Equal(84, Units(column));
    // The lean course is what was eaten, and it is still at the bottom - shorter, not reordered.
    Assert.Equal(lean, column.Segments[0].Mix);
    Assert.Equal(34, column.Segments[0].Units);
    // ...and the rich course above it is untouched, which is what "the gap appears on top" means.
    Assert.Equal(rich, column.Segments[1].Mix);
    Assert.Equal(50, column.Segments[1].Units);
  }

  [Fact]
  public void Unequal_columns_descend_together_rather_than_one_draining_first() {
    // The whole-furnace per-cycle figure is spread over the columns that still hold charge, so the
    // raceway stays fed across the width of the shaft instead of one column emptying under one tuyere
    // while the rest stand full.
    var world = NewWorld();
    var be = Furnace(world);
    ChargeColumn tall = HotBurdenIn(be, 0, 0, 100);
    ChargeColumn shallow = HotBurdenIn(be, 1, 0, 20);

    Melt(be, 16);

    Assert.Equal(120 - 16, Units(tall) + Units(shallow));
    Assert.True(
      Units(shallow) > 0,
      "the shallow column is not drained to nothing first"
    );
    Assert.True(
      Units(tall) < 100 && Units(shallow) < 20,
      "both columns give up charge in the same cycle"
    );
  }

  #endregion

  #region Blast-mix accounting

  /// <summary>Runs the charge scan, handing back its isFull and mix out-parameters. The rejected count the
  /// family gate reads is exercised in the gate tests below.</summary>
  private static int CountCharge(
    BlockEntityBlastFurnaceHot be,
    out bool isFull,
    out BurdenMix mix
  ) {
    object?[] args = { Handle(be), false, default(BurdenMix), 0 };
    int count = (int)ReflectionHelpers.Invoke(be, "ReadChargeMix", args)!;
    isFull = (bool)args[1]!;
    mix = (BurdenMix)args[2]!;
    return count;
  }

  /// <summary>
  /// Ignition is positional and pneumatic, so no fire threshold gates it: "full" means loaded to capacity,
  /// which is the furnace's own geometry.
  /// </summary>
  [Fact]
  public void Mix_count_totals_the_hearth_and_reports_full_at_its_own_capacity() {
    var world = NewWorld();
    var be = Furnace(world);
    int capacity = (int)
      ReflectionHelpers.GetProperty(be, "ChargeCapacityUnits")!;

    // The premise: a capacity of 0 or 1 would make every assertion below trivially true.
    Assert.True(
      capacity > 1,
      $"capacity should be real geometry; it was {capacity}"
    );

    Blastmix(be, 0, 0, capacity);

    int count = CountCharge(be, out bool isFull, out _);

    Assert.Equal(capacity, count);
    Assert.True(isFull, "a hearth loaded to capacity should read as full");
  }

  [Fact]
  public void A_thin_charge_does_not_read_as_full() {
    var world = NewWorld();
    var be = Furnace(world);
    Blastmix(be, 0, 0, 10);

    int count = CountCharge(be, out bool isFull, out _);

    Assert.Equal(10, count);
    Assert.False(isFull);
  }

  /// <summary>
  /// A burden's stamped coke is not carbon. Coke is laid as its own bands, so counting the stamp as well
  /// double-counts: 9 units of coke under 23 units of 30 %-stamped burden would read 15.9 of 32, burning at
  /// about 50 % coke on a charge laid at 30 % and running ~70 °C hotter than the grade advertises. A shaft
  /// holding stamped burden and no coke bands therefore reads 0 % carbon and chills.
  /// </summary>
  [Fact]
  public void A_stamped_burden_with_no_coke_bands_reads_as_no_carbon_at_all() {
    var world = NewWorld();
    var be = Furnace(world);
    BurdenIn(be, 0, 0, 100, new BurdenMix(65f, 5f, 30f));

    CountCharge(be, out _, out BurdenMix mix);

    Assert.Equal(0f, mix.FuelFrac, 3);

    // The control, and it carries the claim: the same total at the same grade, laid as a round, reads 30 %.
    var rounded = Furnace(NewWorld());
    RoundIn(rounded, 0, 0, 100, 0.30f);
    CountCharge(rounded, out _, out BurdenMix roundMix);
    Assert.Equal(0.30f, roundMix.FuelFrac, 3);
  }

  [Fact]
  public void Legacy_blast_mix_reads_as_a_standard_grade_burden() {
    // Unstamped legacy charge keeps its ore and flux shares, so an old world's column still grades as
    // standard rather than reading as a flux shortfall. It must not read as carbon either: the shaft's fuel
    // test is the `fuel` material role, not "anything that is not burden", and a `!Burden.IsAnyCode` test
    // would put an old world's whole shaft at a coke fraction of 1.00.
    var world = NewWorld();
    var be = Furnace(world);
    Blastmix(be, 0, 0, 100);

    CountCharge(be, out _, out BurdenMix mix);

    Assert.Equal(0f, mix.FuelFrac, 3); // no coke bands, so no carbon - see the case above

    // The ore and flux come back at their shipped shares as raw amounts, but the fractions renormalise
    // over the two of them (0.05 of 0.80 is 6.25 %) because the fuel share is not contributed.
    Assert.Equal(
      IwexValues.BfDefaultFluxFrac / (1f - IwexValues.BfDefaultFuelFrac),
      mix.FluxFrac,
      3
    );
    Assert.True(
      mix.IronFrac > 0f,
      "the ore share survives, which is what makes it salvageable"
    );
  }

  [Fact]
  public void A_mixed_column_reads_the_volume_weighted_average_coke_ratio() {
    var world = NewWorld();
    var be = Furnace(world);
    RoundIn(be, 0, 0, 300, 0.35f);
    RoundIn(be, 1, 0, 100, 0.05f);

    CountCharge(be, out _, out BurdenMix mix);

    // 300 units at 35% + 100 at 5% = 27.5%, not the 20% a naive per-column mean would give.
    Assert.Equal(0.275f, mix.FuelFrac, 3);
  }

  [Fact]
  public void A_coke_band_is_carbon_and_the_column_reads_its_own_coke_fraction() {
    // The two-stream split, read end to end. Coke is charged as its own bands rather than mixed into the
    // burden's stamp, so the coke fraction the heat balance and the blast-pressure demand read comes off
    // the geometry of the charge.
    var world = NewWorld();
    var be = Furnace(world);
    // A round: 25 units of coke at the raceway, 75 of carbon-free burden above it.
    // `"coke"`, not `"game:coke"` - a segment stores `Code.ToShortString()`, which drops the implicit
    // `game:` domain, and every code gate reads it back through an AssetLocation that puts it back.
    ChargeColumn column = be.ChargeColumnAt(0, 0)!;
    column.Push("coke", 25, ChargeTemp, default);
    column.Push("iwex:burden", 75, ChargeTemp, new BurdenMix(95f, 5f, 0f));

    int total = CountCharge(be, out _, out BurdenMix mix);

    Assert.Equal(100, total);
    Assert.Equal(0.25f, mix.FuelFrac, 3); // 25 units of pure carbon out of 100
    Assert.Equal(0.7125f, mix.IronFrac, 3); // 75 x 0.95, unit-weighted across the whole shaft
  }

  /// <summary>
  /// A charcoal band is carbon at half a coke band's weight per unit, and the column reads it weighted.
  /// Volume is unchanged - a charcoal band occupies the column a coke band does - while 25 units of
  /// charcoal contribute 12.5 carbon, so the shaft reads 14.3 % carbon on a charge laid at 25 % by volume.
  /// An <c>Accumulate</c> spending <c>fuel += units</c> would answer 0.25 here. <c>"charcoal"</c>, not
  /// <c>"game:charcoal"</c>: a segment stores <c>Code.ToShortString()</c>, the form that lands in a save.
  /// </summary>
  [Fact]
  public void A_charcoal_band_is_carbon_at_HALF_a_coke_bands_weight_per_unit() {
    var world = NewWorld();
    var be = Furnace(world);
    // The same round as the coke case, band for band: 25 units of fuel at the raceway, 75 of carbon-free
    // burden above it. Only the fuel differs, so any difference in the reading is the price and nothing else.
    ChargeColumn column = be.ChargeColumnAt(0, 0)!;
    column.Push("charcoal", 25, ChargeTemp, default);
    column.Push("iwex:burden", 75, ChargeTemp, new BurdenMix(95f, 5f, 0f));

    int total = CountCharge(be, out _, out BurdenMix mix);

    Assert.Equal(100, total); // volume is volume - a charcoal band fills the column a coke band would
    // 25 x 0.5 = 12.5 carbon against 71.25 iron + 3.75 flux, so the shares renormalise over 87.5 rather
    // than over 100. Written as the division that produces them, so retuning the ratio moves the test.
    Assert.Equal(12.5f / 87.5f, mix.FuelFrac, 3);
    Assert.Equal(71.25f / 87.5f, mix.IronFrac, 3);

    // The control: the same volume of the same geometry in coke reads 25 %. Without it this passes just as
    // well on a furnace that reads no carbon from fuel bands at all.
    var cokeFired = Furnace(NewWorld());
    ChargeColumn cokeColumn = cokeFired.ChargeColumnAt(0, 0)!;
    cokeColumn.Push("coke", 25, ChargeTemp, default);
    cokeColumn.Push("iwex:burden", 75, ChargeTemp, new BurdenMix(95f, 5f, 0f));
    Assert.Equal(100, CountCharge(cokeFired, out _, out BurdenMix cokeMix));
    Assert.Equal(0.25f, cokeMix.FuelFrac, 3);
    Assert.True(
      cokeMix.FuelFrac > mix.FuelFrac,
      "the same column laid in coke must read as the richer charge, or the fuels are not priced apart"
    );
  }

  #endregion

  #region Extinguish residue

  /// <summary>
  /// World cells of the crucible floor, where the molten pool freezes - the layout's own
  /// <c>CellRole.Pool</c> cells, which is where the furnace itself reads them from.
  /// </summary>
  private static BlockPos[] BottomLayer(BlockEntityBlastFurnaceHot be) =>
    [.. be.PoolCells];

  [Fact]
  public void Extinguishing_a_melt_solidifies_the_iron_across_the_bottom_layer() {
    var world = NewWorld();
    // Give the block an entity class and factory so SetBlock spawns a real BlockEntityHearthMetal the way
    // the engine would. Without it the nugget count has nothing to be stamped onto and the even-split half
    // of the behaviour goes unasserted.
    Block iron = TestBlocks.Configure(
      new Block(),
      "iwex:hearthmetal-pigiron",
      700
    );
    iron.EntityClass = "hearthmetal";
    world.RegisterBlockEntityFactory(
      "hearthmetal",
      () => new BlockEntityHearthMetal()
    );
    world.Register(iron);

    var be = Furnace(world); // no charge piles -> the burnout walk is a no-op
    ReflectionHelpers.SetField(be, "_moltenIron", 50f);

    Shutdown(be);

    // Idle because a furnace that has never ticked is idle, not because Shutdown set it: Shutdown does the
    // sound, the reset to ambient, the residue and the counters, but on a derived branch the label is a
    // read, and what decides it is `DeriveState` finding no carbon at the raceway.
    Assert.Equal(FurnaceState.Idle, be.State);
    Assert.Equal(0f, Iron(be), 3); // the molten pool is gone

    // The pool freezes onto every free cell of the hearth floor, with the nuggets split evenly.
    BlockPos[] floor = BottomLayer(be);
    Assert.NotEmpty(floor);
    Assert.All(
      floor,
      p => Assert.Equal(iron.BlockId, world.GetBlock(p).BlockId)
    );

    int expectedTotal = (int)(50f / IwexValues.BfUnitsPerSolidNugget);
    Assert.Equal(
      expectedTotal,
      floor.Sum(p =>
        ((BlockEntityHearthMetal)world.GetBlockEntity(p)!).MetalCount
      )
    );
  }

  [Fact]
  public void Extinguishing_burns_the_burden_out_by_height_instead_of_slagging_it() {
    var world = NewWorld();
    // Well clear of the coal piles' ids, which are derived from their Y.
    Block slag = TestBlocks.Configure(new Block(), "iwex:slag", 701);
    world.Register(slag);
    var be = Furnace(world);

    // One column, charged to its own ceiling so it stands from the hearth floor to the top of the shaft.
    // Burn-out splits a column per block, so the gradient is read out of one column at two indices.
    var mix = new BurdenMix(0.75f, 0.05f, 0.20f);
    BlockPos bottom = (BlockPos)
      ReflectionHelpers.Invoke(be, "GetGlobalPos", 0, 1, 0)!;
    BlockPos top = (BlockPos)
      ReflectionHelpers.Invoke(be, "GetGlobalPos", 0, 5, 0)!;
    ChargeColumn column = BurdenIn(be, 0, 0, be.ColumnCapacity(0, 0), mix);
    int blocks = be.ColumnCapacity(0, 0) / be.ChargeUnitsPerBlock;

    Shutdown(be);

    // (a) The burden is still burden. Nothing on this path makes slag - a furnace going out is a
    // setback, not the loss of the whole charge.
    Assert.NotEqual(slag.BlockId, world.GetBlock(bottom).BlockId);
    Assert.NotEqual(slag.BlockId, world.GetBlock(top).BlockId);
    Assert.Equal(be.ColumnCapacity(0, 0), Units(column));

    BurdenMix low = BandIn(be, column, 0).Mix;
    BurdenMix high = BandIn(be, column, blocks - 1).Mix;

    // (b) Coke burns out by height: the bottom course sat on the tuyeres, the top one never saw blast.
    Assert.Equal(0.20f * IwexValues.BfBurnoutFuelRetainedBottom, low.Fuel, 4);
    Assert.Equal(0.20f * IwexValues.BfBurnoutFuelRetainedTop, high.Fuel, 4);
    Assert.True(high.Fuel > low.Fuel);

    // (c) Iron and flux are preserved verbatim, so the salvage can be re-coked and charged again.
    foreach (BurdenMix m in new[] { low, high }) {
      Assert.Equal(0.75f, m.Iron, 4);
      Assert.Equal(0.05f, m.Flux, 4);
    }

    // (d) The grade is invariant under stripping the coke: burden is graded on flux, which burning the coke
    // out cannot alter, so the salvage still reads as the grade it was mixed at.
    Assert.Equal(Burden.ProfileLangKey(mix), Burden.ProfileLangKey(low));
  }

  [Fact]
  public void A_second_extinguish_burns_the_already_spent_burden_out_no_further() {
    // Dirty-precondition pass: re-lighting and losing a furnace on salvaged burden must not keep
    // eating iron and flux, and must not underflow the fuel it already stripped.
    var world = NewWorld();
    var be = Furnace(world);
    ChargeColumn column = BurdenIn(
      be,
      0,
      0,
      100,
      new BurdenMix(0.75f, 0.05f, 0.20f)
    );

    Shutdown(be);
    BurdenMix afterFirst = BandIn(be, column, 0).Mix;

    Shutdown(be);
    BurdenMix afterSecond = BandIn(be, column, 0).Mix;

    Assert.Equal(afterFirst.Iron, afterSecond.Iron, 4);
    Assert.Equal(afterFirst.Flux, afterSecond.Flux, 4);
    Assert.True(afterSecond.Fuel >= 0f);
    // The charge itself is not eaten a second time either: burn-out rewrites composition rather than
    // consuming burden, and a rewrite that dropped units would show up here and nowhere else.
    Assert.Equal(100, Units(column));
  }

  // The three helpers below serve the fuel-band burn-out case only. They lay real rounds - a fuel course
  // then a burden course, one round per charge block - because `ChargeColumn.Rewrite` replays through
  // Push's merge rule, so two adjacent fuel pieces fold into one segment and the gradient is summed away.

  /// <summary>The units a fuel course carries in each round below - a quarter of a charge block.</summary>
  private static int FuelPerRound(BlockEntityBlastFurnaceHot be) =>
    System.Math.Max(1, be.ChargeUnitsPerBlock / 4);

  /// <summary>Fills every column with rounds of <paramref name="fuelCode"/> - one round per charge block,
  /// so round <c>i</c> lands inside block <c>i</c> and burn-out's per-block split has nothing to straddle.
  /// Every column, because the ignition gate is positional.</summary>
  private static void FuelRoundsEverywhere(
    BlockEntityBlastFurnaceHot be,
    string fuelCode,
    BurdenMix mix
  ) {
    int perBlock = be.ChargeUnitsPerBlock;
    int fuel = FuelPerRound(be);
    foreach (var (x, z) in be.ShaftColumns.Keys) {
      ChargeColumn column = be.ChargeColumnAt(x, z)!;
      int rounds = be.ColumnCapacity(x, z) / perBlock;
      for (int i = 0; i < rounds; i++) {
        column.Push(fuelCode, fuel, ChargeTemp, default);
        column.Push("iwex:burden", perBlock - fuel, ChargeTemp, mix);
      }
    }
  }

  /// <summary>The units standing in each fuel band of a column, raceway end first - the gradient burn-out
  /// leaves, read through the production predicate rather than against a code literal.</summary>
  private static List<int> FuelBands(ChargeColumn column) {
    var units = new List<int>();
    foreach (ChargeSegment segment in column.Segments)
      if (BlockEntityFurnaceCore.IsFuelCode(segment.Material))
        units.Add(segment.Units);
    return units;
  }

  private static bool Ignites(BlockEntityBlastFurnaceHot be) =>
    (bool)ReflectionHelpers.Invoke(be, "TryIgniteCharge", Handle(be))!;

  /// <summary>
  /// Burn-out's height gradient is a fact about the shaft, not about which fuel was laid in it, and
  /// <c>BfBurnoutFuelRetainedBottom</c> is 0 for charcoal exactly as for coke. That bottom figure is what
  /// stops a dead furnace re-deriving itself alight: a shaft has no "was lit" flag, only the absence of
  /// carbon at its raceway (<c>BlockEntityShaftFurnace.DeriveState</c>). <c>BurnOutCharge</c> recognises a
  /// fuel band through <c>IsFuelCode</c>; a coke literal would send a charcoal band down the legacy-burden
  /// branch, which keeps every unit and leaves the raceway readable as carbon-bearing. Asserted band for
  /// band against an identically charged coke furnace, with the one literal - the top course - tied to
  /// <c>BfBurnoutFuelRetainedTop</c>.
  /// </summary>
  [Fact]
  public void Burn_out_retains_a_charcoal_band_on_the_same_height_gradient_as_coke() {
    var mix = new BurdenMix(0.75f, 0.05f, 0.20f);

    var charcoalFired = Furnace(NewWorld());
    FuelRoundsEverywhere(charcoalFired, "game:charcoal", mix);
    var cokeFired = Furnace(NewWorld());
    FuelRoundsEverywhere(cokeFired, "game:coke", mix);

    // The premise: a charcoal course at the raceway lights a furnace. Without it everything below holds
    // just as well on a shaft that could never catch on charcoal at all.
    Assert.True(
      Ignites(charcoalFired),
      "a complete raceway course of charcoal must light before burn-out"
    );

    ChargeColumn charcoal = charcoalFired.ChargeColumnAt(0, 0)!;
    ChargeColumn coke = cokeFired.ChargeColumnAt(0, 0)!;
    int charged = FuelPerRound(charcoalFired);
    Assert.True(
      FuelBands(charcoal).Count > 1,
      "the scene needs a column with two ends to it"
    );

    Shutdown(charcoalFired);
    Shutdown(cokeFired);

    List<int> left = FuelBands(charcoal);

    // (a) The same curve, band for band. A retention that keyed off the fuel - or a fuel test that missed
    // charcoal entirely, leaving its bands whole - diverges here on the first band.
    Assert.Equal(FuelBands(coke), left);
    Assert.NotEmpty(left);

    // (b) It ascends, and the top course keeps exactly BfBurnoutFuelRetainedTop of what was charged: the
    // blast burned hardest at the tuyeres and never reached the stockline.
    Assert.True(
      left[^1] > left[0],
      $"the top of the shaft should keep more fuel than the bottom; {left[^1]} vs {left[0]}"
    );
    Assert.Equal(
      (int)(charged * IwexValues.BfBurnoutFuelRetainedTop),
      left[^1]
    );

    // (c) ...and the bottom keeps none - BfBurnoutFuelRetainedBottom is 0 for charcoal too, so the band at
    // the raceway is burden and the ignition gate cannot re-arm off the furnace's own salvage.
    Assert.False(
      BlockEntityFurnaceCore.IsFuelCode(charcoal.Segments[0].Material),
      "burnt-out charge must leave no fuel band in front of a tuyere"
    );
    Assert.False(
      Ignites(charcoalFired),
      "a charcoal furnace that went out must not re-derive itself alight off its own salvage"
    );

    // The burden between the courses is still real salvage, so going out is a setback rather than the loss
    // of the charge - the same promise the coke path makes.
    ChargeSegment burden = charcoal.Segments[0];
    Assert.Equal(0.75f, burden.Mix.Iron, 4);
    Assert.Equal(0.05f, burden.Mix.Flux, 4);
  }

  #endregion

  #region Burden family gate

  // The blast furnace burns ore burden only. Remelt burden (the cupola's charge) counts toward the burn, so
  // a mis-loaded shaft still lights and burns out, but is tallied as rejected, which blocks conversion.
  // These pin the charge-scan half; the tick-level half is in BlastFurnaceScenarioTests.

  /// <summary>Runs the charge scan, surfacing the rejected count too.
  /// <paramref name="rejectedFamily"/> is always null: <c>ReadChargeMix</c> carries no family token, and
  /// the cases below assert its absence.</summary>
  private static int CountChargeFull(
    BlockEntityBlastFurnaceHot be,
    out int rejectedCount,
    out string? rejectedFamily
  ) {
    object?[] args = { Handle(be), false, default(BurdenMix), 0 };
    int count = (int)ReflectionHelpers.Invoke(be, "ReadChargeMix", args)!;
    rejectedCount = (int)args[3]!;
    rejectedFamily = null;
    return count;
  }

  [Fact]
  public void Remelt_burden_is_counted_but_rejected_as_the_wrong_family() {
    var world = NewWorld();
    var be = Furnace(world);
    RemeltIn(be, 0, 0, 100, new BurdenMix(60f, 5f, 35f));

    int total = CountChargeFull(be, out int rejected, out string? family);

    Assert.Equal(100, total); // counted blind to acceptance, so the shaft still lights and burns
    Assert.Equal(100, rejected); // ...but not one unit of it will convert
    Assert.Null(family); // the family token is gone; the count is the whole of the answer now
  }

  [Fact]
  public void Ore_burden_is_accepted_with_no_rejected_charge() {
    var world = NewWorld();
    var be = Furnace(world);
    BurdenIn(be, 0, 0, 100, new BurdenMix(75f, 5f, 20f));

    CountChargeFull(be, out int rejected, out string? family);

    Assert.Equal(0, rejected);
    Assert.Null(family);
  }

  /// <summary>
  /// The claim is about fuel in general, not about coke: a single-code body passes identically against a
  /// furnace whose fuel test is <c>== "game:coke"</c>, so the theory carries a charcoal row.
  /// </summary>
  [Theory]
  [InlineData("coke")]
  [InlineData("charcoal")]
  public void Fuel_is_never_the_wrong_family_however_the_furnace_is_gated(
    string fuel
  ) {
    // The trap the family gate opens once fuel is chargeable: `Burden.FamilyOfCode` answers `ore` for
    // everything that is not remelt burden, fuels included, so running fuel through it tallies a fuel band
    // as wrong-family charge - harmless on a blast furnace, fatal on a cupola that would refuse to convert.
    var world = NewWorld();
    var be = Furnace(world);
    be.ChargeColumnAt(0, 0)!.Push(fuel, 40, ChargeTemp, default);

    int total = CountChargeFull(be, out int rejected, out string? family);

    // 40 whichever fuel it is: the total is a volume, so the charcoal row must not read 20 here. What is
    // priced at half is the carbon - see A_charcoal_band_is_carbon_at_HALF_a_coke_bands_weight_per_unit.
    Assert.Equal(40, total);
    Assert.Equal(0, rejected);
    Assert.Null(family);
  }

  [Fact]
  public void A_mixed_shaft_rejects_only_the_wrong_family_charge() {
    var world = NewWorld();
    var be = Furnace(world);
    BurdenIn(be, 0, 0, 200, new BurdenMix(75f, 5f, 20f));
    RemeltIn(be, 1, 0, 50, new BurdenMix(60f, 5f, 35f));

    int total = CountChargeFull(be, out int rejected, out string? family);

    Assert.Equal(250, total); // both burn
    Assert.Equal(50, rejected); // only the unrecognised charge is rejected
    Assert.Null(family);
  }

  [Fact]
  public void Melting_consumes_wrong_family_charge_as_the_burn_does() {
    // Wrong-family charge is real mass burning in the shaft: it counts family-blind toward the total,
    // descends and comes back as salvage, but never becomes metal. The tick blocks the melt cycle while
    // `ConversionBlocked`, so in play a rejected charge never reaches this call.
    var world = NewWorld();
    var be = Furnace(world);
    // Hot, because the melt reads the band's own carried temperature - see HotBurdenIn.
    ChargeColumn column = be.ChargeColumnAt(0, 0)!;
    column.Push(
      "iwex:remeltburden",
      100,
      IwexValues.BfIronMeltingPoint + 100f,
      new BurdenMix(60f, 5f, 35f)
    );

    Melt(be, 16);

    Assert.Equal(84, Units(column));
  }

  /// <summary>
  /// A dead furnace cannot relight off its own salvage, and that holds for legacy charge too - the case
  /// with no stamp to strip. Ignition derives from carbon at the raceway and unstamped charge burns as the
  /// standard grade (<c>Legacy_blast_mix_reads_as_a_standard_grade_burden</c>), so a burn-out that left it
  /// untouched would leave the raceway readable as coke-bearing and the furnace would re-ignite. Burn-out
  /// therefore stamps the legacy band with the shares its own burn assumed, carbon scaled by the retention,
  /// which is what "spent charge" means.
  /// </summary>
  [Fact]
  public void Legacy_charge_is_stamped_as_spent_so_a_dead_furnace_cannot_relight() {
    var world = NewWorld();
    var be = Furnace(world);
    // Every column, so the raceway course is complete and only the fuel half of the gate can refuse. Real
    // rounds: carbon comes from fuel bands and nowhere else, so an all-burden shaft cannot light and there
    // would be no fire for burn-out to end. The legacy burden under test is the rounds' burden courses.
    foreach (var (x, z) in be.ShaftColumns.Keys) {
      ChargeColumn col = be.ChargeColumnAt(x, z)!;
      col.Push("game:coke", 25, ChargeTemp, default);
      Blastmix(be, x, z, 75);
    }

    Assert.True(
      (bool)ReflectionHelpers.Invoke(be, "TryIgniteCharge", Handle(be))!,
      "a complete raceway course with coke in it must light before burn-out"
    );

    Shutdown(be);

    Assert.False(
      (bool)ReflectionHelpers.Invoke(be, "TryIgniteCharge", Handle(be))!,
      "burnt-out charge at the raceway must not re-arm the ignition gate"
    );

    // The salvage is real rather than merely inert: the ore and flux the legacy grade implied are still
    // there to be re-coked.
    ChargeSegment band = BandIn(be, be.ChargeColumnAt(0, 0)!, 0);
    Assert.Equal(0f, band.Mix.Fuel, 4);
    Assert.True(band.Mix.Iron > 0f);
    Assert.True(band.Mix.Flux > 0f);
    // The salvage must grade as real chargeable material rather than off-spec or empty.
    Assert.NotEqual(
      "iwex:burden-profile-offspec",
      Burden.ProfileLangKey(band.Mix)
    );
    Assert.NotEqual(
      "iwex:burden-profile-empty",
      Burden.ProfileLangKey(band.Mix)
    );
  }

  [Fact]
  public void A_wrong_family_charge_burns_out_on_extinguish_not_destroyed() {
    var world = NewWorld();
    Block slag = TestBlocks.Configure(new Block(), "iwex:slag", 701);
    world.Register(slag);
    var be = Furnace(world);
    BlockPos bottom = (BlockPos)
      ReflectionHelpers.Invoke(be, "GetGlobalPos", 0, 1, 0)!;
    ChargeColumn column = RemeltIn(
      be,
      0,
      0,
      100,
      new BurdenMix(0.60f, 0.05f, 0.35f)
    );

    Shutdown(be);

    // Still remelt burden salvage - not slag, not destroyed: metal + flux kept, coke burned out.
    Assert.NotEqual(slag.BlockId, world.GetBlock(bottom).BlockId);
    ChargeSegment band = BandIn(be, column, 0);
    Assert.Equal("iwex:remeltburden", band.Material);
    Assert.Equal(0.60f, band.Mix.Iron, 4);
    Assert.Equal(0.05f, band.Mix.Flux, 4);
    Assert.Equal(
      0.35f * IwexValues.BfBurnoutFuelRetainedBottom,
      band.Mix.Fuel,
      4
    );
  }

  [Fact]
  public void Extinguishing_does_not_freeze_iron_over_a_wrong_family_charge() {
    // A furnace holding molten iron that then took wrong-family charge into a bottom cell must not
    // overwrite that cell with solid iron - it is the player's salvage. The charge-pile block has to be
    // registered for this to mean anything: without it SyncChargeBlocks is a quiet no-op, the pool cell
    // stays air, and air is trivially free.
    var world = NewWorld();
    Block iron = TestBlocks.Configure(
      new Block(),
      "iwex:hearthmetal-pigiron",
      700
    );
    iron.EntityClass = "hearthmetal";
    world.RegisterBlockEntityFactory(
      "hearthmetal",
      () => new BlockEntityHearthMetal()
    );
    world.Register(iron);
    world.Register(
      TestBlocks.Configure(
        new Block(),
        BlockChargePile.PileCode.ToShortString(),
        702,
        [("type", "chargepile")]
      )
    );

    var be = Furnace(world);
    BlockPos bottom = (BlockPos)
      ReflectionHelpers.Invoke(be, "GetGlobalPos", 0, 1, 0)!;
    ChargeColumn column = RemeltIn(
      be,
      0,
      0,
      100,
      new BurdenMix(0.60f, 0.05f, 0.35f)
    );
    be.SyncChargeBlocks();
    Assert.Equal(
      BlockChargePile.PileCode.ToShortString(),
      world.GetBlock(bottom).Code?.ToShortString()
    );

    ReflectionHelpers.SetField(be, "_moltenIron", 50f);

    Shutdown(be);

    Assert.NotEqual(iron.BlockId, world.GetBlock(bottom).BlockId); // not overwritten
    Assert.Equal("iwex:remeltburden", BandIn(be, column, 0).Material); // salvage survives
  }

  [Fact]
  public void The_rejected_charge_state_round_trips_through_the_tree() {
    // GetBlockInfo runs client-side and never walks the charge, so the mismatch line the HUD prints
    // has to ride the save tree - the same reason _cachedMixCount does.
    var world = NewWorld();
    var src = Furnace(world);
    ReflectionHelpers.SetField(src, "_cachedRejectedCount", 48);

    var tree = new Vintagestory.API.Datastructures.TreeAttribute();
    src.ToTreeAttributes(tree);
    var dst = Furnace(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.Equal(
      48,
      (int)ReflectionHelpers.GetField(dst, "_cachedRejectedCount")!
    );
    // The companion `_cachedRejectedFamily` is asserted absent: a field that came back would ride the save
    // tree again with nothing to fill it, and the HUD line it fed no longer takes a name.
    Assert.Null(
      ReflectionHelpers.TryGetField(dst, "_cachedRejectedFamily", out object? _)
        ? "present"
        : null
    );
  }

  #endregion
}
