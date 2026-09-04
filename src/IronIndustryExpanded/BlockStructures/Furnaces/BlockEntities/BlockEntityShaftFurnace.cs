using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Materials;
using ExpandedLib.Metals;
using IronIndustryExpanded.BlockStructures.Products.BlockEntities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// A furnace whose charge stands in a vertical column over the raceway and descends through it, blown
/// through tuyeres, pooling a molten product it pours out of two taps. The cold and hot blast furnaces and
/// the cupola are all this class. Cold and hot differ only in data - the hot layout carries exhaust outlets
/// the cold one has no cell for, and a cowper on its blast line delivers preheated air at the tuyeres, both
/// of which <see cref="BlockEntityFurnaceCore.ComputeHeatBalance"/> reads.
/// </summary>
/// <remarks>
/// <see cref="ShaftHoldsLayeredCharge"/> is sealed true here; a hearth belongs on
/// <see cref="BlockEntityFireboxFurnace"/>. See <c>docs/design/conventions.md</c> § "the furnace axes
/// become a class tree".
/// </remarks>
public abstract class BlockEntityShaftFurnace : BlockEntityFurnaceCore {
  /// <summary>Fractional charge unit carried between ticks, so a rate under one unit a second still
  /// descends instead of rounding to nothing every tick and stalling the furnace outright.</summary>
  private float _meltCarry;

  /// <summary>
  /// Carbon the raceways burned this tick, in charge units - what <see cref="CirculateGas"/> spent and what
  /// <see cref="SmeltCycle"/> meters the melt against. It also sets the gas temperature, how much gas rises
  /// and how far the column descends. Not saved: recomputed every tick.
  /// </summary>
  /// <remarks>
  /// <c>OnProductionTick</c> must run <see cref="CirculateGas"/> before <see cref="SmeltCycle"/>, or the
  /// melt meters itself against the previous tick's carbon.
  /// </remarks>
  private float _carbonBurnedThisTick;

  /// <summary>Carbon the raceway was owed but could not draw - a starved or chilled column, or a rate
  /// under one unit a second. Carried rather than dropped, or a slow fire never burns anything.</summary>
  private float _burnCarry;

  /// <summary>Which column the tick's carbon spend starts at, advanced every tick. See
  /// <see cref="CirculateGas"/> for why an even split cannot work on whole units.</summary>
  private int _burnRotation;

  #region Tunables

  protected override float MeltingPoint => IiexValues.BfIronMeltingPoint;

  /// <summary>
  /// Zero, and never read on this branch: the core's fuel countdown sits behind <c>!DerivesState</c>. A
  /// campaign ends when the carbon in the shaft is gone, which the raceway meters. The firebox branch, whose
  /// fuel bed burns for a time rather than for a quantity, uses <c>FireboxMaxFuelBurnTime</c>.
  /// </summary>
  protected sealed override int MaxFuelBurnTime => 0;

  /// <summary>
  /// Zero on this branch: the counter-current model warms the charge band by band off the coke actually
  /// burning, so a melt-start soak would be a second soak in series. A shaft charged near the fire threshold
  /// burns its carbon out in under 300 s, so the soak would outlast the campaign.
  /// <see cref="BlockEntityFireboxFurnace"/> has no raceway and uses <c>FireboxMeltStartDelay</c>.
  /// </summary>
  protected sealed override float MeltStartDelay => 0f;

  /// <summary>
  /// Zero, and never read on this branch: <see cref="MeltsPerTick"/> is true, so the cadence is the descent
  /// and the core's interval branch is never taken. The firebox branch uses <c>FireboxMeltIntervalSec</c>.
  /// </summary>
  protected sealed override float MeltIntervalSec => 0f;
  protected override float TuyereIntakeVolume => IiexValues.TuyereIntakeVolume;
  protected override float BlastPressureThreshold =>
    IiexValues.BfBlastPressureAtReference;

  /// <summary>
  /// Every chargeable cell this shaft owns times its own block quantum - the same pair the
  /// <c>bf-info-shaftfull</c> readout divides, so the figure shown to the player and the heat balance's
  /// denominator cannot drift apart.
  /// </summary>
  /// <remarks>
  /// Geometry, never a hand-picked total: a cold blast furnace holds 1 248 units, and a 320-unit total would
  /// read as completely full - paying the whole <c>BfChargeLossFull</c> penalty - while three-quarters
  /// empty. Sealed for the same reason; the cupola's five chargeable cells times its 3 000-unit quantum
  /// already come out of this expression.
  /// </remarks>
  protected sealed override int ChargeCapacityUnits =>
    ChargeableCells.Count * Math.Max(1, ChargeUnitsPerBlock);

  /// <summary>The charge stands in a column over the raceway and descends in layers, which is what makes
  /// this the shaft branch. Sealed so a leaf cannot opt out of it.</summary>
  protected sealed override bool ShaftHoldsLayeredCharge => true;

  // Product identity and per-cycle tunables are virtual so the cupola - this same machine run as a scrap
  // re-melter - swaps them for cast iron and its own slower cadence. The defaults are the blast furnace's.

  /// <summary>Metal token the lower tap pours, resolved through <see cref="MetalRegistry"/>. The blast
  /// furnace makes molten pig iron (crude high-carbon iron, <c>iiex:ingot-pigiron</c>); plain iron comes
  /// downstream from the Bessemer converter. The cupola overrides this to cast iron.</summary>
  protected virtual string MetalProductCode => "pigiron";

  /// <summary>Lang key for the molten-product HUD line (with two <c>{0}/{1}</c> unit args).</summary>
  protected virtual string MoltenProductInfoLangKey =>
    IiexLang.BfInfoMolteniron;

  /// <summary>
  /// Molten product (units) rendered per charge unit of a band carrying <paramref name="mix"/>. The blast
  /// furnace scales it by the band's ore content, so recovery per nugget stays fixed as the ore share moves.
  /// The cupola overrides it flat, remelting rather than reducing. See
  /// <see cref="IiexValues.BfIronPerOreUnit"/>.
  /// </summary>
  protected virtual float ProductPerUnit(BurdenMix mix) =>
    IiexValues.BfIronPerOreUnit * OreShareOf(mix);

  /// <summary>Molten slag (units) rendered per charge unit of a band carrying <paramref name="mix"/>.</summary>
  protected virtual float SlagPerUnit(BurdenMix mix) =>
    IiexValues.BfSlagPerOreUnit * OreShareOf(mix);

