using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Materials;
using ExpandedLib.Metals;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The <b>shaft furnace</b>: a <see cref="BlockEntityFurnaceCore"/> whose charge stands in a vertical
/// column over the raceway and descends through it, blown through tuyeres, pooling a molten product it
/// pours out of two taps. The cold and hot blast furnaces and the cupola are all this machine.
/// <para>
/// There is exactly one blast furnace in the mod. The cold and the hot furnace are the same machine
/// running under different conditions, so they are the same class, and the difference between them is
/// data: the hot furnace's layout carries exhaust outlets the cold one has no cell for, and a cowper
/// on its blast line arrives preheated at the tuyeres. Everything a player would call "the hot blast
/// furnace" - clearing iron's melt line on a leaner burden, and rendering it faster once it does -
/// falls out of <see cref="BlockEntityFurnaceCore.ComputeHeatBalance"/> reading those two facts. The
/// subclasses below exist to be distinct registered types with distinct layouts, not distinct
/// behaviour; the previous pair of byte-identical copies is what this replaces.
/// </para>
/// <para>
/// <b>The name is the invariant.</b> A furnace on this branch holds a layered charge column, so
/// <see cref="ShaftHoldsLayeredCharge"/> is declared true here once rather than opted into per leaf.
/// A hearth - a reverberatory firebox, a kiln, a crucible - is <b>not</b> a shaft furnace and belongs on
/// <see cref="BlockEntityFireboxFurnace"/>; that is what stops it inheriting a shaft model for a firebox.
/// See <c>docs/design/conventions.md</c> § "the furnace axes become a class tree".
/// </para>
/// </summary>
public abstract class BlockEntityShaftFurnace : BlockEntityFurnaceCore
{
  private float _moltenIron = 0;
  private float _moltenSlag = 0;

  /// <summary>Fractional charge unit carried between ticks, so a rate under one unit a second still
  /// descends instead of rounding to nothing every tick and stalling the furnace outright.</summary>
  private float _meltCarry;

  /// <summary>
  /// Carbon the raceways actually burned this tick, in charge units - what <see cref="CirculateGas"/>
  /// spent and what <see cref="SmeltCycle"/> then meters the melt against.
  /// <para>
  /// <b>One number decides everything the furnace does in a second</b>: how hot the gas is, how much of
  /// it rises, how far the column descends, and now how much burden renders. Not saved - it is recomputed
  /// every tick, and a furnace that has not ticked has burned nothing.
  /// </para>
  /// <para>
  /// <b>The ordering is load-bearing.</b> <c>OnProductionTick</c> runs <c>CirculateGas</c> before the
  /// <c>Melting</c> branch reaches <c>SmeltCycle</c>, so this is always this tick's figure. Reversed, the
  /// melt would meter itself against the previous second's carbon - which is only wrong by a tick, and so
  /// exactly the kind of drift no test would ever catch.
  /// </para>
  /// </summary>
  private float _carbonBurnedThisTick;

  /// <summary>Carbon the raceway was owed but could not draw - a starved or chilled column, or a rate
  /// under one unit a second. Carried rather than dropped, or a slow fire never burns anything.</summary>
  private float _burnCarry;

  /// <summary>Which column the tick's carbon spend starts at, advanced every tick - see
  /// <see cref="CirculateGas"/> for why an even split cannot work on whole units.</summary>
  private int _burnRotation;
  private float _maxMoltenIron;
  private float _maxMoltenSlag;

  #region Tunables

  protected override float MeltingPoint => IwexValues.BfIronMeltingPoint;

  /// <summary>
  /// <b>Zero, and never read on this branch.</b> A campaign ends when the carbon in
  /// the shaft is gone, which is what the raceway already meters - so a wall-clock burn limit is a second,
  /// disagreeing answer to "how long does a furnace run". The core's countdown that consumes it sits behind
  /// <c>!DerivesState</c> and only the firebox branch reaches it.
  /// <para>
  /// The firebox branch's equivalent is <c>FireboxMaxFuelBurnTime</c>: a fuel bed
  /// genuinely does burn for a time rather than for a quantity.
  /// </para>
  /// </summary>
  protected sealed override int MaxFuelBurnTime => 0;

  /// <summary>
  /// <b>Zero on the shaft branch.</b> A melt-start soak stands in for a furnace warming through - and
  /// the counter-current model warms it for real, band by band, off the coke actually burning. Keeping
  /// both makes the shaft serve two soaks in series, and the second one is invisible: a furnace whose
  /// charge is genuinely at temperature sits at it doing nothing for five more minutes.
  /// <para>
  /// A soak here is actively wrong, not merely redundant: a shaft charged near the fire threshold burns
  /// its carbon out in under 300 s, so the soak expires <em>after</em> the campaign ends and a legitimate
  /// small furnace can never melt at all.
  /// </para>
  /// <para>
  /// <see cref="BlockEntityFireboxFurnace"/>, which has no raceway to warm through, genuinely needs a
  /// soak; its key is <c>FireboxMeltStartDelay</c>.
  /// </para>
  /// </summary>
  protected sealed override float MeltStartDelay => 0f;

  /// <summary>
  /// <b>Zero, and never read on this branch.</b> The shaft sets <c>MeltsPerTick</c>,
  /// so the raceway renders whatever descended past it this second and the cadence <em>is</em> the descent -
  /// there is no cycle length to wait out. The core's interval branch is the <c>else</c> the shaft never
  /// takes; the firebox branch's key is <c>FireboxMeltIntervalSec</c>.
  /// </summary>
  protected sealed override float MeltIntervalSec => 0f;
  protected override float TuyereIntakeVolume => IwexValues.TuyereIntakeVolume;
  protected override float BlastPressureThreshold =>
    IwexValues.BfBlastPressureAtReference;
  /// <summary>
  /// <b>Geometry, not a config total: every chargeable cell this shaft owns, times its own block
  /// quantum.</b> Exactly the pair the <c>bf-info-shaftfull</c> readout divides, so the number the player
  /// is shown and the number the heat balance divides by cannot drift apart.
  /// <para>
  /// <b>Never a hand-picked total.</b> A tunable "required to fire" number answers "may it light yet",
  /// which ignition does not ask on this branch (it is positional) - so all such a total can do is stand
  /// in as the cold-charge denominator, where it is simply wrong: a cold blast furnace holds <b>1 248</b>
  /// units, so a shaft charged to a 320-unit total would read as <em>completely full</em> and pay the
  /// entire <c>BfChargeLossFull</c> penalty while three-quarters empty.
  /// </para>
  /// <para>
  /// <b>Sealed.</b> A cupola's capacity is its own five chargeable cells times its own 3 000-unit
  /// quantum, which this expression already answers - a leaf re-introducing a hand-picked constant is
  /// how this class of defect is born.
  /// </para>
  /// </summary>
  protected sealed override int ChargeCapacityUnits =>
    ChargeableCells.Count * Math.Max(1, ChargeUnitsPerBlock);

  /// <summary>The charge stands in a column over the raceway and descends in layers - which is what
  /// makes this branch the shaft branch. Carried by the type rather than opted into per leaf, and
  /// <b>sealed</b> so a leaf cannot opt back out of the thing that makes it a shaft furnace.</summary>
  protected sealed override bool ShaftHoldsLayeredCharge => true;

  // Product identity + per-cycle tunables, exposed as virtuals so the cupola - which is this same
  // machine run as a scrap re-melter - swaps them for cast iron and its own (slower) cadence while
  // inheriting the whole melt/drain/residue path. The defaults are the blast furnace's, so its
  // behaviour (and its goldens) are unchanged.

  /// <summary>Metal token the lower tap pours (resolved through <see cref="MetalRegistry"/>). The
  /// blast furnace makes molten <b>pig iron</b> (crude high-carbon iron, <c>iwex:ingot-pigiron</c>);
  /// plain iron is obtained downstream by over-blowing pig in the Bessemer converter. The cupola
  /// overrides this to cast iron.</summary>
  protected virtual string MetalProductCode => "pigiron";

  /// <summary>Lang key for the molten-product HUD line (with two <c>{0}/{1}</c> unit args).</summary>
  protected virtual string MoltenProductInfoLangKey => IwexLang.BfInfoMolteniron;

