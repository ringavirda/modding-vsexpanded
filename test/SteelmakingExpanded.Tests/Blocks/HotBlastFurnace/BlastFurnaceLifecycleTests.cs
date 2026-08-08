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
/// The blast furnace's melting math, driven directly: the per-cycle conversion of hearth blast mix
/// into molten iron + slag (capacity-clamped) and the hearth blast-mix accounting. These are the
/// numbers the firing/melting tick relies on but the gated <c>OnProductionTick</c> made unreachable.
/// </summary>
public class BlastFurnaceLifecycleTests
{
  private static TestWorld NewWorld()
  {
    var world = new TestWorld();
    world.RegisterItem("game:ingot-iron", 1500f);
    world.RegisterItem("iwex:slag");
    return world;
  }

  private static BlockEntityBlastFurnaceHot Furnace(TestWorld world)
  {
    var be = new BlockEntityBlastFurnaceHot
    {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "smex:blastfurnacecore-n",
        1,
        ("side", "north")
      ),
    };
    world.Attach(be);
    // Attach the shipped layout to the placed block before the rotation update reads it. The crucible
    // floor is no longer a hand-declared offset array on the block entity - it is the drawing's own
    // CellRole.Pool - so a furnace with no attributes has no crucible to freeze onto. Nothing is raised:
    // these are unit-level tests that drive the melt directly and never need the structure to complete.
    StructureRig.Around(
      world,
      be,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single()
    );
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
    ReflectionHelpers.Invoke(be, "CacheAttributes");
    return be;
  }

  // Charge is the furnace's own, held in ChargeColumns, since the column cutover. The helpers below
  // used to build vanilla `game:coalpile` block entities and hand the melt a list of them; a column is
  // pure data the core owns, so a "pile" is no longer a thing a test can construct - it charges a column
  // and reads it back. The charge temperature is a single fixed value on purpose: ChargeColumn.Push
  // merges only within 1 °C, so a varying one would shatter a 100-unit load into slivers and every
  // "read the band at this block" assertion would read whichever sliver landed there.

  private const float ChargeTemp = 20f;

  /// <summary>Lays <paramref name="units"/> of <b>unstamped</b> burden into the column at structure-local
  /// <c>(x, z)</c> - charge carrying no composition at all, which the furnace burns as the standard grade.
  /// <para>
  /// This was the <c>iwex:blastmix</c> item until that item was removed. Unstamped burden inherited its
  /// meaning exactly, so every calibration anchor below still means what it did - and the case that
  /// matters, <c>Legacy_blast_mix_reads_as_a_standard_grade_burden</c>, still has a live subject rather
  /// than being retired along with the item.
  /// </para>
  /// </summary>
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
  )
  {
    ChargeColumn column = be.ChargeColumnAt(localX, localZ)!;
    column.Push($"iwex:{itemPath}", units, ChargeTemp, mix ?? default);
    return column;
  }

  /// <summary>The handle the three charge hooks take - the furnace's own column set.</summary>
  private static object Handle(BlockEntityBlastFurnaceHot be) => be.ShaftColumns;

  /// <summary>
  /// Lays burden that has already <b>arrived at the raceway hot</b> - above iron's melt line, which is the
  /// only burden the melt will take.
  /// <para>
  /// The melt condition is per band and reads <c>ChargeSegment.Temperature</c>: what decides whether
  /// burden renders is the heat <em>it</em> carried down the shaft, not the furnace's own. A direct melt
  /// test therefore has to charge hot, or it is arranging a chilled column and asserting it melts.
  /// </para>
  /// </summary>
  private static ChargeColumn HotBurdenIn(
    BlockEntityBlastFurnaceHot be,
    int localX,
    int localZ,
    int units,
    BurdenMix? mix = null
  )
  {
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
  /// Lays a <b>round</b> into the column at <c>(x, z)</c> - a coke course, then a burden course - at the
  /// given coke fraction.
  /// <para>
  /// <b>Carbon comes from fuel bands and nowhere else</b>, so a column's
  /// coke fraction is a fact about the <em>geometry</em> of what was charged rather than about a stamp.
  /// A burden's <c>Mix.Fuel</c> is a grade/display field now and contributes nothing mechanically.
  /// </para>
  /// </summary>
  private static ChargeColumn RoundIn(
    BlockEntityBlastFurnaceHot be,
    int localX,
    int localZ,
    int units,
    float cokeFrac
  )
  {
    ChargeColumn column = be.ChargeColumnAt(localX, localZ)!;
    int coke = (int)System.Math.Round(units * cokeFrac);
    column.Push("game:coke", coke, ChargeTemp, default);
    column.Push(
      "iwex:burden",
      units - coke,
      ChargeTemp,
      new BurdenMix(1f - IwexValues.BfDefaultFluxFrac, IwexValues.BfDefaultFluxFrac, 0f)
    );
    return column;
  }

  /// <summary>Drives one melt directly - <c>ConsumeForMelting</c> on the furnace's own columns.</summary>
  private static void Melt(BlockEntityBlastFurnaceHot be, int units) =>
    ReflectionHelpers.Invoke(be, "ConsumeForMelting", Handle(be), units);

  /// <summary>Everything going out <em>does</em>, minus deciding that it went out - the residue path a
  /// derived branch reaches when <c>DeriveState</c> reads <c>Idle</c>. It replaces
  /// <c>Extinguish()</c> in these tests: that method still exists for the firebox branch, but it assigns a
  /// state, and a shaft's state is a read.</summary>
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
  )
  {
    int low = blockIndex * be.ChargeUnitsPerBlock;
    int at = 0;
    foreach (ChargeSegment segment in column.Segments)
    {
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
  /// <b>The per-cycle yield pair is gone.</b> <c>ConsumeForMelting</c> took the iron and slag a cycle
  /// rendered as arguments (<c>60 u</c> and <c>10 u</c>, flat, whatever was in the column); the yield is a
  /// property of the <b>band</b> now - <c>BfIronPerOreUnit</c> against the burden's own ore share - so a
  /// rich burden really does render more iron than a lean one, and roasting can change what a charge
  /// yields without touching what a pile holds.
  /// </summary>
  [Fact]
  public void A_melt_cycle_renders_burden_into_molten_iron_and_slag()
  {
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
  /// <b>The melt takes burden and steps over coke.</b> It used to take the
  /// lowest <em>N</em> units whatever they were made of, so a melting furnace destroyed coke at the melt
  /// rate <b>on top of</b> the carbon the raceway was burning - about five times faster - and campaign
  /// length stopped being "how much coke was charged" the moment a furnace started producing. Coke leaves a
  /// column by burning and by nothing else.
  /// </summary>
  [Fact]
  public void A_melt_steps_over_the_coke_at_the_raceway_and_takes_only_burden()
  {
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
  /// <b>The charcoal twin of the case above, and it is not a duplicate of it.</b> The claim
  /// <c>A_melt_steps_over_the_coke_at_the_raceway_and_takes_only_burden</c> makes is about <em>fuel</em> -
  /// "coke leaves a column by burning and by nothing else" is a sentence about carbon, not about one item
  /// code - but its body lays coke and only coke, so it passes identically against a melt whose skip is
  /// <c>segment.Material == "game:coke"</c>. This is the row that cannot.
  /// <para>
  /// What that literal would cost is not cosmetic. Charcoal is <b>half</b> the carbon of coke per unit
  /// (<c>BlockEntityFurnaceCore.CarbonPerUnit</c>), so a charcoal campaign already lays twice the volume for
  /// the same heat; a melt that also <em>ate</em> the charcoal course would destroy it at the melt rate on
  /// top of the burn - the exact defect the coke fix closed - and a charcoal furnace would go out
  /// with most of its burden still standing while the player watched the cheap fuel disappear fastest. The
  /// fix would have been half-applied and nothing would have said so.
  /// </para>
  /// </summary>
  [Fact]
  public void A_melt_steps_over_a_CHARCOAL_band_exactly_as_it_steps_over_coke()
  {
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

    // ...and every unit of iron came out of burden, which is the same defect stated in metal rather than in
    // bands: a fuel band carries `default` mix, so a melt that consumed the ten charcoal units would render
    // nothing at all for them and the pool would come up exactly ten units of burden short.
    Assert.Equal(16 * IwexValues.BfIronPerOreUnit * mix.IronFrac, Iron(be), 2);
  }

  [Fact]
  public void Molten_output_is_clamped_at_the_furnace_capacity()
  {
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
  /// <b>This case asserted the opposite until the column cutover, and the inversion is the point.</b>
  /// It was <c>Melting_draws_from_the_upper_piles_first</c>, and that was never a statement about
  /// furnaces: vanilla's coal pile collapses its own column from inside its block entity, so eating the
  /// bottom of a stack was not something the machine could express and the melt took the top instead,
  /// "matching the original drip order".
  /// <para>
  /// A charge column has no collapse in it. The raceway is at the bottom, so that is where descent takes
  /// from - and the empty space appears at the <b>top</b>, which is where the hopper already drips.
  /// </para>
  /// </summary>
  [Fact]
  public void Melting_draws_from_the_BOTTOM_of_a_column_and_the_gap_appears_on_top()
  {
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
  public void Unequal_columns_descend_together_rather_than_one_draining_first()
  {
    // The whole-furnace per-cycle figure is spread over the columns that still hold charge, so the
    // raceway stays fed across the width of the shaft instead of one column emptying under one tuyere
    // while the rest stand full.
    var world = NewWorld();
    var be = Furnace(world);
    ChargeColumn tall = HotBurdenIn(be, 0, 0, 100);
    ChargeColumn shallow = HotBurdenIn(be, 1, 0, 20);

    Melt(be, 16);

    Assert.Equal(120 - 16, Units(tall) + Units(shallow));
    Assert.True(Units(shallow) > 0, "the shallow column is not drained to nothing first");
    Assert.True(
      Units(tall) < 100 && Units(shallow) < 20,
      "both columns give up charge in the same cycle"
    );
  }

  #endregion

  #region Blast-mix accounting

  /// <summary>Runs the charge scan, handing back its out-parameters (isFull + mix; the rejected count
  /// and family the family gate reads are exercised in the gate tests below).
  /// <para>
  /// <c>ReadChargeMix</c>, not the deleted <c>GetBlastMixCount</c>: same five out-parameters, same
  /// contract, walking columns instead of coal-pile inventories.
  /// </para>
  /// </summary>
  private static int CountCharge(
    BlockEntityBlastFurnaceHot be,
    out bool isFull,
    out BurdenMix mix
  )
  {
    object?[] args = { Handle(be), false, default(BurdenMix), 0 };
    int count = (int)ReflectionHelpers.Invoke(be, "ReadChargeMix", args)!;
    isFull = (bool)args[1]!;
    mix = (BurdenMix)args[2]!;
    return count;
  }

  /// <summary>
  /// Renamed from <c>..._reports_full_at_the_fire_threshold</c>. There is no fire
  /// threshold any more - ignition is positional and pneumatic, and <c>BlastMixRequiredToFire</c> is
  /// deleted - so "full" now means <b>loaded to capacity</b>, which is the furnace's own geometry.
  /// </summary>
  [Fact]
  public void Mix_count_totals_the_hearth_and_reports_full_at_its_own_capacity()
  {
    var world = NewWorld();
    var be = Furnace(world);
    int capacity = (int)ReflectionHelpers.GetProperty(be, "ChargeCapacityUnits")!;

    // The premise: a capacity of 0 or 1 would make every assertion below trivially true.
    Assert.True(capacity > 1, $"capacity should be real geometry; it was {capacity}");

    Blastmix(be, 0, 0, capacity);

    int count = CountCharge(be, out bool isFull, out _);

    Assert.Equal(capacity, count);
    Assert.True(isFull, "a hearth loaded to capacity should read as full");
  }

  [Fact]
  public void A_thin_charge_does_not_read_as_full()
  {
    var world = NewWorld();
    var be = Furnace(world);
    Blastmix(be, 0, 0, 10);

    int count = CountCharge(be, out bool isFull, out _);

    Assert.Equal(10, count);
    Assert.False(isFull);
  }

  /// <summary>
  /// <b>A burden's stamped coke is not carbon, and this case once asserted the
  /// opposite.</b> It was <c>Stamped_burden_reports_its_own_coke_fraction</c>: 100 units of burden
  /// stamped at 30 % read back as a 30 % coke column.
  /// <para>
  /// The stamp <em>was</em> the fuel supply, back when a pile held one mixed material. Once charging lays
  /// coke as its own bands it becomes a second, disagreeing answer to "how much carbon is at the raceway":
  /// a round of 9 units of coke under 23 units of 30 %-stamped burden read <b>15.9 of 32</b>, so the
  /// furnace burned at ~50 % coke on a charge the player laid at 30 % and ran ~70 °C hotter than the grade
  /// advertises. The same double-count fed the blast-pressure demand, wrong in the same direction.
  /// </para>
  /// <para>
  /// <b>What it costs a saved world</b>, stated here because this is the test that would have caught it:
  /// a shaft charged before coke became its own band reads 0 % carbon and chills rather than burning at a
  /// phantom fraction. That is the <em>legible</em> failure of the two - the chill names itself and the
  /// charge digs back out.
  /// </para>
  /// </summary>
  [Fact]
  public void A_stamped_burden_with_no_coke_bands_reads_as_no_carbon_at_all()
  {
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
  public void Legacy_blast_mix_reads_as_a_standard_grade_burden()
  {
    // Charge stamped before burden compositions existed keeps its ore and flux shares, so an old world's
    // column still grades as standard rather than reading as a flux shortfall on top of everything else.
    //
    // And it must not read as carbon, which is the way to get this wrong twice over: the shaft's fuel
    // test is the `fuel` material role, not "anything that is not burden". Blast mix is neither - so a
    // `!Burden.IsAnyCode` test would put an old world's whole shaft at a coke fraction of 1.00 and slam
    // the heat balance's fuel factor into its ceiling.
    var world = NewWorld();
    var be = Furnace(world);
    Blastmix(be, 0, 0, 100);

    CountCharge(be, out _, out BurdenMix mix);

    Assert.Equal(0f, mix.FuelFrac, 3); // no coke bands, so no carbon - see the case above

    // The ore and flux come back at their shipped shares as raw amounts, but the fractions renormalise
    // over the two of them (0.05 of 0.80 is 6.25 %) because the fuel share is simply not contributed. That
    // is the honest reading and it is worth pinning: a naive "flux is still 5 %" would fail here, and the
    // temptation would be to "fix" it by putting the stamped fuel back into the sum - which is exactly the
    // double-count this model removed.
    Assert.Equal(
      IwexValues.BfDefaultFluxFrac / (1f - IwexValues.BfDefaultFuelFrac),
      mix.FluxFrac,
      3
    );
    Assert.True(mix.IronFrac > 0f, "the ore share survives, which is what makes it salvageable");
  }

  [Fact]
  public void A_mixed_column_reads_the_volume_weighted_average_coke_ratio()
  {
    var world = NewWorld();
    var be = Furnace(world);
    RoundIn(be, 0, 0, 300, 0.35f);
    RoundIn(be, 1, 0, 100, 0.05f);

    CountCharge(be, out _, out BurdenMix mix);

    // 300 units at 35% + 100 at 5% = 27.5%, not the 20% a naive per-column mean would give.
    Assert.Equal(0.275f, mix.FuelFrac, 3);
  }

  [Fact]
  public void A_coke_band_is_carbon_and_the_column_reads_its_own_coke_fraction()
  {
    // The two-stream split, read end to end. Coke is charged as its own bands rather than mixed into
    // the burden's stamp, so the coke fraction the heat balance and the blast-pressure demand both read
    // comes off the geometry of the charge - which is what makes a properly laid round matter.
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
  /// <b>A charcoal band is carbon at half a coke band's price, and the column reads it weighted.</b> The
  /// charcoal twin of the case above - and the row that separates "the shaft accepts a second fuel" from
  /// "the shaft <em>prices</em> a second fuel", which are different machines. Charcoal lit, burned, made gas
  /// and yielded iron at coke's exact rate for months; nothing here would have noticed,
  /// because every fuel assertion in the suite laid coke.
  /// <para>
  /// The two numbers below carry the whole claim. <b>Volume</b> is volume - the total comes back 100
  /// either way, because a charcoal band occupies exactly the column a coke band does and permeability does
  /// not care what is burning. <b>Carbon</b> is not: 25 units of charcoal contribute 12.5, so the shaft
  /// reads 14.3 % carbon on a charge laid at 25 % by volume and runs cooler for it. An
  /// <c>Accumulate</c> that spent <c>fuel += units</c> would answer 0.25 here and make charcoal a pure
  /// speed buff - identical flame, identical iron, half the price - which is the opposite of what a worse
  /// fuel should be.
  /// </para>
  /// <para>
  /// <c>"charcoal"</c>, not <c>"game:charcoal"</c>, for the reason the coke case states: a segment stores
  /// <c>Code.ToShortString()</c>, and the domainless short form is what lands in a save. Asserting it here
  /// too means the weight lookup is proven to survive the same round-trip for <b>both</b> fuels rather than
  /// only for the one that happened to be tested.
  /// </para>
  /// </summary>
  [Fact]
  public void A_charcoal_band_is_carbon_at_HALF_a_coke_bands_weight_per_unit()
  {
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

    // The control, and it carries the claim: the same volume of the same geometry in coke reads 25 %. Without
    // it this passes just as well on a furnace that reads no carbon from fuel bands at all.
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
  /// <c>CellRole.Pool</c> cells, which is where the furnace itself reads them from now that the
  /// hand-declared <c>SolidifyCells</c> is gone.
  /// </summary>
  private static BlockPos[] BottomLayer(BlockEntityBlastFurnaceHot be) =>
    [.. be.PoolCells];

  [Fact]
  public void Extinguishing_a_melt_solidifies_the_iron_across_the_bottom_layer()
  {
    var world = NewWorld();
    // Give the block an entity class + factory so SetBlock spawns a real BlockEntityHearthMetal,
    // the way the engine would - otherwise the nugget count has nothing to be stamped onto and the
    // even-split half of the behaviour goes unasserted.
    Block iron = TestBlocks.Configure(new Block(), "iwex:hearthmetal-pigiron", 700);
    iron.EntityClass = "hearthmetal";
    world.RegisterBlockEntityFactory(
      "hearthmetal",
      () => new BlockEntityHearthMetal()
    );
    world.Register(iron);

    var be = Furnace(world); // no charge piles -> the burnout walk is a no-op
    ReflectionHelpers.SetField(be, "_moltenIron", 50f);

    Shutdown(be);

    // It is Idle because a furnace that has never ticked is idle, not because Shutdown put it there:
    // the split is the point. Shutdown is everything going out *does* - the sound, the reset to ambient,
    // the residue, the counters - minus deciding that it went out, because on a derived branch the label
    // is a read. What decides is `DeriveState` finding no carbon at the raceway.
    Assert.Equal(FurnaceState.Idle, be.State);
    Assert.Equal(0f, Iron(be), 3); // the molten pool is gone

    // The pool freezes onto the hearth floor - every free cell of it, not two fixed cells - and the
    // nuggets are split evenly, so the same wreck is left every time.
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
  public void Extinguishing_burns_the_burden_out_by_height_instead_of_slagging_it()
  {
    var world = NewWorld();
    // Well clear of the coal piles' ids, which are derived from their Y.
    Block slag = TestBlocks.Configure(new Block(), "iwex:slag", 701);
    world.Register(slag);
    var be = Furnace(world);

    // One column, charged to its own ceiling so it stands from the hearth floor to the top of the
    // shaft. The pile model needed two separate piles to have two heights; a column is continuous, so
    // burn-out splits it per block and the gradient is read out of the same column at two indices.
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
    foreach (BurdenMix m in new[] { low, high })
    {
      Assert.Equal(0.75f, m.Iron, 4);
      Assert.Equal(0.05f, m.Flux, 4);
    }

    // (d) The grade no longer changes when the coke is stripped, and that is the burden family model's
    // whole point rather than a loss. This asserted `burnedout` - one of four bands that graded the fuel
    // stamped on the burden - and coke left the item when charging became layered. Burden is graded on flux
    // now, and burning the coke out of a shaft cannot alter the flux the burden was made with.
    //
    // Asserted as invariance, which is a real claim and the one worth keeping: the salvage still reads
    // as the grade it was mixed at, so a player who re-charges it gets what the readout promised.
    Assert.Equal(Burden.ProfileLangKey(mix), Burden.ProfileLangKey(low));
  }

  [Fact]
  public void A_second_extinguish_burns_the_already_spent_burden_out_no_further()
  {
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
    // And the charge itself is not eaten a second time either: burn-out rewrites composition, it does
    // not consume burden. A rewrite that dropped units would show up here and nowhere else.
    Assert.Equal(100, Units(column));
  }

  // The three helpers below serve the fuel-band burn-out case only. They lay real rounds - a fuel course
  // then a burden course, one round per charge block - because a fuel band's retention is only observable
  // when there is burden between the courses: `ChargeColumn.Rewrite` lays its pieces back down through
  // Push's own merge rule, so two fuel pieces that ended up adjacent would fold into one segment and the
  // gradient the case is about would be summed away before it could be read.

  /// <summary>The units a fuel course carries in each round below - a quarter of a charge block.</summary>
  private static int FuelPerRound(BlockEntityBlastFurnaceHot be) =>
    System.Math.Max(1, be.ChargeUnitsPerBlock / 4);

  /// <summary>Fills <b>every</b> column with rounds of <paramref name="fuelCode"/> - one round per charge
  /// block, so round <c>i</c> lands exactly inside block <c>i</c> and burn-out's per-block split has
  /// nothing to straddle. Every column, because the ignition gate is positional.</summary>
  private static void FuelRoundsEverywhere(
    BlockEntityBlastFurnaceHot be,
    string fuelCode,
    BurdenMix mix
  )
  {
    int perBlock = be.ChargeUnitsPerBlock;
    int fuel = FuelPerRound(be);
    foreach (var (x, z) in be.ShaftColumns.Keys)
    {
      ChargeColumn column = be.ChargeColumnAt(x, z)!;
      int rounds = be.ColumnCapacity(x, z) / perBlock;
      for (int i = 0; i < rounds; i++)
      {
        column.Push(fuelCode, fuel, ChargeTemp, default);
        column.Push("iwex:burden", perBlock - fuel, ChargeTemp, mix);
      }
    }
  }

  /// <summary>The units standing in each <b>fuel</b> band of a column, raceway end first - the gradient
  /// burn-out leaves, read through the production predicate rather than against a code literal.</summary>
  private static List<int> FuelBands(ChargeColumn column)
  {
    var units = new List<int>();
    foreach (ChargeSegment segment in column.Segments)
      if (BlockEntityFurnaceCore.IsFuelCode(segment.Material))
        units.Add(segment.Units);
    return units;
  }

  private static bool Ignites(BlockEntityBlastFurnaceHot be) =>
    (bool)ReflectionHelpers.Invoke(be, "TryIgniteCharge", Handle(be))!;

  /// <summary>
  /// <b>Burn-out's height gradient is a fact about the shaft, not about which fuel was laid in it</b> -
  /// and, at the bottom, <c>BfBurnoutFuelRetainedBottom</c> is <b>0</b> for charcoal exactly as it is for
  /// coke. Both halves matter and only the second is load-bearing.
  /// <para>
  /// <b>The no-was-lit-bit invariant is proven for one code without this.</b> A shaft has no state
  /// machine and no "was lit" flag: what stops a dead furnace re-deriving itself alight on the very next
  /// tick is that burn-out leaves <em>no carbon at its own raceway</em>
  /// (<c>BlockEntityShaftFurnace.DeriveState</c> box). <c>BurnOutCharge</c> recognises a fuel band
  /// through <c>IsFuelCode</c>; make that a coke literal and a charcoal band takes the <b>legacy burden</b>
  /// branch instead - it carries <c>default</c> mix, so it is stamped with the standard-grade shares and
  /// <b>keeps every unit</b>. The raceway still reads as carbon-bearing, the furnace re-ignites, starves
  /// one grace later, and burn-out multiplies the retention again. And again. That is the relight
  /// oscillation the burn-out fix closed, returning through the second fuel with nothing to catch it.
  /// </para>
  /// <para>
  /// Asserted band-for-band against a <b>coke</b> furnace charged identically rather than against
  /// computed literals: the claim is that the two are the same curve, and a comparison says that where a
  /// pair of numbers only implies it. The one literal kept is the top course, tied to
  /// <c>BfBurnoutFuelRetainedTop</c> itself so retuning the pair moves the test with it.
  /// </para>
  /// </summary>
  [Fact]
  public void Burn_out_retains_a_charcoal_band_on_the_same_height_gradient_as_coke()
  {
    var mix = new BurdenMix(0.75f, 0.05f, 0.20f);

    var charcoalFired = Furnace(NewWorld());
    FuelRoundsEverywhere(charcoalFired, "game:charcoal", mix);
    var cokeFired = Furnace(NewWorld());
    FuelRoundsEverywhere(cokeFired, "game:coke", mix);

    // The premise, and it is half the case: a charcoal course at the raceway lights a furnace. Without it
    // everything below would hold just as well on a shaft that could never catch on charcoal at all.
    Assert.True(
      Ignites(charcoalFired),
      "a complete raceway course of charcoal must light before burn-out"
    );

    ChargeColumn charcoal = charcoalFired.ChargeColumnAt(0, 0)!;
    ChargeColumn coke = cokeFired.ChargeColumnAt(0, 0)!;
    int charged = FuelPerRound(charcoalFired);
    Assert.True(FuelBands(charcoal).Count > 1, "the scene needs a column with two ends to it");

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
    Assert.Equal((int)(charged * IwexValues.BfBurnoutFuelRetainedTop), left[^1]);

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

    // ...and the burden between the courses is still real salvage, so this is a setback rather than the
    // loss of the charge - the same promise the coke path makes, restated for the fuel that is cheap.
    ChargeSegment burden = charcoal.Segments[0];
    Assert.Equal(0.75f, burden.Mix.Iron, 4);
    Assert.Equal(0.05f, burden.Mix.Flux, 4);
  }

  #endregion

  #region Burden family gate

  // The blast furnace burns ore burden only. Remelt burden (the cupola's charge) counts toward the burn
  // - so a mis-loaded shaft still lights and burns out - but is tallied as rejected, which blocks the
  // conversion and never lets the wrong family become molten iron. These pin the charge-scan half; the
  // tick-level "never converts / stays Firing" half is in BlastFurnaceScenarioTests.

  /// <summary>Runs the charge scan, surfacing the rejected-family count and token too.</summary>
  // `ReadChargeMix` lost its `out string? rejectedFamily` with the burden family model. The
  // out-param is kept here, always null, so the cases below still read as three claims (counted / rejected
  // / named) and the day it stopped being nameable is visible in them rather than silently absent.
  private static int CountChargeFull(
    BlockEntityBlastFurnaceHot be,
    out int rejectedCount,
    out string? rejectedFamily
  )
  {
    object?[] args = { Handle(be), false, default(BurdenMix), 0 };
    int count = (int)ReflectionHelpers.Invoke(be, "ReadChargeMix", args)!;
    rejectedCount = (int)args[3]!;
    rejectedFamily = null;
    return count;
  }

  [Fact]
  public void Remelt_burden_is_counted_but_rejected_as_the_wrong_family()
  {
    var world = NewWorld();
    var be = Furnace(world);
    RemeltIn(be, 0, 0, 100, new BurdenMix(60f, 5f, 35f));

    int total = CountChargeFull(be, out int rejected, out string? family);

    Assert.Equal(100, total); // counted blind to acceptance, so the shaft still lights and burns
    Assert.Equal(100, rejected); // ...but not one unit of it will convert
    Assert.Null(family); // the family token is gone; the count is the whole of the answer now
  }

  [Fact]
  public void Ore_burden_is_accepted_with_no_rejected_charge()
  {
    var world = NewWorld();
    var be = Furnace(world);
    BurdenIn(be, 0, 0, 100, new BurdenMix(75f, 5f, 20f));

    CountChargeFull(be, out int rejected, out string? family);

    Assert.Equal(0, rejected);
    Assert.Null(family);
  }

  /// <summary>
  /// <b>Was <c>Coke_is_never_the_wrong_family_however_the_furnace_is_gated</c>, a <c>[Fact]</c>, until the
  /// second fuel was priced.</b> The claim it makes is about <em>fuel</em> - nothing in the family gate has
  /// ever been about coke specifically - but a single-code body passes identically against a furnace whose
  /// fuel test is <c>== "game:coke"</c>, and that furnace tallies every charcoal band in a <b>cupola</b> as
  /// wrong-family charge and blocks its conversion for ever while the HUD names "ore" as the offender in a
  /// machine that burns remelt. The rename is the honest claim, and the second row is what proves it.
  /// </summary>
  [Theory]
  [InlineData("coke")]
  [InlineData("charcoal")]
  public void Fuel_is_never_the_wrong_family_however_the_furnace_is_gated(
    string fuel
  )
  {
    // The trap the family gate opens once fuel is chargeable: `Burden.FamilyOfCode` answers `ore` for
    // everything that is not remelt burden, every fuel included. Running fuel through it would tally a
    // fuel band as wrong-family charge - harmless-looking on a blast furnace, which burns ore, and fatal on
    // a cupola, which would refuse to convert for ever while the HUD named "ore" as the offender.
    var world = NewWorld();
    var be = Furnace(world);
    be.ChargeColumnAt(0, 0)!.Push(fuel, 40, ChargeTemp, default);

    int total = CountChargeFull(be, out int rejected, out string? family);

    // 40 whichever fuel it is: the total is a volume, so the charcoal row must not read 20 here. What is
    // priced at half is the carbon (see A_charcoal_band_is_carbon_at_HALF_a_coke_bands_weight_per_unit),
    // and confusing the two is the double-count this model has already made once.
    Assert.Equal(40, total);
    Assert.Equal(0, rejected);
    Assert.Null(family);
  }

  [Fact]
  public void A_mixed_shaft_rejects_only_the_wrong_family_charge()
  {
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
  public void Melting_consumes_wrong_family_charge_as_the_burn_does()
  {
    // This case asserted the opposite - "the wrong-family charge is untouched" - and the change is a
    // correction, not a regression. The old per-slot filter never had anything to filter: the tick blocks
    // the whole melt cycle while `ConversionBlocked`, so a rejected charge cannot reach this call in
    // play. What the filter did do was contradict the model the HUD and the burn already followed -
    // wrong-family charge is real mass burning in the shaft, which is why it counts family-blind toward
    // the total and comes back as salvage. It descends like anything else; it simply never becomes metal.
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
  /// <b>A dead furnace cannot relight off its own salvage</b>, and that has to hold for legacy charge
  /// too - the case that has no stamp to strip.
  /// <para>
  /// The loop this closes is a real one the pile model only survived by accident. Ignition is derived
  /// now: the raceway must hold carbon, and unstamped charge reads as carrying the reference coke
  /// fraction (it burns as the standard grade - see <c>Legacy_blast_mix_reads_as_a_standard_grade_burden</c>).
  /// So a burn-out that left legacy charge untouched - which is exactly what the pile path did - would
  /// leave the raceway readable as coke-bearing for ever: <c>Extinguish</c> leaves the shaft full, the
  /// gate re-arms on the next tick, the furnace re-ignites, starves out again one grace later, and
  /// burn-out multiplies the retention a second time. And a third. The pile path got away with it only
  /// because releasing a pile also called <c>Extinguish()</c> on the pile's own fire.
  /// </para>
  /// <para>
  /// The fix is to <b>stamp</b> the legacy band with the shares its own burn already assumed, carbon
  /// scaled by the retention - which is what "spent charge" means, and what makes the salvage legible
  /// rather than indistinguishable from fresh charge.
  /// </para>
  /// </summary>
  [Fact]
  public void Legacy_charge_is_stamped_as_spent_so_a_dead_furnace_cannot_relight()
  {
    var world = NewWorld();
    var be = Furnace(world);
    // Every column, so the raceway course is complete and only the fuel half of the gate can refuse.
    // Real rounds, not the bare legacy burden this used to lay: carbon comes from fuel bands and nowhere
    // else, so an all-burden shaft cannot light at all and there would be no fire for burn-out to end. The
    // legacy burden is still the thing under test - it is what the rounds' burden courses are - and the
    // migration cost is stated in A_stamped_burden_with_no_coke_bands_reads_as_no_carbon_at_all.
    foreach (var (x, z) in be.ShaftColumns.Keys)
    {
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

    // ...and the salvage is real rather than merely inert: the ore and flux the legacy grade implied are
    // still there to be re-coked, which is the recoverability promise.
    ChargeSegment band = BandIn(be, be.ChargeColumnAt(0, 0)!, 0);
    Assert.Equal(0f, band.Mix.Fuel, 4);
    Assert.True(band.Mix.Iron > 0f);
    Assert.True(band.Mix.Flux > 0f);
    // The `burnedout` grade went with the coke grade bands. What still has to hold is that the salvage
    // grades as something real rather than off-spec - it is chargeable material, and the readout must not
    // tell the player otherwise.
    Assert.NotEqual("iwex:burden-profile-offspec", Burden.ProfileLangKey(band.Mix));
    Assert.NotEqual("iwex:burden-profile-empty", Burden.ProfileLangKey(band.Mix));
  }

  [Fact]
  public void A_wrong_family_charge_burns_out_on_extinguish_not_destroyed()
  {
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
    Assert.Equal(0.35f * IwexValues.BfBurnoutFuelRetainedBottom, band.Mix.Fuel, 4);
  }

  [Fact]
  public void Extinguishing_does_not_freeze_iron_over_a_wrong_family_charge()
  {
    // A furnace holding molten iron from an earlier clean melt, which then took wrong-family charge into
    // a bottom cell, must not overwrite that cell with solid iron - it is the player's salvage.
    //
    // The charge-pile block has to be registered for this to mean anything. Without it
    // SyncChargeBlocks is a quiet no-op, the pool cell stays air, air is trivially free, and the case
    // passes while asserting nothing about the guard it exists for.
    var world = NewWorld();
    Block iron = TestBlocks.Configure(new Block(), "iwex:hearthmetal-pigiron", 700);
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
  public void The_rejected_charge_state_round_trips_through_the_tree()
  {
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
    // The companion `_cachedRejectedFamily` was removed and is asserted absent rather than
    // dropped from the case: a field that quietly comes back would start riding the save tree again with
    // nothing to fill it, and the HUD line it fed no longer takes a name.
    Assert.Null(ReflectionHelpers.TryGetField(dst, "_cachedRejectedFamily", out object? _) ? "present" : null);
  }

  #endregion
}