  /// <summary>
  /// How many of this furnace's charge units one blast-furnace charge item is worth - 1 for the two blast
  /// furnaces, ~94 for the cupola, which counts its remelt charge in metal units (3 000 a block) rather than
  /// in items (32 a block). Every raceway constant is calibrated in items, so unscaled a cupola runs ~94x
  /// too slow and can never melt. See <c>docs/design/layered-charge.md</c>.
  /// </summary>
  /// <remarks>
  /// Two constants need it. <see cref="IiexValues.BfRacewayCarbonPerTuyerePerSecond"/> is a rate in charge
  /// units, so it scales directly; <see cref="IiexValues.BfShaftGasTransferFrac"/> is a fraction absorbed
  /// per unit, so it scales inversely, or one cupola band absorbs the whole gas and the shaft above stays
  /// cold. Everything else is already scale-free.
  /// </remarks>
  protected float ChargeUnitScale =>
    Math.Max(
      1f,
      ChargeUnitsPerBlock
        / (float)
          Math.Max(
            1,
            IiexValues.ChargeItemsPerBand * ChargeColumn.BandsPerBlock
          )
    );

  /// <summary>
  /// Carbon this furnace's raceways burn per second at full blast, in its own charge units - the per-tuyere
  /// rate times the tuyeres it has. Floored at one tuyere, so a natural-draught shaft still burns and a
  /// furnace whose outlet scan has not run yet does not silently stop consuming.
  /// </summary>
  private float RacewayCarbonPerSecond =>
    IiexValues.BfRacewayCarbonPerTuyerePerSecond
    * Math.Max(1, _tuyeres.Count)
    * ChargeUnitScale;

  /// <summary>
  /// The ore share of a band - its stamped <see cref="BurdenMix.IronFrac"/>, or the shares unstamped legacy
  /// charge is read at. Must agree with <see cref="Accumulate"/>'s fallback, or the furnace melts a burden
  /// its heat balance treated as inert.
  /// </summary>
  private static float OreShareOf(BurdenMix mix) =>
    mix.HasContent
      ? mix.IronFrac
      : Math.Max(
        0f,
        1f - IiexValues.BfDefaultFuelFrac - IiexValues.BfDefaultFluxFrac
      );

  /// <summary>
  /// Capacity of the whole crucible floor (units), one band per pool cell. Read from the layout's cell
  /// count rather than from the placed cells, so a furnace whose hearth blocks are not down yet reports
  /// the room it will have rather than none - which would stall melting before it began.
  /// </summary>
  protected int MaxPooledMetalUnits =>
    PoolCells.Count * IiexValues.HearthUnitsPerBand;

  /// <summary>
  /// Slag capacity of the crucible floor (units). The crucible is one shared volume - slag floats on the
  /// iron rather than having a vessel of its own - so it is bounded by the same band as the metal.
  /// <c>HearthSlagSpoutBand</c> is the height the slag notch sits at, which decides where slag leaves,
  /// not how much of it fits.
  /// </summary>
  protected int MaxPooledSlagUnits => MaxPooledMetalUnits;

  #endregion

  #region Initialization

  /// <summary>
  /// Re-reads the product and charge tunables alongside the core's, so a live <c>/exmod config iiex ...</c>
  /// change applies on the next tick. The core calls this at init and at the top of every production tick.
  /// </summary>
  protected override void CacheAttributes() {
    base.CacheAttributes();
  }

  #endregion

  #region Molten stack construction

  private ItemStack? CreateMoltenStack(string metalCode, int units, float temp) {
    // The molten network and molds expect the registry's item code per metal (iron game:ingot-iron, slag
    // iiex:slag). MetalRegistry.MoltenItemOf resolves the short token, falling back to the
    // game:ingot-<code> convention for a metal that ships no explicit entry.
    AssetLocation loc = MetalRegistry.MoltenItemOf(metalCode);

    Item? item = Api.World.GetItem(loc);
    if (item == null)
      return null;

    var stack = new ItemStack(item, units);
    item.SetTemperature(Api.World, stack, temp, false);
    return stack;
  }

  #endregion

  // Foreign matter is refused by IsChargeCode; there is no separate family accept list.

  #region Charge identity

  /// <summary>
  /// A shaft counts fuel as charge as well as burden: coke is charged as its own bands rather than mixed
  /// into the burden's stamp, so a column holds <c>iiex:burden</c> and <c>game:coke</c> side by side. The
  /// fuel clause is required - <see cref="ReadChargeMix"/> rejects whatever <c>IsChargeCode</c> refuses, so
  /// without it the furnace would reject the fuel it burns and block its own conversion.
  /// </summary>
  public override bool IsChargeItem(ItemStack? stack) =>
    base.IsChargeItem(stack) || IsFuelStack(stack);

  /// <inheritdoc cref="IsChargeItem"/>
  public override bool IsChargeCode(string? material) =>
    base.IsChargeCode(material) || IsFuelCode(material);

  #endregion

  #region Charge hooks

  /// <summary>
  /// The shaft's charge is its columns. Sealed: the handle type is what the other three charge hooks cast
  /// to, so a leaf returning something else would fail at the cast rather than at compile time.
  /// </summary>
  protected sealed override object CollectCharge() => ShaftColumns;

  /// <summary>The handle type <see cref="CollectCharge"/> hands out, named once so its consumers cast to
  /// the same thing.</summary>
  private static IReadOnlyDictionary<(int X, int Z), ChargeColumn> ColumnsOf(
    object chargeHandle
  ) => (IReadOnlyDictionary<(int X, int Z), ChargeColumn>)chargeHandle;

  /// <summary>
  /// Totals the charge in the shaft and sums its composition in one walk over every segment of every column.
  /// Both are family-blind: a shaft of the wrong burden still lights, burns and reads its coke fraction, it
  /// just will not convert. The rejected count rides alongside so the tick can block the conversion and the
  /// HUD can name the mismatch.
  /// </summary>
  /// <remarks>
  /// The composition is unit-weighted across the whole shaft, not per column and not off the top band, so a
  /// mixed shaft burns at its true average coke ratio - what
  /// <see cref="BlockEntityFurnaceCore.RequiredBlastPressureFor"/> and
  /// <see cref="BlockEntityFurnaceCore.ComputeHeatBalance"/> read.
  /// </remarks>
  protected override int ReadChargeMix(
    object chargeHandle,
    out bool isFull,
    out BurdenMix mix,
    out int rejectedCount
  ) {
    int totalMix = 0;
    int rejected = 0;
    float iron = 0f;
    float flux = 0f;
    float fuel = 0f;

    foreach (ChargeColumn column in ColumnsOf(chargeHandle).Values) {
      foreach (ChargeSegment segment in column.Segments) {
        // Unrecognised matter is counted like everything else and rejected on top, never skipped. Skipping
        // it leaves ConversionBlocked false, so the furnace prints "Melting" over a shaft that can never
        // render a drop, and makes a packed shaft read as "not full". The totals stay blind to acceptance
        // because a furnace full of the wrong stuff really does get hot.
        if (!IsChargeCode(segment.Material))
          rejected += segment.Units;

        totalMix += segment.Units;
        Accumulate(segment, ref iron, ref flux, ref fuel);
      }
    }

    mix = new BurdenMix(iron, flux, fuel);
    // `isFull` means "loaded to capacity" and nothing more on this branch: the core's ignition gate that
    // reads it sits behind `!DerivesState`, which the shaft never takes. It survives as a dirty-tracking and
    // readout flag, measured against the geometric capacity so it and the heat balance cannot disagree.
    isFull = totalMix >= ChargeCapacityUnits;
    rejectedCount = rejected;
    return totalMix;
  }