  /// <summary>
  /// Molten product (units) rendered per charge unit of a band carrying <paramref name="mix"/>.
  /// <para>
  /// <b>The blast furnace scales it by the band's own ore content</b>, because what it recovers is
  /// iron from <em>ore</em> and a burden unit is only partly that. Stated as a rate against ore rather
  /// than against charge is what makes the number survive coke leaving the burden stamp: the ore share
  /// climbs from ~0.75 to ~0.94 and recovery per nugget does not move, which is correct. See
  /// <see cref="IwexValues.BfIronPerOreUnit"/> and <c>OreRecoveryGuardRailTests</c>.
  /// </para>
  /// <para>
  /// The cupola overrides it flat: it <b>remelts</b> rather than reduces, so there is no ore share in
  /// its charge to scale by - a unit of pig in is a unit of cast iron out, less what the slag takes.
  /// </para>
  /// </summary>
  protected virtual float ProductPerUnit(BurdenMix mix) =>
    IwexValues.BfIronPerOreUnit * OreShareOf(mix);

  /// <summary>Molten slag (units) rendered per charge unit of a band carrying <paramref name="mix"/>.</summary>
  protected virtual float SlagPerUnit(BurdenMix mix) =>
    IwexValues.BfSlagPerOreUnit * OreShareOf(mix);

  /// <summary>
  /// How many of <b>this</b> furnace's charge units one blast-furnace charge item is worth - 1 for the two
  /// blast furnaces, ~94 for the cupola.
  /// <para>
  /// <b>The seam that lets one raceway model serve two unit currencies</b> (see
  /// <c>docs/design/layered-charge.md</c>). A blast furnace counts its charge
  /// in <b>items</b> (32 a block); a cupola counts its remelt charge in <b>metal units</b> (3 000 a block),
  /// because a 5 u bit, a 25 u chunk and a 375 u pig all have to go into the same pile. Every raceway
  /// constant is calibrated in the first currency, so a cupola inheriting them unscaled runs ~94x too
  /// slow: its raceway is 3 000 units deep against a 0.35 u/s burn, and the gas pass lifts a band about
  /// 4 °C a tick. It can never melt anything, and no test says so unless a fixture charges it with coke.
  /// </para>
  /// <para>
  /// <b>Only two constants need it</b>, and knowing which is the whole of the trick.
  /// <see cref="IwexValues.BfRacewayCarbonPerTuyerePerSecond"/> is a rate in charge units, so it scales; and
  /// <see cref="IwexValues.BfShaftGasTransferFrac"/> is a fraction absorbed <em>per unit</em>, so it scales
  /// <b>inversely</b> or a cupola band would absorb everything in one step and the shaft above it would
  /// stay cold. Everything else is already scale-free: <c>BfRacewayGasPerCokeUnit</c> multiplies carbon that
  /// has itself been scaled, <c>BfBurdenPerCarbonUnit</c> is a ratio of two quantities in the same currency,
  /// and the raceway depth and gas resolution both read <see cref="BlockEntityFurnaceCore.ChargeUnitsPerBlock"/>
  /// directly.
  /// </para>
  /// </summary>
  protected float ChargeUnitScale =>
    Math.Max(
      1f,
      ChargeUnitsPerBlock
        / (float)
          Math.Max(1, IwexValues.ChargeItemsPerBand * ChargeColumn.BandsPerBlock)
    );

  /// <summary>
  /// Carbon this furnace's raceways burn per second at full blast, in its own charge units - the per-tuyere
  /// rate times the tuyeres it actually has.
  /// <para>
  /// <b>Per tuyere, so a bigger furnace really is a bigger furnace.</b> One whole-furnace
  /// number would make the hot furnace's sixteen columns burn exactly as much carbon as
  /// the cupola's one - the larger machine buying capacity and campaign length but no throughput. The
  /// tuyeres are where the blast arrives, so keying the carbon to them is also what keeps "air is the
  /// reagent" literally true rather than merely stated.
  /// </para>
  /// <para>
  /// Floored at one tuyere: a natural-draught shaft (should one ever be drawn) still burns, and a
  /// furnace whose outlet scan has not run yet must not silently stop consuming.
  /// </para>
  /// </summary>
  private float RacewayCarbonPerSecond =>
    IwexValues.BfRacewayCarbonPerTuyerePerSecond
    * Math.Max(1, _tuyeres.Count)
    * ChargeUnitScale;

  /// <summary>
  /// The ore share of a band - its stamped <see cref="BurdenMix.IronFrac"/>, or the shares unstamped
  /// legacy charge is read at. The same fallback <see cref="Accumulate"/> uses, and for the same
  /// reason: the two answering differently is how a furnace comes to melt a burden its heat balance
  /// treated as inert.
  /// </summary>
  private static float OreShareOf(BurdenMix mix) =>
    mix.HasContent
      ? mix.IronFrac
      : Math.Max(
        0f,
        1f - IwexValues.BfDefaultFuelFrac - IwexValues.BfDefaultFluxFrac
      );

  /// <summary>Maximum molten product (units) the furnace holds before stalling.</summary>
  protected virtual float MaxMoltenProduct => IwexValues.BfMaxMoltenIron;

  /// <summary>Maximum molten slag (units) the furnace holds before stalling.</summary>
  protected virtual float MaxMoltenSlagPool => IwexValues.BfMaxMoltenSlag;

  #endregion

  #region Initialization

  /// <summary>
  /// Re-reads the blast-furnace product/charge tunables alongside the core's, so a live
  /// <c>/exmod config iwex ...</c> change applies on the next tick. The core invokes this (virtually)
  /// at init and at the top of every production tick.
  /// </summary>
  protected override void CacheAttributes()
  {
    base.CacheAttributes();
    _maxMoltenIron = MaxMoltenProduct;
    _maxMoltenSlag = MaxMoltenSlagPool;
  }

  #endregion

  #region Molten stack construction

