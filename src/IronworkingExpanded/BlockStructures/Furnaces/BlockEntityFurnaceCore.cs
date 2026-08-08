using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Materials;
using ExpandedLib.Networks;
using ExpandedLib.Process;
using IronworkingExpanded.Items;
using IronworkingExpanded.Patches;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.Furnaces;

/// <summary>
/// The <b>fired core</b> every furnace multiblock in the suite is built on. It owns the parts that are the
/// same whatever the furnace is for: the firing state machine, the heat balance and internal temperature,
/// the charge walk, exhaust venting, extinguish and residue, serialization and the HUD.
/// <para>
/// <b>Melting is optional, not assumed.</b> A furnace that pools and taps a liquid (blast furnace, cupola,
/// puddling, open hearth) overrides the molten-product members - <see cref="LiquidCapacityReached"/>,
/// <see cref="DrainProducts"/>, <see cref="DrainedMetalUnits"/>, <see cref="SolidProductBlock"/>,
/// <see cref="StampSolidProduct"/>, <see cref="ClearMoltenPools"/>. One that only raises its charge's
/// temperature (the reheat furnace, and roasting on the same machine) overrides <b>none</b> of them and
/// inherits defaults that are true rather than stubbed: no pool means nothing to fill, nothing to drain and
/// nothing to freeze, so <see cref="SolidifyBottomLayer"/> leaves on its first line. That is what lets a
/// non-melting furnace sit here honestly instead of being forced to implement six lies.
/// </para>
/// <para>
/// <b>Blast is optional too</b>, and already was: a furnace with no tuyeres and no gas outlets declares
/// empty cell lists and sets <see cref="RequiresBlast"/> false, and the tick's air and exhaust loops simply
/// have nothing to walk. That is how the reverberatory furnaces run on natural chimney draft without a
/// branch anywhere in here.
/// </para>
/// <para>
/// <b>The charge store is a class tree, not a parameter</b> - unlike the other two axes. Where a furnace
/// keeps its charge is four knobs that must agree (<see cref="ShaftHoldsLayeredCharge"/>,
/// <see cref="ReadChargeMix"/>, <see cref="ChargeCapacityUnits"/>, <see cref="IsChargeCode"/>), and
/// nothing could enforce that while they were per-leaf: a hearth inherited a shaft's ignition threshold,
/// its burden-only charge read and its layered column, and shipped unable to light. So the two shapes are
/// branch classes - <see cref="BlockEntities.BlockEntityShaftFurnace"/> (a burden column over the raceway,
/// blown) and <see cref="BlockEntities.BlockEntityFireboxFurnace"/> (a fuel bed beside the work, natural
/// draught) - each carrying all four answers, sealed. Picking the base is the whole decision.
/// </para>
/// <para>
/// <b>Product stays a parameter</b>, and deliberately: pouring is orthogonal to shaftness. The open
/// hearth is a reverberatory furnace that taps molten steel - a firebox that pours - so the molten
/// virtuals above stay here with defaults that are true rather than stubbed, rather than being pulled onto
/// the shaft branch. See <c>docs/design/conventions.md</c> § "the furnace axes become a class tree".
/// </para>
/// </summary>
public abstract class BlockEntityFurnaceCore : BlockEntityMultiblockStructure
{
  // The furnace runs on game time: on reload it replays the game-time it spent unloaded as bounded
  // 1-second sub-ticks (its timers all accumulate dt, so a replayed tick is an ordinary one). A furnace
  // left burning while the player was away has therefore progressed on their return - up to ~10 minutes
  // of smelting, capped so a long absence neither stalls the server nor leaps a timer.
  protected override int MaxAwayCatchupSteps => 600;

  /// <summary>Whether the exhaust network is full, stalling production.</summary>
  public bool IsChoked { get; protected set; }

  /// <summary>
  /// Current operating state of the furnace - <b>read-only, with no setter at all</b>.
  /// <para>
  /// <b>Do not give this a setter, not even a private or protected one, and least of all "for tests".</b>
  /// On the shaft branch the label is <em>recomputed from the charge every tick</em>
  /// (<see cref="DerivesState"/>), so a fixture that arranges a furnace by assigning <c>State</c> is
  /// asserting a value the next tick throws away - the arrangement is a no-op that <b>looks</b> like a
  /// premise. That is the green-suite lie this repo has already been bitten by, and 27 fixture call sites
  /// were written against a setter before it was removed. With no setter, <c>ReflectionHelpers.SetProperty</c>
  /// throws rather than quietly succeeding, so the mistake is loud at the first run instead of silent for
  /// ever. <c>FurnaceBranchGuards.NoFurnaceExposesASettableState</c> states it as a law across every mod.
  /// </para>
  /// <para>
  /// <b>Why it is stored at all</b> rather than being a pure expression. Two reasons, and both are real:
  /// the firebox branch genuinely owns a state machine (a fuel bed has no raceway to derive from), and the
  /// value has to survive a save - a breached furnace reloads still burning, which is
  /// <see cref="CanRunProduction"/>'s whole premise. The tick below is the only writer.
  /// </para>
  /// </summary>
  public FurnaceState State => _state;

  private FurnaceState _state = FurnaceState.Idle;

  protected int _cachedMixCount = 0;
  protected bool _cachedIsFull = false;

  // Charge in the shaft this furnace does not recognise as its own: it still burns (and burns out), but
  // it blocks the conversion to molten while present, and the HUD says so. Cached (and serialized) because
  // GetBlockInfo runs client-side and the client never walks the charge - the same reason _cachedMixCount
  // rides the tree.
  //
  // There is one burden item, so the only thing to report is how much will not convert; naming "which
  // family" would be naming a set with one member.
  protected int _cachedRejectedCount = 0;

  // World cells, refreshed by ScanForOutlets. Held as IReadOnlyList because they are handed straight back
  // by the layout's own per-role cache rather than copied into a list this class owns.
  protected IReadOnlyList<BlockPos> _gasOutlets = [];
  protected IReadOnlyList<BlockPos> _tuyeres = [];

  protected float _internalTemp = 20f;

  // Timers accumulate elapsed seconds (dt) so durations are independent of the
  // production-tick interval. Thresholds below are in seconds.
  protected float _secondsAboveMelting = 0;
  protected float _meltSeconds = 0;
  protected float _extinguishSeconds = 0;
  protected float _belowMeltingSeconds = 0;
  protected float _fuelBurnSeconds = 0;

  // Sound throttles (world-elapsed ms): the furnace fire ambience and the molten
  // tap-pour hiss are looping, gated so the per-second tick doesn't spam audio.
  protected long _lastFireSoundMs;
  protected long _lastTapSoundMs;

  private string _cachedInfoText = "";
  private long _lastInfoUpdate = 0;

  /// <summary>Last heat balance the tick computed, for the HUD and the client-side readout.</summary>
  protected HeatBalance _lastHeatBalance;

  /// <summary>Summed composition of the whole charge column, for the burden-grade readout.</summary>
  protected BurdenMix _chargeMix;

  // Block attributes cached at init instead of re-parsing the JsonObject every tick / HUD refresh.
  protected float _ironMeltingPoint;
  protected int _maxFuelBurnTime;
  protected float _meltStartDelay;
  protected float _meltIntervalSec;
  protected float _tuyereIntakeVolume;
  protected float _starvationSupplyFrac;
  protected float _ambientTemp = 20f;

  // Whether the fire is currently starving for air (blast supply under the floor while lit). Rides the
  // save tree because GetBlockInfo runs client-side and the client never reads the tuyere network - the
  // same reason _cachedMixCount does - so the HUD can name the stall.
  protected bool _airStarved;

  protected override int CompletionTickMs => 3000;

  #region Tunables

  /// <summary>Temperature at which the charge melts and the furnace can enter Melting.</summary>
  protected abstract float MeltingPoint { get; }

  /// <summary>Maximum Firing time (seconds) before the fuel burns out and the furnace extinguishes.</summary>
  protected abstract int MaxFuelBurnTime { get; }

  /// <summary>Soak time (seconds) above the melt point before transitioning to Melting.</summary>
  protected abstract float MeltStartDelay { get; }

  /// <summary>Interval (seconds) between melt cycles.</summary>
  protected abstract float MeltIntervalSec { get; }

  /// <summary>Volume drawn from each tuyere per second <b>at the reference coke fraction</b>; the live
  /// draw is <see cref="TuyereDrawFor"/>.</summary>
  protected abstract float TuyereIntakeVolume { get; }

  /// <summary>Air-pressure threshold (atm) a tuyere must read to count as receiving blast <b>at the
  /// reference coke fraction</b>; the live threshold is <see cref="RequiredBlastPressureFor"/>.</summary>
  protected abstract float BlastPressureThreshold { get; }

  #region Burden-derived blast demand

  // Neither the pressure a furnace needs nor the air it draws is a property of the furnace - both are
  // properties of what is charged into it, and both come out of the same number: the burden's coke
  // fraction.
  //
  //   Air: air is the oxidant for coke. A coke-rich burden burns more fuel per ton of iron and needs
  //        proportionally more air to do it. (Hot blast's real historical value was cutting coke per
  //        ton - and with it, the blast volume per ton.)
  //
  //   Pressure: coke is the permeable skeleton of the charge column, the coarse non-fusing component
  //        that holds gas channels open through the stack. A coke-lean burden packs denser, so the
  //        pressure drop across it is higher and the blast has to be driven harder to get through.
  //
  // Together they make the tier gate emergent rather than declared: a mechanically blown furnace can
  // always be brute-forced with a coke-rich charge (cheap pressure, expensive fuel), while the
  // coke-lean charge that actually saves fuel demands a pressure only the steam tier can raise and only
  // the steam tier's pipe can hold. Nothing branches on which furnace this is.

  /// <summary>
  /// The blast pressure (atm) a burden of <paramref name="mix"/> demands: the reference requirement
  /// plus whatever its coke shortfall against <see cref="IwexValues.BfReferenceFuelFrac"/> adds, clamped.
  /// A charge with no burden stamp reads as the default (standard) grade, exactly as the heat balance
  /// treats it.
  /// </summary>
  public float RequiredBlastPressureFor(BurdenMix mix)
  {
    float fuelFrac = mix.HasContent
      ? mix.FuelFrac
      : IwexValues.BfDefaultFuelFrac;
    float shortfall = IwexValues.BfReferenceFuelFrac - fuelFrac;
    return GameMath.Clamp(
      BlastPressureThreshold + shortfall * IwexValues.BfBlastPressureCokeSensitivity,
      IwexValues.BfBlastPressureMin,
      IwexValues.BfBlastPressureMax
    );
  }

  /// <summary>
  /// Air (L/s) each tuyere draws for a burden of <paramref name="mix"/>: the reference draw scaled by
  /// how the burden's coke fraction compares to the reference, clamped so a starved or packed charge
  /// still breathes something sane.
  /// </summary>
  public float TuyereDrawFor(BurdenMix mix)
  {
    float fuelFrac = mix.HasContent
      ? mix.FuelFrac
      : IwexValues.BfDefaultFuelFrac;
    float reference = Math.Max(0.0001f, IwexValues.BfReferenceFuelFrac);
    float factor = GameMath.Clamp(
      fuelFrac / reference,
      IwexValues.BfTuyereDrawMinFactor,
      IwexValues.BfTuyereDrawMaxFactor
    );
    return _tuyereIntakeVolume * factor;
  }

  #endregion

  /// <summary>
  /// Everything this furnace can hold, in whatever unit it counts its charge in - <b>its capacity</b>, and
  /// the reference "full" is measured against.
  /// <para>
  /// <b>It is the denominator of the cold-charge heat penalty</b> (see
  /// <see cref="ComputeHeatBalance"/>): a furnace loaded to this number pays the whole of
  /// <c>BfChargeLossFull</c>, an empty one pays none, and it is linear in between. That is the one job it
  /// does on <em>every</em> branch, and moving it moves the heat balance with it.
  /// </para>
  /// <para>
  /// <b>The shaft does not gate ignition on quantity at all</b>: ignition there is positional and
  /// pneumatic (a complete raceway course, and air at pressure), so on that branch this number is
  /// <em>only</em> the heat denominator. The <b>firebox</b> branch still gates on it, and legitimately -
  /// a fuel bed is lit when its cells are loaded, so "loaded to capacity" is a real rule there rather
  /// than a tunable total.
  /// </para>
  /// <para>
  /// Both branches derive it from <b>geometry</b>, never from a config key. A hand-picked total is how
  /// the old defect was born: one sized for a shaft was inherited by a two-cell firebox and then by a
  /// one-cell one, ten times out of physical reach, and nothing in the type system or the suite could
  /// notice.
  /// </para>
  /// </summary>
  protected abstract int ChargeCapacityUnits { get; }

  /// <summary>Internal temperature the furnace snaps to on ignition.</summary>
  protected virtual float IgnitionTemp => 900f;

  /// <summary>Mix floor below which a lit furnace counts a disruption toward extinguish.</summary>
  protected virtual int DisruptionMixFloor => 144;

  /// <summary>
  /// Whether this furnace needs pressurised blast to stay lit. True for every blown furnace in the mod
  /// (the cold + hot blast furnace and the cupola all run off a blower through their tuyeres), so a
  /// tuyere gone dry - a stopped blower, a cut main - starves the fire and, sustained, extinguishes it.
  /// A future natural-aspirated furnace that draws its own draught overrides this to false to opt out of
  /// air-starvation entirely (the same shape as the family-gate opt-out).
  /// </summary>
  protected virtual bool RequiresBlast => true;

  /// <summary>Exhaust volume vented through each gas outlet per tick.</summary>
  protected virtual float ExhaustVolumePerTick => 24f;

  /// <summary>Factor applied to the internal temperature for vented exhaust temperature.</summary>
  protected virtual float ExhaustTempFactor => 0.8f;

  /// <summary>
  /// What the gas outlets vent at, in °C. A fixed fraction of the furnace's own temperature for anything
  /// with one hot space - a firebox hearth's flue really is the hearth, a bit cooler.
  /// <para>
  /// The shaft branch overrides it with something <b>emergent</b>: the gas that actually made it out of
  /// the top of its columns, having given up whatever the charge above the raceway took. That is the
  /// counter-current model's own signature - <b>a well-charged tall furnace exhausts cooler</b>, and it
  /// does so because the heat stayed in the shaft rather than because a factor said it should.
  /// </para>
  /// </summary>
  protected virtual float ExhaustTemperature => _internalTemp * ExhaustTempFactor;

  /// <summary>Extinguish threshold (seconds) with a single disruption.</summary>
  protected virtual int ExtinguishThresholdDefault => 30;

  /// <summary>Extinguish threshold (seconds) with two or more concurrent disruptions.</summary>
  protected virtual int ExtinguishThresholdSevere => 0;

  /// <summary>Cold-soak time (seconds) below the melt point before Melting reverts to Firing.</summary>
  protected virtual int BelowMeltingReset => 30;

  #endregion

  #region Combustion seams

  // Three seams, and all three exist because the shaft branch answers them differently from every
  // other furnace in the mod. They are here rather than on the branch because the tick is shared and a
  // tick full of `if (this is BlockEntityShaftFurnace)` is the thing a class tree exists to prevent.

  /// <summary>
  /// The composition <see cref="ComputeHeatBalance"/> burns. Everything the furnace holds, by default -
  /// which is right for a firebox, where the whole bed is in the fire at once.
  /// <para>
  /// <b>A shaft is the exception, and it is the whole of the counter-current model.</b> Coke burns at
  /// the <b>raceway</b> and nowhere else, so what sets the flame temperature is the coke fraction of the
  /// round currently <em>at</em> the raceway - not the shaft's average. A furnace charged three rounds
  /// lean and one round rich runs cool, then hot, then cool again as each round arrives; averaging the
  /// column would make it run at one flat middle temperature for ever and there would be nothing for the
  /// player's charging to express.
  /// </para>
  /// <para>
  /// <paramref name="shaftMix"/> is what <see cref="ReadChargeMix"/> summed over everything. It stays
  /// the answer for permeability (<see cref="RequiredBlastPressureFor"/>) and for the grade readout: the
  /// blast has to get through the <em>whole</em> column, not only the round that is burning.
  /// </para>
  /// </summary>
  protected virtual BurdenMix CombustionMix(
    object chargeHandle,
    BurdenMix shaftMix
  ) => shaftMix;