  /// <summary>
  /// Adds one band's contribution to a running composition, in units rather than fractions, so the caller
  /// ends with a unit-weighted mix over the span it walked. Shared by <see cref="ReadChargeMix"/> (the whole
  /// shaft) and <see cref="CombustionMix"/> (the raceway round), which must read a band the same way.
  /// </summary>
  /// <remarks>
  /// Carbon comes from fuel bands and nowhere else: coke is laid as its own bands, so counting a burden
  /// band's stamped <c>Mix.Fuel</c> too would double the carbon at the raceway. A shaft charged before coke
  /// became its own band therefore reads 0 % carbon and chills, which the chill readout names. The stamp's
  /// fuel component survives as a grade and display field and as the ore share <see cref="OreShareOf"/>
  /// reads.
  /// </remarks>
  private static void Accumulate(
    ChargeSegment segment,
    ref float iron,
    ref float flux,
    ref float fuel
  ) {
    int units = segment.Units;
    if (IsFuelCode(segment.Material)) {
      // Weighted by the fuel's own carbon rather than counted as bands: CarbonPerUnit is 1.0 for coke and
      // 0.5 for charcoal, so a shaft charged 30 % by volume with charcoal reads ~17.6 % carbon and runs
      // cooler.
      fuel += units * CarbonPerUnit(segment.Material);
      return;
    }

    if (segment.Mix.HasContent) {
      iron += segment.Mix.IronFrac * units;
      flux += segment.Mix.FluxFrac * units;
      return;
    }

    // Unstamped charge carries no composition, so it is read at the standard grade's ore and flux shares,
    // with no fuel like every other burden band. The flux share must clear every grade's flux floor, or an
    // old world's charge grades as a shortfall.
    iron +=
      Math.Max(
        0f,
        1f - IiexValues.BfDefaultFuelFrac - IiexValues.BfDefaultFluxFrac
      ) * units;
    flux += IiexValues.BfDefaultFluxFrac * units;
  }

  /// <summary>
  /// Whether the shaft will catch. Two positional conditions, neither of them a tunable number: every column
  /// of the shaft holds charge (a tuyere with nothing in front of it is blowing into empty air), and there
  /// is fuel at the raceway, burden carrying no carbon of its own. The course requirement is the furnace's
  /// own column count, so a redrawn layout updates it.
  /// </summary>
  /// <remarks>
  /// The course condition must not be re-expressed as "at least N units": the two agree on a full course and
  /// disagree everywhere else. Because <c>BfBurnoutFuelRetainedBottom</c> is 0, a furnace that has gone out
  /// has no fuel at its raceway and cannot re-ignite off its own salvage. These two gates are the positional
  /// half of the fire front in <c>docs/design/layered-charge.md</c>, whose per-column lit height is not built.
  /// </remarks>
  protected override bool TryIgniteCharge(object chargeHandle) {
    var columns = ColumnsOf(chargeHandle);
    if (columns.Count == 0)
      return false;

    int perBand = Math.Max(1, ChargeUnitsPerBlock / ChargeColumn.BandsPerBlock);
    foreach (ChargeColumn column in columns.Values)
      if (!RacewayIsLightable(column, perBand))
        return false;
    return true;
  }

  private static bool RacewayIsLightable(ChargeColumn column, int perBand) {
    if (column.TotalUnits <= 0)
      return false; // this column's raceway is empty - the course is not complete

    // A fuel band, never a burden's stamped coke: the stamp is not a carbon supply, so lighting off it would
    // catch a shaft on a burden the heat balance then treats as inert. Same source Accumulate reads.
    foreach (ChargeSegment segment in column.LowestUnits(perBand))
      if (IsFuelCode(segment.Material))
        return true;
    return false;
  }

  /// <summary>
  /// Melting is evaluated every tick rather than on a cycle - see
  /// <see cref="BlockEntityFurnaceCore.MeltsPerTick"/>. The raceway slice routinely spans a coke/burden
  /// boundary, which a per-cycle implementation could only melt all of or none of.
  /// </summary>
  protected sealed override bool MeltsPerTick => true;

  /// <summary>
  /// Burden rendered this tick is the carbon <see cref="CirculateGas"/> burned times
  /// <see cref="IiexValues.BfBurdenPerCarbonUnit"/>, scaled by <c>MeltSpeedFactor</c> so a hotter furnace
  /// makes more iron from the same carbon. The blast is the only throttle: a flat melt rate beside it clears
  /// burden out of the raceway before the coke under it, banking coke in front of the tuyeres.
  /// </summary>
  /// <remarks>
  /// dt is already inside the carbon figure and must not be applied again here, or the melt goes quadratic
  /// in the step and a chunk-load catch-up empties the shaft.
  /// </remarks>
  protected override void SmeltCycle(object chargeHandle, float dt) {
    float budget =
      (
        _carbonBurnedThisTick
        * IiexValues.BfBurdenPerCarbonUnit
        * MeltSpeedFactor()
      ) + _meltCarry;
    int units = (int)budget;
    _meltCarry = budget - units;
    if (units > 0)
      ConsumeForMelting(ColumnsOf(chargeHandle), units);
  }