  private ItemStack? CreateMoltenStack(string metalCode, int units, float temp)
  {
    // The molten network/molds expect the registry's item code per metal (iron game:ingot-iron, slag
    // iwex:slag). MetalRegistry.MoltenItemOf resolves the short token, falling back to the
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

  // There is no family accept list; what refuses foreign matter is `IsChargeCode`, one seam lower and
  // one question simpler.

  #region Charge identity

  /// <summary>
  /// A shaft burns <b>fuel as well as burden</b>, and both are charge. That is the whole of the
  /// two-stream split: coke is charged as its own bands rather than mixed into the burden's stamp, so a
  /// column holds <c>iwex:burden</c> and <c>game:coke</c> side by side and the raceway reads its true
  /// coke fraction off the column instead of off a per-stack attribute.
  /// <para>
  /// <b>Fuel must be charge, or a cupola's own coke reads as rubbish.</b> <see cref="ReadChargeMix"/>
  /// rejects whatever <c>IsChargeCode</c> refuses, so a branch that dropped <c>|| IsFuelStack</c> here
  /// would reject the fuel it burns and block its own conversion for ever.
  /// </para>
  /// </summary>
  public override bool IsChargeItem(ItemStack? stack) =>
    base.IsChargeItem(stack) || IsFuelStack(stack);

  /// <inheritdoc cref="IsChargeItem"/>
  public override bool IsChargeCode(string? material) =>
    base.IsChargeCode(material) || IsFuelCode(material);

  #endregion

  #region Charge hooks

  /// <summary>
  /// The shaft's charge <b>is</b> its columns.
  /// <para>
  /// <b>Sealed</b>, for the reason the firebox seals its own: the handle type is what the other three
  /// hooks agree on, and a leaf returning something else would break all three at once at a cast rather
  /// than at a compile error.
  /// </para>
  /// </summary>
  protected sealed override object CollectCharge() => ShaftColumns;

  /// <summary>The handle <see cref="CollectCharge"/> hands out, named once so the consumers cannot come
  /// to disagree about what they are casting to.</summary>
  private static IReadOnlyDictionary<(int X, int Z), ChargeColumn> ColumnsOf(
    object chargeHandle
  ) => (IReadOnlyDictionary<(int X, int Z), ChargeColumn>)chargeHandle;

  /// <summary>
  /// Totals the charge in the shaft and sums its composition in the same walk over every segment of every
  /// column. The total and the composition are <b>family-blind</b>: a shaft of the wrong burden still
  /// lights, burns and reads its coke fraction, because a furnace full of the wrong stuff still gets hot
  /// (and burns out) - it just will not convert. The rejected-family count/token are tracked alongside so
  /// the tick can block the conversion and the HUD can name the mismatch.
  /// <para>
  /// The composition is <b>unit-weighted across the whole shaft</b>, not per column and not off the top
  /// band: a mixed shaft burns at its true average coke ratio. That is what
  /// <see cref="BlockEntityFurnaceCore.RequiredBlastPressureFor"/> and
  /// <see cref="BlockEntityFurnaceCore.ComputeHeatBalance"/> read, which is why the design's "permeability
  /// keeps reading the column's coke fraction" needs no hot/cold branch anywhere.
  /// </para>
  /// <para>
  /// A <b>fuel</b> segment (<c>game:coke</c>, mix <c>default</c>) contributes <c>fuel += Units</c> and
  /// nothing else, so a column charged in proper rounds reads its coke fraction straight off the geometry
  /// of the charge rather than off a stamp. A <b>burden</b> segment contributes its stamped mix, or - for
  /// charge that carries none - the legacy default shares, which is what keeps a world charged before the
  /// mixer existed burning as standard grade.
  /// </para>
  /// </summary>
  protected override int ReadChargeMix(
    object chargeHandle,
    out bool isFull,
    out BurdenMix mix,
    out int rejectedCount
  )
  {
    int totalMix = 0;
    int rejected = 0;
    float iron = 0f;
    float flux = 0f;
    float fuel = 0f;

    foreach (ChargeColumn column in ColumnsOf(chargeHandle).Values)
    {
      foreach (ChargeSegment segment in column.Segments)
      {
        // Matter this furnace does not recognise as charge is counted like everything else and rejected
        // on top - it is not skipped. Skipped, it falls through uncounted: `ConversionBlocked` reads
        // false and the furnace prints "Melting" over a shaft that can never render a drop. This line is
        // the one thing saying "a furnace that will render nothing is burning, not melting".
        //
        // Counting is the load-bearing half and the easy one to get wrong. The totals here are
        // deliberately blind to acceptance - a shaft packed with the wrong stuff still lights, still burns
        // and still reads its own coke fraction, because a furnace full of the wrong stuff really does get
        // hot. Skipping the units instead would make the same shaft read as "not full", which is a
        // different machine: it would refuse to light and give the player no reason.
        if (!IsChargeCode(segment.Material))
          rejected += segment.Units;

        totalMix += segment.Units;
        Accumulate(segment, ref iron, ref flux, ref fuel);
      }
    }

    mix = new BurdenMix(iron, flux, fuel);
    // `isFull` means "loaded to capacity" and nothing more on this branch: the core's ignition gate that
    // reads it (`State == Idle && StructureComplete && _cachedIsFull && !IsChoked`) sits behind
    // `!DerivesState`, which the shaft never takes. It survives as a dirty-tracking and readout flag.
    //
    // Capacity is geometry, never a hand-picked total. A constant here lets "how full" be answered two
    // different ways by the same furnace - one for this flag, one for the heat balance - and nothing
    // notices the disagreement.
    isFull = totalMix >= ChargeCapacityUnits;
    rejectedCount = rejected;
    return totalMix;
  }

  /// <summary>
  /// Adds one band's contribution to a running composition, in <b>units</b> rather than fractions so the
  /// caller ends with a unit-weighted mix over whatever span it walked.
  /// <para>
  /// <b>Factored out because two callers must not drift.</b> <see cref="ReadChargeMix"/> walks the
  /// whole shaft and <see cref="CombustionMix"/> walks the raceway round; if the two ever read a band
  /// differently, a furnace lights on a burden its heat balance then treats as inert - which is the exact
  /// class of defect the ignition gate and the melt condition disagreeing already produced once.
  /// </para>
  /// <para>
  /// <b>Carbon comes from fuel bands and nowhere else.</b> A burden band's
  /// stamped <c>Mix.Fuel</c> contributes <b>nothing</b> here, however rich the grade says it is. With
  /// charging laying coke as its own bands, a stamp that also counted would be a second, disagreeing
  /// answer to "how much carbon is at the raceway": a round of 9 units of
  /// coke under 23 units of 30 %-stamped burden reads <b>15.9 of 32</b>, so the furnace burns at ~50 % coke
  /// on a charge the player laid at 30 % and runs ~70 °C hotter than the grade advertises. The same
  /// double-count would feed <see cref="BlockEntityFurnaceCore.RequiredBlastPressureFor"/>, so blast demand
  /// would be wrong in the same direction.
  /// <para>
  /// <b>What this costs a saved world:</b> a shaft charged before coke became its own band reads 0 %
  /// carbon, so it runs cold and chills instead of burning at a phantom coke fraction. That is the
  /// <em>legible</em> failure of the two - the chill readout names it and the charge digs back out. The
  /// stamp's fuel component is due to be deleted outright; until then it survives as a grade/display
  /// field and as the ore share <see cref="OreShareOf"/> reads, which is a real fact about what the mixer
  /// put in the item.
  /// </para>
  /// </summary>
  private static void Accumulate(
    ChargeSegment segment,
    ref float iron,
    ref float flux,
    ref float fuel
  )
  {
    int units = segment.Units;
    if (IsFuelCode(segment.Material))
    {
      // Weighted by the fuel's own carbon, not counted as bands. A fuel band is carbon and nothing else,
      // but two fuels are not the same carbon: `CarbonPerUnit` is 1.0 for coke and 0.5 for charcoal, so a
      // shaft charged 30 % by volume with charcoal reads ~17.6 % carbon and runs cooler for it. Counting
      // the band instead would make charcoal a pure speed buff - identical flame, identical iron, half the
      // campaign - which is the opposite of what a worse fuel should be.
      fuel += units * CarbonPerUnit(segment.Material);
      return;
    }

    if (segment.Mix.HasContent)
    {
      iron += segment.Mix.IronFrac * units;
      flux += segment.Mix.FluxFrac * units;
      return;
    }

    // Unstamped charge carries no composition at all, so it is read at the standard grade's ore and flux
    // shares - with no fuel, like every other burden band. The flux share must clear every grade's flux
    // floor or an old world's charge would grade as a shortfall on top of everything else.
    iron += Math.Max(
      0f,
      1f - IwexValues.BfDefaultFuelFrac - IwexValues.BfDefaultFluxFrac
    ) * units;
    flux += IwexValues.BfDefaultFluxFrac * units;
  }

  /// <summary>
  /// Whether the shaft will catch. Two conditions, and <b>neither is a tunable number</b>:
  /// <list type="number">
  /// <item><b>The raceway course must be complete</b> - every column of the shaft holds charge. A
  /// tuyere with nothing in front of it is blowing into empty air, so a shaft with two thousand units
  /// piled in one column is not a furnace that is ready to light. This must never be re-expressed as
  /// "≥ N units": the two agree on a full course and disagree everywhere else. Each furnace's requirement
  /// is simply its own column count - nine on the cold blast furnace, one on the cupola - so a redrawn
  /// layout updates it for free and there is nothing to tune or drift.</item>
  /// <item><b>Fuel at the raceway.</b> Burden on its own cannot burn - it has no carbon in it - so the
  /// bottom band of every column has to be something that does. That is what makes the burn-out
  /// irreversible without a "was lit" bit anywhere: <c>BfBurnoutFuelRetainedBottom</c> is <b>0</b>, so a
  /// furnace that has gone out has no fuel left at its own raceway and cannot re-ignite on the next tick
  /// off its own salvage.</item>
  /// </list>
  /// <para>
  /// <b>Derived, with no stored state.</b> There is no <c>allBurning</c> flag and no per-pile burn bit;
  /// the pile block stays the near-stateless renderer it is specified as.
  /// </para>
  /// <para>
  /// <b>What this is not, yet.</b> The design (<c>docs/design/layered-charge.md</c> § <i>The
  /// ignition sequence, fully specified</i>) has the fire <b>propagate</b> - torch an open tap, the hearth
  /// catches after a beat, the bottom piles light, and a front climbs the shaft neighbour-to-neighbour,
  /// stopping dead at a burden-only gap. That is a per-column <i>lit height</i> still to be built; the two
  /// gates above are the half of it that is positional rather than temporal.
  /// </para>
  /// </summary>
  protected override bool TryIgniteCharge(object chargeHandle)
  {
    var columns = ColumnsOf(chargeHandle);
    if (columns.Count == 0)
      return false;

    int perBand = Math.Max(1, ChargeUnitsPerBlock / ChargeColumn.BandsPerBlock);
    foreach (ChargeColumn column in columns.Values)
      if (!RacewayIsLightable(column, perBand))
        return false;
    return true;
  }

  private static bool RacewayIsLightable(ChargeColumn column, int perBand)
  {
    if (column.TotalUnits <= 0)
      return false; // this column's raceway is empty - the course is not complete

    // A real coke band, and nothing else will do. The stamp is not a carbon supply, so lighting off a
    // burden's stamped coke would catch a shaft on a burden the heat balance then treats as inert. It
    // reads the same source Accumulate does, deliberately.
    foreach (ChargeSegment segment in column.LowestUnits(perBand))
      if (IsFuelCode(segment.Material))
        return true;
    return false;
  }

  /// <summary>
  /// Melting is evaluated <b>every tick</b>, not on a cycle - see
  /// <see cref="BlockEntityFurnaceCore.MeltsPerTick"/>. The raceway slice routinely spans a coke/burden
  /// boundary, and a per-cycle implementation can only ever melt all of a round or none of it.
  /// </summary>
  protected sealed override bool MeltsPerTick => true;

  /// <summary>
  /// <b>The melt is metered by the carbon, and by nothing else.</b> Burden
  /// rendered this tick is the carbon <see cref="CirculateGas"/> actually burned, times
  /// <see cref="IwexValues.BfBurdenPerCarbonUnit"/> - the mod's <b>coke rate</b>, read the useful way round.
  /// <para>
  /// A flat independent melt rate is a second throttle beside the carbon: burden clears out of the
  /// raceway long before the coke beneath it does, coke banks up in front of the tuyeres until no burden
  /// is left in the span at all, and the furnace pulses <c>Melting</c>/<c>Firing</c> while it burns the
  /// surplus off. The blast must be the only throttle.
  /// </para>
  /// <para>
  /// <c>MeltSpeedFactor</c> still multiplies it, and now it means something specific: a hotter furnace
  /// makes <b>more iron from the same carbon</b>. That is precisely what hot blast bought historically, and
  /// it is why a preheated furnace wants a leaner charge rather than merely tolerating one.
  /// </para>
  /// <para>
  /// dt is already inside the carbon figure, so it does not appear again here - and must not, or the melt
  /// would go quadratic in the step and a chunk-load catch-up would empty a shaft.
  /// </para>
  /// </summary>
  protected override void SmeltCycle(object chargeHandle, float dt)
  {
    float budget =
      (_carbonBurnedThisTick * IwexValues.BfBurdenPerCarbonUnit * MeltSpeedFactor())
      + _meltCarry;
    int units = (int)budget;
    _meltCarry = budget - units;
    if (units > 0)
      ConsumeForMelting(ColumnsOf(chargeHandle), units);
  }

  /// <summary>
  /// One tick's melt: renders up to <paramref name="unitsToConsume"/> units of <b>burden</b> out of the
  /// columns' raceway slices and banks what it made.
  /// <para>
  /// <b>The melt condition is per band, and it is the whole model in three lines.</b> Burden melts iff
  /// <em>the temperature it carried down the shaft</em> clears the melting point - which is why a taller
  /// furnace needs less coke (a band descends through more gas and arrives hotter) and why an under-coked
  /// one chills (a cool flame never gets the band there).
  /// </para>
  /// <para>
  /// <b>Burden only. Coke leaves a column by burning and by nothing else.</b>
  /// Taking the lowest <em>N</em> units off the bottom whatever they are made of destroys coke through
  /// <see cref="ChargeColumn.Take"/> <b>on top of</b> the carbon <see cref="CirculateGas"/> is burning -
  /// most of a campaign's coke can vanish without ever being burnt, and the furnace goes out with a
  /// third of its burden still standing.
  /// <para>
  /// That would make <c>docs/design/layered-charge.md</c> § <i>What sets the rate: the blast</i> false for
  /// most of a furnace's life: campaign length set by the <em>melt</em> rate rather than by the blast,
  /// which is the one thing that page rules must meter the carbon. Nothing observable says so - the
  /// furnace simply runs short. Metered by the carbon, a full cold shaft goes out at exactly
  /// <c>coke ÷ 0.35 u/s</c>.
  /// </para>
  /// <para>
  /// So the removal is a <b>mid-span rewrite</b>, not a take off the bottom: a round is coke <em>then</em>
  /// burden, so the burden being melted almost always has coke beneath it, and
  /// <see cref="ChargeColumn.Take"/> cannot skip what it is standing on. Both consumers now work the same
  /// way over the same span - <see cref="BurnCarbon"/> shrinks the fuel bands in it, this shrinks the burden
  /// bands - and the column descends into whatever gap either of them leaves.
  /// </para>
  /// </para>
  /// <para>
  /// <b>The design's "carried temperature + heat from the coke burning with it" is one term here, not
  /// two.</b> That coke's heat is already in the band, delivered by
  /// <see cref="ChargeColumn.RiseGasThrough"/> in <see cref="CirculateGas"/>, because the gas the raceway
  /// makes is exactly that coke's. Adding a second term would count the same carbon twice and no test
  /// would notice - the furnace would simply run richer than it was calibrated to.
  /// </para>
  /// <para>
  /// A cold band <b>stops its column</b> rather than being skipped over. Everything above it is
  /// physically resting on it, so a raceway that reached past would be melting burden that never got
  /// there. That stop is the <i>chill</i>, and it is per column: a well-coked neighbour keeps descending.
  /// </para>
  /// <para>
  /// <b>Why the split across columns is round-robin.</b> The design says the raceway consumes the lowest
  /// units of <em>each</em> column; the rate is one whole-furnace number. Spreading it evenly over the
  /// columns that still hold charge - in ascending <c>(x, z)</c> key order, remainder to the first keys,
  /// the same determinism convention <c>SolidifyBottomLayer</c> uses - makes two identical furnaces
  /// consume identically and keeps unequal columns descending together rather than draining one to nothing
  /// before touching the next.
  /// </para>
  /// </summary>
  private void ConsumeForMelting(
    IReadOnlyDictionary<(int X, int Z), ChargeColumn> columns,
    int unitsToConsume
  )
  {
    // There is deliberately no per-segment acceptance filter here. The tick already blocks the whole
    // melt cycle while ConversionBlocked (any wrong-family charge in the shaft), so a filter here could
    // only ever be reached with nothing to filter - while wrong-family units are consumed by the burn,
    // which is the intended behaviour.
    var ordered = new List<ChargeColumn>();
    foreach (var key in Ordered(columns.Keys))
      ordered.Add(columns[key]);

    float product = 0f;
    float slag = 0f;
    int remaining = unitsToConsume;
    while (remaining > 0)
    {
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
      foreach (ChargeColumn column in live)
      {
        if (remaining <= 0)
          break;
        int want = Math.Min(each, Math.Min(remaining, column.TotalUnits));
        int melted = MeltBurden(column, want, ref product, ref slag);
        if (melted <= 0)
          continue; // this column is chilled, or holds only coke - it hangs while the others descend
        remaining -= melted;
        progressed = true;
      }

      // Every live column is hung. Without this the loop spins for ever on a furnace that has stalled,
      // which is a state the model makes ordinary rather than exceptional.
      if (!progressed)
        break;
    }

    // Blocks disappear as the columns shorten, and every surviving pile republishes its snapshot.
    SyncChargeBlocks();

    _moltenIron = Math.Min(_moltenIron + product, _maxMoltenIron);
    _moltenSlag = Math.Min(_moltenSlag + slag, _maxMoltenSlag);
  }

  /// <summary>
  /// Melts up to <paramref name="want"/> units of <b>burden</b> out of <paramref name="column"/>'s raceway
  /// slice, removes them, accumulates what they rendered, and returns how much actually went.
  /// <para>
  /// Walks raceway-first over <see cref="RacewayDepthUnits"/> - the same span
  /// <see cref="BurnCarbon"/> burns in, deliberately, because the raceway is one zone and the two things
  /// happening in it are happening side by side. Exact at <b>unit</b> granularity: the rewrite splits
  /// whatever band the budget runs out inside, and both halves keep their temperature.
  /// </para>
  /// <list type="bullet">
  /// <item><b>Coke is stepped over, never consumed.</b> It is not something that melts, and it is already
  /// being spent by the burn - see the box on <see cref="ConsumeForMelting"/> for what counting it here
  /// cost.</item>
  /// <item><b>A cold burden band stops the column</b> rather than being skipped. Everything above it is
  /// physically resting on it, so a raceway that reached past would be melting burden that never arrived.
  /// That stop is the <i>chill</i>, and it is per column: a well-coked neighbour keeps descending.</item>
  /// </list>
  /// </summary>
  private int MeltBurden(
    ChargeColumn column,
    int want,
    ref float product,
    ref float slag
  )
  {
    if (want <= 0)
      return 0;

    int budget = want;
    int melted = 0;
    bool chilled = false;
    float made = 0f;
    float cinder = 0f;

    column.RewriteLowest(
      RacewayDepthUnits,
      segment =>
      {
        if (chilled || budget <= 0 || IsFuelCode(segment.Material))
          return segment;

        if (segment.Temperature < _ironMeltingPoint)
        {
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
  /// shares, so two identical furnaces behave identically and a scenario's campaign length is stable.</summary>
  private static List<(int X, int Z)> Ordered(
    IEnumerable<(int X, int Z)> keys
  )
  {
    var ordered = new List<(int X, int Z)>(keys);
    ordered.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
    return ordered;
  }

  #endregion

  #region The raceway

  // All the coke burns here, at the bottom of each column, and nowhere else. The gas it makes rises
  // and warms the burden descending toward it, so a band is heated by coke that burned beneath it and
  // spends its own only when its turn comes. Everything the design promises falls out of those two
  // sentences with no extra machinery: a taller shaft is more efficient because a band spends longer
  // being warmed on the way down; hot blast saves coke because the flame is hotter for the same carbon;
  // and campaign length is simply how much coke was charged.

  /// <summary>
  /// Gas temperature leaving the top of the shaft, in °C - what the outlets vent. Not saved: it is
  /// recomputed from the columns on the first lit tick after a load, and an idle furnace vents nothing.
  /// </summary>
  private float _stackGasTemp = 20f;

  /// <summary>
  /// How deep into a column the raceway reaches, in charge units - <b>one whole round</b>, which is one
  /// charge block's worth.
  /// <para>
  /// <b>The slice spans a whole round</b> - at least one coke course plus one burden
  /// course - so the coke fraction it reads is the ratio the player <em>charged</em>, steadily. The
  /// rejected reading (a slice inside one course) makes that fraction alternate between ~1.0 and ~0.0 as
  /// each course arrives, which swings the required blast pressure from 1.2 to 3.5 atm against a twin-tub
  /// blower that tops out at 2.2 - so the tuyere gate would close on <b>every burden course</b> and the
  /// furnace would pulse hot and cold. A much fussier machine, for no gain.
  /// </para>
  /// <para>
  /// Derived, not a config key. A round is the coke-to-coke span and block boundaries quantise nothing,
  /// so no constant can track the player's actual rounds - but one block is the span the whole rest of the
  /// model already speaks in: it is what the hopper's <c>N coke, M burden of 16</c> readout counts, what a
  /// player digs out in one break, and what burn-out interpolates over. It follows
  /// <see cref="BlockEntityFurnaceCore.ChargeUnitsPerBlock"/>, so the cupola's remelt quantum moves it
  /// without a second override.
  /// </para>
  /// </summary>
  protected virtual int RacewayDepthUnits => Math.Max(1, ChargeUnitsPerBlock);

  /// <summary>
  /// The composition actually burning: a unit-weighted mix over the lowest
  /// <see cref="RacewayDepthUnits"/> of <b>every</b> column, which is what sets the flame temperature.
  /// <para>
  /// This is where the counter-current model replaces the flat one. The shaft's <em>average</em> coke
  /// fraction - what <see cref="ReadChargeMix"/> returns - makes a furnace run at
  /// one flat middle temperature for the whole campaign however the player charged it. Reading the round
  /// at the raceway means a lean round arrives, the flame drops, and the burden behind it goes cold: the
  /// chill, emerging from the charging rather than from a rule about it.
  /// </para>
  /// <para>
  /// Across every column rather than per column because the furnace has one flame temperature and one
  /// heat balance. Per-column consequences - a column that hangs while its neighbours descend - key off
  /// this same slice.
  /// </para>
  /// </summary>
  protected override BurdenMix CombustionMix(object chargeHandle, BurdenMix shaftMix)
  {
    float iron = 0f;
    float flux = 0f;
    float fuel = 0f;
    bool any = false;

    foreach (ChargeColumn column in ColumnsOf(chargeHandle).Values)
      foreach (ChargeSegment segment in column.LowestUnits(RacewayDepthUnits))
      {
        any = true;
        Accumulate(segment, ref iron, ref flux, ref fuel);
      }

    // An empty raceway is not "burning nothing at the reference grade" - it is a furnace with nothing in
    // front of its tuyeres, and handing back the shaft average there would let a shaft charged entirely
    // above the raceway burn as though it were not. The caller's own empty-mix fallback is the honest one.
    return any ? new BurdenMix(iron, flux, fuel) : default;
  }

  /// <summary>
  /// The raceway flame follows the coke arriving in front of the tuyeres with <b>no lag of its own</b>.
  /// <para>
  /// <b>Sealed, and the seal is the point.</b> The first-order chase this replaces is a hearth's
  /// thermal inertia, and a shaft carries its inertia somewhere else entirely - on the charge segments,
  /// which warm through over minutes while the flame itself responds within a tick. Putting a chase back
  /// here would be a second inertia in series with the real one, so a lean round arriving at the raceway
  /// would take a further quarter of an hour to be felt and the chill would stop being diagnosable.
  /// </para>
  /// </summary>
  protected sealed override void ApplyProcessTemperature(float targetTemp, float dt) =>
    _internalTemp = targetTemp;

  /// <summary>
  /// One tick's counter-current pass: the gas the raceway made climbs every column, warming each band
  /// toward it and arriving at the stockline with whatever is left.
  /// <para>
  /// <b>The blast is the throttle, and it is the only throttle.</b> Air is a reagent, so the furnace
  /// burns exactly as much carbon as the arriving air can burn - which is why the budget below scales with
  /// <c>HeatBalance.AirFactor</c> rather than being a flat rate, and why a stopped blower slows the fire to
  /// natural draught instead of leaving it burning at full tilt against a countdown. In a real works the
  /// blowing engine is what you open up to raise production; the blast:coke ratio does the rest.
  /// </para>
  /// <para>
  /// <b>Two knobs, and keeping them separate is the model.</b> How <em>hot</em> the gas is comes from
  /// <see cref="BlockEntityFurnaceCore.ComputeHeatBalance"/>, where the preheat lives. How <em>much</em> gas
  /// there is comes from the carbon this tick actually <b>burned</b>. A furnace on hot blast therefore
  /// reaches the same shaft temperature on a leaner burden with no hidden multiplier: Neilson, falling out
  /// of the arithmetic.
  /// </para>
  /// <para>
  /// <b>Burned, not present.</b> Reading the coke merely standing at the raceway
  /// lets a thick course heat a shaft for ever off carbon that never runs out, and leaves
  /// campaign length to a timer rather than to what the player charged.
  /// </para>
  /// <para>
  /// Per column, because each has its own raceway and its own coke. The stack temperature is the
  /// <b>hottest</b> of what the columns vented: the outlets share one flue, and a flue carries the
  /// hottest thing feeding it rather than an average of everything.
  /// </para>
  /// </summary>
  protected override void CirculateGas(object chargeHandle, float dt)
  {
    _carbonBurnedThisTick = 0f;

    var columns = ColumnsOf(chargeHandle);
    if (columns.Count == 0)
      return;

    // Air is the reagent: what the blast delivered decides the carbon, and the carbon decides everything
    // else.
    float budget =
      (RacewayCarbonPerSecond * _lastHeatBalance.AirFactor * dt) + _burnCarry;

    // Spent over the columns in a rotating order, not split evenly between them. Coke is items and a
    // fuel band burns in whole units, so an even split hands each column ~0.04 u a second - which truncates
    // to nothing every single tick. The whole budget then rides `_burnCarry` for ~26 s and every column
    // burns a unit on the same tick: the furnace consumes its charge, makes its gas and (since the melt is
    // metered by the carbon) renders its entire minute's iron in twenty-six-second gulps, with the raceway
    // reading burden-less in between and the state pulsing Melting/Firing across the gap.
    //
    // Rotating the start makes the same total spend arrive as one unit every three seconds and still land
    // evenly across the columns over any real stretch. The cap keeps a large budget spread rather than
    // letting the first column swallow it.
    var ordered = Ordered(columns.Keys);
    int start = _burnRotation % ordered.Count;
    _burnRotation = (_burnRotation + 1) % ordered.Count;
    float perColumn = Math.Max(1f, MathF.Ceiling(budget / ordered.Count));

    float hottest = _ambientTemp;
    float left = budget;
    float spent = 0f;
    bool moved = false;

    for (int i = 0; i < ordered.Count && left >= 1f; i++)
    {
      ChargeColumn column = columns[ordered[(start + i) % ordered.Count]];
      float burned = BurnCarbon(column, Math.Min(perColumn, left));
      left -= burned;
      if (burned <= 0f)
        continue;

      spent += burned;
      float stack = column.RiseGasThrough(
        _internalTemp,
        burned * IwexValues.BfRacewayGasPerCokeUnit,
        // Per unit, so it scales inversely with the unit size - see ChargeUnitScale. A cupola unit is
        // ~94 charge items, so at the raw fraction one band would strip the gas of everything and the
        // shaft above it would never warm at all.
        IwexValues.BfShaftGasTransferFrac / ChargeUnitScale,
        ChargeUnitsPerBlock
      );
      hottest = Math.Max(hottest, stack);
      moved = true;
    }

    // Carbon a starved or chilled column could not supply is carried, not lost: a rate under one unit a
    // second must still accumulate, or a slow fire never burns anything at all.
    _burnCarry = left;
    _carbonBurnedThisTick = spent;

    if (!moved)
      return;
    _stackGasTemp = hottest;
    // The glow is push-based, so a column whose temperature moved has to be told to redraw even though
    // its height did not. SyncChargeBlocks is the one route that republishes a pile's snapshot, and it
    // already sends nothing when the picture it produces is unchanged.
    SyncChargeBlocks();
  }

  /// <summary>
  /// Spends up to <paramref name="want"/> units of carbon out of one column's raceway slice and returns
  /// what it actually got.
  /// <para>
  /// <b>Carbon arrives two ways, and they leave differently.</b> A <b>fuel band</b> is carbon, so it
  /// burns away entirely and the column descends into the gap. A <b>burden band</b> carries coke in its
  /// stamp: that coke burns out of the stamp while the ore it was mixed with <em>stays put</em> until it
  /// melts - which is exactly right, and is why the band's ore share rises as its carbon goes.
  /// </para>
  /// <para>
  /// <b>The second path is what keeps older saved worlds burning.</b> A shaft charged while burden
  /// still carried its coke in the stamp holds no fuel bands, so without this path nothing in it would
  /// burn. Once every charge lays coke as its own bands the first path is the whole story; this one then
  /// only serves saved worlds.
  /// </para>
  /// <para>
  /// It burns while the furnace is merely <b>Firing</b>, not only while it is producing. A furnace
  /// sitting hot and melting nothing is still burning its charge away, which is a real and correct way to
  /// waste a campaign - and it is what makes <c>FireboxMaxFuelBurnTime</c> redundant rather than deleted.
  /// </para>
  /// </summary>
  private float BurnCarbon(ChargeColumn column, float want)
  {
    if (want <= 0f || column.TotalUnits <= 0)
      return 0f;

    float left = want;
    column.RewriteLowest(
      RacewayDepthUnits,
      segment =>
      {
        // Fuel bands only. A burden band's stamped coke is not a carbon supply - see Accumulate for what
        // reading it as one does to the flame temperature. Burden leaves the raceway by melting, and only
        // by melting; a band the fire cannot melt stays exactly where it is, which is the chill.
        float carbon = CarbonPerUnit(segment.Material);
        if (left <= 0f || carbon <= 0f)
          return segment;

        // The budget is carbon and the band is units, so the conversion happens here - per segment,
        // inside the walk, because one raceway slice routinely spans a coke course and a charcoal one and
        // they are not worth the same. Resolving the weight once before the walk would spend charcoal at
        // coke's price or the reverse, depending only on which the player laid first.
        //
        // Whole units only: a fuel band is items, and half a lump of coke is not a thing. What is left over
        // rides _burnCarry to the next tick, which is also what absorbs the fractional remainder a fuel
        // worth less than one carbon a unit leaves behind - no second carry is needed.
        int burn = (int)Math.Min(segment.Units, left / carbon);
        left -= burn * carbon;
        return segment with { Units = segment.Units - burn };
      }
    );

    return want - left;
  }

  /// <summary>
  /// <b>Emergent, and it is the counter-current model's signature.</b> A well-charged tall furnace
  /// exhausts <em>cooler</em> than a short or badly-charged one, because the heat stayed in the shaft
  /// instead of going up the flue - which is exactly the thing a fixed
  /// <see cref="BlockEntityFurnaceCore.ExhaustTempFactor"/> could never express. What a cowper gets to
  /// regenerate is therefore what the furnace actually wasted.
  /// </summary>
  protected override float ExhaustTemperature => _stackGasTemp;

  /// <summary>
  /// <b>The shaft has no state machine.</b> What it is doing is recomputed from the charge every tick:
  /// <b>Idle</b> when nothing is burning, <b>Melting</b> when what has arrived at a raceway is hot enough
  /// to render, <b>Firing</b> in between.
  /// <para>
  /// <b>Nothing is stored and nothing counts down.</b> The five timers this replaces - the fuel clock,
  /// the melt-start soak, the melt interval, the cold-soak reversal, the extinguish countdown - were each
  /// standing in for something the counter-current model now does for real. A furnace runs while there is
  /// carbon at its raceway and air to burn it; it stops when either runs out; and "how long a campaign
  /// lasts" is what the player charged, not a budget. That is also why there is no "was lit" bit
  /// anywhere: burn-out retains no fuel at the raceway (<c>BfBurnoutFuelRetainedBottom</c> is 0), so a
  /// furnace that has gone out cannot re-derive itself alight off its own salvage.
  /// </para>
  /// <para>
  /// <b>Choked, not merely un-blown.</b> A sealed furnace suffocates and goes out; a furnace that has
  /// lost its blast falls back to natural draught and keeps burning - the two failure modes are opposites,
  /// and this is where that distinction lives. <c>docs/design/layered-charge.md</c> § <i>Two failure modes,
  /// and they are opposites</i>.
  /// </para>
  /// </summary>
  protected sealed override bool DerivesState => true;

  protected sealed override FurnaceState DeriveState(object chargeHandle)
  {
    if (IsChoked || !RacewayHoldsCarbon(chargeHandle))
      return FurnaceState.Idle;

    // A breached furnace keeps burning but can never be re-lit, and that clause is what keeps breach and
    // choke distinguishable. Without it a player could knock a wall out of a cold furnace and light it
    // through the hole, which would make "opened to the air" a strictly better way to run one.
    if (!StructureComplete && State == FurnaceState.Idle)
      return FurnaceState.Idle;

    // A shaft holding wrong-family charge reads Firing however hot it is, and that is the label staying
    // honest rather than a second gate. `Melting` is what the HUD prints, what the sounds play on and what
    // the handbook teaches; a furnace that will render nothing while a remelt pile is in it is not melting,
    // it is burning. The stored machine expressed this by refusing the transition
    // (`_secondsAboveMelting >= delay && !ConversionBlocked`), and dropping it on the derived branch would
    // have shown "Melting" over a furnace producing nothing until the player dug the offending pile out.
    return !ConversionBlocked && AtMeltingTemperature(chargeHandle)
      ? FurnaceState.Melting
      : FurnaceState.Firing;
  }

  /// <summary>
  /// Whether there is carbon in front of the tuyeres - on <b>every</b> column, because a tuyere blowing
  /// into an empty cell is not part of a working furnace.
  /// <para>
  /// The positional gate, evaluated continuously rather than once at ignition. It is the same question
  /// <c>TryIgniteCharge</c> asks, which is the point: "will it light" and "is it still alight" must not be
  /// two different answers, or a furnace lights on a charge it then treats as inert.
  /// </para>
  /// </summary>
  private bool RacewayHoldsCarbon(object chargeHandle)
  {
    var columns = ColumnsOf(chargeHandle);
    if (columns.Count == 0)
      return false;

    foreach (ChargeColumn column in columns.Values)
    {
      bool carbon = false;
      foreach (ChargeSegment segment in column.LowestUnits(RacewayDepthUnits))
        if (IsFuelCode(segment.Material))
        {
          carbon = true;
          break;
        }
      if (!carbon)
        return false;
    }
    return true;
  }

  /// <summary>
  /// A shaft is melting when the flame is over the line <b>and</b> at least one column is offering burden
  /// the raceway can actually take. Both halves are needed: the flame alone is reached within a tick of
  /// lighting, and burden alone could sit hot in a furnace whose fire has died.
  /// <para>
  /// This is what keeps the label honest once the melt-start soak is gone. Without it a furnace reads
  /// <c>Melting</c> - with the sounds and the HUD line - for the several minutes its charge spends warming
  /// through, producing nothing the whole time.
  /// </para>
  /// <para>
  /// <b>It asks <see cref="RacewayBurden"/>, not "is there any hot burden down there".</b>
  /// Scanning the whole raceway slice for any burden over the line disagrees with
  /// <see cref="MeltBurden"/>: the melt walk stops dead at the first cold burden band,
  /// because everything above is physically resting on it. A column laid <c>[cold burden][hot burden]</c>
  /// would report <c>Melting</c> - label, sound and HUD - while melting precisely nothing, which is
  /// the exact failure the paragraph above says this method exists to prevent. Reading the first band
  /// is also what makes <b>the hang halt the furnace</b>: when every column is chilled no column
  /// offers anything, so the furnace derives <c>Firing</c> and sits there burning its carbon away - which
  /// <c>layered-charge.md</c> calls out as "a real and correct way to waste a campaign".
  /// </para>
  /// </summary>
  protected sealed override bool AtMeltingTemperature(object chargeHandle)
  {
    if (!base.AtMeltingTemperature(chargeHandle))
      return false;

    foreach (ChargeColumn column in ColumnsOf(chargeHandle).Values)
      if (RacewayBurden(column) is { } burden && burden.Temperature >= _ironMeltingPoint)
        return true;
    return false;
  }

  #endregion

  #region The chill

  /// <summary>
  /// The first <b>burden</b> band inside the raceway slice - the one <see cref="MeltBurden"/> will meet, and
  /// the only one whose temperature can decide anything. <c>null</c> when the slice is all fuel.
  /// <para>
  /// <b>The first, not the hottest and not the average.</b> The melt walk steps over fuel and then stops
  /// at the first burden it cannot melt, because the column above is resting on it. Every question about
  /// whether this column is going anywhere is therefore a question about this one band.
  /// </para>
  /// </summary>
  private ChargeSegment? RacewayBurden(ChargeColumn column)
  {
    foreach (ChargeSegment segment in column.LowestUnits(RacewayDepthUnits))
      if (!IsFuelCode(segment.Material))
        return segment;
    return null;
  }

  /// <summary>
  /// <b>The chill.</b> Whether the column at <c>(<paramref name="localX"/>, <paramref name="localZ"/>)</c>
  /// is <b>hung</b>: under-coked burden has arrived at its raceway too cold to melt, so it does not leave,
  /// so no space appears beneath the column and nothing above it descends. Hanging (scaffolding) is the most
  /// famous way a real blast furnace goes wrong.
  /// <para>
  /// <b>Derived every time it is asked, and stored nowhere.</b> There is no hung flag in the save tree
  /// and there must not be one, for the same reason there is no "was lit" bit: a stored hang is a hang that
  /// can disagree with the column it describes. It is cheap - one walk of the raceway slice.
  /// </para>
  /// <para>
  /// <b>A cold furnace is not hung, it is out.</b> The gate is <see cref="BlockEntityFurnaceCore.State"/>,
  /// which is last tick's derived value and never recomputed here - <see cref="AtMeltingTemperature"/> runs
  /// *inside* the derivation and asks <see cref="RacewayBurden"/> directly, so there is no cycle.
  /// </para>
  /// <para>
  /// A column whose raceway slice holds <b>no burden at all</b> is not hung either: it is a fuel course
  /// waiting for its burden, which is what the bottom of a freshly-charged shaft looks like.
  /// </para>
  /// </summary>
  public bool IsHung(int localX, int localZ) =>
    State != FurnaceState.Idle
    && ChargeColumnAt(localX, localZ) is { } column
    && RacewayBurden(column) is { } burden
    && burden.Temperature < _ironMeltingPoint;

  /// <summary>
  /// How many of this shaft's columns are hung right now - 0 on any idle furnace.
  /// <para>
  /// <b>There is no "enough columns hang and the furnace halts" threshold, and adding one would be
  /// authoring a rule the model already produces.</b> A hung column offers <see cref="MeltBurden"/> nothing,
  /// so when every column hangs the furnace stops making iron **by arithmetic** - and
  /// <see cref="AtMeltingTemperature"/> then reports <c>Firing</c> rather than <c>Melting</c>, which is the
  /// honest label for a furnace that is lit, burning and producing nothing. A constant would only be able to
  /// disagree with that.
  /// </para>
  /// <para>
  /// The self-reinforcement is automatic too: the hung column's fuel keeps burning while its burden does
  /// not melt, so the raceway mix goes leaner, so
  /// <see cref="BlockEntityFurnaceCore.RequiredBlastPressureFor"/>'s shortfall term raises the pressure the
  /// furnace demands with no new code anywhere. Denser charge, harder to blow through, dies faster.
  /// </para>
  /// </summary>
  public int HungColumnCount
  {
    get
    {
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
  /// eating fuel and simply stops making iron.
  /// <para>
  /// The <b>whole stockline</b> case gets its own line rather than reading "9 of 9". That is the state in
  /// which the furnace has stopped being a furnace and is only burning its charge away, and it deserves to
  /// say so in the words a player can act on.
  /// </para>
  /// </summary>
  protected override void AppendChillInfo(StringBuilder sb)
  {
    int hung = HungColumnCount;
    if (hung <= 0)
      return;

    int total = Math.Max(1, ShaftColumns.Count);
    sb.AppendLine(
      hung >= total
        ? Lang.Get("iwex:bf-info-hungall")
        : Lang.Get("iwex:bf-info-hung", hung, total)
    );
  }

  #endregion

  #region Extinguish residue

  /// <summary>
  /// Burns the column out by height: the bands sitting on the tuyeres are stripped to
  /// <c>BfBurnoutFuelRetainedBottom</c> of their carbon and the bands at the top of the shaft - which the
  /// blast never reached - keep <c>BfBurnoutFuelRetainedTop</c>. Iron and flux survive verbatim, so the
  /// player digs the column out, re-cokes it in the mixer and charges it again.
  /// <para>
  /// <b>Fuel bands are consumed by the same fraction, not merely scaled in a stamp.</b> With coke
  /// charged as its own bands, a burn-out that only touched <c>BurdenMix.Fuel</c> would leave every coke
  /// band whole - the salvage would be richer than <c>bf-info-burnedout</c> promises, and the raceway
  /// would still hold carbon, which re-arms <see cref="TryIgniteCharge"/> on the very next tick.
  /// </para>
  /// <para>
  /// The height fraction is the <b>block's own</b> position in the shaft
  /// (<c>(y - yMin) / span</c>) - which is what keeps the bottom course at exactly
  /// <c>RetainedBottom</c> and the top course at exactly <c>RetainedTop</c> rather than at whatever a
  /// per-band interpolation happens to land on. It is also why <see cref="ChargeColumn.Rewrite"/> splits
  /// per block: what the player digs out is a block, so that is the granularity the salvage is decided at.
  /// </para>
  /// </summary>
  protected override void BurnOutCharge()
  {
    if (ShaftBox is not { } box)
      return;

    int yMin = Math.Min(box.min.Y, box.max.Y);
    int yMax = Math.Max(box.min.Y, box.max.Y);
    float span = Math.Max(1, yMax - yMin);
    int perBlock = ChargeUnitsPerBlock;
    float retainedBottom = IwexValues.BfBurnoutFuelRetainedBottom;
    float retainedTop = IwexValues.BfBurnoutFuelRetainedTop;
    // The shares ReadChargeMix assumes for unstamped charge, so a legacy segment is stamped with exactly
    // what it was already burning as.
    float legacyIron = Math.Max(
      0f,
      1f - IwexValues.BfDefaultFuelFrac - IwexValues.BfDefaultFluxFrac
    );

    foreach (var (key, column) in ShaftColumns)
    {
      IReadOnlyList<Vec3i> cells = ChargeCellsOf(key.X, key.Z);
      if (cells.Count == 0)
        continue;

      column.Rewrite(
        perBlock,
        (segment, blockIndex) =>
        {
          // A column may stand taller than it has cells to draw in (charged past its own roof). Clamping
          // to the top cell keeps such a band at the top course's retention rather than throwing.
          Vec3i cell = cells[Math.Min(blockIndex, cells.Count - 1)];
          float height = GameMath.Clamp((cell.Y - yMin) / span, 0f, 1f);
          float retained = GameMath.Lerp(retainedBottom, retainedTop, height);

          // A fuel band is carbon, so the retention applies to the band itself. Rounding down means a
          // fully-burned bottom course leaves nothing at all rather than a one-unit ember.
          if (IsFuelCode(segment.Material))
            return segment with { Units = (int)(segment.Units * retained) };

          // Burden: strip the stamped carbon, keep the ore and flux.
          if (segment.Mix.HasContent)
            return segment with
            {
              Mix = segment.Mix with { Fuel = segment.Mix.Fuel * retained },
            };

          // Legacy count-only charge carries no stamp, so burn-out stamps it - with the standard-grade
          // shares its own burn already assumed, its carbon scaled by the same retention. Left
          // unstamped, a derived ignition gate reads such charge as still carrying the reference coke
          // fraction, so a dead furnace could relight off its own salvage for ever.
          return segment with
          {
            Mix = new BurdenMix(
              legacyIron,
              IwexValues.BfDefaultFluxFrac,
              IwexValues.BfDefaultFuelFrac * retained
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

  protected override bool LiquidCapacityReached =>
    _moltenIron >= _maxMoltenIron || _moltenSlag >= _maxMoltenSlag;

  protected override void DrainProducts(ref bool dirty)
  {
    DrainIronTap(ref dirty);
    DrainSlagTap(ref dirty);
  }

  private void DrainIronTap(ref bool dirty)
  {
    // The drain point is the layout's, not a literal: a shaft furnace whose drawing marks no metal tap
    // simply never drains, the same way it would if the tap block were missing from the cell.
    if (MetalTapPos is not { } lowerTapPos)
      return;
    if (
      Api.World.BlockAccessor.GetBlockEntity(lowerTapPos)
        is not BlockEntityFurnaceTap lowerTap
      || !lowerTap.IsPouring
      || _moltenIron <= 0
    )
      return;

    int units = Math.Min(IwexValues.TapDrainPerTick, (int)_moltenIron);
    ItemStack? ironStack = CreateMoltenStack(
      MetalProductCode,
      (int)Math.Ceiling(units * IwexValues.TapIronStackFactor),
      _internalTemp
    );
    if (ironStack == null)
      return;

    int accepted = lowerTap.TryPourMetal(ironStack, _internalTemp);
    if (accepted > 0)
    {
      _moltenIron -= accepted;
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

  private void DrainSlagTap(ref bool dirty)
  {
    if (SlagTapPos is not { } higherTapPos)
      return;
    if (
      Api.World.BlockAccessor.GetBlockEntity(higherTapPos)
        is not BlockEntityFurnaceTap higherTap
      || !higherTap.IsPouring
      || _moltenSlag <= 0
    )
      return;

    int units = Math.Min(IwexValues.TapDrainPerTick, (int)_moltenSlag);
    ItemStack? slagStack = CreateMoltenStack(
      "slag",
      (int)Math.Ceiling(units * IwexValues.TapSlagStackFactor),
      _internalTemp
    );
    if (slagStack == null)
      return;

    int accepted = higherTap.TryPourMetal(slagStack, _internalTemp);
    if (accepted > 0)
    {
      _moltenSlag -= accepted;
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

  // The residue itself (freeze the pool onto the hearth floor, burn the burden out by height, never
  // slag) is the core's, shared with every other furnace. All the blast furnace supplies is what its
  // pool was made of - a cupola swaps these four members for cast iron and inherits the rest.

  protected override AssetLocation SolidProductBlock { get; } =
    new("iwex", "hearthmetal-pigiron");

  protected override float DrainedMetalUnits => _moltenIron;

  protected override void StampSolidProduct(BlockPos pos, int units)
  {
    if (
      Api.World.BlockAccessor.GetBlockEntity(pos)
      is BlockEntityHearthMetal solid
    )
    {
      solid.MetalCount = units;
      solid.MarkDirty(true);
    }
  }

  protected override void ClearMoltenPools()
  {
    _moltenIron = 0;
    _moltenSlag = 0;
  }

  #endregion

  #region Serialization

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldAccessForResolve
  )
  {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    _moltenIron = tree.GetFloat("moltenIron", 0f);
    _moltenSlag = tree.GetFloat("moltenSlag", 0f);
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetFloat("moltenIron", _moltenIron);
    tree.SetFloat("moltenSlag", _moltenSlag);
  }

  #endregion

  #region HUD product lines

  // The molten pools are shown at the taps that drain them, not the core: the lower tap surfaces the
  // metal pool, the upper the slag (the tap picks which by comparing its cell to Metal/SlagTapPos).
  // Self-gated so a cold or empty tap shows only its open/closed line - the readout appears exactly
  // when there is metal to pour (while melting, or a pool still draining down after the fire stops).

  public override void AppendMoltenMetalInfo(StringBuilder sb)
  {
    if (
      !StructureComplete
      || (State != FurnaceState.Melting && _moltenIron <= 0f)
    )
      return;
    sb.AppendLine(
      Lang.Get(MoltenProductInfoLangKey, _moltenIron, _maxMoltenIron)
    );
  }

  public override void AppendMoltenSlagInfo(StringBuilder sb)
  {
    if (
      !StructureComplete
      || (State != FurnaceState.Melting && _moltenSlag <= 0f)
    )
      return;
    sb.AppendLine(
      Lang.Get(IwexLang.BfInfoMoltenslag, _moltenSlag, _maxMoltenSlag)
    );
  }

  /// <summary>
  /// <b><c>bf-info-partiallylit</c> has no producer here.</b> Lit-ness is derived
  /// (<see cref="TryIgniteCharge"/>) and has no per-pile half to be partial about, so an idle-but-ready
  /// shaft reads plainly ready.
  /// <para>
  /// The key is <b>not</b> repurposed to mean "only some columns are coked". It looks like the same
  /// sentence and is not: the design's fire <i>front</i> is what will genuinely make a shaft
  /// partly lit, and pointing the key at a different condition now would leave that work with a key
  /// already spent on something else.
  /// </para>
  /// </summary>
  protected override void AppendReadyInfo(StringBuilder sb) =>
    sb.AppendLine(Lang.Get(IwexLang.BfInfoReady));

  #endregion
}