  /// <summary>
  /// Moves the furnace's own temperature toward <paramref name="targetTemp"/> - a first-order chase at
  /// <c>FireboxHeatRatePerSecond</c> / <c>FireboxCoolRatePerSecond</c>, which is where a firebox hearth's thermal
  /// inertia lives. Preheat, soak and blow-in are all this transient; without it "buy temperature by
  /// building the chimney taller" becomes a step function with nothing observable in between.
  /// <para>
  /// <b>The shaft branch assigns directly instead</b>, because its inertia is carried on the charge
  /// segments: <c>_internalTemp</c> becomes the <b>raceway flame temperature</b>, which follows the coke
  /// arriving in front of the tuyeres with no lag of its own, and the slow part of the furnace is the
  /// column warming through. Two inertias in series would be one too many.
  /// </para>
  /// </summary>
  protected virtual void ApplyProcessTemperature(float targetTemp, float dt)
  {
    // Rates are per-second; scale by dt for tick-independence. Blast raises the target rather than the
    // rate - a hot furnace is hot because it makes more heat, not because it heats up faster.
    if (_internalTemp < targetTemp)
      _internalTemp = Math.Min(
        _internalTemp + IwexValues.FireboxHeatRatePerSecond * dt,
        targetTemp
      );
    else if (_internalTemp > targetTemp)
      _internalTemp = Math.Max(
        _internalTemp - IwexValues.FireboxCoolRatePerSecond * dt,
        targetTemp
      );
    // No absolute clamp - TProcess is already floored at ambient, and a ceiling here silently capped the
    // advertised hot-blast temperature.
  }

  /// <summary>
  /// Runs the products of combustion through whatever stands over the fire. Nothing, by default: a
  /// firebox hearth's flame is drawn <em>across</em> the work by the chimney, so there is no column of
  /// charge for it to climb and no profile to develop.
  /// <para>
  /// The shaft branch sends the raceway gas up every column
  /// (<see cref="ChargeColumn.RiseGasThrough"/>), which is what puts heat into burden <b>before</b> it
  /// reaches the fire - and is why a taller shaft needs less coke, with no depth subsidy anywhere.
  /// </para>
  /// </summary>
  protected virtual void CirculateGas(object chargeHandle, float dt) { }

  /// <summary>
  /// Whether the furnace is at melting temperature - which is what the <c>Melting</c> label means and what
  /// the melt cycle runs on. The machine's own temperature, by default: a hearth is one hot space, so if it
  /// is over the line, what is in it is too.
  /// <para>
  /// <b>A shaft is not one hot space, and that is the whole point of it.</b> The raceway reaches the
  /// flame temperature within a tick while the burden above is still climbing, so "the furnace is hot
  /// enough" and "anything in it can melt" are <em>different questions</em> separated by minutes of
  /// warm-through. Reading only the machine put a furnace into <c>Melting</c> - HUD, sounds and all - while
  /// it rendered nothing at all, which is precisely the lie the state machine is being retired for.
  /// </para>
  /// </summary>
  protected virtual bool AtMeltingTemperature(object chargeHandle) =>
    _internalTemp >= _ironMeltingPoint;

  /// <summary>
  /// Whether this branch recomputes <see cref="State"/> from the charge every tick instead of storing it
  /// and stepping it with timers. False by default - a firebox hearth keeps its state machine, because a
  /// fuel bed has no raceway and no descent to derive anything from.
  /// <para>
  /// True on the shaft branch, and it retires <b>five timers and two thresholds</b>: the fuel clock,
  /// the melt-start soak, the melt interval, the cold-soak reversal and the extinguish countdown. Each of
  /// them was standing in for something the counter-current model now does for real - a furnace runs while
  /// there is carbon at its raceway and air to burn it, melts while what arrives there is hot enough, and
  /// stops when either runs out. <c>docs/design/layered-charge.md</c> § <i>The state machine mostly
  /// dissolves</i>.
  /// </para>
  /// </summary>
  protected virtual bool DerivesState => false;

  /// <summary>
  /// This tick's state, computed from the charge. Only called when <see cref="DerivesState"/>.
  /// <para>
  /// It must be a <b>pure read</b>: the tick compares it with last tick's label to decide what changed,
  /// so a derivation with a side effect would fire on every comparison.
  /// </para>
  /// </summary>
  protected virtual FurnaceState DeriveState(object chargeHandle) => State;

  #endregion

  #region Structure geometry

  // One hand-declared cell is left in this region, and it is the last one anywhere on a furnace.
  //
  // ShaftCentre is a structure-local offset in the anchor's north frame - the same frame the core block's
  // multiblock layout is drawn in, so it must land inside the layout's 'c' column, and the
  // offset-vs-layout test checks the correspondence the compiler cannot. It stays hand-declared because it
  // is a single geometric point - where a sound plays - and not a set of cells that are for something.
  // CellRole deliberately has no role for it; see the enum's own doc-comment.
  //
  // Everything else here is the layout answering about itself, through a CellRole. There is nothing to keep
  // in step and nothing to override: a furnace whose drawing marks no cell with the role answers empty (or
  // null, for a [SingleCell] role and for the shaft box), which is how the hearths state "no tuyeres" and
  // "no taps" without a line of C#. The two drains made the case for that: BlockEntityPuddlingFurnace
  // inherited MetalTapCell = (2,1,0), which is column 8 of an 8-wide drawing - off the structure entirely -
  // and overrode SlagTapCell onto a fire-brick slab. Nothing noticed, because a hearth pours nothing.

  /// <summary>
  /// Structure-local cell at the middle of the shaft - where the ignition whoosh, the fire ambience
  /// and the extinguish hiss play, and the centre of the charge walk.
  /// </summary>
  protected virtual Vec3i ShaftCentre => new(0, 3, 0);

  // Derived once and kept, never recomputed: ShaftBounds() is on the tick path (CollectChargePiles walks
  // it every production tick of every lit furnace) and EnsureShaftColumns indexes off it.
  //
  // Only a non-empty derivation is memoised, and that is the load-bearing half. A furnace whose block
  // carries no layout at all - a bare BlockEntity in a fixture, a block whose attributes have not been
  // attached - would otherwise cache "no box" permanently on the first read and never recover once the
  // layout arrived. A furnace whose layout is genuinely present and genuinely marks no fuel cell re-derives
  // a dictionary miss per read, which is the cheapest thing in this file and happens on no shipped machine.
  private (Vec3i Min, Vec3i Max)? _shaftBox;

  /// <summary>
  /// The shaft box in structure-local coordinates: the <b>bounding box</b> of the cells this furnace's own
  /// drawing marks as fuel - <see cref="CellRole.Chargeable"/> on a shaft furnace,
  /// <see cref="CellRole.Firebox"/> on a reverberatory hearth - or <c>null</c> when it marks neither.
  /// <para>
  /// <b>Derived, never declared.</b> Hand-written corner declarations beside the drawing can only be
  /// kept honest by a test that re-derives them from the layout and compares - one declaration too many.
  /// The box has no independent existence: it is a summary of the burden column the drawing already shows.
  /// </para>
  /// <para>
  /// <b>The box is not the charge volume.</b> The cold blast furnace's is 3x3x5 = 45 cells and its layout
  /// marks 39 of them chargeable, because the lowest level is mostly the tuyere pair and brick. Anything
  /// that needs the cells themselves reads <see cref="ChargeableCells"/>; the box is for the things that
  /// genuinely want a box - a <c>WalkBlocks</c> range, one column per <c>(x, z)</c>, a height to
  /// interpolate burn-out across.
  /// </para>
  /// <para>
  /// <b>Both roles, one derivation, no per-branch virtual.</b> A layout that claimed
  /// <see cref="CellRole.Chargeable"/> and <see cref="CellRole.Firebox"/> at once fails its own build
  /// (<c>MultiblockLayoutBuilder.ValidateRoles</c>), so the union of the two can never be a mixture - it is
  /// whichever one this machine drew. That is what lets a hearth's firebox and a shaft's burden column come
  /// out of the same two lines, which is the same trick <c>CollectChargePiles</c> already turns: separating
  /// fuel from work is a coordinate change, not a different machine.
  /// </para>
  /// <para>
  /// <b>Null is a real answer, not a failure.</b> A role a drawing declines to mark has no cells and
  /// therefore no bounding box, and two corners cannot express that - <see cref="ShaftBounds"/> and
  /// <see cref="EnsureShaftColumns"/> both re-sort per axis, so any "impossible" corner pair they were
  /// handed would be normalised straight back into a real box somewhere. So the emptiness is carried above
  /// the corners, and every consumer answers nothing rather than inventing a box at the origin. The same
  /// decision, for the same reason, as <c>MetalTapPos</c>/<c>SlagTapPos</c> being nullable.
  /// </para>
  /// </summary>
  protected (Vec3i min, Vec3i max)? ShaftBox
  {
    get
    {
      if (_shaftBox is { } cached)
        return (cached.Min, cached.Max);

      Vec3i? min = null;
      Vec3i? max = null;
      foreach (CellRole role in _fuelRoles)
      foreach (var (x, y, z) in LocalCellsWithRole(role))
      {
        min = min is { } lo
          ? new Vec3i(Math.Min(lo.X, x), Math.Min(lo.Y, y), Math.Min(lo.Z, z))
          : new Vec3i(x, y, z);
        max = max is { } hi
          ? new Vec3i(Math.Max(hi.X, x), Math.Max(hi.Y, y), Math.Max(hi.Z, z))
          : new Vec3i(x, y, z);
      }

      if (min is not { } lowest || max is not { } highest)
        return null;

      _shaftBox = (lowest, highest);
      return (lowest, highest);
    }
  }

  /// <summary>The two roles a fuel cell can carry, in one place so the union above reads as one fact.
  /// A layout may declare either, never both.</summary>
  private static readonly CellRole[] _fuelRoles =
  [
    CellRole.Chargeable,
    CellRole.Firebox,
  ];

  /// <summary>
  /// Every world cell of this furnace's own footprint that its layout marks as a liquid pool, for the placed
  /// facing - the crucible floor the molten metal freezes across when the furnace is put out.
  /// <para>
  /// <b>Derived, never declared.</b> A hand-written cell array beside the drawing needs a test to keep
  /// the two declarations agreeing - the tell that there is one declaration too many - and a furnace that
  /// inherits another's cells can point at an air cell or a solid brick with nothing to catch it, because
  /// a hearth pools no metal and never reaches the freeze.
  /// </para>
  /// <para>
  /// A crucible cell carries <see cref="CellRole.Pool"/> <b>and</b> <see cref="CellRole.Chargeable"/>:
  /// burden rests on it while the furnace runs and metal freezes onto it when the furnace dies. Those are
  /// the same cells today and the layouts say both.
  /// </para>
  /// </summary>
  public IReadOnlyList<BlockPos> PoolCells => CellsWithRole(CellRole.Pool);

  /// <summary>
  /// Every world cell of this furnace's own footprint that its layout marks as a fuel bed, for the placed
  /// facing - the firebox cells, as opposed to the <see cref="ShaftBox"/> bounding box that contains them.
  /// <para>
  /// <b>The group a firebox charges as one pool.</b> A deposit into any one of these cells is spread
  /// across all of them (<c>BlockEntityFirebox.Charge</c>), which is what makes "fill one, fill all" cost
  /// per cell. Grouping by the role rather than by adjacency is load-bearing: two hearths built back to
  /// back would otherwise merge their fuel.
  /// </para>
  /// <para>
  /// <b>A shaft furnace answers empty</b>, with no branch to make it so - a shaft's drawing marks
  /// <see cref="CellRole.Chargeable"/>, and the layout's own build refuses a drawing claiming both. The
  /// mirror image of <see cref="ChargeableCells"/> answering empty on a hearth.
  /// </para>
  /// </summary>
  public IReadOnlyList<BlockPos> FireboxCells => CellsWithRole(CellRole.Firebox);

  /// <summary>
  /// Every world cell of this furnace's own footprint that its layout marks as burden column, for the
  /// placed facing - the charge volume, as opposed to the <see cref="ShaftBox"/> <b>bounding box</b> that
  /// merely contains it (and is derived from it). The cells a <see cref="BlockChargePile"/> may stand in.
  /// <para>
  /// <b>The two are not the same set, and the difference is large.</b> The cold blast furnace's box is
  /// 3x3x5 = 45 cells; its layout marks 38 of them chargeable, because the lowest level is mostly the
  /// tuyere pair and brick - only <c>(0,1,0)</c> and <c>(1,1,0)</c> are open there. Filling the box would
  /// put charge inside a wall.
  /// </para>
  /// <para>
  /// <b>Derived, never declared.</b> This is the layout answering about itself through
  /// <see cref="BlockEntityMultiblockStructure.CellsWithRole"/>, so it cannot fall out of step with the
  /// drawing the furnace is actually built from. <see cref="PoolCells"/> is the same fact for the lowest
  /// level, and reads the same way for the same reason.
  /// </para>
  /// <para>
  /// <b>Asked as a role, not as a block code.</b> Asking by block code
  /// (<c>CellsAccepting(BlockChargePile.PileCode)</c>) answers the same cells - but only for as long
  /// as every shaft legend keeps <c>chargepile</c> inside its alternation, a coupling between two files
  /// that nothing enforces. <see cref="CellRole.Chargeable"/> states what the cells are <em>for</em>, so
  /// retyping or renaming the charge block cannot silently empty the shaft. The layout's own build refuses
  /// a drawing that claims both <see cref="CellRole.Chargeable"/> and <see cref="CellRole.Firebox"/>.
  /// </para>
  /// <para>
  /// <b>A firebox furnace answers empty</b>, with no branch anywhere here to make it so: a hearth's
  /// drawing marks its fuel cells <see cref="CellRole.Firebox"/>, which is a different role, and the two
  /// are mutually exclusive by construction. That is the honest answer rather than a convenient one - a
  /// firebox holds loose fuel beside the work, not a layered column over it, so there is nowhere in it for
  /// a charge pile to stand.
  /// </para>
  /// <para>
  /// <see cref="SyncChargeBlocks"/> walks exactly this set to place and remove piles as a column's
  /// height changes, and <see cref="ChargeCellsOf"/> slices it per column. Cached by the base per role and
  /// invalidated when the structure reloads, so a per-height-change read costs a dictionary lookup.
  /// </para>
  /// </summary>
  public IReadOnlyList<BlockPos> ChargeableCells =>
    CellsWithRole(CellRole.Chargeable);

  /// <summary>World cell of a structure-local offset for the placed rotation.</summary>
  protected BlockPos GlobalOf(Vec3i local) =>
    GetGlobalPos(local.X, local.Y, local.Z);

  /// <summary>
  /// Structure-local offset of a world cell for the placed rotation - the exact inverse of
  /// <see cref="GlobalOf"/>, for the components that arrive knowing only where they physically stand
  /// (a charge pile has no idea which column it draws until it asks this).
  /// <para>
  /// <b>The inverse must be the inverse, not a second rotation.</b> Turning the delta by
  /// <c>-_currentAngle</c> is what makes <c>LocalOf(GlobalOf(c)) == c</c> hold at all four facings;
  /// rotating it forward instead is right at north and 180 deg out at south, which no shipped shaft can
  /// show, because a 3x3 column set is closed under 90 deg rotation. See
  /// <c>FurnaceOrientationMatrixTests</c>' lopsided shaft.
  /// </para>
  /// <para>
  /// Loads the structure lazily, exactly as <see cref="OwnsCell"/> does, so it answers on the client too -
  /// where the monitor tick never runs and <c>_currentAngle</c> would otherwise still be -1.
  /// </para>
  /// </summary>
  public Vec3i LocalOf(BlockPos world)
  {
    EnsureStructureLoaded();
    return ExOrientation.RotateOffset(
      world.X - Pos.X,
      world.Y - Pos.Y,
      world.Z - Pos.Z,
      -_currentAngle
    );
  }