  /// <summary>
  /// One tick's melt: renders up to <paramref name="unitsToConsume"/> units of burden out of the columns'
  /// raceway slices and banks what it made. A band melts iff its temperature clears the melting point.
  /// </summary>
  /// <remarks>
  /// Burden only - coke leaves a column by burning alone - so the removal is a mid-span rewrite rather than
  /// a take off the bottom, which would destroy coke on top of what <see cref="CirculateGas"/> burns and set
  /// campaign length by the melt rate instead of the blast (<c>docs/design/layered-charge.md</c>). The heat
  /// the coke gave a band is already on it, put there by <see cref="ChargeColumn.RiseGasThrough"/>. A cold
  /// band stops its column rather than being skipped, everything above resting on it; the split is
  /// round-robin in ascending <c>(x, z)</c> with the remainder to the first keys.
  /// </remarks>
  private void ConsumeForMelting(
    IReadOnlyDictionary<(int X, int Z), ChargeColumn> columns,
    int unitsToConsume
  ) {
    // No per-segment acceptance filter: the tick blocks the whole melt cycle while ConversionBlocked, so a
    // filter here would never have anything to filter. Wrong-family units are still consumed by the burn.
    var ordered = new List<ChargeColumn>();
    foreach (var key in Ordered(columns.Keys))
      ordered.Add(columns[key]);

    float product = 0f;
    float slag = 0f;
    int remaining = unitsToConsume;
    while (remaining > 0) {
      var live = new List<ChargeColumn>();
      foreach (ChargeColumn column in ordered)
        if (column.TotalUnits > 0)
          live.Add(column);
      if (live.Count == 0)
        break;

      // At least 1, so the loop always makes progress and terminates even when the remainder is thinner
      // than the column count.
      int each = Math.Max(1, remaining / live.Count);
      bool progressed = false;
      foreach (ChargeColumn column in live) {
        if (remaining <= 0)
          break;
        int want = Math.Min(each, Math.Min(remaining, column.TotalUnits));
        int melted = MeltBurden(column, want, ref product, ref slag);
        if (melted <= 0)
          continue; // this column is chilled, or holds only coke - it hangs while the others descend
        remaining -= melted;
        progressed = true;
      }

      // Every live column is hung. Without this the loop spins for ever on a stalled furnace, an ordinary
      // state here rather than an exceptional one.
      if (!progressed)
        break;
    }

    // Blocks disappear as the columns shorten, and every surviving pile republishes its snapshot.
    SyncChargeBlocks();

    // A cell holds whole units where the old float pool did not, so the fraction each cycle renders is
    // carried rather than truncated away - otherwise a furnace loses up to a unit of each per cycle, which
    // over a campaign is a real yield cut. Deliberately not serialized: it is worth less than one unit, and
    // a pool key on the furnace is exactly what moving the pool into the world removed.
    _productCarry += product;
    _slagCarry += slag;
    int wholeProduct = (int)_productCarry;
    int wholeSlag = (int)_slagCarry;
    _productCarry -= wholeProduct;
    _slagCarry -= wholeSlag;

    PoolIntoHearth(wholeProduct, wholeSlag);
  }

  /// <summary>Fractions of a unit rendered but not yet poolable, carried to the next cycle.</summary>
  private float _productCarry;
  private float _slagCarry;

  /// <summary>
  /// Puts what the cycle rendered into the crucible floor: the hearth block goes down in every free pool
  /// cell and the metal is spread across their cells, remainder to the first, so two identical furnaces
  /// fill identically. Overflow past a cell's capacity is refused by the cell and is what
  /// <see cref="LiquidCapacityReached"/> then reports.
  /// </summary>
  private void PoolIntoHearth(int product, int slag) {
    if (Api.Side != EnumAppSide.Server || (product <= 0 && slag <= 0))
      return;

    List<BlockPos> cells = ClaimPoolCells();
    if (cells.Count == 0)
      return;

    Spread(
      cells,
      product,
      BlockEntityHearthMetal.IronCellKey,
      MetalProductCode
    );
    Spread(cells, slag, BlockEntityHearthMetal.SlagCellKey, "slag");
  }

  /// <summary>Spreads <paramref name="units"/> of <paramref name="metal"/> over the named cell of each
  /// claimed hearth block, remainder to the first cells.</summary>
  private void Spread(
    List<BlockPos> cells,
    int units,
    string cellKey,
    string metal
  ) {
    if (units <= 0)
      return;

    string carrier = MetalRegistry.MoltenItemOf(metal).ToString();
    int each = units / cells.Count;
    int extra = units % cells.Count;

    for (int i = 0; i < cells.Count; i++) {
      int share = each + (i < extra ? 1 : 0);
      if (share <= 0)
        continue;

      BEBehaviorMoltenCell? cell = Api
        .World.BlockAccessor.GetBlockEntity(cells[i])
        .MoltenCell(cellKey);
      if (cell == null)
        continue;

      // Capacity follows the live config rather than a number baked into the block definition, and is
      // the same band for both cells: one crucible volume, with slag floating on the metal.
      cell.SetCapacity(IiexValues.HearthUnitsPerBand);
      cell.PushMetalRaw(share, carrier, _internalTemp, Api.World);
    }
  }

  /// <summary>Units of metal standing on the crucible floor, summed over the hearth blocks' iron cells.</summary>
  protected int PooledMetalUnits =>
    PooledUnits(BlockEntityHearthMetal.IronCellKey);

  /// <summary>Units of slag standing on the crucible floor.</summary>
  protected int PooledSlagUnits =>
    PooledUnits(BlockEntityHearthMetal.SlagCellKey);

  private int PooledUnits(string cellKey) {
    int total = 0;
    foreach (BlockPos pos in PoolCells)
      total +=
        Api.World.BlockAccessor.GetBlockEntity(pos)
          .MoltenCell(cellKey)
          ?.CellAmount
        ?? 0;
    return total;
  }

  /// <summary>
  /// Melts up to <paramref name="want"/> units of burden out of <paramref name="column"/>'s raceway slice,
  /// removes them, accumulates what they rendered, and returns how much actually went. Walks
  /// <see cref="RacewayDepthUnits"/> - the same span <see cref="BurnCarbon"/> burns in - and is exact at
  /// unit granularity: the rewrite splits whatever band the budget runs out inside, and both halves keep
  /// their temperature.
  /// </summary>
  /// <remarks>
  /// Coke is stepped over, never consumed; it is already being spent by the burn. A cold burden band stops
  /// the column rather than being skipped - see <see cref="ConsumeForMelting"/>.
  /// </remarks>
  private int MeltBurden(
    ChargeColumn column,
    int want,
    ref float product,
    ref float slag
  ) {
    if (want <= 0)
      return 0;

    int budget = want;
    int melted = 0;
    bool chilled = false;
    float made = 0f;
    float cinder = 0f;

    column.RewriteLowest(
      RacewayDepthUnits,
      segment => {
        if (chilled || budget <= 0 || IsFuelCode(segment.Material))
          return segment;

        if (segment.Temperature < _ironMeltingPoint) {
          chilled = true;
          return segment;
        }

        int take = Math.Min(segment.Units, budget);
        budget -= take;
        melted += take;
        made += take * ProductPerUnit(segment.Mix);
        cinder += take * SlagPerUnit(segment.Mix);
        return segment with { Units = segment.Units - take };
      }
    );

    product += made;
    slag += cinder;
    return melted;
  }

  /// <summary>Column keys in ascending <c>(x, z)</c> - the deterministic order every walk over the shaft
  /// shares, so two identical furnaces behave identically and campaign length is stable.</summary>
  private static List<(int X, int Z)> Ordered(IEnumerable<(int X, int Z)> keys) {
    var ordered = new List<(int X, int Z)>(keys);
    ordered.Sort(
      (a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z)
    );
    return ordered;
  }

  #endregion

  #region The raceway

  // All the coke burns at the bottom of each column and nowhere else. The gas it makes rises and warms the
  // burden descending toward it, so a band is heated by coke that burned beneath it and spends its own only
  // when its turn comes. A taller shaft is therefore more efficient, hot blast saves coke, and campaign
  // length is how much coke was charged.

  /// <summary>
  /// Gas temperature leaving the top of the shaft, in °C - what the outlets vent. Not saved: it is
  /// recomputed from the columns on the first lit tick after a load, and an idle furnace vents nothing.
  /// </summary>
  private float _stackGasTemp = 20f;

  /// <summary>
  /// How deep into a column the raceway reaches, in charge units - one whole round, which is one charge
  /// block's worth. Spanning a whole round (at least one coke course plus one burden course) is what makes
  /// the coke fraction it reads the ratio the player charged: a slice inside one course would alternate
  /// between ~1.0 and ~0.0 and swing the required blast pressure from 1.2 to 3.5 atm against a twin-tub
  /// blower that tops out at 2.2.
  /// </summary>
  /// <remarks>
  /// Derived rather than a config key: it follows <see cref="BlockEntityFurnaceCore.ChargeUnitsPerBlock"/>,
  /// so the cupola's remelt quantum moves it without a second override.
  /// </remarks>
  protected virtual int RacewayDepthUnits => Math.Max(1, ChargeUnitsPerBlock);

  /// <summary>
  /// The composition actually burning: a unit-weighted mix over the lowest <see cref="RacewayDepthUnits"/>
  /// of every column, which is what sets the flame temperature. Reading the round at the raceway rather than
  /// the shaft average (<see cref="ReadChargeMix"/>) makes the flame drop when a lean round arrives. Across
  /// every column, the furnace having one flame temperature and one heat balance.
  /// </summary>
  protected override BurdenMix CombustionMix(
    object chargeHandle,
    BurdenMix shaftMix
  ) {
    float iron = 0f;
    float flux = 0f;
    float fuel = 0f;
    bool any = false;

    foreach (ChargeColumn column in ColumnsOf(chargeHandle).Values)
      foreach (ChargeSegment segment in column.LowestUnits(RacewayDepthUnits)) {
        any = true;
        Accumulate(segment, ref iron, ref flux, ref fuel);
      }

    // An empty raceway means nothing is in front of the tuyeres. Handing back the shaft average would let a
    // shaft charged entirely above the raceway burn as though it were not; the caller's empty-mix fallback
    // applies instead.
    return any ? new BurdenMix(iron, flux, fuel) : default;
  }

  /// <summary>
  /// The raceway flame follows the coke arriving in front of the tuyeres with no lag of its own. Sealed: a
  /// shaft carries its inertia on the charge segments, which warm through over minutes, so the first-order
  /// chase this replaces would put a second inertia in series with that one and a lean round would take far
  /// longer to be felt.
  /// </summary>
  protected sealed override void ApplyProcessTemperature(
    float targetTemp,
    float dt
  ) => _internalTemp = targetTemp;

  /// <summary>
  /// One tick's counter-current pass: the gas the raceway made climbs every column, warming each band
  /// toward it and arriving at the stockline with whatever is left. Air is the reagent, so the carbon
  /// budget scales with <c>HeatBalance.AirFactor</c> rather than being a flat rate, and a stopped blower
  /// slows the fire to natural draught.
  /// </summary>
  /// <remarks>
  /// How hot the gas is comes from <see cref="BlockEntityFurnaceCore.ComputeHeatBalance"/>, where the
  /// preheat lives; how much there is comes from the carbon this tick burned, not the coke merely standing
  /// at the raceway. Runs per column, each having its own raceway and coke; the stack temperature is the
  /// hottest the columns vented, the outlets sharing one flue.
  /// </remarks>
  protected override void CirculateGas(object chargeHandle, float dt) {
    _carbonBurnedThisTick = 0f;

    var columns = ColumnsOf(chargeHandle);
    if (columns.Count == 0)
      return;

    // What the blast delivered decides the carbon, and the carbon decides everything else.
    float budget =
      (RacewayCarbonPerSecond * _lastHeatBalance.AirFactor * dt) + _burnCarry;

    // Spent over the columns in a rotating order rather than split evenly. A fuel band burns in whole units,
    // so an even split hands each column ~0.04 u a second, which truncates to nothing every tick; the budget
    // then rides `_burnCarry` for ~26 s and every column burns on the same tick, pulsing the state between
    // Melting and Firing. Rotating the start spends the same total, landing evenly over any real stretch.
    // The per-column cap keeps a large budget spread rather than letting the first column swallow it.
    var ordered = Ordered(columns.Keys);
    int start = _burnRotation % ordered.Count;
    _burnRotation = (_burnRotation + 1) % ordered.Count;
    float perColumn = Math.Max(1f, MathF.Ceiling(budget / ordered.Count));

    float hottest = _ambientTemp;
    float left = budget;
    float spent = 0f;
    bool moved = false;

    for (int i = 0; i < ordered.Count && left >= 1f; i++) {
      ChargeColumn column = columns[ordered[(start + i) % ordered.Count]];
      float burned = BurnCarbon(column, Math.Min(perColumn, left));
      left -= burned;
      if (burned <= 0f)
        continue;

      spent += burned;
      float stack = column.RiseGasThrough(
        _internalTemp,
        burned * IiexValues.BfRacewayGasPerCokeUnit,
        // Per unit, so it scales inversely with the unit size - see ChargeUnitScale. A cupola unit is ~94
        // charge items, so at the raw fraction one band would strip the gas and the shaft above would
        // never warm.
        IiexValues.BfShaftGasTransferFrac / ChargeUnitScale,
        ChargeUnitsPerBlock
      );
      hottest = Math.Max(hottest, stack);
      moved = true;
    }

    // Carbon a starved or chilled column could not supply is carried, not lost: a rate under one unit a
    // second must still accumulate, or a slow fire never burns anything.
    _burnCarry = left;
    _carbonBurnedThisTick = spent;

    if (!moved)
      return;
    _stackGasTemp = hottest;
    // The glow is push-based, so a column whose temperature moved must be told to redraw even though its
    // height did not. SyncChargeBlocks is the only route that republishes a pile's snapshot, and it sends
    // nothing when the picture is unchanged.
    SyncChargeBlocks();
  }