  /// <summary>World cell of <see cref="ShaftCentre"/> for the placed rotation.</summary>
  protected BlockPos ShaftCentrePos => GlobalOf(ShaftCentre);

  /// <summary>
  /// The shaft box in world space for the placed rotation. Rotating the two structure-local corners
  /// can swap either horizontal axis, so the corners are re-sorted per component - a box walk needs a
  /// true min/max, not "whatever the local low corner rotated into".
  /// <para>
  /// Null when the drawing marks no fuel cell at all, for the reason spelled out on <see cref="ShaftBox"/>:
  /// there is no pair of corners that means "nothing", so the caller is told rather than handed a box.
  /// </para>
  /// </summary>
  protected (BlockPos min, BlockPos max)? ShaftBounds()
  {
    if (ShaftBox is not { } box)
      return null;

    BlockPos a = GlobalOf(box.min);
    BlockPos b = GlobalOf(box.max);
    return (
      new BlockPos(
        Math.Min(a.X, b.X),
        Math.Min(a.Y, b.Y),
        Math.Min(a.Z, b.Z),
        Pos.dimension
      ),
      new BlockPos(
        Math.Max(a.X, b.X),
        Math.Max(a.Y, b.Y),
        Math.Max(a.Z, b.Z),
        Pos.dimension
      )
    );
  }

  #endregion

  #region Shaft charge columns

  // Each (x, z) of the shaft box owns a ChargeColumn and the furnace owns the columns - see
  // docs/design/layered-charge.md. What lives here is ownership, geometry and persistence only.
  // The columns are the shaft's charge: both hoppers push onto them, the melt takes from the bottom of
  // them, burn-out rewrites them and SyncChargeBlocks makes the world match them.

  /// <summary>
  /// Whether this furnace's charge descends as layers in a vertical shaft. A furnace with this off
  /// allocates no columns and writes not one attribute for them, so its save tree is byte-identical to
  /// one from before the columns existed.
  /// <para>
  /// <b>A leaf cannot override this</b>, and that is enforced rather than asked for: both branch classes
  /// declare it <c>sealed</c> - true on <see cref="BlockEntities.BlockEntityShaftFurnace"/>, false on
  /// <see cref="BlockEntities.BlockEntityFireboxFurnace"/>, whose "shaft" is a firebox beside the work
  /// rather than a column over the raceway. Picking the right base is the whole decision. This member
  /// stays <c>virtual</c> only so the two branches can answer it; anything hanging off a branch inherits
  /// the branch's answer.
  /// </para>
  /// </summary>
  protected virtual bool ShaftHoldsLayeredCharge => false;

  /// <summary>Sub-tree per column rather than one flat tree: <see cref="ChargeColumn"/>'s own keys are
  /// fixed strings, so nine columns sharing a tree would overwrite each other and every one would reload
  /// as a copy of the last written. The key carries the structure-local <c>(x, z)</c>.</summary>
  private const string ColumnTreePrefix = "chargeCol";

  private static readonly ReadOnlyDictionary<
    (int X, int Z),
    ChargeColumn
  > _noColumns = new(new Dictionary<(int X, int Z), ChargeColumn>());

  // Built once, then only indexed: descent reads this set every tick for every column of every lit
  // furnace. Null while the furnace has no columns at all - either because it is a hearth, or because
  // nothing has asked yet.
  //
  // The invariant that makes "never invalidated" correct: the shaft box is a compile-time constant per
  // block type. It is the bounding box of the cells that type's drawing marks as fuel (ShaftBox), and a
  // drawing is part of the block definition - so no instance state, not the variant and not the placed
  // angle, can move it. Two consequences worth stating, because both look like they should need
  // invalidation and neither does:
  //
  //   - Rotation, OnExchanged and a structure rebuild leave the set alone, because the keys are
  //     structure-local. Only GlobalOf's answer moves, and that is computed per call.
  //   - EnsureShaftColumns is reached from ReadShaftColumns via FromTreeAttributes, which the engine
  //     calls before Initialize - so the set is materialised at the earliest possible instant.
  //
  // Block is already assigned at that point: vanilla assigns it in CreateBehaviors, and every load path
  // calls CreateBehaviors immediately before FromTreeAttributes - ServerChunk's chunk read,
  // ClientChunk.PreLoadBlockEntitiesFromPacket, both SpawnBlockEntity paths and the worldgen accessor
  // (verified against the decompiled 1.22 assemblies). Api is what is missing that early, not Block - and
  // reading a layout needs only Block, so the box is derivable here.
  //
  // Belt and braces anyway, because the failure would be silent and permanent: ShaftBox memoises only a
  // non-empty derivation. A furnace that somehow read its box before its layout existed would answer "no
  // columns" for that one call and re-derive on the next, rather than capturing nothing for its whole life
  // and coming back from a save short. A furnace whose box could genuinely change while it lived would
  // still have to null both fields here at the moment it changed; nothing in the tree does.
  private Dictionary<(int X, int Z), ChargeColumn>? _shaftColumns;
  private ReadOnlyDictionary<(int X, int Z), ChargeColumn>? _readOnlyShaftColumns;

  /// <summary>
  /// Every column of the shaft, keyed by its <b>structure-local</b> <c>(x, z)</c>. Empty on a furnace
  /// that does not hold a layered charge.
  /// <para>
  /// <b>Why the keys are local and not world.</b> A local key does not depend on where the furnace was
  /// built or which way it faces, so the save survives both, and there is one conversion the other way -
  /// <c>GlobalOf(new Vec3i(x, y, z))</c> - at the point of use. Deriving the set from
  /// <see cref="ShaftBox"/> is also what makes it rotation-correct with no per-machine case: the cupola's
  /// single column and the blast furnace's nine come out of the same two lines.
  /// </para>
  /// <para>
  /// Genuinely read-only rather than the backing dictionary behind an interface - the columns themselves
  /// are the mutable part, and a caller who could add or drop a key would be inventing shaft geometry.
  /// </para>
  /// </summary>
  public IReadOnlyDictionary<(int X, int Z), ChargeColumn> ShaftColumns
  {
    get
    {
      EnsureShaftColumns();
      return _readOnlyShaftColumns ?? _noColumns;
    }
  }

  /// <summary>
  /// The column at structure-local <c>(<paramref name="localX"/>, <paramref name="localZ"/>)</c>, or
  /// null when that is not a column of this shaft - which is also what a hearth answers for every cell.
  /// The hopper's drip and descent both address a column this way.
  /// </summary>
  public ChargeColumn? ChargeColumnAt(int localX, int localZ)
  {
    EnsureShaftColumns();
    return _shaftColumns != null
      && _shaftColumns.TryGetValue((localX, localZ), out ChargeColumn? column)
      ? column
      : null;
  }

  /// <summary>
  /// The column standing under <paramref name="worldCell"/> and <b>which charge block of it that cell
  /// is</b> - <paramref name="blockIndex"/> 0 at the shaft floor, counting up. Null (and
  /// <paramref name="blockIndex"/> -1) when the cell is not in this furnace's shaft at all, which is also
  /// what a hearth answers everywhere.
  /// <para>
  /// This is the whole world-to-column conversion in one place, so a renderer never does the arithmetic
  /// itself. Both halves are the trap: the <c>(x, z)</c> half has to go through <see cref="LocalOf"/> or
  /// the shaft transposes under rotation, and the <c>y</c> half has to be measured from the shaft's own
  /// floor - which is <see cref="ShaftBox"/>'s low <c>y</c>, <b>never</b> the anchor's - or every column
  /// reads one block index out and the bands the block draws come from the wrong window.
  /// </para>
  /// <para>
  /// The corners are re-sorted on <c>y</c> for the same reason <see cref="ShaftBounds"/> and
  /// <see cref="EnsureShaftColumns"/> do it on <c>x</c>/<c>z</c>: a furnace that drew its box the
  /// other way up should draw oddly at worst, not index its blocks from the roof down.
  /// </para>
  /// </summary>
  public ChargeColumn? ChargeColumnAt(BlockPos worldCell, out int blockIndex)
  {
    blockIndex = -1;
    if (!ShaftHoldsLayeredCharge || ShaftBox is not { } box)
      return null;

    Vec3i local = LocalOf(worldCell);
    int floorY = Math.Min(box.min.Y, box.max.Y);
    int roofY = Math.Max(box.min.Y, box.max.Y);
    if (local.Y < floorY || local.Y > roofY)
      return null;

    ChargeColumn? column = ChargeColumnAt(local.X, local.Z);
    if (column == null)
      return null;

    // The index is this cell's position in its own column's cell list, not `y` minus anything. Both
    // wrong answers it replaces were arithmetic on y:
    //
    //   - off the shaft box's floor: the crucible is a three-cell well in the middle row at y=1, so the
    //     box's floor is 1 while six of nine columns start at 2. Those columns' first pile got index 1, so
    //     it drew bands 16-31 of a column that only has bands 0-15 - a physically present pile rendering
    //     empty until the column stood two blocks tall.
    //   - off the column's own floor: right for a contiguous column, wrong for one with a hole in it. A
    //     drawing that leaves a cell out mid-column makes `y - floor` skip an index, while
    //     `SyncChargeBlocks` fills the list from the bottom with no gap. The two then disagree by the
    //     size of the hole and every pile above it draws the wrong window.
    //
    // Indexing the list is what makes the three readings agree by construction: placement walks it,
    // this walks it, and `ChargeColumn.BlocksTall` counts blocks from the column's base. See
    // `ChargeCellsOf`.
    //
    // On a contiguous column all three readings coincide, which is why every test written before the
    // crucible was widened still passes either way. That is also why each needed a case of its own.
    IReadOnlyList<Vec3i> cells = ChargeCellsOf(local.X, local.Z);
    for (int i = 0; i < cells.Count; i++)
      if (cells[i].Y == local.Y)
      {
        blockIndex = i;
        return column;
      }

    return null;
  }

  /// <summary>
  /// The <see cref="CellRole.Chargeable"/> cells of column
  /// <c>(<paramref name="localX"/>, <paramref name="localZ"/>)</c> in structure-local coordinates,
  /// <b>ordered bottom-up</b> - empty when that is not a column of this shaft.
  /// <para>
  /// <b>The single source of "which cell is the n-th block of this column".</b> Charge fills this list
  /// from index 0 and <see cref="ChargeColumnAt(BlockPos, out int)"/> reads a cell's index out of it, so
  /// placement and the render window cannot drift apart - not over a well in the hearth, and not over a
  /// hole in the drawing. Anything that recomputes the index as <c>y - something</c> is reintroducing the
  /// bug this replaced.
  /// </para>
  /// </summary>
  /// <summary>
  /// What one charge block of this furnace's column holds, in whatever this furnace measures its charge in.
  /// <para>
  /// <b>There are two kinds of pile and this is the seam between them.</b> An
  /// <b>ore</b> charge (blast furnace) is counted in <b>items</b>: 16 bands x
  /// <c>ChargeItemsPerBand</c> = 32 items of coke and burden. A <b>remelt</b> charge (cupola) is counted in
  /// metal <b>units</b> up to <c>CupolaChargeMetalUnitsPerBlock</c>, because a 5 u bit, a 25 u chunk and a
  /// 375 u pig must all be able to go into the same pile - which is how a cupola was charged in life.
  /// </para>
  /// <para>
  /// It is deliberately a per-block figure rather than per-band. The remelt quantum is 3 000 / 16 =
  /// <b>187.5</b> units a band, which no integer per-band constant can express; <see cref="ChargeColumn"/>
  /// derives band boundaries from this by multiplying before dividing, so both kinds stay exact.
  /// </para>
  /// </summary>
  public virtual int ChargeUnitsPerBlock =>
    IwexValues.ChargeItemsPerBand * ChargeColumn.BandsPerBlock;

  protected IReadOnlyList<Vec3i> ChargeCellsOf(int localX, int localZ)
  {
    var cells = new List<Vec3i>();
    foreach (BlockPos cell in ChargeableCells)
    {
      Vec3i local = LocalOf(cell);
      if (local.X == localX && local.Z == localZ)
        cells.Add(local);
    }
    cells.Sort((a, b) => a.Y.CompareTo(b.Y));
    return cells;
  }

  /// <summary>
  /// The structure-local <c>y</c> of the lowest <see cref="CellRole.Chargeable"/> cell in column
  /// <c>(<paramref name="localX"/>, <paramref name="localZ"/>)</c>, or null when the column has none.
  /// <para>
  /// Charge rests on the lowest open cell of <b>its own</b> column, which is not the same height for
  /// every column of a furnace whose hearth has a well in it.
  /// </para>
  /// <para>
  /// This is the floor only - it is <b>not</b> what block indices are measured from. See
  /// <see cref="ChargeCellsOf"/> for why that is a list position rather than a height difference.
  /// </para>
  /// </summary>
  protected int? ColumnFloorY(int localX, int localZ)
  {
    IReadOnlyList<Vec3i> cells = ChargeCellsOf(localX, localZ);
    return cells.Count == 0 ? null : cells[0].Y;
  }

  /// <summary>Everything the whole shaft holds, across every column - the left-hand number in the
  /// hopper's <c>27 of 36</c> readout, and what the fire threshold will be compared against once the
  /// furnace runs off the columns.</summary>
  public int ShaftChargeUnits
  {
    get
    {
      EnsureShaftColumns();
      if (_shaftColumns == null)
        return 0;
      int total = 0;
      foreach (ChargeColumn column in _shaftColumns.Values)
        total += column.TotalUnits;
      return total;
    }
  }

  /// <summary>
  /// Most units column <c>(<paramref name="localX"/>, <paramref name="localZ"/>)</c> can hold - its own
  /// chargeable cell count times <see cref="ChargeUnitsPerBlock"/>. 0 when it is not a column of this shaft.
  /// <para>
  /// Per column, not per furnace: a hearth with a well in it gives its middle columns one cell more than
  /// the rest, so a single figure would over-fill six of nine. Charge pushed past this is real - it is
  /// counted, walked and saved - but it stands above the roof where no block can draw it and no player can
  /// dig it out, which is the same class of defect keying the columns off the bounding box was.
  /// </para>
  /// </summary>
  public int ColumnCapacity(int localX, int localZ) =>
    ChargeCellsOf(localX, localZ).Count * Math.Max(1, ChargeUnitsPerBlock);