  /// <summary>
  /// Spends up to <paramref name="want"/> units of carbon out of one column's raceway slice and returns what
  /// it actually got. A fuel band is carbon, so it burns away entirely and the column descends into the gap;
  /// a burden band's stamped coke burns out of the stamp while the ore stays put until it melts, so the
  /// band's ore share rises as its carbon goes.
  /// </summary>
  /// <remarks>
  /// The stamped-coke path serves saved worlds charged while burden still carried its coke in the stamp,
  /// which hold no fuel bands. Burning happens while the furnace is merely Firing, not only while it
  /// produces: a furnace sitting hot and melting nothing still burns its charge away.
  /// </remarks>
  private float BurnCarbon(ChargeColumn column, float want) {
    if (want <= 0f || column.TotalUnits <= 0)
      return 0f;

    float left = want;
    column.RewriteLowest(
      RacewayDepthUnits,
      segment => {
        // Fuel bands only. A burden band's stamped coke is not a carbon supply - see Accumulate. Burden
        // leaves the raceway by melting and only by melting; a band the fire cannot melt stays where it
        // is, which is the chill.
        float carbon = CarbonPerUnit(segment.Material);
        if (left <= 0f || carbon <= 0f)
          return segment;

        // The budget is carbon and the band is units, so the conversion happens per segment inside the walk:
        // one raceway slice can span a coke course and a charcoal one, and resolving the weight once before
        // the walk would spend one at the other's price. Whole units only, since a fuel band is items; the
        // remainder rides _burnCarry to the next tick.
        int burn = (int)Math.Min(segment.Units, left / carbon);
        left -= burn * carbon;
        return segment with { Units = segment.Units - burn };
      }
    );

    return want - left;
  }

  /// <summary>
  /// The gas temperature the columns actually vented, rather than a fixed fraction of the furnace's own
  /// temperature (<see cref="BlockEntityFurnaceCore.ExhaustTempFactor"/>). A well-charged tall furnace
  /// therefore exhausts cooler, and what a cowper can regenerate is what the furnace actually wasted.
  /// </summary>
  protected override float ExhaustTemperature => _stackGasTemp;

  /// <summary>
  /// The shaft has no state machine: its state is recomputed from the charge every tick - Idle when nothing
  /// is burning, Melting when what has arrived at a raceway is hot enough to render, Firing in between.
  /// Nothing is stored and nothing counts down, so the fuel clock, melt-start soak, melt interval, cold-soak
  /// reversal and extinguish countdown are all unused on this branch.
  /// </summary>
  /// <remarks>
  /// <see cref="BlownIn"/> is the one exception, and it is not a state machine: it records that a player
  /// lit the furnace, which no amount of looking at the charge can answer. A choked furnace suffocates and
  /// goes out; one that has merely lost its blast falls back to natural draught and keeps burning. See
  /// <c>docs/design/layered-charge.md</c>.
  /// </remarks>
  protected sealed override bool DerivesState => true;

  /// <summary>
  /// Whether a player has lit this furnace. Set by a flame held through an open tap-hole and cleared when
  /// the furnace goes out, so blowing in is once per campaign: the residue burn takes the carbon at every
  /// raceway with it (<c>BfBurnoutFuelRetainedBottom</c> is 0), and a recharged shaft has to be lit again.
  /// </summary>
  public bool BlownIn { get; private set; }

  /// <summary>
  /// Lights the furnace from a tap. The tap has already checked that it is open and that it belongs to
  /// this furnace; what is left here is the flame itself. Whether the charge catches is
  /// <see cref="DeriveState"/>'s to decide on the next production tick, so this asks none of the questions
  /// it asks - "will it light" and "is it still alight" have to keep giving one answer.
  /// </summary>
  public override bool TryLightFromTap(BlockPos tapPos) {
    if (BlownIn)
      return false;

    BlownIn = true;
    ExSounds.Play(Api, tapPos, ExSounds.Ignite, 1f, 32f);
    MarkDirty(true);
    return true;
  }

  /// <summary>
  /// Going out ends the blow-in with everything else it ends. The residue burn is what makes this safe to
  /// latch rather than re-derive: it leaves no carbon at any raceway, so there is nothing for a stale flag
  /// to relight.
  /// </summary>
  protected override void ExtinguishResidue() {
    BlownIn = false;
    base.ExtinguishResidue();
  }

  protected sealed override FurnaceState DeriveState(object chargeHandle) {
    if (IsChoked || !RacewayHoldsCarbon(chargeHandle))
      return FurnaceState.Idle;

    // Nobody has put a flame in it. A charged, blown, structurally sound shaft sits cold until a player
    // reaches a torch through an open tap-hole: this is the one bit of shaft state that is not derived,
    // and the reason it has to be stored is that the charge alone cannot say whether it was lit.
    if (!BlownIn)
      return FurnaceState.Idle;

    // A breached furnace keeps burning but can never be re-lit. Without this clause a player could knock a
    // wall out of a cold furnace and light it through the hole.
    if (!StructureComplete && State == FurnaceState.Idle)
      return FurnaceState.Idle;

    // A shaft holding wrong-family charge reads Firing however hot it is: it will render nothing while a
    // rejected pile is in it, and Melting is what the HUD prints and the sounds play on.
    return !ConversionBlocked && AtMeltingTemperature(chargeHandle)
      ? FurnaceState.Melting
      : FurnaceState.Firing;
  }

  /// <summary>
  /// Whether there is carbon in front of the tuyeres, on every column. The same positional question
  /// <see cref="TryIgniteCharge"/> asks, evaluated continuously: "will it light" and "is it still alight"
  /// must not give two different answers.
  /// </summary>
  private bool RacewayHoldsCarbon(object chargeHandle) {
    var columns = ColumnsOf(chargeHandle);
    if (columns.Count == 0)
      return false;

    foreach (ChargeColumn column in columns.Values) {
      bool carbon = false;
      foreach (ChargeSegment segment in column.LowestUnits(RacewayDepthUnits))
        if (IsFuelCode(segment.Material)) {
          carbon = true;
          break;
        }
      if (!carbon)
        return false;
    }
    return true;
  }

  /// <summary>
  /// A shaft is melting when the flame is over the line and at least one column is offering burden the
  /// raceway can take. Both halves are needed: the flame alone is reached within a tick of lighting, and hot
  /// burden alone can sit in a furnace whose fire has died.
  /// </summary>
  /// <remarks>
  /// It asks <see cref="RacewayBurden"/> for the first burden band rather than scanning the slice for any
  /// hot band, because <see cref="MeltBurden"/> stops at the first cold band: a column laid
  /// <c>[cold burden][hot burden]</c> would otherwise report Melting while melting nothing. Reading the
  /// first band is also what makes a hang halt the furnace - with every column chilled no column offers
  /// anything, so the state derives Firing and the furnace burns its carbon away.
  /// </remarks>
  protected sealed override bool AtMeltingTemperature(object chargeHandle) {
    if (!base.AtMeltingTemperature(chargeHandle))
      return false;

    foreach (ChargeColumn column in ColumnsOf(chargeHandle).Values)
      if (
        RacewayBurden(column) is { } burden
        && burden.Temperature >= _ironMeltingPoint
      )
        return true;
    return false;
  }

  #endregion

  #region The chill

  /// <summary>
  /// The first burden band inside the raceway slice - the one <see cref="MeltBurden"/> will meet - or null
  /// when the slice is all fuel. The first, not the hottest and not the average: the melt walk steps over
  /// fuel and stops at the first burden it cannot melt.
  /// </summary>
  private ChargeSegment? RacewayBurden(ChargeColumn column) {
    foreach (ChargeSegment segment in column.LowestUnits(RacewayDepthUnits))
      if (!IsFuelCode(segment.Material))
        return segment;
    return null;
  }

  /// <summary>
  /// Whether the column at <c>(<paramref name="localX"/>, <paramref name="localZ"/>)</c> is hung:
  /// under-coked burden has arrived at its raceway too cold to melt, so it does not leave, no space
  /// appears beneath the column and nothing above it descends.
  /// </summary>
  /// <remarks>
  /// Derived on each call and stored nowhere - a saved hang could disagree with the column it describes -
  /// and cheap, being one walk of the raceway slice. The gate is
  /// <see cref="BlockEntityFurnaceCore.State"/>, last tick's derived value, never recomputed here, so there
  /// is no cycle with the derivation. A column whose raceway slice holds no burden is not hung either - it
  /// is a fuel course waiting for its burden.
  /// </remarks>
  public bool IsHung(int localX, int localZ) =>
    State != FurnaceState.Idle
    && ChargeColumnAt(localX, localZ) is { } column
    && RacewayBurden(column) is { } burden
    && burden.Temperature < _ironMeltingPoint;

  /// <summary>How many of this shaft's columns are hung right now - 0 on any idle furnace.</summary>
  /// <remarks>
  /// There is no halt threshold. A hung column offers <see cref="MeltBurden"/> nothing, so when every column
  /// hangs the furnace stops making iron by arithmetic and <see cref="AtMeltingTemperature"/> reports
  /// Firing. Its fuel keeps burning while its burden does not melt, so the raceway mix goes leaner and
  /// <see cref="BlockEntityFurnaceCore.RequiredBlastPressureFor"/>'s shortfall term raises the pressure the
  /// furnace demands.
  /// </remarks>
  public int HungColumnCount {
    get {
      if (State == FurnaceState.Idle)
        return 0;
      int hung = 0;
      foreach (var (x, z) in ShaftColumns.Keys)
        if (IsHung(x, z))
          hung++;
      return hung;
    }
  }

  /// <summary>
  /// Names the hang, because nothing else about it is visible: a hung furnace stays lit, stays hot, keeps
  /// eating fuel and stops making iron. The whole-stockline case gets its own line rather than reading
  /// "9 of 9".
  /// </summary>
  protected override void AppendChillInfo(StringBuilder sb) {
    int hung = HungColumnCount;
    if (hung <= 0)
      return;

    int total = Math.Max(1, ShaftColumns.Count);
    sb.AppendLine(
      hung >= total
        ? Lang.Get("iiex:bf-info-hungall")
        : Lang.Get("iiex:bf-info-hung", hung, total)
    );
  }

  #endregion

  #region Extinguish residue

  /// <summary>
  /// Burns the column out by height: the bands on the tuyeres are stripped to
  /// <c>BfBurnoutFuelRetainedBottom</c> of their carbon, and the bands at the top of the shaft, which the
  /// blast never reached, keep <c>BfBurnoutFuelRetainedTop</c>. Iron and flux survive verbatim.
  /// </summary>
  /// <remarks>
  /// Fuel bands are consumed by the same fraction rather than merely scaled in a stamp; touching only
  /// <c>BurdenMix.Fuel</c> would leave every coke band whole, so the raceway would still hold carbon and
  /// re-arm <see cref="TryIgniteCharge"/> on the next tick. The height fraction is the block's own position
  /// in the shaft (<c>(y - yMin) / span</c>), which is why <see cref="ChargeColumn.Rewrite"/> splits per
  /// block: a block is what the player digs out.
  /// </remarks>
  protected override void BurnOutCharge() {
    if (ShaftBox is not { } box)
      return;

    int yMin = Math.Min(box.min.Y, box.max.Y);
    int yMax = Math.Max(box.min.Y, box.max.Y);
    float span = Math.Max(1, yMax - yMin);
    int perBlock = ChargeUnitsPerBlock;
    float retainedBottom = IiexValues.BfBurnoutFuelRetainedBottom;
    float retainedTop = IiexValues.BfBurnoutFuelRetainedTop;
    // The shares ReadChargeMix assumes for unstamped charge, so a legacy segment is stamped with exactly
    // what it was already burning as.
    float legacyIron = Math.Max(
      0f,
      1f - IiexValues.BfDefaultFuelFrac - IiexValues.BfDefaultFluxFrac
    );

    foreach (var (key, column) in ShaftColumns) {
      IReadOnlyList<Vec3i> cells = ChargeCellsOf(key.X, key.Z);
      if (cells.Count == 0)
        continue;

      column.Rewrite(
        perBlock,
        (segment, blockIndex) => {
          // A column may stand taller than it has cells to draw in (charged past its own roof). Clamping to
          // the top cell keeps such a band at the top course's retention rather than throwing.
          Vec3i cell = cells[Math.Min(blockIndex, cells.Count - 1)];
          float height = GameMath.Clamp((cell.Y - yMin) / span, 0f, 1f);
          float retained = GameMath.Lerp(retainedBottom, retainedTop, height);

          // A fuel band is carbon, so the retention applies to the band itself. Rounding down leaves a
          // fully-burned bottom course with nothing rather than a one-unit ember.
          if (IsFuelCode(segment.Material))
            return segment with { Units = (int)(segment.Units * retained) };

          // Burden: strip the stamped carbon, keep the ore and flux.
          if (segment.Mix.HasContent)
            return segment with {
              Mix = segment.Mix with { Fuel = segment.Mix.Fuel * retained },
            };

          // Legacy count-only charge carries no stamp, so burn-out stamps it with the standard-grade shares
          // its own burn assumed, its carbon scaled by the same retention. Left unstamped, a derived
          // ignition gate would read it as still carrying the reference coke fraction, so a dead furnace
          // could relight off its own salvage indefinitely.
          return segment with {
            Mix = new BurdenMix(
              legacyIron,
              IiexValues.BfDefaultFluxFrac,
              IiexValues.BfDefaultFuelFrac * retained
            ),
          };
        }
      );
    }

    // The columns are shorter where fuel bands burned away; make the world say so.
    SyncChargeBlocks();
  }