  /// <summary>
  /// The column a fresh load of <paramref name="material"/> should be laid on, and how much room is left in
  /// it - or null when the shaft will not take that material anywhere right now.
  /// <para>
  /// <b>The one place the charging rule lives</b>, so the tall hopper and smex's bell hopper cannot come
  /// to charge differently. Two clauses:
  /// </para>
  /// <list type="number">
  /// <item><b>The lowest column first</b> (ties on ascending <c>(x, z)</c>, so it is deterministic). That
  /// is what makes a shaft fill in level courses rather than one tower at a time - and a level course is
  /// exactly what the raceway needs before the furnace will light.</item>
  /// <item><b>Fuel may only go on burden.</b> A round is fuel <em>then</em> burden; laying fuel straight
  /// onto fuel is not a round, it is a fire with no ore in it. An empty column takes either, because that
  /// is where a round starts.
  /// <para>
  /// <b>The top is tested by <see cref="IsFuelCode"/>, never by equality with the incoming material.</b>
  /// <c>top == material</c> gives the same answer for every case a one-fuel shaft can produce - coke onto
  /// coke is refused either way - so nothing can see the difference until a second priced fuel exists.
  /// Under the equality a hopper of charcoal lays a second fuel course straight onto the coke one:
  /// nothing throws, the units all add up, the bands draw, and the furnace lights on a shaft that has no
  /// ore between its fuel and can never make iron.
  /// <c>HopperTallTests.Charcoal_will_not_go_onto_a_column_topped_with_COKE</c> is the only case in the
  /// suite that can tell the two spellings apart.
  /// </para></item>
  /// </list>
  /// <para>
  /// A column already at <see cref="ColumnCapacity"/> is skipped, so a full shaft answers null and the
  /// hopper holds its tank rather than pushing charge above the roof.
  /// </para>
  /// </summary>
  public ChargeColumn? NextChargeColumn(string? material, out int room)
  {
    room = 0;
    EnsureShaftColumns();
    if (_shaftColumns == null || string.IsNullOrEmpty(material))
      return null;

    bool isFuel = IsFuelCode(material);

    ChargeColumn? best = null;
    (int X, int Z) bestKey = default;
    int bestRoom = 0;

    foreach (var ((x, z), column) in _shaftColumns)
    {
      int free = ColumnCapacity(x, z) - column.TotalUnits;
      if (free <= 0)
        continue;
      // Fuel onto any fuel, not merely onto the same one - see the second clause of the remarks.
      if (isFuel && IsFuelCode(column.TopMaterial))
        continue;

      if (
        best == null
        || column.TotalUnits < best.TotalUnits
        || (
          column.TotalUnits == best.TotalUnits
          && (x < bestKey.X || (x == bestKey.X && z < bestKey.Z))
        )
      )
      {
        best = column;
        bestKey = (x, z);
        bestRoom = free;
      }
    }

    room = bestRoom;
    return best;
  }

  /// <summary>
  /// The temperature freshly-charged material enters a column at: world ambient, <b>rounded to the nearest
  /// 5 °C</b>.
  /// <para>
  /// <b>The rounding is load-bearing, not cosmetic.</b> <see cref="ChargeColumn.Push"/> merges only
  /// within <see cref="ChargeColumn.TempMergeEpsilon"/> (1 °C), so charging at raw per-tick ambient
  /// shatters one hand-laid course into a fresh segment on almost every drip - nothing throws, the totals
  /// stay right and the bands still draw, and the symptom is a save tree with thousands of segments.
  /// Quantising is what makes "a load spread over ten ticks is one band" true by construction.
  /// </para>
  /// <para>
  /// Ambient, never <c>_internalTemp</c>: new charge arrives cold onto a hot furnace and warms on the
  /// way down. Pushing at the furnace's own temperature would invent heat the coke never made - and would
  /// shatter the segment list twice over, because a firing furnace's temperature moves every tick.
  /// </para>
  /// </summary>
  public float ChargeTemperature => MathF.Round(_ambientTemp / 5f) * 5f;

  private void EnsureShaftColumns()
  {
    if (_shaftColumns != null || !ShaftHoldsLayeredCharge)
      return;
    // Returns without assigning, so nothing is memoised: a furnace whose layout has not arrived yet gets
    // to try again rather than owning no columns for the rest of its life. See the remark on _shaftColumns.
    if (ShaftBox is not { } box)
      return;

    // Keyed off the chargeable cells, not off the box's (x,z) grid. The box is a bounding box, so a
    // shaft that is not a solid rectangle in plan has (x,z) pairs inside it that the drawing marks no
    // chargeable cell in - keying off the grid would mint a live, pushable ChargeColumn for every one
    // of them.
    //
    // Charge pushed into such a column is counted by ShaftChargeUnits, walked by any handle over
    // ShaftColumns, and written to the save - but SyncChargeBlocks skips it (`ChargeCellsOf` is empty), so
    // it never becomes a block, can never be dug back out, and inflates the fire threshold and the heat
    // balance's cold-charge term for the rest of the furnace's life. Nothing throws; the shaft just quietly
    // holds charge that does not exist.
    //
    // Both shipped shafts are solid 3x3 (and the cupola 1x1) above the hearth, so today the two key sets
    // are identical and no world is affected. It is the L-shaped and stepped drawings - which the layout
    // DSL fully allows, and which the suite's own chiral fixtures already are - that this closes.
    // It also means the key set is exactly what `ChargeCellsOf` answers for, which is what lets
    // SyncChargeBlocks iterate `_shaftColumns` directly instead of re-deriving the columns from the cells.
    var seen = new Dictionary<(int X, int Z), ChargeColumn>();
    foreach (BlockPos cell in ChargeableCells)
    {
      Vec3i local = LocalOf(cell);
      seen.TryAdd((local.X, local.Z), new ChargeColumn());
    }
    // Empty means the layout marked nothing chargeable - do not memoise that, for the same reason the
    // ShaftBox guard above does not: a furnace read before its layout arrived gets to try again rather than
    // owning no columns for the rest of its life.
    if (seen.Count == 0)
      return;

    Dictionary<(int X, int Z), ChargeColumn> columns = seen;

    _shaftColumns = columns;
    _readOnlyShaftColumns = new ReadOnlyDictionary<(int X, int Z), ChargeColumn>(
      columns
    );
  }

  /// <summary>
  /// <b>Invariant, never the ambient culture.</b> An interpolated <c>int</c> takes its sign from
  /// <c>NumberFormatInfo.NegativeSign</c>, and that is <b>not</b> ASCII <c>-</c> under sv-SE, fi-FI or
  /// lt-LT (U+2212 MINUS SIGN) nor under ar-SA or fa-IR (bidi marks). This string is a <b>save key</b>:
  /// a world written on a Swedish machine and opened on an English one would match only the sign-free
  /// <c>(0, 0)</c> column and silently empty the other eight, and <see cref="ReadShaftColumns"/>'
  /// "no column key at all" guard cannot see it because that one key does match. Same rule, and the same
  /// reason, as <c>ExConfigRegister</c>, <c>ExMeasure</c> and <c>StockMesh</c>: a string that has to mean
  /// the same thing on two machines is formatted invariantly.
  /// </summary>
  private static string ColumnKey(int x, int z) =>
    string.Create(CultureInfo.InvariantCulture, $"{ColumnTreePrefix}_{x}_{z}");

  private void WriteShaftColumns(ITreeAttribute tree)
  {
    // Not one attribute on a furnace that holds no layered charge - checked before the columns are
    // even built, so a hearth never allocates them either.
    if (!ShaftHoldsLayeredCharge)
      return;

    EnsureShaftColumns();
    // Not `_shaftColumns!`. EnsureShaftColumns is allowed to build nothing - a shaft furnace whose
    // drawing has not arrived has no box and therefore no columns - and a save must not throw out of
    // ToTreeAttributes because of it. Writing no column attribute is the same thing this method already
    // does for a hearth, and ReadShaftColumns' "no column key at all" branch reads it back unharmed.
    if (_shaftColumns == null)
      return;

    foreach (var ((x, z), column) in _shaftColumns)
    {
      var sub = new TreeAttribute();
      column.ToTree(sub);
      tree[ColumnKey(x, z)] = sub;
    }
  }

  /// <summary>
  /// Reads the columns back, looking each one up by its own key.
  /// <para>
  /// <b>A save whose shaft is a different shape loads anyway.</b> Because the walk is over the columns
  /// the furnace has now - not over the keys the tree happens to carry - a saved column outside the
  /// current box is simply never read and is dropped, and a column the save does not describe comes back
  /// empty. Dropping is the mildest of the three options: throwing would make a rebuilt (or, when a later
  /// phase moves the drawing's lowest chargeable course and so <see cref="ShaftBox"/> with it, merely
  /// upgraded) furnace unloadable, and quietly folding a stray
  /// column onto a valid key would move a player's charge to a cell they never charged.
  /// </para>
  /// <para>
  /// A tree carrying <b>no</b> column key at all leaves the columns alone rather than clearing them: that
  /// is a save from before the columns existed, or a partial tree, and neither is the world saying the
  /// shaft is empty.
  /// </para>
  /// </summary>
  private void ReadShaftColumns(ITreeAttribute tree)
  {
    if (!ShaftHoldsLayeredCharge)
      return;

    EnsureShaftColumns();
    // Same reason as WriteShaftColumns: no box means no columns to read into, and a load must not throw.
    // The saved keys are left in the tree untouched - this block entity simply has nowhere to put them.
    if (_shaftColumns == null)
      return;

    int described = 0;
    foreach (var (x, z) in _shaftColumns.Keys)
      if (tree.GetTreeAttribute(ColumnKey(x, z)) != null)
        described++;
    if (described == 0)
      return;

    foreach (var ((x, z), column) in _shaftColumns)
      column.FromTree(
        tree.GetTreeAttribute(ColumnKey(x, z)) ?? new TreeAttribute()
      );
  }