  #endregion

  #region Products: capacity, drain, residue

  // A furnace whose drawing marks no pool cell has no crucible to fill and so can never back up on its
  // own output - the hearths are the case. Without the count test its empty pool would read as a full
  // one (0 >= 0) and melting would be blocked for ever.
  protected override bool LiquidCapacityReached =>
    PoolCells.Count > 0
    && (
      PooledMetalUnits >= MaxPooledMetalUnits
      || PooledSlagUnits >= MaxPooledSlagUnits
    );

  protected override void DrainProducts(ref bool dirty) {
    DrainIronTap(ref dirty);
    DrainSlagTap(ref dirty);
  }

  private void DrainIronTap(ref bool dirty) {
    // The drain point comes from the layout, not a literal: a furnace whose drawing marks no metal tap
    // never drains, as it would if the tap block were missing from the cell.
    if (MetalTapPos is not { } lowerTapPos)
      return;
    if (
      Api.World.BlockAccessor.GetBlockEntity(lowerTapPos)
        is not BlockEntityFurnaceTap lowerTap
      || !lowerTap.IsPouring
      || PooledMetalUnits <= 0
    )
      return;

    int units = Math.Min(IiexValues.TapDrainPerTick, PooledMetalUnits);
    ItemStack? ironStack = CreateMoltenStack(
      MetalProductCode,
      (int)Math.Ceiling(units * IiexValues.TapIronStackFactor),
      _internalTemp
    );
    if (ironStack == null)
      return;

    int accepted = lowerTap.TryPourMetal(ironStack, _internalTemp);
    if (accepted > 0) {
      DrainFromCells(BlockEntityHearthMetal.IronCellKey, accepted);
      dirty = true;
      ExSounds.PlayThrottled(
        Api,
        lowerTapPos,
        ExSounds.MoltenMetal,
        ref _lastTapSoundMs,
        2000,
        0.5f
      );
    }
  }

  private void DrainSlagTap(ref bool dirty) {
    if (SlagTapPos is not { } higherTapPos)
      return;
    if (
      Api.World.BlockAccessor.GetBlockEntity(higherTapPos)
        is not BlockEntityFurnaceTap higherTap
      || !higherTap.IsPouring
      || PooledSlagUnits <= 0
    )
      return;

    int units = Math.Min(IiexValues.TapDrainPerTick, PooledSlagUnits);
    ItemStack? slagStack = CreateMoltenStack(
      "slag",
      (int)Math.Ceiling(units * IiexValues.TapSlagStackFactor),
      _internalTemp
    );
    if (slagStack == null)
      return;

    int accepted = higherTap.TryPourMetal(slagStack, _internalTemp);
    if (accepted > 0) {
      DrainFromCells(BlockEntityHearthMetal.SlagCellKey, accepted);
      dirty = true;
      ExSounds.PlayThrottled(
        Api,
        higherTapPos,
        ExSounds.MoltenMetal,
        ref _lastTapSoundMs,
        2000,
        0.5f
      );
    }
  }

  // The residue itself - freeze the pool onto the hearth floor, burn the burden out by height, never slag -
  // is the core's. This branch supplies only what its pool was made of; a cupola swaps these four members
  // for cast iron and inherits the rest.

  protected override AssetLocation SolidProductBlock { get; } =
    new("iiex", "hearthmetal-pigiron");

  /// <summary>
  /// Takes <paramref name="amount"/> units out of the named cell across the crucible floor, cell by cell
  /// until it is met. Draining in place rather than proportionally keeps the arithmetic the tap's own
  /// regression numbers pin.
  /// </summary>
  private void DrainFromCells(string cellKey, int amount) {
    int left = amount;
    foreach (BlockPos pos in PoolCells) {
      if (left <= 0)
        break;
      BEBehaviorMoltenCell? cell = Api
        .World.BlockAccessor.GetBlockEntity(pos)
        .MoltenCell(cellKey);
      if (cell == null || cell.CellAmount <= 0)
        continue;
      left -= cell.DrainMetal(left);
    }
  }

  #endregion

  #region Serialization

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldAccessForResolve
  ) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    // The pool is no longer furnace state: it lives in the hearth blocks' own cells, which serialize
    // themselves. The old "moltenIron"/"moltenSlag" keys are dropped rather than migrated - a running
    // furnace re-pools within a cycle, and a dead one already froze.
    //
    // Defaulted to whatever the furnace was doing: a shaft saved before the blow-in existed carries no
    // key, and reading that as "never lit" would put every running furnace in every existing world out on
    // load - the state is derived, so it would be Idle again on the first tick with nothing to say why.
    BlownIn = tree.GetBool("blownIn", State != FurnaceState.Idle);
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetBool("blownIn", BlownIn);
  }

  #endregion

  #region HUD product lines

  // The molten pools are shown at the taps that drain them rather than at the core: the lower tap surfaces
  // the metal pool, the upper the slag, each picking which by comparing its cell to Metal/SlagTapPos.
  // Self-gated so the readout appears only when there is metal to pour.

  public override void AppendMoltenMetalInfo(StringBuilder sb) {
    int pooled = PooledMetalUnits;
    if (!StructureComplete || (State != FurnaceState.Melting && pooled <= 0))
      return;
    sb.AppendLine(
      Lang.Get(
        MoltenProductInfoLangKey,
        (float)pooled,
        (float)MaxPooledMetalUnits
      )
    );
  }

  public override void AppendMoltenSlagInfo(StringBuilder sb) {
    int pooled = PooledSlagUnits;
    if (!StructureComplete || (State != FurnaceState.Melting && pooled <= 0))
      return;
    sb.AppendLine(
      Lang.Get(
        IiexLang.BfInfoMoltenslag,
        (float)pooled,
        (float)MaxPooledSlagUnits
      )
    );
  }

  /// <summary>
  /// An idle-but-ready shaft reads plainly ready. <c>bf-info-partiallylit</c> has no producer here: lit-ness
  /// is derived (<see cref="TryIgniteCharge"/>) with no per-pile half to be partial about, and the key is
  /// reserved for the design's fire front.
  /// </summary>
  protected override void AppendReadyInfo(StringBuilder sb) =>
    sb.AppendLine(Lang.Get(IiexLang.BfInfoReady));

  #endregion
}