  /// <summary>
  /// Makes the world match the columns: places and removes <c>iwex:furnace-chargepile</c> so each column
  /// stands exactly <see cref="ChargeColumn.BlocksTall"/> blocks high, from its own floor upward.
  /// <para>
  /// <b>Idempotent, and that is the contract.</b> It is safe to call after every charge, every descent
  /// and every load; it writes only the cells whose occupancy actually changed. Callers do not track what
  /// they last placed - the columns are the truth and this reconciles the world to them.
  /// </para>
  /// <para>
  /// Walks <see cref="ChargeableCells"/>, <b>never</b> <see cref="ShaftBox"/>. The box is a bounding box
  /// round the marked cells and is larger: on the cold blast furnace it is 45 cells against 39 marked, and
  /// the difference is brick, the tuyere pair and the taps. Placing off the box would put charge blocks
  /// inside the walls.
  /// </para>
  /// <para>
  /// Each column fills its own <see cref="ChargeCellsOf"/> list from index 0, which on a hearth with a
  /// well does not start at the same height for every column - and which is the <b>same</b> list
  /// <see cref="ChargeColumnAt(BlockPos, out int)"/> reads a block's index out of, so a drawing with a
  /// hole in a column stacks around the hole in both directions rather than the two disagreeing by the
  /// size of the hole.
  /// </para>
  /// <para>
  /// <b>Never writes over a block it did not place.</b> A cell holding anything but air or its own pile
  /// is skipped, so the column draws short while the obstruction stands and heals when it goes. See the
  /// remark at the guard for the cell this is actually about - the crucible, which the shipped drawings
  /// mark <see cref="CellRole.Chargeable"/> and <see cref="CellRole.Pool"/> at once.
  /// </para>
  /// <para>
  /// Every surviving pile in a touched column gets <see cref="BlockEntityChargePile.OnColumnChanged"/>.
  /// That is the only route that republishes the snapshot the tesselation thread reads, so a column whose
  /// contents changed without changing height still redraws.
  /// </para>
  /// </summary>
  /// <remarks>
  /// <b>Public, not protected</b> — the callers are outside this class and outside this <em>mod</em>: the
  /// tall hopper drips into a column it does not own, and smex's bell hopper does the same across an assembly
  /// boundary. Both must reconcile the world afterwards or the charge they added never becomes a block.
  /// <para>
  /// That is safe precisely because of the idempotence contract above: this method reads the columns and
  /// writes the world, never the reverse, so an outside caller cannot use it to invent charge — it can only
  /// ask for what the columns already say to be made true.
  /// </para>
  /// </remarks>
  public void SyncChargeBlocks()
  {
    if (!ShaftHoldsLayeredCharge || Api?.World is not { } world)
      return;
    EnsureShaftColumns();
    if (_shaftColumns == null)
      return;

    // Resolved once per call. Null when the block is not registered - which is a headless test that never
    // registered it, not a broken world, so it is a quiet no-op rather than a throw.
    Block? pile = world.GetBlock(BlockChargePile.PileCode);
    if (pile == null)
      return;

    foreach (var (key, column) in _shaftColumns)
    {
      // Bottom-up, from the shared list - the same one ChargeColumnAt indexes, which is what keeps the
      // block a cell holds and the bands it draws from being two different opinions. A key with no
      // chargeable cells (the tuyere columns, which ShaftBox spans and the drawing does not mark) yields
      // an empty list and this iteration does nothing.
      IReadOnlyList<Vec3i> local = ChargeCellsOf(key.X, key.Z);
      if (local.Count == 0)
        continue;

      int want = ChargeColumn.BlocksTall(column.TotalUnits, ChargeUnitsPerBlock);

      bool touched = false;
      // The piles that are standing when this call is done - the set that gets the redraw. Collected as
      // we go rather than re-read afterwards, so a cell we just cleared cannot be told to redraw.
      var standingPiles = new List<BlockPos>();
      for (int i = 0; i < local.Count; i++)
      {
        BlockPos pos = GlobalOf(local[i]);
        Block? standing = world.BlockAccessor.GetBlock(pos);
        bool has = standing?.BlockId == pile.BlockId;

        if (i < want && !has)
        {
          // Never write over a block that is neither air nor our own pile. The cell this guard exists
          // for is the crucible, which the shipped drawings mark Chargeable and Pool at once - so the
          // furnace itself puts a block there and this walk would take it straight back.
          //
          // The pool is a live block, not an extinguish-only one: `iwex:hearthmetal` appears the moment
          // melting starts and stands for the whole campaign (the float pair `_moltenIron`/`_moltenSlag`
          // this file still reads is the old model, kept only until the hearth-cell pool replaces it). So
          // this is not a one-shot collision at shutdown - without the guard the furnace and this walk
          // fight over the same cell every tick, one placing hearth metal and the other replacing it
          // with charge.
          //
          // Skipping instead means the column draws one block short while the cell is taken, and heals the
          // moment it frees up. The units are not dropped - the column still holds them and the hopper
          // still reports them, so this cannot silently eat charge either.
          //
          // Dropping `Chargeable` from the hearth glyph outright is the planned real fix and makes the
          // crucible case here moot; this guard keeps the interim stable, and it still earns its keep
          // afterwards: a player can put a block in an open shaft cell.
          if (standing != null && standing.BlockId != 0)
            continue;
          world.BlockAccessor.SetBlock(pile.BlockId, pos);
          standingPiles.Add(pos);
          touched = true;
        }
        else if (i >= want && has)
        {
          world.BlockAccessor.SetBlock(0, pos);
          touched = true;
        }
        else if (has)
          standingPiles.Add(pos);
      }

      // Redraw the whole column when anything about it moved - not only the cells written. A pile two
      // blocks down draws a different window once the column's contents change, and it is the same call
      // either way.
      if (!touched && !ColumnContentsMayHaveMoved)
        continue;
      foreach (BlockPos pos in standingPiles)
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityChargePile pileBe)
          pileBe.OnColumnChanged();
    }
  }

  /// <summary>
  /// Whether a <see cref="SyncChargeBlocks"/> call should redraw columns whose <b>height</b> did not
  /// change. Defaults true because the common caller - a charge, a descent, a smelt - moves contents
  /// without necessarily crossing a block boundary, and a column that redrew only on height change would
  /// hold a stale mesh for most of a campaign.
  /// <para>
  /// A seam rather than a constant so a future caller that knows nothing moved (a reload reconcile) can
  /// skip the walk.
  /// </para>
  /// </summary>
  protected virtual bool ColumnContentsMayHaveMoved => true;

  #endregion

  #region Charge family gate

  // A furnace burns exactly one burden family (the blast furnace: ore burden; the cupola: remelt
  // burden). Wrong-family charge is a legible mistake, not a silent one: it still lights and burns in
  // the shaft - and burns out to salvageable spent burden on extinguish, like any charge - but the
  // furnace will not render molten metal out of it, and the HUD names what it is holding versus what it
  // burns. Deriving the family from the item's identity (Burden.FamilyOf) keeps the two burdens distinct
  // items that can never merge into one pile. Every gate below reads the same shaft walk the tick does,
  // so every route charge can enter a shaft by (hand-placed pile, hopper drop, in-situ regrade, chute)
  // is covered at the one seam - the charge read - not per entry point.

  // There is no per-family accept list: with one burden item a family gate has one possible answer, and
  // a gate with one answer is not a gate - it is a thing that can only ever be wrong.
  //
  // What a furnace declares is what it charges (`IsChargeItem` / `IsChargeCode`), which is a stronger
  // statement and one seam instead of two: the cupola says "scrap and fuel", the blast furnace says
  // "burden and fuel", and anything else in the shaft is rejected by not matching either.

  /// <summary>Item-level charge identity, family-blind: anything the shaft counts as chargeable at all
  /// (prepared burden of either family, or the legacy count-only blast mix). The family gate is layered
  /// a furnace whose charge is items rather than burden overrides this.
  /// <para>
  /// <b>Public because the hoppers ask it.</b> A hopper carrying its own opinion about what is
  /// chargeable is a fourth copy of a predicate that already exists in three places - which is how one
  /// hopper block serving the blast furnace, the cupola, the heating furnace and the coke oven can refuse
  /// the very fuel one of them burns. The machine declares its own charge and the tank delegates.
  /// </para>
  /// </summary>
  public virtual bool IsChargeItem(ItemStack? stack) => Items.Burden.Is(stack);

  /// <summary>
  /// <see cref="IsChargeItem"/> asked of a material <b>code</b> rather than a stack - what a layered
  /// column can answer, because a <c>ChargeSegment</c> holds units of a substance and never a stack.
  /// <para>
  /// The two must stay in step. A furnace that overrides one and not the other gates its piles and its
  /// columns differently, which during the cutover means the same charge is accepted through one route and
  /// refused through the other - with nothing failing, because both answers are individually plausible.
  /// </para>
  /// </summary>
  public virtual bool IsChargeCode(string? material) =>
    Items.Burden.IsCode(material);

  /// <summary>
  /// Whether <paramref name="material"/> is a carbon reductant - coke or charcoal, the <c>fuel</c>
  /// material role.
  /// <para>
  /// <b>This is not "not burden".</b> <b>Unstamped</b> burden is neither: it carries no composition
  /// and burns as the standard grade. A shaft that took <c>!IsAnyCode</c> as its fuel test would read
  /// every unit of it as <b>pure carbon</b> - a coke fraction of 1.00 against the reference 0.20 - which
  /// pins the heat balance's fuel factor at its ceiling and reads as a furnace that got mysteriously
  /// hotter after an update.
  /// </para>
  /// </summary>
  public static bool IsFuelCode(string? material) =>
    !string.IsNullOrEmpty(material)
    && MaterialRoleRegistry.IsRole(Roles.Fuel, new AssetLocation(material));

  /// <summary>
  /// Whether a <b>stack</b> is a carbon reductant - <see cref="IsFuelCode"/> asked of an item.
  /// <para>
  /// <b>Here rather than inline at the call sites, and that is the point.</b> Three separate spellings of
  /// "consult <c>Roles.Fuel</c>" existed across this class and
  /// <see cref="BlockEntities.BlockEntityShaftFurnace"/>. They agreed only because all three passed the same
  /// token - and the moment one of them grew a <em>weight</em> beside it
  /// (<see cref="CarbonPerUnit"/>) the others would have become a machine that accepts a fuel it cannot
  /// price. One predicate, one weight, one place.
  /// </para>
  /// </summary>
  public static bool IsFuelStack(ItemStack? stack) =>
    MaterialRoleRegistry.IsRole(Roles.Fuel, stack);

  /// <summary>
  /// How much <b>carbon</b> one charge unit of <paramref name="material"/> carries, in coke units - 1.0 for
  /// coke, 0.5 for charcoal, 0 for anything that is not a fuel at all.
  /// <para>
  /// <b>This is what makes a shaft able to burn more than one fuel.</b> The
  /// raceway spends carbon, never bands: the material's own <c>fuel</c>-role value over
  /// <see cref="IwexValues.BfFuelCarbonReference"/>. Charcoal was already accepted everywhere in the shaft -
  /// it lit, burned, made gas, descended and yielded iron at <b>coke's exact rate</b>, and rendered as coke
  /// pixel-for-pixel - so this is not what lets it in. It is what finally charges for it.
  /// </para>
  /// <para>
  /// <b>Returns 0 for a non-fuel, and callers must treat that as "not carbon" rather than dividing by
  /// it.</b> The registry's own fallback for a valueless grant is 1.0, which against a reference of 2.0
  /// reads as half of coke - so a mod that grants the fuel role and forgets the value gets charcoal's rate,
  /// not coke's. That is deliberately the safe direction.
  /// </para>
  /// <para>
  /// <b>Granting <c>Roles.Fuel</c> is granting a shaft-charge permit</b>, because
  /// <see cref="BlockEntities.BlockEntityShaftFurnace.IsChargeItem"/> accepts anything holding it. Do not
  /// grant it to bituminous or anthracite however tempting the tidy-up looks: the firebox burns those
  /// happily (it asks a different question - "will it carry a heat"), but raw coal crushes to dust under a
  /// burden column and <c>docs/design/processes/coking.md</c> forbids it in a shaft. The two taxonomies stay
  /// separate on purpose.
  /// </para>
  /// </summary>
  public static float CarbonPerUnit(string? material)
  {
    if (!IsFuelCode(material))
      return 0f;
    float reference = Math.Max(0.01f, IwexValues.BfFuelCarbonReference);
    return MaterialRoleRegistry.ValueOf(
        Roles.Fuel,
        new AssetLocation(material)
      ) / reference;
  }

  /// <summary>
  /// True while charge this furnace does not recognise is blocking the melt-to-molten conversion.
  /// <para>
  /// The rule here is not about families: <i>a furnace that will render nothing is burning, not
  /// melting</i>. Drop it and the HUD prints <c>Melting</c> over a shaft that can never produce a drop,
  /// which reads as a bug in the furnace rather than as a wrong charge.
  /// </para>
  /// </summary>
  protected bool ConversionBlocked => _cachedRejectedCount > 0;

  #endregion

  #region Abstract method implementations

  /// <summary>
  /// The core is a plainly-oriented block (the <c>side</c> variant the vanilla
  /// <c>HorizontalOrientable</c> behaviour stamps at placement), so the structure angle comes
  /// straight off that variant - no stored yaw, and no offset: the layout grids are drawn in the
  /// core's own north frame.
  /// </summary>
  protected override void UpdateStructureRotation()
  {
    if (Block == null)
      return;

    SetStructureAngle(ExOrientation.AngleFromSide(Block.Variant["side"]));
  }

  protected override void OnStructureCompleted() => ScanForOutlets();

  /// <summary>
  /// <b>Losing the walls does not put the fire out.</b> Calling <c>Extinguish()</c> here would apply the
  /// <b>choke</b> behaviour - sealed and starved, so it goes out - to its exact physical opposite. A
  /// furnace that loses its stack becomes a bonfire in a brick ruin: opened to the air, it draws harder
  /// and keeps burning its coke off at open-air temperature. A furnace whose <em>blast</em> fails is the
  /// one that goes out, because a packed shaft has almost no natural draught of its own.
  /// <para>
  /// <b>Not extinguishing is only half of it</b>: <c>OnProductionTick</c> is gated on structure
  /// completeness, so a breach without the matching change to that guard yields a <em>frozen</em> furnace
  /// rather than a burning one - the same end state by a different route, and no test would tell them
  /// apart. See the guard.
  /// </para>
  /// <para>
  /// Deliberately empty rather than deleted: the override documents that doing nothing is the answer, so
  /// the next person to look does not "fix" the missing extinguish back in.
  /// </para>
  /// </summary>
  protected override void OnStructureLost() { }

  protected override string GetIncompleteMessage(int missingCount) =>
    Lang.Get("iwex:bf-error-incomplete", missingCount);

  protected override string GetCompleteMessage() =>
    Lang.Get("iwex:bf-error-complete");

  #endregion

  #region Initialization

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    CacheAttributes();
    if (api.Side == EnumAppSide.Server && StructureComplete)
      ScanForOutlets();
  }

  /// <summary>
  /// Re-reads the tunables into the cached fields. Called once at init and again at the top of each
  /// production tick so a live `/exmod config` change applies immediately. Subclasses override to
  /// refresh their own product/charge tunables, calling base first.
  /// </summary>
  protected virtual void CacheAttributes()
  {
    _ironMeltingPoint = MeltingPoint;
    _maxFuelBurnTime = MaxFuelBurnTime;
    _meltStartDelay = MeltStartDelay;
    _meltIntervalSec = MeltIntervalSec;
    _tuyereIntakeVolume = TuyereIntakeVolume;
    _starvationSupplyFrac = IwexValues.BfStarvationSupplyFrac;
    _ambientTemp = ReadAmbientTemperature();
  }

  /// <summary>
  /// Outside air temperature at the furnace, sampled once per tick alongside the tunables - the
  /// climate lookup is far too heavy to run per HUD read, and the HUD runs per looking player.
  /// <c>NowValues</c> is not one of the modes the API documents as never-null (and the headless test
  /// accessor returns null outright), so an unavailable climate falls back to the configured default.
  /// </summary>
  protected float ReadAmbientTemperature()
  {
    ClimateCondition? climate = Api?.World?.BlockAccessor?.GetClimateAt(
      Pos,
      EnumGetClimateMode.NowValues
    );
    return climate?.Temperature ?? IwexValues.BfAmbientFallbackTemp;
  }

  #endregion

  #region Tick

  /// <summary>
  /// <b>A burning furnace ticks even with its walls gone.</b> The inherited gate is
  /// <c>StructureComplete</c>, so a breach stops the production tick being <em>called at all</em> - and a
  /// furnace that does not tick is <b>frozen</b>, not burning: it holds its temperature, its charge and its
  /// pool for ever, which is the same end state an extinguish gives by a different route and which no test
  /// could tell apart.
  /// <para>
  /// <b>This override is the load-bearing half of the breach rule.</b> Leaving <c>OnStructureLost</c>
  /// empty achieves nothing on its own, and neither does the guard at the top of the tick body: the real
  /// block is here, in <see cref="ExpandedLib.Blocks.Structures.BlockEntityMultiblockStructure"/>, one
  /// layer up. The symptom of missing it is a breached furnace that consumes exactly zero coke.
  /// </para>
  /// <para>
  /// An <em>idle</em> incomplete furnace still costs nothing: it is only the fire that survives losing the
  /// walls, and a fire that has gone out cannot be restarted through the hole.
  /// </para>
  /// </summary>
  protected override bool CanRunProduction =>
    StructureComplete || State != FurnaceState.Idle;

  /// <summary>The furnace keeps its production listener when the walls come out - see
  /// <see cref="CanRunProduction"/>. Overriding the gate alone leaves a <b>frozen</b> furnace, because the
  /// gate is only read by a listener that still exists.</summary>
  protected override bool StopsProductionOnStructureLost => false;

  protected override void OnProductionTick(float dt)
  {
    // "Return only if idle", not "return if incomplete" - the same rule CanRunProduction states, kept
    // here too because this method is also reached through the away-catch-up replay.
    if (!StructureComplete && State == FurnaceState.Idle)
      return;

    // Re-read the tunables each tick so a live `/exmod config iwex ...` change (e.g. shortening the
    // melt-start delay) takes effect immediately, instead of staying pinned to whatever was cached
    // when this furnace last loaded. Reading the generated accessor is a plain field read - cheap.
    CacheAttributes();

    bool dirty = false;

    bool failedAny = false;
    if (State != FurnaceState.Idle)
    {
      foreach (var pos in _gasOutlets)
      {
        if (Api.World.BlockAccessor.GetBlockEntity(pos) is IPipeNode outlet)
        {
          if (
            !outlet.TryProduce(ExhaustVolumePerTick, ExhaustTemperature, "Exhaust")
          )
            failedAny = true;
        }
        else
        {
          ScanForOutlets();
        }
      }
    }

    if (IsChoked != failedAny)
    {
      IsChoked = failedAny;
      dirty = true;
    }

    // One scan of the charge per tick; all charge-based reads/writes below
    // reuse this handle instead of re-walking the hearth region.
    object chargeHandle = CollectCharge();
    int mixCount = ReadChargeMix(
      chargeHandle,
      out bool isFull,
      out BurdenMix mix,
      out int rejectedCount
    );
    if (
      _cachedMixCount != mixCount
      || _cachedIsFull != isFull
      || _cachedRejectedCount != rejectedCount
    )
      dirty = true;
    _cachedMixCount = mixCount;
    _cachedIsFull = isFull;
    _cachedRejectedCount = rejectedCount;
    _chargeMix = mix;

    bool tuyeresReceiveExhaust = false;
    float blastTemp = _ambientTemp;
    float blastSupplied = 0f;

    // Air is a consumed reagent, not a sensed one: a lit furnace draws its blast out of the tuyere
    // network and decrements it. TuyereIntakeVolume is a per-second rate, so the draw scales with dt
    // for tick-independence (and the demand it is measured against scales the same way). Gated on
    // State != Idle: an idle furnace is not burning, so it pulls no air - and must not silently bleed
    // a shared blast main dry while it sits cold.
    // Both the draw and the pressure the line must hold come from the charge, not from this furnace -
    // see the "Burden-derived blast demand" region. Recomputed each tick so re-charging a running
    // furnace with a different grade immediately changes what its blast main has to deliver.
    float requiredPressure = RequiredBlastPressureFor(mix);
    float perTuyereDraw = TuyereDrawFor(mix) * dt;
    if (State != FurnaceState.Idle)
    {
      foreach (var pos in _tuyeres)
      {
        if (Api.World.BlockAccessor.GetBlockEntity(pos) is IPipeNode tuyere)
        {
          if (tuyere is BlockEntityPipe pipe)
          {
            if (pipe.Medium == "Exhaust")
              tuyeresReceiveExhaust = true;

            // The pressure test comes before the draw. Testing after it means a main that cannot meet
            // the charge's required pressure is still drained dry every tick while the furnace credits
            // itself zero supply - it pays for air it never received, and starves every other machine
            // sharing that main at the same time. A line that cannot deliver is simply not drawn on, and
            // the furnace slides back to natural draught, which is what the model already intended.
            if (pipe.Medium == "Air" && pipe.Pressure >= requiredPressure)
            {
              // How much air actually arrived, not merely whether a line is attached: an
              // under-supplied tuyere slides the furnace back toward natural draught (cooler T_in),
              // and a near-dry one starves it out entirely (the disruption below).
              blastSupplied += tuyere.TryConsume(perTuyereDraw);
              blastTemp = Math.Max(blastTemp, pipe.Temperature);
            }
          }
          else
          {
            // A non-pipe intake node carries no pressure to test, so it is drawn on as before.
            tuyere.TryConsume(perTuyereDraw);
          }
        }
        else
        {
          ScanForOutlets();
        }
      }
    }

    float blastDemand = _tuyeres.Count * perTuyereDraw;
    float blastSupplyFrac = blastDemand > 0f ? blastSupplied / blastDemand : 0f;

    // A breach is open to the air, so it runs on natural draught alone however good the blast main
    // still is - the pressure goes out through the hole rather than through the burden. This is what makes
    // a breached furnace burn cooler (~970 °C, the pinned natural-draught row) while still burning.
    if (!StructureComplete)
      blastSupplyFrac = 0f;

    bool isLiquidCapacityReached = LiquidCapacityReached;

    // A lit furnace that needs blast is starving when the air actually arriving falls under the floor
    // (a stopped blower, a cut or bled-out main). This is what "not enough air" means mechanically: it
    // has already cooled T_in toward natural draught via the air factor, and now it also counts toward
    // extinguish (below). Gated on State != Idle so an idle furnace - which draws no air by design -
    // never reads as starved.
    bool airStarved =
      State != FurnaceState.Idle
      && RequiresBlast
      && blastSupplyFrac < _starvationSupplyFrac;
    if (_airStarved != airStarved)
    {
      _airStarved = airStarved;
      dirty = true;
    }

    // A branch that derives its state has no ignition event, no extinguish countdown and no timers at
    // all: what it is doing is recomputed from the charge every tick. Everything between here and the
    // `if (State != Idle)` block below is the stored-state machine, and only the firebox branch runs it.
    if (DerivesState)
    {
      FurnaceState derived = DeriveState(chargeHandle);
      if (derived != State)
      {
        if (derived == FurnaceState.Idle)
          Shutdown(); // residue and sounds only - it can no longer set a state
        else if (State == FurnaceState.Idle)
          ExSounds.Play(Api, ShaftCentrePos, ExSounds.Ignite, 1f, 32f);
        _state = derived;
        dirty = true;
      }
    }
    else if (
      State == FurnaceState.Idle
      && StructureComplete
      && _cachedIsFull
      && !IsChoked
    )
    {
      if (TryIgniteCharge(chargeHandle))
      {
        _state = FurnaceState.Firing;
        _fuelBurnSeconds = 0;
        _internalTemp = IgnitionTemp;
        dirty = true;
        // Whoosh as the charge catches.
        ExSounds.Play(Api, ShaftCentrePos, ExSounds.Ignite, 1f, 32f);
      }
    }

    if (State != FurnaceState.Idle && !DerivesState)
    {
      int disruptionCount = 0;
      if (mixCount < DisruptionMixFloor)
        disruptionCount++;
      if (tuyeresReceiveExhaust)
        disruptionCount++;
      if (IsChoked)
        disruptionCount++;
      if (isLiquidCapacityReached)
        disruptionCount++;
      // Air starvation is one more disruption, not a parallel mechanism: sub-floor blast held for the
      // extinguish grace (~30 s alone, instant when it compounds another disruption) snuffs the fire
      // through the same _extinguishSeconds counter and timer reset as every other stall.
      if (airStarved)
        disruptionCount++;

      if (disruptionCount > 0)
      {
        _extinguishSeconds += dt;
        dirty = true;

        int extinguishThreshold =
          disruptionCount >= 2
            ? ExtinguishThresholdSevere
            : ExtinguishThresholdDefault;

        if (_extinguishSeconds >= extinguishThreshold)
        {
          Extinguish();
          return;
        }
      }
      else
      {
        if (_extinguishSeconds != 0)
          dirty = true;
        _extinguishSeconds = 0;
      }
    }

    if (State == FurnaceState.Firing || State == FurnaceState.Melting)
    {
      // Roaring furnace ambience while lit.
      ExSounds.PlayThrottled(
        Api,
        ShaftCentrePos,
        ExSounds.Fire,
        ref _lastFireSoundMs,
        5000,
        0.6f,
        32f
      );

      // The furnace has no maximum temperature: it chases wherever making and losing heat balance.
      // CombustionMix, not _chargeMix. On a shaft they differ: what burns is the round at the raceway,
      // while _chargeMix is the whole column and stays the answer for permeability and the grade readout.
      _lastHeatBalance = ComputeHeatBalance(
        CombustionMix(chargeHandle, _chargeMix),
        blastSupplyFrac,
        blastTemp,
        mixCount
      );

      float oldTemp = _internalTemp;
      ApplyProcessTemperature(_lastHeatBalance.TProcess, dt);
      if (Math.Abs(_internalTemp - oldTemp) > 0.1f)
        dirty = true;

      // After the temperature, because the gas leaves the raceway at it: this is the pass that puts
      // heat into burden before it reaches the fire, and it is what makes descent time worth anything.
      CirculateGas(chargeHandle, dt);

      if (State == FurnaceState.Firing && !DerivesState)
      {
        _fuelBurnSeconds += dt;
        if (_fuelBurnSeconds >= _maxFuelBurnTime)
        {
          Extinguish();
          return;
        }

        if (AtMeltingTemperature(chargeHandle))
        {
          _secondsAboveMelting += dt;
          dirty = true;
          // Wrong-family charge blocks the conversion: the furnace holds at heat, burning its fuel out,
          // but never crosses into Melting while a rejected pile is in the shaft. The soak timer keeps
          // accruing so it converts the instant the offending pile is dug out.
          if (_secondsAboveMelting >= _meltStartDelay && !ConversionBlocked)
          {
            TransitionToMelting();
            return;
          }
        }
        else
        {
          if (_secondsAboveMelting != 0)
            dirty = true;
          _secondsAboveMelting = 0;
        }
      }
      else if (State == FurnaceState.Melting)
      {
        // The cold-soak reversal is stored-state machinery: a derived branch simply reads Firing again
        // on the next tick, with nothing to reset.
        if (!DerivesState && !AtMeltingTemperature(chargeHandle))
        {
          _belowMeltingSeconds += dt;
          dirty = true;
          if (_belowMeltingSeconds >= BelowMeltingReset)
          {
            _state = FurnaceState.Firing;
            _secondsAboveMelting = 0;
            _belowMeltingSeconds = 0;
            _fuelBurnSeconds = 0;
            dirty = true;
          }
        }
        else
        {
          if (_belowMeltingSeconds != 0)
            dirty = true;
          _belowMeltingSeconds = 0;

          // ConversionBlocked also guards the melt cycle: a wrong-family pile dropped into an
          // already-melting furnace stops new metal being rendered (existing molten still drains).
          if (!isLiquidCapacityReached && !ConversionBlocked)
          {
            if (MeltsPerTick)
            {
              // No cycle length at all. The raceway renders whatever descended past it this second,
              // band by band, so the cadence is the descent rather than a timer over it.
              SmeltCycle(chargeHandle, dt);
              dirty = true;
            }
            else
            {
              _meltSeconds += dt;
              // The harder the furnace is being blown past the melt line, the faster the burden
              // renders - which is what makes a hot blast out-produce a cold one without either
              // carrying its own per-cycle yield constant.
              if (_meltSeconds >= _meltIntervalSec / MeltSpeedFactor())
              {
                _meltSeconds = 0;
                SmeltCycle(chargeHandle, _meltIntervalSec / MeltSpeedFactor());
                dirty = true;
              }
            }
          }

          DrainProducts(ref dirty);
        }
      }
    }

    if (dirty)
      MarkDirty(true);
  }

  #endregion

  #region Heat balance

  /// <summary>
  /// The furnace's dynamic heat balance - <c>T_process = T_in - T_loss</c>, floored at ambient, with
  /// no maximum temperature anywhere in it. <c>T_in</c> is coke combustion (how rich the burden is in
  /// fuel, scaled by how much blast actually reached the tuyeres) plus whatever preheat a cowper put
  /// into the air. <c>T_loss</c> is stack radiation plus the cold mass of the charge plus a cold day.
  /// <para>
  /// This is the single mechanism behind the cold/hot split: nothing here is overridden per furnace.
  /// A high-coke burden clears iron's melt line on cold blast; a low-coke burden only clears it once
  /// a cowper is preheating the blast; an under-pressure line slides either back toward natural
  /// draught. See docs/design/conventions.md and docs/design/{iwex,smex}.md.
  /// </para>
  /// </summary>
  protected HeatBalance ComputeHeatBalance(
    BurdenMix charge,
    float blastSupplyFrac,
    float blastTemp,
    int mixCount
  )
  {
    float fuelFrac = charge.HasContent
      ? charge.FuelFrac
      : IwexValues.BfDefaultFuelFrac;

    float reference = Math.Max(0.0001f, IwexValues.BfReferenceFuelFrac);
    float fuelFactor = GameMath.Clamp(
      1f + IwexValues.BfCokeSensitivity * (fuelFrac - reference) / reference,
      IwexValues.BfMinFuelFactor,
      IwexValues.BfMaxFuelFactor
    );

    float natural = IwexValues.BfNaturalDraughtFactor;
    float airFactor =
      natural + (1f - natural) * GameMath.Clamp(blastSupplyFrac, 0f, 1f);

    float preheatGain =
      IwexValues.BfPreheatCoefficient * Math.Max(0f, blastTemp - _ambientTemp);

    float tIn =
      IwexValues.BfCombustionBaseTemp
      + IwexValues.BfCombustionCokeGain * fuelFactor * airFactor
      + preheatGain;

    int requiredMix = Math.Max(1, ChargeCapacityUnits);
    float chargeLoss =
      IwexValues.BfChargeLossFull
      * GameMath.Clamp((float)mixCount / requiredMix, 0f, 1f);
    float ambientLoss =
      IwexValues.BfAmbientLossPerDegree
      * Math.Max(0f, IwexValues.BfAmbientReferenceTemp - _ambientTemp);

    float tLoss = IwexValues.BfRadiationLossBase + chargeLoss + ambientLoss;

    // The T_process = T_in - T_loss floor (at ambient) and the record shape are the exlib helper's,
    // shared with the converter; the furnace supplies its coke-combustion T_in/T_loss and contributors.
    return HeatBalance.Compute(
      tIn,
      tLoss,
      _ambientTemp,
      fuelFrac,
      fuelFactor,
      airFactor,
      blastSupplyFrac > 0f,
      blastTemp,
      preheatGain,
      chargeLoss,
      ambientLoss
    );
  }

  /// <summary>
  /// Melt-cycle speed as a multiple of the nominal rate, from how far above the melt line the hearth
  /// is running. This is where the design docs' "cold ~30 u/s, hot ~45 u/s" comes from - a ratio that
  /// falls out of the heat balance rather than a second yield constant per furnace. Setting
  /// <c>BfMeltMarginGain</c> to 0 restores a flat rate.
  /// </summary>
  protected float MeltSpeedFactor() =>
    Math.Max(
      0.01f, // never let a retuned floor of 0 divide the melt interval into infinity
      GameMath.Clamp(
        1f
          + IwexValues.BfMeltMarginGain
            * (_internalTemp - _ironMeltingPoint)
            / Math.Max(1f, IwexValues.BfMeltMarginReference),
        IwexValues.BfMeltSpeedMin,
        IwexValues.BfMeltSpeedMax
      )
    );

  #endregion

  #region State transitions

  private void TransitionToMelting()
  {
    _state = FurnaceState.Melting;
    _meltSeconds = 0;
    _fuelBurnSeconds = 0;
    MarkDirty(true);
  }

  /// <summary>
  /// Puts the fire out: the sound, the residue, and the counters back to zero.
  /// <para>
  /// <b>A derived branch calls <see cref="Shutdown"/> instead</b>, which is this without the state
  /// assignment - it cannot set a state, because on that branch the state is a read. The two share
  /// <see cref="ExtinguishResidue"/> so the residue order stays one thing: freeze the pool, burn out the
  /// charge, clear the pools, in that order and no other. The planned hearth-cell pool replaces the first
  /// of those, not the order.
  /// </para>
  /// </summary>
  private void Extinguish()
  {
    _state = FurnaceState.Idle;
    Shutdown();
  }

  /// <summary>
  /// Everything going out <em>does</em>, minus deciding that it went out: the sound, the reset to ambient,
  /// the residue, and the counters. Safe to call when the state has already been recomputed to
  /// <see cref="FurnaceState.Idle"/> by <see cref="DeriveState"/>.
  /// </summary>
  private void Shutdown()
  {
    ExSounds.Play(Api, ShaftCentrePos, ExSounds.Extinguish, 1f, 32f);

    _internalTemp = 20f;
    // A dead furnace is off, not starving: clear the flag so the serialized state (and the HUD) does
    // not report a stall on a cold hearth.
    _airStarved = false;

    ExtinguishResidue();

    _secondsAboveMelting = 0;
    _meltSeconds = 0;
    _extinguishSeconds = 0;
    _belowMeltingSeconds = 0;
    _fuelBurnSeconds = 0;

    MarkDirty(true);
  }

  #endregion

  #region Furnace-specific hooks

  /// <summary>
  /// Re-reads the gas-outlet and tuyere world cells for the current rotation off the layout's own
  /// <see cref="CellRole.GasOutlet"/> and <see cref="CellRole.Tuyere"/> marks.
  /// <para>
  /// As hand-written offset arrays these were a second copy of a fact the layout owned: the cold furnace
  /// and the cupola said "my open top is my chimney" (<c>GasOutletCells =&gt; []</c>) and the hearths said
  /// "no blast", when their drawings already said both by carrying no outlet and no tuyere glyph. Asked as
  /// roles, an absence needs no declaration at all.
  /// </para>
  /// </summary>
  protected virtual void ScanForOutlets()
  {
    _gasOutlets = CellsWithRole(CellRole.GasOutlet);
    _tuyeres = CellsWithRole(CellRole.Tuyere);
  }

  /// <summary>
  /// Walks the charge once and returns an opaque handle the other charge hooks consume, so the tick
  /// does a single hearth walk per tick. The returned handle is passed back to
  /// <see cref="ReadChargeMix"/>, <see cref="TryIgniteCharge"/>, and <see cref="SmeltCycle"/>.
  /// <para>
  /// <b>There is no default, and that is deliberate.</b> Both branches own their charge outright -
  /// the shaft its <see cref="ChargeColumn"/>s, the firebox its <c>BEBehaviorFirebox</c> beds - so a
  /// fallback walk here would only ever be reached by a branch that forgot to override it, and it would
  /// answer "no charge" rather than fail: a furnace that silently never lights. Abstract makes that a
  /// compile error.
  /// </para>
  /// </summary>
  protected abstract object CollectCharge();

  /// <summary>
  /// Reads the charge in one walk of the shaft. Returns the total charge count (family-blind: enough of
  /// <em>anything</em> chargeable lights and burns the furnace), and hands back whether that total is
  /// full enough to fire, the summed composition of the whole column (so the heat balance and the HUD
  /// read one coke fraction), and how much of that charge is the <b>wrong family</b> - the count and the
  /// family token that drive the conversion block and the mismatch HUD line. While lit this is also where
  /// the furnace keeps its charge managed/burning (the side effect the original <c>GetBlastMixCount</c> ran).
  /// </summary>
  protected abstract int ReadChargeMix(
    object chargeHandle,
    out bool isFull,
    out BurdenMix mix,
    out int rejectedCount
  );

  /// <summary>Returns whether the charge is fully lit (all piles burning), igniting it as needed.</summary>
  protected abstract bool TryIgniteCharge(object chargeHandle);

  /// <summary>
  /// Consumes from the charge and accumulates molten product for <paramref name="dt"/> seconds' worth of
  /// melting. <paramref name="dt"/> is the tick's own step when <see cref="MeltsPerTick"/>, and the
  /// interval that just elapsed otherwise, so an implementation reads it the same way either way.
  /// </summary>
  protected abstract void SmeltCycle(object chargeHandle, float dt);

  /// <summary>
  /// Whether melting is evaluated <b>every tick</b> rather than on a fixed cycle. False by default: a
  /// hearth that renders nothing has no cadence to speak of, and one that does can keep a cycle length.
  /// <para>
  /// The shaft branch is true, and it is the melt condition that forces it: burden melts iff the
  /// temperature <em>it carried down the shaft</em> clears the line, evaluated at <b>unit</b> granularity
  /// over the raceway slice. A per-cycle implementation can only ever melt all of a round or none of it,
  /// so a slice spanning a coke/burden boundary - which is most of them - would be wrong every time.
  /// </para>
  /// </summary>
  protected virtual bool MeltsPerTick => false;

  /// <summary>Whether any molten product pool has reached its capacity. False for a furnace that pools
  /// nothing - it can never be backed up by its own output.</summary>
  protected virtual bool LiquidCapacityReached => false;

  /// <summary>Drains the molten products into their taps; sets <paramref name="dirty"/> when state changed.
  /// A no-op on a furnace with no pool and no taps.</summary>
  protected virtual void DrainProducts(ref bool dirty) { }

  #endregion

  #region Extinguish residue

  // What a dead furnace leaves behind is the same story for every furnace in the mod - the pool
  // freezes onto the hearth floor and the burden is left as spent, salvageable charge - so it lives
  // here once. A furnace supplies only the two facts that are genuinely its own: which solid block
  // its pool freezes into and how much metal was in it. Nothing on this path makes slag: a furnace
  // that goes out is a setback, not a total loss of the charge.

  /// <summary>Block placed where the molten pool freezes (solidified iron; cast iron for a cupola). Null
  /// on a furnace that melts nothing, which never reaches the freeze anyway.</summary>
  protected virtual AssetLocation? SolidProductBlock => null;

  /// <summary>
  /// Units of molten metal in the pool being frozen. <b>Zero is the meaningful default</b>: a furnace that
  /// only heats its charge has no pool, so <see cref="SolidifyBottomLayer"/> returns immediately and the
  /// three members below are never reached. That is what lets a non-melting furnace inherit this residue
  /// path truthfully instead of stubbing it.
  /// </summary>
  protected virtual float DrainedMetalUnits => 0f;

  /// <summary>Stamps the frozen product's nugget count onto the block entity placed at <paramref name="pos"/>.</summary>
  protected virtual void StampSolidProduct(BlockPos pos, int units) { }

  /// <summary>Zeroes the furnace's molten pools once the residue has been placed.</summary>
  protected virtual void ClearMoltenPools() { }

  /// <summary>
  /// Residue behaviour shared by every furnace: the molten pool freezes across the bottommost layer
  /// of the shaft, and the remaining burden is burned out by height rather than destroyed - the
  /// player can dig it out, re-coke it in the mixer and charge it again.
  /// </summary>
  protected virtual void ExtinguishResidue()
  {
    SolidifyBottomLayer();
    BurnOutCharge();
    ClearMoltenPools();
  }

  /// <summary>
  /// Freezes the molten pool onto the crucible floor - the layout's <see cref="CellRole.Pool"/> cells -
  /// spread evenly over whichever of them are actually free. The split is deterministic (remainder to the
  /// first cells, no world RNG) so the residue is testable and so two identical furnaces leave identical
  /// wrecks.
  /// </summary>
  private void SolidifyBottomLayer()
  {
    // A furnace that melts nothing reports 0 here and leaves at once - the whole freeze is molten-only.
    float units = DrainedMetalUnits;
    if (units <= 0f || SolidProductBlock is not { } solidCode)
      return;

    Block? solid = Api.World.GetBlock(solidCode);
    if (solid == null)
      return;

    var cells = new List<BlockPos>();
    string pileCode = BlockChargePile.PileCode.Path;
    foreach (BlockPos pos in PoolCells)
    {
      Block occupant = Api.World.BlockAccessor.GetBlock(pos);
      // Free = empty, or a charge pile that was being consumed here. A pile holding wrong-family charge
      // is not free: freezing the pool over it would silently destroy the salvage the player is owed
      // (this furnace never converted that charge, so the metal was made from the accepted charge only).
      //
      // The occupant is `iwex:furnace-chargepile`, not `game:coalpile`. The shipped drawings mark the
      // crucible Chargeable and Pool at once, so this cell reliably holds a pile - and a test for the
      // wrong code reads as "no free cell", which silently freezes no metal at all rather than throwing.
      if (occupant.Id == 0)
        cells.Add(pos);
      else if (occupant.Code?.Path == pileCode && !PileHoldsRejectedCharge(pos))
        cells.Add(pos);
    }
    if (cells.Count == 0)
      return;

    float perNugget = Math.Max(0.0001f, IwexValues.BfUnitsPerSolidNugget);
    int totalNuggets = Math.Max(1, (int)Math.Floor(units / perNugget));
    int each = totalNuggets / cells.Count;
    int extra = totalNuggets % cells.Count;

    for (int i = 0; i < cells.Count; i++)
    {
      int nuggets = each + (i < extra ? 1 : 0);
      if (nuggets <= 0)
        continue;
      Api.World.BlockAccessor.SetBlock(solid.BlockId, cells[i]);
      StampSolidProduct(cells[i], nuggets);
    }
  }

  /// <summary>
  /// Whether the charge standing in the pool cell at <paramref name="pos"/> is charge this furnace refused
  /// to convert - the cell the solidify walk must not overwrite, so the wrong-family salvage survives.
  /// <para>
  /// It asks the <b>column</b>, not the block: the pile is a window and holds nothing itself. The
  /// question is what stands in <em>that block's</em> slice of the column, so the walk is over the units
  /// covering this block index rather than over the whole column - a wrong-family course at the stockline
  /// says nothing about whether the hearth floor is free.
  /// </para>
  /// <para>
  /// <b>It asks <see cref="IsChargeCode"/> and nothing else.</b> A family test here reads every fuel band
  /// as wrong-family charge in a furnace that accepts only one family - so a cupola broken or put out
  /// while coke still stood at its raceway finds <b>no</b> free pool cell, and
  /// <see cref="SolidifyBottomLayer"/> returns having frozen nothing: <see cref="ClearMoltenPools"/> then
  /// destroys the whole bath.
  /// <para>
  /// <see cref="BlockEntities.BlockEntityShaftFurnace.ReadChargeMix"/> carries the same guard for the same
  /// reason, and the two must not drift: they are the two places a furnace decides that charge in front of
  /// it is not its own.
  /// </para>
  /// </para>
  /// </summary>
  private bool PileHoldsRejectedCharge(BlockPos pos)
  {
    if (ChargeColumnAt(pos, out int blockIndex) is not { } column)
      return false;

    int perBlock = Math.Max(1, ChargeUnitsPerBlock);
    int low = blockIndex * perBlock;
    int at = 0;
    foreach (ChargeSegment segment in column.Segments)
    {
      int end = at + segment.Units;
      if (end > low && at < low + perBlock)
        if (!IsChargeCode(segment.Material))
          return true;
      at = end;
      if (at >= low + perBlock)
        break;
    }
    return false;
  }

  /// <summary>
  /// Rewrites the remaining charge in place as spent charge instead of destroying it - the carbon burns
  /// out, the ore/metal and flux survive, and the player digs the wreck out, re-cokes it in the mixer and
  /// charges it again. R2, the recoverability promise: a furnace that goes out is a setback, not a loss.
  /// <para>
  /// <b>Abstract, because the two branches no longer store charge the same way</b> - and no longer share
  /// a rule either. A shaft's column is metres tall and the blast only ever reached the bottom of it, so
  /// what survives is interpolated by <em>height</em>
  /// (<see cref="BlockEntities.BlockEntityShaftFurnace"/>). A firebox is one course of cells all equally
  /// in the fire, so only the bottom fraction is honest
  /// (<see cref="BlockEntities.BlockEntityFireboxFurnace"/>). There is no shared fallback body: a
  /// fallback nobody reaches is a fallback that hides a branch which forgot to override.
  /// </para>
  /// </summary>
  protected abstract void BurnOutCharge();

  #endregion

  #region Block lifecycle

  public override void OnBlockRemoved()
  {
    if (Api?.Side == EnumAppSide.Server && State != FurnaceState.Idle)
      Extinguish();
    base.OnBlockRemoved();
  }

  #endregion

  #region Serialization

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldAccessForResolve
  )
  {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    IsChoked = tree.GetBool("isChoked");
    _state = (FurnaceState)tree.GetInt("bfState", 0);
    _internalTemp = tree.GetFloat("internalTemp", 20f);
    _secondsAboveMelting = tree.GetFloat("secondsAboveMelting", 0);
    _meltSeconds = tree.GetFloat("meltSeconds", 0);
    _extinguishSeconds = tree.GetFloat("extinguishSeconds", 0);
    _belowMeltingSeconds = tree.GetFloat("belowMeltingSeconds", 0);
    _fuelBurnSeconds = tree.GetFloat("fuelBurnSeconds", 0);
    _cachedMixCount = tree.GetInt("cachedMixCount", 0);
    _cachedIsFull = tree.GetBool("cachedIsFull", false);
    _cachedRejectedCount = tree.GetInt("cachedRejectedCount", 0);
    _airStarved = tree.GetBool("airStarved", false);
    ReadHeatBalance(tree);
    ReadShaftColumns(tree);
  }

  // The whole balance rides the tree, not just its result. GetBlockInfo runs on the client, and the
  // client never walks the charge or reads the pipes - so anything the HUD prints has to arrive here
  // or it prints zeroes. This is the same reason _cachedMixCount is serialized.

  private void ReadHeatBalance(ITreeAttribute tree)
  {
    _lastHeatBalance = new HeatBalance(
      tree.GetFloat("hbIn"),
      tree.GetFloat("hbLoss"),
      tree.GetFloat("hbProcess", 20f),
      tree.GetFloat("hbFuelFrac"),
      tree.GetFloat("hbFuelFactor"),
      tree.GetFloat("hbAirFactor"),
      tree.GetBool("hbBlastSupplied"),
      tree.GetFloat("hbBlastTemp"),
      tree.GetFloat("hbPreheat"),
      tree.GetFloat("hbChargeLoss"),
      tree.GetFloat("hbAmbientLoss")
    );
    _chargeMix = new BurdenMix(
      tree.GetFloat("chargeIron"),
      tree.GetFloat("chargeFlux"),
      tree.GetFloat("chargeFuel")
    );
  }

  private void WriteHeatBalance(ITreeAttribute tree)
  {
    HeatBalance hb = _lastHeatBalance;
    tree.SetFloat("hbIn", hb.TIn);
    tree.SetFloat("hbLoss", hb.TLoss);
    tree.SetFloat("hbProcess", hb.TProcess);
    tree.SetFloat("hbFuelFrac", hb.FuelFrac);
    tree.SetFloat("hbFuelFactor", hb.FuelFactor);
    tree.SetFloat("hbAirFactor", hb.AirFactor);
    tree.SetBool("hbBlastSupplied", hb.BlastSupplied);
    tree.SetFloat("hbBlastTemp", hb.BlastTemp);
    tree.SetFloat("hbPreheat", hb.PreheatGain);
    tree.SetFloat("hbChargeLoss", hb.ChargeLoss);
    tree.SetFloat("hbAmbientLoss", hb.AmbientLoss);
    tree.SetFloat("chargeIron", _chargeMix.Iron);
    tree.SetFloat("chargeFlux", _chargeMix.Flux);
    tree.SetFloat("chargeFuel", _chargeMix.Fuel);
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetBool("isChoked", IsChoked);
    tree.SetInt("bfState", (int)State);
    tree.SetFloat("internalTemp", _internalTemp);
    tree.SetFloat("secondsAboveMelting", _secondsAboveMelting);
    tree.SetFloat("meltSeconds", _meltSeconds);
    tree.SetFloat("extinguishSeconds", _extinguishSeconds);
    tree.SetFloat("belowMeltingSeconds", _belowMeltingSeconds);
    tree.SetFloat("fuelBurnSeconds", _fuelBurnSeconds);
    tree.SetInt("cachedMixCount", _cachedMixCount);
    tree.SetBool("cachedIsFull", _cachedIsFull);
    tree.SetInt("cachedRejectedCount", _cachedRejectedCount);
    tree.SetBool("airStarved", _airStarved);
    WriteHeatBalance(tree);
    WriteShaftColumns(tree);
  }

  #endregion

  #region Component HUD slices

  // The furnace HUD is spread across its functional component blocks: each scans up to this core (its
  // anchor) and reads the synced state below, showing only its own slice - the taps the pool they drain,
  // the tall hopper the burden in the shaft - while the core keeps the heat ledger. The formatting and
  // lang keys live here, with the data, so a component stays a thin caller and the cupola inherits the
  // split for free. See the survey / docs for the rationale.

  /// <summary>How far a functional component scans to find its core: <see cref="ComponentScanBelow"/>
  /// cells down (the cold furnace's tall hopper sits six cells above the hearth, the taps one-two),
  /// <see cref="ComponentScanAbove"/> up, and <see cref="ComponentScanHorizontal"/> out per horizontal
  /// axis (the taps reach x=+/-2). Generous over the current layouts; <see cref="OwnsCell"/> rejects any
  /// non-owning core the wider box happens to catch, so the slack is safe.</summary>
  public const int ComponentScanHorizontal = 3;
  public const int ComponentScanBelow = 8;
  public const int ComponentScanAbove = 1;

  /// <summary>
  /// World cell of the lower (metal) tap for the placed rotation, or <c>null</c> on a furnace whose
  /// drawing has no metal tap. A tap compares its own position against this to know it is the metal tap -
  /// and shows the metal pool rather than the slag.
  /// <para>
  /// <b>Derived, never declared.</b> A hand-declared <c>Vec3i</c> literal on this class is inherited by
  /// every furnace whether or not it has a tap to put there, and on a hearth's own drawing it can land
  /// outside the structure altogether with nothing to notice, because a hearth pours nothing.
  /// </para>
  /// <para>
  /// <b>Nullable, deliberately.</b> <see cref="CellRole.MetalTap"/> is <c>[SingleCell]</c>, so a layout
  /// that declares it declares exactly one cell and the build refuses anything else - but a layout may
  /// decline to declare it at all, and that is the honest answer for the two hearths. A non-null return
  /// would have to invent a cell.
  /// </para>
  /// </summary>
  public BlockPos? MetalTapPos => SingleCellWithRole(CellRole.MetalTap);

  /// <summary>World cell of the higher (slag) tap for the placed rotation, or <c>null</c> on a furnace
  /// whose drawing has no slag tap. See <see cref="MetalTapPos"/>.</summary>
  public BlockPos? SlagTapPos => SingleCellWithRole(CellRole.SlagTap);

  /// <summary>
  /// The one world cell a <see cref="SingleCellAttribute">single-cell</see> role names, or null when the
  /// layout does not mark it. The build-time arity guard is what makes reading a point out of a cell set
  /// legal here; the null case is a layout that declared nothing, not a layout that declared too much.
  /// </summary>
  private BlockPos? SingleCellWithRole(CellRole role)
  {
    IReadOnlyList<BlockPos> cells = CellsWithRole(role);
    return cells.Count == 1 ? cells[0] : null;
  }

  /// <summary>
  /// The tall hopper's slice: the burden loaded in the shaft against the fire threshold, plus the
  /// wrong-family warning - shown at the hopper the player charges. Silent until the structure is
  /// complete (an incomplete furnace has no meaningful shaft count).
  /// </summary>
  public void AppendShaftChargeInfo(StringBuilder sb)
  {
    if (!StructureComplete)
      return;

    // No separate "mix loaded" line: ignition is positional rather than quantity-gated, so "how full is
    // the shaft" is the only question left and `bf-info-shaftfull` below answers it. A second line
    // against `ChargeCapacityUnits` would print the identical pair of numbers twice.

    // A shaft can read full and still refuse to make metal; name the mismatch beside the count.
    AppendWrongBurdenInfo(sb);

    // The three lines below are the charging readout, and they exist because charging is a skill:
    // a round is fuel then burden, laid in level courses, and a player who cannot see what they just
    // laid is charging blind. All three are derived - not one field is added or persisted for them.
    // "fuel", not "coke": the shaft takes either, and a charcoal band is half the carbon of a coke
    // one - so the readout that names it is the only thing standing between the player and a round
    // worth half what they think.
    if (!ShaftHoldsLayeredCharge)
      return;
    AppendCourseInfo(sb);
    AppendChargeVerdict(sb);
    sb.AppendLine(
      Lang.Get(
        "iwex:bf-info-shaftfull",
        ShaftChargeUnits,
        ChargeableCells.Count * Math.Max(1, ChargeUnitsPerBlock)
      )
    );
  }

  /// <summary>
  /// Branch hook for the <b>chill</b> line - what a hung stockline says about itself. Empty on the core,
  /// because a hang is a property of a descending column and only the shaft branch has one.
  /// <para>
  /// A hang has no other symptom the player can attribute. The furnace stays lit, the temperature stays
  /// high, the fuel keeps going down and the iron stops - so without this line the whole mechanic reads as
  /// the machine having broken. It is the difference <c>layered-charge.md</c> draws between "a chill being
  /// a mystery and a chill being a risk the player took knowingly".
  /// </para>
  /// </summary>
  protected virtual void AppendChillInfo(StringBuilder sb) { }

  /// <summary>
  /// The course being laid, as bands: how much of the top block is fuel and how much is burden, on the
  /// column the <b>next</b> load will land in. <c>Bands</c> is the block's own resolution, so the pair
  /// reads as "N of 16".
  /// <para>
  /// <b><see cref="FuelMaterial"/> is what stops the readout calling every fuel coke.</b>
  /// A shaft burns coke <em>or</em> charcoal, and the two are not interchangeable: a
  /// charcoal band carries half the carbon (<see cref="CarbonPerUnit"/>), so a line that buckets both
  /// under "coke" tells the player the round they just laid is worth twice what it is - and charging is a
  /// skill precisely because that ratio is the thing being judged. It carries the fuel's own <b>code</b>
  /// rather than a counter per fuel, so a third fuel needs no new field, no new branch and no new lang key.
  /// </para>
  /// <para>
  /// <b>Two different facts share the null, and callers must read the pair.</b>
  /// <c>FuelBands == 0</c> is "no fuel in this course, nothing to name"; <c>FuelBands &gt; 0</c> with a
  /// null material is a <b>mixed</b> course - two or more fuels laid into the same top block, which no
  /// single name describes honestly. Picking the first one instead would be the same lie in a smaller
  /// font.
  /// </para>
  /// </summary>
  public readonly record struct ChargeCourse(
    int FuelBands,
    int BurdenBands,
    int Bands,
    string? FuelMaterial
  );

  /// <summary>
  /// The top course of the column the <b>next</b> load lands on.
  /// <para>
  /// That column is the lowest one (ties on ascending <c>(x, z)</c>) - the same rule
  /// <see cref="NextChargeColumn"/> follows, so the readout describes the course the player is actually
  /// working on rather than an arbitrary one. Reading a different column would drift out of step with the
  /// hopper on every single drip.
  /// </para>
  /// <para>
  /// <b>Public because it is the seam the HUD line can be pinned at.</b> The headless lang service
  /// echoes a key and drops its arguments, so a test that only read the rendered string could tell that
  /// the line is present and never that its numbers are right - the same reason
  /// <c>BlockEntityChargePile.RenderSlabs</c> is public.
  /// </para>
  /// </summary>
  public ChargeCourse TopCourse
  {
    get
    {
      ChargeColumn? target = null;
      (int X, int Z) key = default;
      foreach (var ((x, z), column) in ShaftColumns)
        if (
          target == null
          || column.TotalUnits < target.TotalUnits
          || (
            column.TotalUnits == target.TotalUnits
            && (x < key.X || (x == key.X && z < key.Z))
          )
        )
        {
          target = column;
          key = (x, z);
        }

      if (target is not { TotalUnits: > 0 })
        return new ChargeCourse(0, 0, ChargeColumn.BandsPerBlock, null);

      int perBlock = Math.Max(1, ChargeUnitsPerBlock);
      int topBlock = Math.Max(
        0,
        ChargeColumn.BlocksTall(target.TotalUnits, perBlock) - 1
      );

      int fuel = 0;
      int burden = 0;
      AssetLocation? fuelCode = null;
      string? fuelMaterial = null;
      bool mixedFuel = false;
      foreach (ChargeBandRun run in target.BandsAt(topBlock, perBlock))
        if (IsFuelCode(run.Material))
        {
          fuel += run.Bands;
          // The first fuel run names the course; a later run of a different fuel makes it unnameable.
          // It cannot stop at the first pair: runs coalesce on material, so a course can hold coke,
          // then charcoal, then coke again - three runs, of which only the middle one disagrees.
          var code = new AssetLocation(run.Material);
          if (fuelCode == null)
            (fuelCode, fuelMaterial) = (code, run.Material);
          else if (!fuelCode.Equals(code))
            mixedFuel = true;
        }
        else
          burden += run.Bands;

      return new ChargeCourse(
        fuel,
        burden,
        ChargeColumn.BandsPerBlock,
        // Compared as AssetLocations, not strings: a segment stores whatever code was pushed, and
        // `coke` and `game:coke` are the same fuel spelled two ways. String equality would report a
        // one-fuel course as mixed - the very hedge this is here to avoid printing.
        mixedFuel ? null : fuelMaterial
      );
    }
  }

  private void AppendCourseInfo(StringBuilder sb)
  {
    ChargeCourse course = TopCourse;
    if (course.FuelBands + course.BurdenBands <= 0)
      return;
    sb.AppendLine(
      Lang.Get(
        IwexLang.BfInfoCourse,
        course.FuelBands,
        CourseFuelName(course),
        course.BurdenBands,
        course.Bands
      )
    );
  }

  /// <summary>
  /// What the course line calls its fuel: the fuel's own item name when the course holds exactly one,
  /// "mixed fuel" when it holds several, and the bare word "fuel" when it holds none - so a burden-only
  /// course reads "0 fuel" instead of naming something that is not in there.
  /// <para>
  /// <b>The name comes off the item, not a per-fuel lang key of ours.</b> Coke and charcoal are vanilla
  /// items with vanilla names in every locale, so there is nothing for iwex to translate and a fuel a
  /// third mod grants <see cref="Roles.Fuel"/> to names itself for free. A hand-kept <c>bf-fuel-{path}</c>
  /// table would have been a closed set pretending to be an open one - the same shape of mistake as
  /// testing for coke by literal instead of by role.
  /// </para>
  /// </summary>
  private string CourseFuelName(in ChargeCourse course)
  {
    if (course.FuelBands <= 0)
      return Lang.Get(IwexLang.BfInfoCourseFuelnone);
    if (course.FuelMaterial is not { } material)
      return Lang.Get(IwexLang.BfInfoCourseFuelmixed);
    // An unresolvable code falls back to the generic word. Rendering the raw code at the player is the
    // one outcome worse than being vague, and a shaft loaded from an old save can hold a code whose mod
    // is gone.
    return MaterialDisplayName(material)
      ?? Lang.Get(IwexLang.BfInfoCourseFuelnone);
  }

  /// <summary>
  /// Display name for a charge material code, or null when nothing resolves. Items first, then blocks -
  /// <c>BlockEntityChargePile.StackOf</c>'s order, and for its reason: a segment stores a plain code
  /// string precisely so the set of chargeable things is not closed.
  /// </summary>
  private string? MaterialDisplayName(string material)
  {
    if (Api?.World is not { } world || string.IsNullOrEmpty(material))
      return null;

    var code = new AssetLocation(material);
    if (world.GetItem(code) is { } item)
      return new ItemStack(item).GetName();
    if (world.GetBlock(code) is { } block)
      return new ItemStack(block).GetName();
    return null;
  }

  /// <summary>
  /// Whether this charge will actually melt - stated as the temperature it settles at against the melt
  /// line, so the player can see how far off they are rather than only that they are off.
  /// <para>
  /// <b>The projection is "on full blast, at ambient".</b> It asks what the burden in the shaft would do
  /// with the blowers running and no preheat, which is the <em>cold</em> reading - a cowper only ever
  /// makes it better, so the verdict never promises a melt the furnace cannot deliver. The assumption is
  /// stated here rather than left implicit, because a verdict whose derivation is unwritten is worse than
  /// no verdict.
  /// </para>
  /// <para>
  /// It reads <see cref="ComputeHeatBalance"/> rather than re-deriving a coke-fraction threshold of its
  /// own, so it cannot drift from the model - and if the melt condition is later replaced with a real
  /// enthalpy test, this line follows for free instead of becoming a second, stale opinion.
  /// </para>
  /// </summary>
  private void AppendChargeVerdict(StringBuilder sb)
  {
    if (_cachedMixCount <= 0)
      return;

    HeatBalance projected = ComputeHeatBalance(
      _chargeMix,
      1f,
      _ambientTemp,
      _cachedMixCount
    );
    bool melts = projected.TProcess >= _ironMeltingPoint;
    sb.AppendLine(
      Lang.Get(
        melts ? "iwex:bf-info-chargemelts" : "iwex:bf-info-chargechills",
        (int)projected.TProcess,
        (int)_ironMeltingPoint
      )
    );
  }

  /// <summary>The lower tap's slice: the molten metal pool it drains. No-op on a furnace with no metal
  /// pool; <see cref="BlockEntities.BlockEntityShaftFurnace"/> overrides it.</summary>
  public virtual void AppendMoltenMetalInfo(StringBuilder sb) { }

  /// <summary>The upper tap's slice: the molten slag pool it drains. See <see cref="AppendMoltenMetalInfo"/>.</summary>
  public virtual void AppendMoltenSlagInfo(StringBuilder sb) { }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    long now = Api.World.ElapsedMilliseconds;
    if (now - _lastInfoUpdate > 1000)
    {
      StringBuilder sb = new StringBuilder();
      if (!StructureComplete)
      {
        sb.AppendLine(Lang.Get("iwex:bf-info-incomplete"));
      }
      else
      {
        // The furnace HUD is spread across its component blocks: the mix-loaded + wrong-burden lines
        // moved to the tall hopper (the burden slice), and the molten pools to the taps that drain them.
        // This core keeps only the temperature/threshold/heat-contributor ledger and the lit-state lines.
        if (State != FurnaceState.Idle)
        {
          string stateName = Lang.Get(
            "iwex:bf-state-" + State.ToString().ToLowerInvariant()
          );
          sb.AppendLine(Lang.Get("iwex:bf-info-state", stateName));
          AppendHeatBalanceInfo(sb);

          if (
            State == FurnaceState.Firing
            && _internalTemp >= _ironMeltingPoint
            // A blocked furnace is at heat but will never cross into Melting - the wrong-burden line
            // (now at the hopper) is the honest readout, not a melting-progress bar that would climb
            // to 100% and stall.
            && !ConversionBlocked
          )
          {
            // Progress toward the Melting phase as a percentage (matches the
            // Bessemer converter's readout) rather than a raw seconds countdown.
            int pct = (int)
              GameMath.Clamp(
                100f
                  * _secondsAboveMelting
                  / System.Math.Max(1f, _meltStartDelay),
                0,
                100
              );
            sb.AppendLine(Lang.Get("iwex:bf-info-meltingin", pct));
          }

          // Name an air-starved stall, the same way the wrong-burden and heat lines name theirs, so the
          // countdown below reads as a cause (a dead blower, a cut main) and not an unexplained snuffing.
          if (_airStarved)
            sb.AppendLine(Lang.Get(IwexLang.BfInfoAirstarved));

          // And name a hang, for exactly the same reason and right beside it. The two are the pair of
          // stalls a lit furnace can suffer - one starved of air, one blocked by its own cold burden - and
          // a hang has no other symptom at all: the furnace stays lit, stays hot, goes on eating fuel and
          // simply stops making iron. It reads here rather than in the hopper's charging slice because it
          // is a condition of the fire, and this is where the fire's lines live.
          AppendChillInfo(sb);

          if (_extinguishSeconds > 0)
          {
            int remainingSeconds = (int)
              System.Math.Max(
                0f,
                ExtinguishThresholdDefault - _extinguishSeconds
              );
            sb.AppendLine(
              Lang.Get("iwex:bf-info-extinguishingin", remainingSeconds)
            );
          }
        }
        else
        {
          AppendNotLitInfo(sb);
        }
      }
      _cachedInfoText = sb.ToString();
      _lastInfoUpdate = now;
    }
    dsc.Append(_cachedInfoText);
  }

  /// <summary>
  /// Reports the heat balance the way a furnaceman reads one: where the hearth is, where it needs to
  /// be, and which side of the ledger is at fault. Without the contributors a stalled furnace is an
  /// unexplained number - and the charge-mass loss term in particular inverts the naive intuition
  /// (topping a marginal furnace up makes it cooler), so it has to be visible or it reads as a bug.
  /// Everything comes off the balance the tick already computed; nothing is recalculated here.
  /// </summary>
  private void AppendHeatBalanceInfo(StringBuilder sb)
  {
    // The temperature/threshold/heat-in/heat-loss/blast ledger is the shared exlib formatter (the
    // converter reuses it with its own keys); the burden grade and melt rate below are the furnace's own.
    HeatBalanceHud.AppendLedger(
      sb,
      _lastHeatBalance,
      _internalTemp,
      _ironMeltingPoint,
      HeatLedgerKeys,
      FormatTemp
    );

    sb.AppendLine(
      Lang.Get(
        IwexLang.BfInfoBurdengrade,
        Lang.Get(Burden.ProfileLangKey(_chargeMix))
      )
    );

    if (State == FurnaceState.Melting)
      sb.AppendLine(
        Lang.Get(
          IwexLang.BfInfoMeltrate,
          (int)System.Math.Round(MeltSpeedFactor() * 100f)
        )
      );
  }

  // The furnace's lang keys for the shared ledger, and the measurement formatter it uses. ExMeasure
  // lives downstream of exlib, so the formatter is handed to the helper rather than referenced by it.
  private static readonly HeatBalanceLedgerKeys HeatLedgerKeys = new(
    Temp: IwexLang.BfInfoTemp,
    HeatOk: IwexLang.BfInfoHeatok,
    HeatStall: IwexLang.BfInfoHeatstall,
    HeatIn: IwexLang.BfInfoHeatin,
    HeatLoss: IwexLang.BfInfoHeatloss,
    BlastNone: IwexLang.BfInfoNodraught,
    BlastHot: IwexLang.BfInfoBlasthot,
    BlastCold: IwexLang.BfInfoBlastcold
  );

  private static readonly System.Func<float, string> FormatTemp = t =>
    ExMeasure.Temperature(t);

  /// <summary>
  /// Appends the not-lit status line. The generic reasons (exhaust full, needs mix) are handled
  /// here; the subclass supplies the lit-readiness line via <see cref="AppendReadyInfo"/>.
  /// </summary>
  private void AppendNotLitInfo(StringBuilder sb)
  {
    if (IsChoked)
      sb.AppendLine(Lang.Get("iwex:bf-info-exhaustfull"));
    else if (!_cachedIsFull)
      sb.AppendLine(Lang.Get("iwex:bf-info-needsmix"));
    else
      AppendReadyInfo(sb);

    // No "burned out - re-coke it" line: fuel is no longer stamped on the burden item. Salvaged charge
    // is still salvageable; what it needs is fuel bands charged over it, and the furnace-side display of
    // the layered charge is what shows that.
  }

  /// <summary>
  /// Names the stall: how many units in the shaft this furnace will not convert. Shown in every state,
  /// because the failure it explains (a full shaft that refuses to make metal) reads as a bug otherwise -
  /// the same house rule as the heat balance.
  /// <para>
  /// With one burden item, the sentence says the only thing still worth saying - the quantity - and the
  /// player finds the offender by looking at the shaft, which is where it is.
  /// </para>
  /// </summary>
  private void AppendWrongBurdenInfo(StringBuilder sb)
  {
    if (_cachedRejectedCount <= 0)
      return;
    sb.AppendLine(Lang.Get(IwexLang.BfInfoWrongburden, _cachedRejectedCount));
  }

  /// <summary>Appends the charge-ready status line (full but not yet lit). Furnace-specific.</summary>
  protected abstract void AppendReadyInfo(StringBuilder sb);

  #endregion
}
