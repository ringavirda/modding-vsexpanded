using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Heat;
using ExpandedLib.Helpers;
using ExpandedLib.Materials;
using ExpandedLib.Networks;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using IronIndustryExpanded.Patches;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>
/// Shared base for every furnace multiblock in the suite: the firing state machine, the heat balance and
/// internal temperature, the charge walk, exhaust venting, extinguish and residue, serialization and the HUD.
/// </summary>
/// <remarks>
/// Where the charge lives is a class tree - <see cref="BlockEntities.BlockEntityShaftFurnace"/> (a blown
/// burden column over the raceway) and <see cref="BlockEntities.BlockEntityFireboxFurnace"/> (a fuel bed
/// beside the work) - each sealing the four members that must agree: <see cref="ShaftHoldsLayeredCharge"/>,
/// <see cref="ReadChargeMix"/>, <see cref="ChargeCapacityUnits"/> and <see cref="IsChargeCode"/>. Melting
/// and blast stay parameters: a furnace with no pool overrides no molten member, and one with no tuyeres
/// sets <see cref="RequiresBlast"/> false. See <c>docs/design/conventions.md</c>.
/// </remarks>
public abstract class BlockEntityFurnaceCore : BlockEntityMultiblockMachine {
  // The furnace runs on game time: on reload it replays the game-time it spent unloaded as 1-second
  // sub-ticks (the timers all accumulate dt, so a replayed tick is an ordinary one), capped so a long
  // absence neither stalls the server nor leaps a timer. Up to ~10 minutes of smelting.
  protected override int MaxAwayCatchupSteps => 600;

  /// <summary>Whether the exhaust network is full, stalling production.</summary>
  public bool IsChoked { get; protected set; }

  /// <summary>
  /// Current operating state. Has no setter: on the shaft branch the value is recomputed from the charge
  /// every tick (<see cref="DerivesState"/>), so an assignment would be discarded. It is stored rather than
  /// computed because the firebox branch owns a real state machine and the value must survive a save - a
  /// breached furnace reloads still burning. The production tick is the only writer.
  /// </summary>
  public FurnaceState State => _state;

  private FurnaceState _state = FurnaceState.Idle;

  protected int _cachedMixCount = 0;
  protected bool _cachedIsFull = false;

  // Unrecognised charge: still burns, but blocks conversion to molten while present. Cached and
  // serialized because GetBlockInfo runs client-side, where the charge is never walked.
  protected int _cachedRejectedCount = 0;

  // World cells, refreshed by ScanForOutlets. Held as IReadOnlyList because they are handed straight back
  // by the layout's own per-role cache rather than copied into a list this class owns.
  protected IReadOnlyList<BlockPos> _gasOutlets = [];
  protected IReadOnlyList<BlockPos> _tuyeres = [];

  protected float _internalTemp = 20f;

  /// <summary>
  /// The temperature inside the furnace right now. Read-only: it is driven from the heat balance every
  /// tick, so anything that assigned it would be writing a value the next tick throws away.
  /// </summary>
  public float InternalTemperature => _internalTemp;

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

  // Whether the fire is starving for air (blast supply under the floor while lit). Serialized because
  // GetBlockInfo runs client-side, where the tuyere network is never read, so the HUD can name the stall.
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

  /// <summary>Volume (L/s) drawn from each tuyere at the reference coke fraction; the live draw is
  /// <see cref="TuyereDrawFor"/>.</summary>
  protected abstract float TuyereIntakeVolume { get; }

  /// <summary>Air-pressure threshold (atm) a tuyere must read to count as receiving blast, at the
  /// reference coke fraction; the live threshold is <see cref="RequiredBlastPressureFor"/>.</summary>
  protected abstract float BlastPressureThreshold { get; }

  #region Burden-derived blast demand

  // Both the pressure a furnace needs and the air it draws derive from the burden's coke fraction. Air is
  // the oxidant for coke, so a coke-rich burden needs proportionally more of it; coke is also the permeable
  // skeleton holding gas channels open, so a coke-lean burden packs denser and the blast has to be driven
  // harder. Nothing branches on which furnace this is.

  /// <summary>
  /// The blast pressure (atm) a burden of <paramref name="mix"/> demands: the reference requirement plus
  /// whatever its coke shortfall against <see cref="IiexValues.BfReferenceFuelFrac"/> adds, clamped. A
  /// charge with no burden stamp reads as the default grade, as the heat balance treats it.
  /// </summary>
  public float RequiredBlastPressureFor(BurdenMix mix) {
    float fuelFrac = mix.HasContent
      ? mix.FuelFrac
      : IiexValues.BfDefaultFuelFrac;
    float shortfall = IiexValues.BfReferenceFuelFrac - fuelFrac;
    return GameMath.Clamp(
      BlastPressureThreshold
        + shortfall * IiexValues.BfBlastPressureCokeSensitivity,
      IiexValues.BfBlastPressureMin,
      IiexValues.BfBlastPressureMax
    );
  }

  /// <summary>
  /// Air (L/s) each tuyere draws for a burden of <paramref name="mix"/>: the reference draw scaled by
  /// how the burden's coke fraction compares to the reference, clamped so a starved or packed charge
  /// still breathes something sane.
  /// </summary>
  public float TuyereDrawFor(BurdenMix mix) {
    float fuelFrac = mix.HasContent
      ? mix.FuelFrac
      : IiexValues.BfDefaultFuelFrac;
    float reference = Math.Max(0.0001f, IiexValues.BfReferenceFuelFrac);
    float factor = GameMath.Clamp(
      fuelFrac / reference,
      IiexValues.BfTuyereDrawMinFactor,
      IiexValues.BfTuyereDrawMaxFactor
    );
    return _tuyereIntakeVolume * factor;
  }

  #endregion

  /// <summary>
  /// Everything this furnace can hold, in whatever unit it counts its charge in, and what "full" is measured
  /// against. Also the denominator of the cold-charge heat penalty (<see cref="ComputeHeatBalance"/>): a
  /// furnace loaded to this number pays the whole of <c>BfChargeLossFull</c>, an empty one pays none, linear
  /// in between.
  /// </summary>
  /// <remarks>
  /// On the shaft branch it is only the heat denominator, ignition there being positional and pneumatic. The
  /// firebox branch also gates ignition on it, a fuel bed being lit when its cells are loaded. Both branches
  /// derive it from geometry rather than a config key, so a total sized for one machine cannot be inherited
  /// by a much smaller one and land out of physical reach.
  /// </remarks>
  protected abstract int ChargeCapacityUnits { get; }

  /// <summary>Internal temperature the furnace snaps to on ignition.</summary>
  protected virtual float IgnitionTemp => 900f;

  /// <summary>Mix floor below which a lit furnace counts a disruption toward extinguish.</summary>
  protected virtual int DisruptionMixFloor => 144;

  /// <summary>
  /// Heat (C) a full charge takes out of the fire - this machine's thermal sink at capacity, scaled down
  /// linearly by how loaded it actually is. The default is the blast furnace's, a 320-unit column of cold
  /// ore descending through the flame; a machine heating a bed or a few pots overrides it, or it pays a
  /// column's penalty for a handful of work.
  /// </summary>
  protected virtual float ChargeLossFull => IiexValues.BfChargeLossFull;

  /// <summary>
  /// Flue courses over the fire - the height the natural-draught curve is read at. Zero on a furnace with
  /// no stack of its own, which is every blown one: a packed shaft pulls almost nothing by itself and gets
  /// its air from the tuyeres.
  /// </summary>
  protected virtual int StackCourses => 0;

  /// <summary>
  /// Flue courses this furnace is rated at: what its own drawing fixes, unless the chimney continues past
  /// the drawing, in which case the machine names the height its process is designed around.
  /// </summary>
  /// <remarks>
  /// Not the same question as <see cref="StackCourses"/>, which is what is standing. This is what the
  /// machine is meant to have, so the block info can tell a player what to build towards and a
  /// reachability check can measure the machine rather than the moment.
  /// </remarks>
  public virtual int RatedStackCourses => StackCourses;

  /// <summary>
  /// The air factor this furnace pulls with everything the player controls in its favour: its rated stack,
  /// the damper open and no door venting. The ceiling, not the reading.
  /// </summary>
  public float BestNaturalDraught =>
    StackDraught.NaturalDraughtFor(
      RatedStackCourses,
      damperOpen: true,
      venting: false
    );

  /// <summary>Whether the chimney damper stands open. True where there is no damper to shut.</summary>
  protected virtual bool DamperOpen => true;

  /// <summary>Whether a door stands open, spilling the stack's pull into the room.</summary>
  protected virtual bool Venting => false;

  /// <summary>
  /// Heat (C) lost carrying the flame from the fire to the work. Zero wherever the two share a chamber,
  /// which is every shaft furnace; a reverberatory furnace pays it across the bridge, and that cost is
  /// what keeps one from melting iron without any constant having to say so.
  /// </summary>
  /// <remarks>
  /// A virtual rather than a branch constant on purpose. The crucible furnace is a firebox machine whose
  /// pots stand in the coke bed with no bridge at all, so it overrides this back to ~0; a constant on the
  /// branch would make that machine unbuildable. See <c>docs/design/machines/crucible-furnace.md</c>.
  /// </remarks>
  protected virtual float TransferLoss => 0f;

  /// <summary>
  /// Whether this furnace needs pressurised blast to stay lit. True for every blown furnace in the mod, so a
  /// tuyere gone dry - a stopped blower, a cut main - starves the fire and, sustained, extinguishes it. A
  /// naturally-aspirated furnace overrides this to false to opt out of air starvation.
  /// </summary>
  protected virtual bool RequiresBlast => true;

  /// <summary>Exhaust volume vented through each gas outlet per tick.</summary>
  protected virtual float ExhaustVolumePerTick => 24f;

  /// <summary>Factor applied to the internal temperature for vented exhaust temperature.</summary>
  protected virtual float ExhaustTempFactor => 0.8f;

  /// <summary>
  /// What the gas outlets vent at, in °C. A fixed fraction of the furnace's own temperature for anything with
  /// one hot space, a firebox hearth's flue being the hearth a little cooler. The shaft branch overrides it
  /// with the gas that actually left the top of its columns.
  /// </summary>
  protected virtual float ExhaustTemperature =>
    _internalTemp * ExhaustTempFactor;

  /// <summary>Extinguish threshold (seconds) with a single disruption.</summary>
  protected virtual int ExtinguishThresholdDefault => 30;

  /// <summary>Extinguish threshold (seconds) with two or more concurrent disruptions.</summary>
  protected virtual int ExtinguishThresholdSevere => 0;

  /// <summary>Cold-soak time (seconds) below the melt point before Melting reverts to Firing.</summary>
  protected virtual int BelowMeltingReset => 30;

  #endregion

  #region Combustion seams

  // Three seams the shaft branch answers differently from every other furnace. They live here rather than
  // on the branch because the tick is shared.

  /// <summary>
  /// The composition <see cref="ComputeHeatBalance"/> burns - everything the furnace holds by default, which
  /// is right for a firebox, where the whole bed is in the fire at once. On a shaft coke burns at the raceway
  /// and nowhere else, so the flame temperature follows the coke fraction of that round.
  /// </summary>
  /// <remarks>
  /// <c>shaftMix</c> is what <see cref="ReadChargeMix"/> summed over the whole charge. It remains the answer
  /// for permeability (<see cref="RequiredBlastPressureFor"/>) and for the grade readout, the blast having to
  /// get through the whole column rather than only the round that is burning.
  /// </remarks>
  protected virtual BurdenMix CombustionMix(
    object chargeHandle,
    BurdenMix shaftMix
  ) => shaftMix;

  /// <summary>
  /// Moves the furnace's own temperature toward <paramref name="targetTemp"/> - a first-order chase at
  /// <c>FireboxHeatRatePerSecond</c> / <c>FireboxCoolRatePerSecond</c>, where a firebox hearth's thermal
  /// inertia lives. The shaft branch assigns directly instead: its inertia is carried on the charge
  /// segments, and <c>_internalTemp</c> there is the raceway flame temperature, which has no lag.
  /// </summary>
  protected virtual void ApplyProcessTemperature(float targetTemp, float dt) {
    // Rates are per-second; scale by dt for tick-independence. Blast raises the target rather than the rate.
    if (_internalTemp < targetTemp)
      _internalTemp = Math.Min(
        _internalTemp + IiexValues.FireboxHeatRatePerSecond * dt,
        targetTemp
      );
    else if (_internalTemp > targetTemp)
      _internalTemp = Math.Max(
        _internalTemp - IiexValues.FireboxCoolRatePerSecond * dt,
        targetTemp
      );
    // No absolute clamp: TProcess is already floored at ambient, and a ceiling here would cap the advertised
    // hot-blast temperature.
  }

  /// <summary>
  /// Runs the products of combustion through whatever stands over the fire. Nothing by default: a firebox
  /// hearth's flame is drawn across the work by the chimney, so there is no column of charge to climb. The
  /// shaft branch sends the raceway gas up every column (<see cref="ChargeColumn.RiseGasThrough"/>), putting
  /// heat into burden before it reaches the fire.
  /// </summary>
  protected virtual void CirculateGas(object chargeHandle, float dt) { }

  /// <summary>
  /// Whether the furnace is at melting temperature - what the <c>Melting</c> label means and what the melt
  /// cycle runs on. The machine's own temperature by default, a hearth being one hot space. A shaft is not:
  /// its raceway reaches the flame temperature within a tick while the burden above is still climbing, so
  /// the branch overrides this to ask the charge instead.
  /// </summary>
  protected virtual bool AtMeltingTemperature(object chargeHandle) =>
    _internalTemp >= _ironMeltingPoint;

  /// <summary>
  /// Whether this branch recomputes <see cref="State"/> from the charge every tick instead of storing it and
  /// stepping it with timers. False by default: a firebox hearth keeps its state machine, a fuel bed having
  /// no raceway or descent to derive from. True on the shaft branch, where the fuel clock, melt-start soak,
  /// melt interval, cold-soak reversal and extinguish countdown all go unused. See
  /// <c>docs/design/layered-charge.md</c>.
  /// </summary>
  protected virtual bool DerivesState => false;

  /// <summary>
  /// This tick's state, computed from the charge. Only called when <see cref="DerivesState"/>. It must be
  /// a pure read: the tick compares it with last tick's label to decide what changed, so a derivation with
  /// a side effect would fire on every comparison.
  /// </summary>
  protected virtual FurnaceState DeriveState(object chargeHandle) => State;

  #endregion

  #region Structure geometry

  // ShaftCentre is the one hand-declared cell left on a furnace: a structure-local offset in the anchor's
  // north frame, the same frame the core block's multiblock layout is drawn in, so it must land inside the
  // layout's 'c' column; the offset-vs-layout test checks that. CellRole has no role for it, it being a
  // single geometric point rather than a set of cells with a purpose.
  //
  // Everything else in this region is the layout answering about itself through a CellRole. A drawing that
  // marks no cell with a role answers empty, or null for a [SingleCell] role and for the shaft box, which
  // is how the hearths state "no tuyeres" and "no taps" without any C#.

  /// <summary>
  /// Structure-local cell at the middle of the shaft - where the ignition whoosh, the fire ambience
  /// and the extinguish hiss play, and the centre of the charge walk.
  /// </summary>
  protected virtual Vec3i ShaftCentre => new(0, 3, 0);

  // Derived once and kept: ShaftBounds() is on the tick path (CollectChargePiles walks it every production
  // tick of every lit furnace) and EnsureShaftColumns indexes off it. Only a non-empty derivation is
  // memoised, so a furnace whose block carries no layout yet does not cache "no box" permanently and never
  // recover once the layout arrives.
  private (Vec3i Min, Vec3i Max)? _shaftBox;

  /// <summary>
  /// The bounding box, in structure-local coordinates, of the cells this furnace's drawing marks as fuel -
  /// <see cref="CellRole.Chargeable"/> on a shaft furnace, <see cref="CellRole.Firebox"/> on a reverberatory
  /// hearth - or null when it marks neither.
  /// </summary>
  /// <remarks>
  /// Not the charge volume: the cold blast furnace's box is 3x3x5 = 45 cells against 39 marked chargeable.
  /// Callers needing the cells read <see cref="ChargeableCells"/>; the box serves those that want a box - a
  /// <c>WalkBlocks</c> range, one column per <c>(x, z)</c>, a burn-out height. One derivation covers both
  /// roles, <c>MultiblockLayoutBuilder.ValidateRoles</c> refusing a layout that claims both. Null rather
  /// than an empty corner pair, which <see cref="ShaftBounds"/> would re-sort back into a real box.
  /// </remarks>
  protected (Vec3i min, Vec3i max)? ShaftBox {
    get {
      if (_shaftBox is { } cached)
        return (cached.Min, cached.Max);

      Vec3i? min = null;
      Vec3i? max = null;
      foreach (CellRole role in _fuelRoles)
        foreach (var (x, y, z) in LocalCellsWithRole(role)) {
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

  /// <summary>The two roles a fuel cell can carry. A layout may declare either, never both.</summary>
  private static readonly CellRole[] _fuelRoles =
  [
    CellRole.Chargeable,
    CellRole.Firebox,
  ];

  /// <summary>
  /// Every world cell of this furnace's footprint that its layout marks as a liquid pool, for the placed
  /// facing - the crucible floor the molten metal freezes across when the furnace is put out. A crucible cell
  /// carries <see cref="CellRole.Pool"/> and <see cref="CellRole.Chargeable"/> at once: burden rests on it
  /// while the furnace runs, metal freezes onto it when the furnace dies.
  /// </summary>
  public IReadOnlyList<BlockPos> PoolCells => CellsWithRole(CellRole.Pool);

  /// <summary>
  /// Every world cell of this furnace's footprint that its layout marks as a fuel bed, for the placed facing
  /// - the firebox cells, as opposed to the <see cref="ShaftBox"/> bounding box that contains them. A deposit
  /// into any one of them is spread across all of them (<c>BlockEntityFirebox.Charge</c>); grouping by role
  /// rather than adjacency keeps two hearths built back to back from merging their fuel. A shaft furnace
  /// answers empty, its drawing marking <see cref="CellRole.Chargeable"/> instead.
  /// </summary>
  public IReadOnlyList<BlockPos> FireboxCells =>
    CellsWithRole(CellRole.Firebox);

  /// <summary>
  /// Every world cell of this furnace's footprint that its layout marks as burden column, for the placed
  /// facing - the charge volume, as opposed to the <see cref="ShaftBox"/> bounding box that contains it. The
  /// cells a <see cref="BlockChargePile"/> may stand in; a firebox furnace answers empty.
  /// </summary>
  /// <remarks>
  /// Not the same set as the box: the cold blast furnace's box is 45 cells against 38 marked chargeable, so
  /// filling the box would put charge inside a wall. Asked as a role rather than by block code, which would
  /// hold only while every shaft legend keeps <c>chargepile</c> inside its alternation.
  /// <see cref="SyncChargeBlocks"/> walks exactly this set and <see cref="ChargeCellsOf"/> slices it per
  /// column. Cached by the base per role and invalidated when the structure reloads.
  /// </remarks>
  public IReadOnlyList<BlockPos> ChargeableCells =>
    CellsWithRole(CellRole.Chargeable);

  /// <summary>World cell of a structure-local offset for the placed rotation.</summary>
  protected BlockPos GlobalOf(Vec3i local) =>
    GetGlobalPos(local.X, local.Y, local.Z);

  /// <summary>
  /// Structure-local offset of a world cell for the placed rotation - the inverse of <see cref="GlobalOf"/>,
  /// for components that arrive knowing only where they physically stand. Turning the delta by
  /// <c>-_currentAngle</c> is what makes <c>LocalOf(GlobalOf(c)) == c</c> hold at all four facings; rotating
  /// forward instead is right at north and 180 deg out at south.
  /// </summary>
  /// <remarks>
  /// Loads the structure lazily, as <c>OwnsCell</c> does, so it answers on the client, where the monitor tick
  /// never runs and <c>_currentAngle</c> would otherwise still be -1.
  /// </remarks>
  public Vec3i LocalOf(BlockPos world) {
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
  /// The shaft box in world space for the placed rotation. Rotating the two structure-local corners can
  /// swap either horizontal axis, so the corners are re-sorted per component: a box walk needs a true
  /// min/max. Null when the drawing marks no fuel cell at all - see <see cref="ShaftBox"/>.
  /// </summary>
  protected (BlockPos min, BlockPos max)? ShaftBounds() {
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
  // docs/design/layered-charge.md. Only ownership, geometry and persistence live here. Both hoppers push
  // onto the columns, the melt takes from the bottom of them, burn-out rewrites them and SyncChargeBlocks
  // makes the world match them.

  /// <summary>
  /// Whether this furnace's charge descends as layers in a vertical shaft. A furnace with this off allocates
  /// no columns and writes no attribute for them, so its save tree matches one from before the columns
  /// existed. Virtual only so the two branch classes can answer it - both seal it.
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

  // Built once, then only indexed: descent reads this set every tick for every column of every lit furnace.
  // Null while the furnace has no columns at all - a hearth, or nothing has asked yet.
  //
  // Never invalidated, the shaft box being constant per block type: it is the bounding box of the cells that
  // type's drawing marks as fuel (ShaftBox), so rotation, OnExchanged and a structure rebuild leave the set
  // alone - the keys are structure-local, and only GlobalOf's answer moves, computed per call.
  //
  // EnsureShaftColumns is reached from ReadShaftColumns via FromTreeAttributes, which the engine calls
  // before Initialize. Block is already assigned by then (every load path calls CreateBehaviors immediately
  // before FromTreeAttributes); Api is what is missing that early, and reading a layout needs only Block.
  // ShaftBox memoises only a non-empty derivation, so a furnace that read its box before its layout existed
  // answers "no columns" for that one call and re-derives on the next.
  private Dictionary<(int X, int Z), ChargeColumn>? _shaftColumns;
  private ReadOnlyDictionary<
    (int X, int Z),
    ChargeColumn
  >? _readOnlyShaftColumns;

  /// <summary>
  /// Every column of the shaft, keyed by its structure-local <c>(x, z)</c>. Empty on a furnace that does not
  /// hold a layered charge. Local keys do not depend on where the furnace was built or which way it faces, so
  /// the save survives both; the one conversion the other way happens at the point of use. Genuinely
  /// read-only - the columns themselves are the mutable part.
  /// </summary>
  public IReadOnlyDictionary<(int X, int Z), ChargeColumn> ShaftColumns {
    get {
      EnsureShaftColumns();
      return _readOnlyShaftColumns ?? _noColumns;
    }
  }

  /// <summary>
  /// The column at structure-local <c>(<paramref name="localX"/>, <paramref name="localZ"/>)</c>, or
  /// null when that is not a column of this shaft - which is also what a hearth answers for every cell.
  /// The hopper's drip and descent both address a column this way.
  /// </summary>
  public ChargeColumn? ChargeColumnAt(int localX, int localZ) {
    EnsureShaftColumns();
    return
      _shaftColumns != null
      && _shaftColumns.TryGetValue((localX, localZ), out ChargeColumn? column)
      ? column
      : null;
  }

  /// <summary>
  /// The column standing under <paramref name="worldCell"/> and which charge block of it that cell is -
  /// <paramref name="blockIndex"/> 0 at the shaft floor, counting up. Null (and -1) when the cell is not in
  /// this furnace's shaft, which is also what a hearth answers everywhere.
  /// </summary>
  /// <remarks>
  /// The whole world-to-column conversion in one place, so a renderer never does the arithmetic itself. The
  /// <c>(x, z)</c> half must go through <see cref="LocalOf"/>, or the shaft transposes under rotation; the
  /// <c>y</c> half is measured from <see cref="ShaftBox"/>'s low <c>y</c>, never the anchor's. The corners
  /// are re-sorted on <c>y</c> so a furnace that drew its box the other way up does not index its blocks
  /// from the roof down.
  /// </remarks>
  public ChargeColumn? ChargeColumnAt(BlockPos worldCell, out int blockIndex) {
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

    // The index is this cell's position in its own column's cell list, never `y` minus anything. Measuring
    // off the shaft box's floor is wrong where the crucible well makes the box floor lower than most columns
    // start; measuring off the column's own floor is wrong where the drawing leaves a cell out mid-column.
    // Indexing the list makes placement, this walk and `ChargeColumn.BlocksTall` agree by construction.
    IReadOnlyList<Vec3i> cells = ChargeCellsOf(local.X, local.Z);
    for (int i = 0; i < cells.Count; i++)
      if (cells[i].Y == local.Y) {
        blockIndex = i;
        return column;
      }

    return null;
  }

  /// <summary>
  /// What one charge block of this furnace's column holds, in whatever unit this furnace measures its charge
  /// in. An ore charge (blast furnace) is counted in items: 16 bands x <c>ChargeItemsPerBand</c> = 32 items
  /// of coke and burden. A remelt charge (cupola) is counted in metal units up to
  /// <c>CupolaChargeMetalUnitsPerBlock</c>, so a 5 u bit, a 25 u chunk and a 375 u pig share one pile.
  /// </summary>
  /// <remarks>
  /// Per block rather than per band, the remelt quantum being 3 000 / 16 = 187.5 units a band, which no
  /// integer per-band constant can express. <see cref="ChargeColumn"/> derives band boundaries from this by
  /// multiplying before dividing, so both kinds stay exact.
  /// </remarks>
  public virtual int ChargeUnitsPerBlock =>
    IiexValues.ChargeItemsPerBand * ChargeColumn.BandsPerBlock;

  /// <summary>
  /// The <see cref="CellRole.Chargeable"/> cells of column
  /// <c>(<paramref name="localX"/>, <paramref name="localZ"/>)</c> in structure-local coordinates, ordered
  /// bottom-up - empty when that is not a column of this shaft.
  /// </summary>
  /// <remarks>
  /// The single source of which cell is the n-th block of a column. Charge fills this list from index 0 and
  /// <see cref="ChargeColumnAt(BlockPos, out int)"/> reads a cell's index out of it, so placement and the
  /// render window cannot drift apart over a well in the hearth or a hole in the drawing. Recomputing the
  /// index as <c>y - something</c> breaks that.
  /// </remarks>
  protected IReadOnlyList<Vec3i> ChargeCellsOf(int localX, int localZ) {
    var cells = new List<Vec3i>();
    foreach (BlockPos cell in ChargeableCells) {
      Vec3i local = LocalOf(cell);
      if (local.X == localX && local.Z == localZ)
        cells.Add(local);
    }
    cells.Sort((a, b) => a.Y.CompareTo(b.Y));
    return cells;
  }

  /// <summary>
  /// The structure-local <c>y</c> of the lowest <see cref="CellRole.Chargeable"/> cell in column
  /// <c>(<paramref name="localX"/>, <paramref name="localZ"/>)</c>, or null when the column has none. Charge
  /// rests on the lowest open cell of its own column, which is not the same height for every column of a
  /// furnace whose hearth has a well. The floor only, not what block indices are measured from - see
  /// <see cref="ChargeCellsOf"/>.
  /// </summary>
  protected int? ColumnFloorY(int localX, int localZ) {
    IReadOnlyList<Vec3i> cells = ChargeCellsOf(localX, localZ);
    return cells.Count == 0 ? null : cells[0].Y;
  }

  /// <summary>Everything the whole shaft holds, across every column - the left-hand number in the
  /// hopper's <c>27 of 36</c> readout.</summary>
  public int ShaftChargeUnits {
    get {
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
  /// chargeable cell count times <see cref="ChargeUnitsPerBlock"/>, or 0 when it is not a column of this
  /// shaft. Per column, since a hearth with a well gives its middle columns one cell more. Charge pushed past
  /// this is still counted, walked and saved, but stands above the roof where no block can draw it.
  /// </summary>
  public int ColumnCapacity(int localX, int localZ) =>
    ChargeCellsOf(localX, localZ).Count * Math.Max(1, ChargeUnitsPerBlock);

  /// <summary>
  /// The column a fresh load of <paramref name="material"/> should be laid on, and how much room is left in
  /// it, or null when the shaft will not take that material anywhere. The one place the charging rule lives.
  /// </summary>
  /// <remarks>
  /// The lowest column goes first, ties broken on ascending <c>(x, z)</c>, which fills a shaft in level
  /// courses - what the raceway needs before the furnace will light. Fuel may only go on burden, a round
  /// being fuel then burden; an empty column takes either. The top is tested by <see cref="IsFuelCode"/> and
  /// never by equality with the incoming material, or a hopper of charcoal lays a second fuel course onto
  /// the coke one and the furnace lights on a shaft with no ore between its fuel. A column at
  /// <see cref="ColumnCapacity"/> is skipped, so a full shaft answers null.
  /// </remarks>
  public ChargeColumn? NextChargeColumn(string? material, out int room) {
    room = 0;
    EnsureShaftColumns();
    if (_shaftColumns == null || string.IsNullOrEmpty(material))
      return null;

    bool isFuel = IsFuelCode(material);

    ChargeColumn? best = null;
    (int X, int Z) bestKey = default;
    int bestRoom = 0;

    foreach (var ((x, z), column) in _shaftColumns) {
      int free = ColumnCapacity(x, z) - column.TotalUnits;
      if (free <= 0)
        continue;
      // Fuel is refused onto any fuel, not merely onto the same one - see the remarks.
      if (isFuel && IsFuelCode(column.TopMaterial))
        continue;

      if (
        best == null
        || column.TotalUnits < best.TotalUnits
        || (
          column.TotalUnits == best.TotalUnits
          && (x < bestKey.X || (x == bestKey.X && z < bestKey.Z))
        )
      ) {
        best = column;
        bestKey = (x, z);
        bestRoom = free;
      }
    }

    room = bestRoom;
    return best;
  }

  /// <summary>
  /// The temperature freshly-charged material enters a column at: world ambient, rounded to the nearest
  /// 5 °C. The rounding is required: <see cref="ChargeColumn.Push"/> merges only within
  /// <see cref="ChargeColumn.TempMergeEpsilon"/> (1 °C), so charging at raw per-tick ambient would shatter
  /// one hand-laid course into a fresh segment on almost every drip. Ambient rather than
  /// <c>_internalTemp</c>, because new charge arrives cold onto a hot furnace and warms on the way down.
  /// </summary>
  public float ChargeTemperature => MathF.Round(_ambientTemp / 5f) * 5f;

  private void EnsureShaftColumns() {
    if (_shaftColumns != null || !ShaftHoldsLayeredCharge)
      return;
    // Returns without assigning, so nothing is memoised: a furnace whose layout has not arrived yet gets to
    // try again rather than owning no columns for the rest of its life. See the remark on _shaftColumns.
    if (ShaftBox is not { } box)
      return;

    // Keyed off the chargeable cells, not off the box's (x,z) grid. The box is a bounding box, so a shaft
    // that is not a solid rectangle in plan has (x,z) pairs inside it the drawing marks no chargeable cell
    // in, and keying off the grid would mint a live, pushable ChargeColumn for each. Charge pushed into such
    // a column is counted, walked and saved, but SyncChargeBlocks skips it (`ChargeCellsOf` is empty), so it
    // never becomes a block and it inflates the fire threshold and the heat balance's cold-charge term. It
    // also makes the key set exactly what `ChargeCellsOf` answers for, which lets SyncChargeBlocks iterate
    // `_shaftColumns` directly instead of re-deriving the columns from the cells.
    var seen = new Dictionary<(int X, int Z), ChargeColumn>();
    foreach (BlockPos cell in ChargeableCells) {
      Vec3i local = LocalOf(cell);
      seen.TryAdd((local.X, local.Z), new ChargeColumn());
    }
    // Empty means the layout marked nothing chargeable, which is not memoised for the same reason the
    // ShaftBox guard above is not.
    if (seen.Count == 0)
      return;

    Dictionary<(int X, int Z), ChargeColumn> columns = seen;

    _shaftColumns = columns;
    _readOnlyShaftColumns = new ReadOnlyDictionary<
      (int X, int Z),
      ChargeColumn
    >(columns);
  }

  /// <summary>
  /// The save key for one column, formatted under the invariant culture and never the ambient one. An
  /// interpolated <c>int</c> takes its sign from <c>NumberFormatInfo.NegativeSign</c>, which is not ASCII
  /// <c>-</c> under sv-SE, fi-FI or lt-LT (U+2212) nor under ar-SA or fa-IR (bidi marks), so a world written
  /// on one machine and opened on another would match only the sign-free <c>(0, 0)</c> column and silently
  /// empty the rest, which <see cref="ReadShaftColumns"/>' "no column key at all" guard cannot see.
  /// </summary>
  private static string ColumnKey(int x, int z) =>
    string.Create(CultureInfo.InvariantCulture, $"{ColumnTreePrefix}_{x}_{z}");

  private void WriteShaftColumns(ITreeAttribute tree) {
    // Not one attribute on a furnace that holds no layered charge - checked before the columns are even
    // built, so a hearth never allocates them either.
    if (!ShaftHoldsLayeredCharge)
      return;

    EnsureShaftColumns();
    // EnsureShaftColumns is allowed to build nothing - a shaft furnace whose drawing has not arrived has no
    // box - and a save must not throw out of ToTreeAttributes because of it. ReadShaftColumns' "no column
    // key at all" branch reads the result back unharmed.
    if (_shaftColumns == null)
      return;

    foreach (var ((x, z), column) in _shaftColumns) {
      var sub = new TreeAttribute();
      column.ToTree(sub);
      tree[ColumnKey(x, z)] = sub;
    }
  }

  /// <summary>
  /// Reads the columns back, looking each one up by its own key. A save whose shaft is a different shape
  /// loads anyway: the walk is over the columns the furnace has now, so a saved column outside the current
  /// box is dropped and a column the save does not describe comes back empty. A tree carrying no column key
  /// at all leaves the columns alone rather than clearing them - a save from before the columns existed, or
  /// a partial tree, not the world saying the shaft is empty.
  /// </summary>
  private void ReadShaftColumns(ITreeAttribute tree) {
    if (!ShaftHoldsLayeredCharge)
      return;

    EnsureShaftColumns();
    // Same reason as WriteShaftColumns: no box means no columns to read into, and a load must not throw. The
    // saved keys are left in the tree untouched.
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
  /// Makes the world match the columns: places and removes <c>iiex:furnace-chargepile</c> so each column
  /// stands exactly <see cref="ChargeColumn.BlocksTall"/> blocks high from its own floor. Idempotent.
  /// </summary>
  /// <remarks>
  /// Writes only the cells whose occupancy changed, so it is safe after every charge, descent and load. It
  /// walks <see cref="ChargeableCells"/> and never the larger <see cref="ShaftBox"/>, which would place
  /// charge blocks inside the walls; a cell holding anything but air or its own pile is skipped, so a column
  /// draws short while an obstruction stands. Every surviving pile in a touched column gets
  /// <see cref="BlockEntityChargePile.OnColumnChanged"/>, the only route that republishes the snapshot the
  /// tesselation thread reads. Public: the two hoppers drip into columns they do not own and must reconcile.
  /// </remarks>
  public void SyncChargeBlocks() {
    if (!ShaftHoldsLayeredCharge || Api?.World is not { } world)
      return;
    EnsureShaftColumns();
    if (_shaftColumns == null)
      return;

    // Resolved once per call. Null when the block is not registered, which happens in a headless test rather
    // than in a world, so it is a quiet no-op rather than a throw.
    Block? pile = world.GetBlock(BlockChargePile.PileCode);
    if (pile == null)
      return;

    foreach (var (key, column) in _shaftColumns) {
      // Bottom-up, from the shared list ChargeColumnAt also indexes, so the block a cell holds and the bands
      // it draws cannot disagree. A key with no chargeable cells yields an empty list and does nothing.
      IReadOnlyList<Vec3i> local = ChargeCellsOf(key.X, key.Z);
      if (local.Count == 0)
        continue;

      int want = ChargeColumn.BlocksTall(
        column.TotalUnits,
        ChargeUnitsPerBlock
      );

      bool touched = false;
      // The piles standing when this call is done - the set that gets the redraw. Collected during the walk
      // rather than re-read afterwards, so a cell just cleared cannot be told to redraw.
      var standingPiles = new List<BlockPos>();
      for (int i = 0; i < local.Count; i++) {
        BlockPos pos = GlobalOf(local[i]);
        Block? standing = world.BlockAccessor.GetBlock(pos);
        bool has = standing?.BlockId == pile.BlockId;

        if (i < want && !has) {
          // Never write over a block that is neither air nor this furnace's own pile - a coal pile a player
          // left in an open shaft cell, or, on siex's hot furnace, the `iiex:hearthmetal` its drawing still
          // lets stand in a chargeable cell, where without the guard the furnace and this walk would fight
          // over the same cell every tick. Skipping draws the column one block short while the cell is
          // taken; the units are still held by the column, so no charge is lost.
          if (standing != null && standing.BlockId != 0)
            continue;
          world.BlockAccessor.SetBlock(pile.BlockId, pos);
          standingPiles.Add(pos);
          touched = true;
        } else if (i >= want && has) {
          world.BlockAccessor.SetBlock(0, pos);
          touched = true;
        } else if (has)
          standingPiles.Add(pos);
      }

      // Redraw the whole column when anything about it moved, not only the cells written: a pile two blocks
      // down draws a different window once the column's contents change.
      if (!touched && !ColumnContentsMayHaveMoved)
        continue;
      foreach (BlockPos pos in standingPiles)
        if (
          world.BlockAccessor.GetBlockEntity(pos)
          is BlockEntityChargePile pileBe
        )
          pileBe.OnColumnChanged();
    }
  }

  /// <summary>
  /// Whether a <see cref="SyncChargeBlocks"/> call should redraw columns whose height did not change. True by
  /// default, a charge or a smelt moving contents without necessarily crossing a block boundary, so a column
  /// that redrew only on height change would hold a stale mesh for most of a campaign. A seam rather than a
  /// constant so a caller that knows nothing moved can skip the walk.
  /// </summary>
  protected virtual bool ColumnContentsMayHaveMoved => true;

  #endregion

  #region Charge family gate

  // A furnace burns one burden family (the blast furnace ore burden, the cupola remelt burden). Wrong-family
  // charge still lights and burns in the shaft, and burns out to salvageable spent burden on extinguish, but
  // the furnace will not render molten metal out of it and the HUD names the mismatch.
  //
  // There is no per-family accept list. What a furnace declares is what it charges (`IsChargeItem` /
  // `IsChargeCode`): the cupola says "scrap and fuel", the blast furnace "burden and fuel", and anything else
  // matches neither. Every gate reads the same shaft walk the tick does, so every route charge can enter by
  // is covered at the charge read rather than per entry point.

  /// <summary>
  /// Item-level charge identity, family-blind: anything the shaft counts as chargeable at all, meaning
  /// prepared burden of either family or the legacy count-only blast mix. A furnace whose charge is items
  /// rather than burden overrides this. Public because the hoppers delegate to it - one hopper block serves
  /// the blast furnace, the cupola, the heating furnace and the coke oven.
  /// </summary>
  public virtual bool IsChargeItem(ItemStack? stack) => Items.Burden.Is(stack);

  /// <summary>
  /// <see cref="IsChargeItem"/> asked of a material code rather than a stack - what a layered column can
  /// answer, a <c>ChargeSegment</c> holding units of a substance and never a stack. A furnace that overrides
  /// one and not the other gates its piles and its columns differently, so the same charge is accepted
  /// through one route and refused through the other.
  /// </summary>
  public virtual bool IsChargeCode(string? material) =>
    Items.Burden.IsCode(material);

  /// <summary>
  /// Whether <paramref name="material"/> is a carbon reductant - coke or charcoal, the <c>fuel</c> material
  /// role. Not the complement of burden: unstamped burden is neither, carrying no composition and burning as
  /// the standard grade, and a fuel test of <c>!IsAnyCode</c> would read every unit of it as pure carbon (a
  /// coke fraction of 1.00 against the reference 0.20) and pin the heat balance's fuel factor at its ceiling.
  /// </summary>
  public static bool IsFuelCode(string? material) =>
    !string.IsNullOrEmpty(material)
    && MaterialRoleRegistry.IsRole(Roles.Fuel, new AssetLocation(material));

  /// <summary>
  /// Whether a stack is a carbon reductant - <see cref="IsFuelCode"/> asked of an item. Named here rather
  /// than spelled out at the call sites so the predicate and its weight (<see cref="CarbonPerUnit"/>) stay
  /// together.
  /// </summary>
  public static bool IsFuelStack(ItemStack? stack) =>
    MaterialRoleRegistry.IsRole(Roles.Fuel, stack);

  /// <summary>
  /// How much carbon one charge unit of <paramref name="material"/> carries, in coke units - 1.0 for coke,
  /// 0.5 for charcoal, 0 for a non-fuel: the material's <c>fuel</c>-role value over
  /// <see cref="IiexValues.BfFuelCarbonReference"/>.
  /// </summary>
  /// <returns>0 for a non-fuel, which callers must treat as "not carbon" rather than dividing by. The
  /// registry's fallback for a valueless grant is 1.0, half of coke against a reference of 2.0.</returns>
  /// <remarks>
  /// Granting <c>Roles.Fuel</c> also grants a shaft-charge permit, since
  /// <see cref="BlockEntities.BlockEntityShaftFurnace.IsChargeItem"/> accepts anything holding it. Not to be
  /// granted to bituminous or anthracite: <c>docs/design/processes/coking.md</c> forbids raw coal in a shaft.
  /// </remarks>
  public static float CarbonPerUnit(string? material) {
    if (!IsFuelCode(material))
      return 0f;
    float reference = Math.Max(0.01f, IiexValues.BfFuelCarbonReference);
    return MaterialRoleRegistry.ValueOf(Roles.Fuel, new AssetLocation(material))
      / reference;
  }

  /// <summary>
  /// True while charge this furnace does not recognise is blocking the melt-to-molten conversion. Without
  /// it the HUD prints <c>Melting</c> over a shaft that can never produce a drop.
  /// </summary>
  protected bool ConversionBlocked => _cachedRejectedCount > 0;

  #endregion

  #region Abstract method implementations

  /// <summary>
  /// The core is a plainly-oriented block - the <c>side</c> variant vanilla's <c>HorizontalOrientable</c>
  /// behaviour stamps at placement - so the structure angle comes off that variant with no stored yaw and no
  /// offset: the layout grids are drawn in the core's own north frame.
  /// </summary>
  protected override void UpdateStructureRotation() {
    if (Block == null)
      return;

    SetStructureAngle(ExOrientation.AngleFromSide(Block.Variant["side"]));
  }

  protected override void OnStructureCompleted() => ScanForOutlets();

  /// <summary>
  /// Losing the walls does not put the fire out, so this is empty. Extinguishing here would apply the choke
  /// behaviour - sealed and starved - to its physical opposite: a furnace that loses its stack is opened to
  /// the air, draws harder and keeps burning its coke off. A furnace whose blast fails is the one that goes
  /// out, a packed shaft having almost no natural draught of its own.
  /// </summary>
  /// <remarks>
  /// Half of the breach rule. <see cref="CanRunProduction"/> and
  /// <see cref="StopsProductionOnStructureLost"/> carry the other half; without them a breached furnace is
  /// frozen rather than burning.
  /// </remarks>
  protected override void OnStructureLost() { }

  protected override string GetIncompleteMessage(int missingCount) =>
    Lang.Get("iiex:bf-error-incomplete", missingCount);

  protected override string GetCompleteMessage() =>
    Lang.Get("iiex:bf-error-complete");

  #endregion

  #region Initialization

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    CacheAttributes();
    if (api.Side == EnumAppSide.Server && StructureComplete)
      ScanForOutlets();
  }

  /// <summary>
  /// Re-reads the tunables into the cached fields. Called once at init and again at the top of each
  /// production tick, so a live <c>/exmod config</c> change applies immediately. Subclasses override to
  /// refresh their own tunables, calling base first.
  /// </summary>
  protected virtual void CacheAttributes() {
    _ironMeltingPoint = MeltingPoint;
    _maxFuelBurnTime = MaxFuelBurnTime;
    _meltStartDelay = MeltStartDelay;
    _meltIntervalSec = MeltIntervalSec;
    _tuyereIntakeVolume = TuyereIntakeVolume;
    _starvationSupplyFrac = IiexValues.BfStarvationSupplyFrac;
    _ambientTemp = ReadAmbientTemperature();
  }

  /// <summary>
  /// Outside air temperature at the furnace, in °C, sampled once per tick alongside the tunables: the climate
  /// lookup is too heavy to run per HUD read, and the HUD runs per looking player. <c>NowValues</c> is not
  /// documented as never-null, so an unavailable climate falls back to the configured default.
  /// </summary>
  protected float ReadAmbientTemperature() {
    ClimateCondition? climate = Api?.World?.BlockAccessor?.GetClimateAt(
      Pos,
      EnumGetClimateMode.NowValues
    );
    return climate?.Temperature ?? IiexValues.BfAmbientFallbackTemp;
  }

  #endregion

  #region Tick

  /// <summary>
  /// A burning furnace ticks even with its walls gone. The inherited gate is <c>StructureComplete</c>, so a
  /// breach would stop the production tick being called at all, and a furnace that does not tick is frozen
  /// rather than burning: it holds its temperature, charge and pool indefinitely. An idle incomplete furnace
  /// still costs nothing - a fire that has gone out cannot be restarted through the hole.
  /// </summary>
  protected override bool CanRunProduction =>
    StructureComplete || State != FurnaceState.Idle;

  /// <summary>The furnace keeps its production listener when the walls come out - see
  /// <see cref="CanRunProduction"/>. Overriding the gate alone leaves a frozen furnace, because the gate
  /// is only read by a listener that still exists.</summary>
  protected override bool StopsProductionOnStructureLost => false;

  protected override void OnProductionTick(float dt) {
    // Return only if idle, not if incomplete - the same rule CanRunProduction states, repeated here
    // because this method is also reached through the away-catch-up replay.
    if (!StructureComplete && State == FurnaceState.Idle)
      return;

    // Re-read the tunables each tick so a live `/exmod config iiex ...` change takes effect immediately
    // instead of staying pinned to what was cached when this furnace last loaded.
    CacheAttributes();

    bool dirty = false;

    bool failedAny = false;
    if (State != FurnaceState.Idle) {
      foreach (var pos in _gasOutlets) {
        if (Api.World.BlockAccessor.GetBlockEntity(pos) is IPipeNode outlet) {
          if (
            !outlet.TryProduce(
              ExhaustVolumePerTick,
              ExhaustTemperature,
              "Exhaust"
            )
          )
            failedAny = true;
        } else {
          ScanForOutlets();
        }
      }
    }

    if (IsChoked != failedAny) {
      IsChoked = failedAny;
      dirty = true;
    }

    // One scan of the charge per tick; the charge-based reads and writes below reuse this handle instead
    // of re-walking the hearth region.
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

    // Air is a consumed reagent: a lit furnace draws its blast out of the tuyere network and decrements it.
    // TuyereIntakeVolume is a per-second rate, so the draw scales with dt, as does the demand it is measured
    // against. Gated on State != Idle, so a cold furnace does not bleed a shared blast main dry. Both the
    // draw and the pressure the line must hold come from the charge (see "Burden-derived blast demand"),
    // recomputed each tick so re-charging with a different grade changes what the main has to deliver.
    float requiredPressure = RequiredBlastPressureFor(mix);
    float perTuyereDraw = TuyereDrawFor(mix) * dt;
    if (State != FurnaceState.Idle) {
      foreach (var pos in _tuyeres) {
        if (Api.World.BlockAccessor.GetBlockEntity(pos) is IPipeNode tuyere) {
          if (tuyere is BlockEntityPipe pipe) {
            if (pipe.Medium == "Exhaust")
              tuyeresReceiveExhaust = true;

            // The pressure test comes before the draw. Testing after it drains a main that cannot meet the
            // charge's required pressure every tick while the furnace credits itself zero supply, starving
            // every other machine on that main. A line that cannot deliver is not drawn on.
            if (pipe.Medium == "Air" && pipe.Pressure >= requiredPressure) {
              // How much air actually arrived, not merely whether a line is attached: an under-supplied
              // tuyere slides the furnace toward natural draught (cooler T_in), and a near-dry one starves
              // it out entirely (the disruption below).
              blastSupplied += tuyere.TryConsume(perTuyereDraw);
              blastTemp = Math.Max(blastTemp, pipe.Temperature);
            }
          } else {
            // A non-pipe intake node carries no pressure to test, so it is drawn on unconditionally.
            tuyere.TryConsume(perTuyereDraw);
          }
        } else {
          ScanForOutlets();
        }
      }
    }

    float blastDemand = _tuyeres.Count * perTuyereDraw;
    float blastSupplyFrac = blastDemand > 0f ? blastSupplied / blastDemand : 0f;

    // A breach is open to the air, so the furnace runs on natural draught however good the blast main still
    // is: the pressure goes out through the hole rather than through the burden. A breached furnace burns
    // cooler (~970 °C, the natural-draught row) while still burning.
    if (!StructureComplete)
      blastSupplyFrac = 0f;

    bool isLiquidCapacityReached = LiquidCapacityReached;

    // A lit furnace that needs blast is starving when the air arriving falls under the floor - a stopped
    // blower, a cut or bled-out main. The shortfall has already cooled T_in toward natural draught via the
    // air factor, and it now also counts toward extinguish below. Gated on State != Idle, an idle furnace
    // drawing no air by design.
    bool airStarved =
      State != FurnaceState.Idle
      && RequiresBlast
      && blastSupplyFrac < _starvationSupplyFrac;
    if (_airStarved != airStarved) {
      _airStarved = airStarved;
      dirty = true;
    }

    // A branch that derives its state has no ignition event, no extinguish countdown and no timers.
    // Everything from here to the `if (State != Idle)` block below is the stored-state machine, which only
    // the firebox branch runs.
    if (DerivesState) {
      FurnaceState derived = DeriveState(chargeHandle);
      if (derived != State) {
        if (derived == FurnaceState.Idle)
          Shutdown(); // residue and sounds only - it can no longer set a state
        else if (State == FurnaceState.Idle)
          ExSounds.Play(Api, ShaftCentrePos, ExSounds.Ignite, 1f, 32f);
        _state = derived;
        dirty = true;
      }
    } else if (
        State == FurnaceState.Idle
        && StructureComplete
        && _cachedIsFull
        && !IsChoked
      ) {
      if (TryIgniteCharge(chargeHandle)) {
        _state = FurnaceState.Firing;
        _fuelBurnSeconds = 0;
        _internalTemp = IgnitionTemp;
        dirty = true;
        // Whoosh as the charge catches.
        ExSounds.Play(Api, ShaftCentrePos, ExSounds.Ignite, 1f, 32f);
      }
    }

    if (State != FurnaceState.Idle && !DerivesState) {
      int disruptionCount = 0;
      if (mixCount < DisruptionMixFloor)
        disruptionCount++;
      if (tuyeresReceiveExhaust)
        disruptionCount++;
      if (IsChoked)
        disruptionCount++;
      if (isLiquidCapacityReached)
        disruptionCount++;
      // Air starvation is one more disruption rather than a parallel mechanism: sub-floor blast held for the
      // extinguish grace (~30 s alone, instant when compounded with another) snuffs the fire through the
      // same _extinguishSeconds counter and reset as every other stall.
      if (airStarved)
        disruptionCount++;

      if (disruptionCount > 0) {
        _extinguishSeconds += dt;
        dirty = true;

        int extinguishThreshold =
          disruptionCount >= 2
            ? ExtinguishThresholdSevere
            : ExtinguishThresholdDefault;

        if (_extinguishSeconds >= extinguishThreshold) {
          Extinguish();
          return;
        }
      } else {
        if (_extinguishSeconds != 0)
          dirty = true;
        _extinguishSeconds = 0;
      }
    }

    if (State == FurnaceState.Firing || State == FurnaceState.Melting) {
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
      // CombustionMix rather than _chargeMix - on a shaft they differ, what burns being the round at the
      // raceway while _chargeMix is the whole column.
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

      // After the temperature, because the gas leaves the raceway at it. This is the pass that puts heat
      // into burden before it reaches the fire.
      CirculateGas(chargeHandle, dt);

      if (State == FurnaceState.Firing && !DerivesState) {
        _fuelBurnSeconds += dt;
        if (_fuelBurnSeconds >= _maxFuelBurnTime) {
          Extinguish();
          return;
        }

        if (AtMeltingTemperature(chargeHandle)) {
          _secondsAboveMelting += dt;
          dirty = true;
          // Rejected charge blocks the conversion: the furnace holds at heat, burning its fuel out, but
          // never crosses into Melting while a rejected pile is in the shaft. The soak timer keeps accruing,
          // so it converts the instant that pile is dug out.
          if (_secondsAboveMelting >= _meltStartDelay && !ConversionBlocked) {
            TransitionToMelting();
            return;
          }
        } else {
          if (_secondsAboveMelting != 0)
            dirty = true;
          _secondsAboveMelting = 0;
        }
      } else if (State == FurnaceState.Melting) {
        // The cold-soak reversal is stored-state machinery: a derived branch reads Firing again on the next
        // tick, with nothing to reset.
        if (!DerivesState && !AtMeltingTemperature(chargeHandle)) {
          _belowMeltingSeconds += dt;
          dirty = true;
          if (_belowMeltingSeconds >= BelowMeltingReset) {
            _state = FurnaceState.Firing;
            _secondsAboveMelting = 0;
            _belowMeltingSeconds = 0;
            _fuelBurnSeconds = 0;
            dirty = true;
          }
        } else {
          if (_belowMeltingSeconds != 0)
            dirty = true;
          _belowMeltingSeconds = 0;

          // ConversionBlocked also guards the melt cycle: a rejected pile dropped into an already-melting
          // furnace stops new metal being rendered, while existing molten drains.
          if (!isLiquidCapacityReached && !ConversionBlocked) {
            if (MeltsPerTick) {
              // No cycle length: the raceway renders whatever descended past it this second, band by band,
              // so the cadence is the descent rather than a timer over it.
              SmeltCycle(chargeHandle, dt);
              dirty = true;
            } else {
              _meltSeconds += dt;
              // The harder the furnace is blown past the melt line, the faster the burden renders, so a hot
              // blast out-produces a cold one without either carrying a per-cycle yield constant.
              if (_meltSeconds >= _meltIntervalSec / MeltSpeedFactor()) {
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
  /// The furnace's dynamic heat balance: <c>T_process = T_in - T_loss</c>, floored at ambient, with no
  /// maximum temperature. <c>T_in</c> is coke combustion - how rich the burden is in fuel, scaled by how
  /// much blast reached the tuyeres - plus whatever preheat a cowper put into the air. <c>T_loss</c> is
  /// stack radiation plus the cold mass of the charge plus a cold day.
  /// </summary>
  /// <remarks>
  /// The single mechanism behind the cold/hot split; nothing here is overridden per furnace. A high-coke
  /// burden clears iron's melt line on cold blast, a low-coke burden only once a cowper is preheating, and an
  /// under-pressure line slides either back toward natural draught. See
  /// <c>docs/design/mechanics/heat-balance.md</c>.
  /// </remarks>
  protected HeatBalance ComputeHeatBalance(
    BurdenMix charge,
    float blastSupplyFrac,
    float blastTemp,
    int mixCount
  ) =>
    ComputeHeatBalanceAt(
      charge,
      blastSupplyFrac,
      blastTemp,
      mixCount,
      StackDraught.NaturalDraughtFor(StackCourses, DamperOpen, Venting)
    );

  /// <summary>
  /// The same balance at a stated natural draught rather than the one the chimney and damper are giving
  /// now - what this furnace would reach if the stack were built and the damper thrown.
  /// </summary>
  /// <remarks>
  /// Separated so reachability can be asked as a question about the machine: a furnace whose chimney the
  /// player builds is below its process temperature for most of its life, and that is the design rather
  /// than a defect.
  /// </remarks>
  protected HeatBalance ComputeHeatBalanceAt(
    BurdenMix charge,
    float blastSupplyFrac,
    float blastTemp,
    int mixCount,
    float natural
  ) {
    float fuelFrac = charge.HasContent
      ? charge.FuelFrac
      : IiexValues.BfDefaultFuelFrac;

    float reference = Math.Max(0.0001f, IiexValues.BfReferenceFuelFrac);
    float fuelFactor = GameMath.Clamp(
      1f + IiexValues.BfCokeSensitivity * (fuelFrac - reference) / reference,
      IiexValues.BfMinFuelFactor,
      IiexValues.BfMaxFuelFactor
    );

    float airFactor =
      natural + (1f - natural) * GameMath.Clamp(blastSupplyFrac, 0f, 1f);

    float preheatGain =
      IiexValues.BfPreheatCoefficient * Math.Max(0f, blastTemp - _ambientTemp);

    float tIn =
      IiexValues.BfCombustionBaseTemp
      + IiexValues.BfCombustionCokeGain * fuelFactor * airFactor
      + preheatGain;

    int requiredMix = Math.Max(1, ChargeCapacityUnits);
    float chargeLoss =
      ChargeLossFull * GameMath.Clamp((float)mixCount / requiredMix, 0f, 1f);
    float ambientLoss =
      IiexValues.BfAmbientLossPerDegree
      * Math.Max(0f, IiexValues.BfAmbientReferenceTemp - _ambientTemp);
    float transferLoss = TransferLoss;

    float tLoss =
      IiexValues.BfRadiationLossBase + chargeLoss + ambientLoss + transferLoss;

    // The floor at ambient and the record shape belong to the exlib helper, shared with the converter;
    // the furnace supplies its coke-combustion T_in/T_loss and the contributors.
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
      ambientLoss,
      transferLoss
    );
  }

  /// <summary>
  /// Melt-cycle speed as a multiple of the nominal rate, from how far above the melt line the hearth is
  /// running - the design docs' "cold ~30 u/s, hot ~45 u/s" falling out of the heat balance rather than a
  /// second yield constant per furnace. Setting <c>BfMeltMarginGain</c> to 0 restores a flat rate.
  /// </summary>
  protected float MeltSpeedFactor() =>
    Math.Max(
      0.01f, // a retuned floor of 0 would divide the melt interval into infinity
      GameMath.Clamp(
        1f
          + IiexValues.BfMeltMarginGain
            * (_internalTemp - _ironMeltingPoint)
            / Math.Max(1f, IiexValues.BfMeltMarginReference),
        IiexValues.BfMeltSpeedMin,
        IiexValues.BfMeltSpeedMax
      )
    );

  #endregion

  #region State transitions

  private void TransitionToMelting() {
    _state = FurnaceState.Melting;
    _meltSeconds = 0;
    _fuelBurnSeconds = 0;
    MarkDirty(true);
  }

  /// <summary>
  /// Puts the fire out: the sound, the residue, and the counters back to zero. A derived branch calls
  /// <see cref="Shutdown"/> instead, which is this without the state assignment. Both share
  /// <see cref="ExtinguishResidue"/>, so the residue order is one thing: freeze the pool, burn out the
  /// charge, clear the pools.
  /// </summary>
  private void Extinguish() {
    _state = FurnaceState.Idle;
    Shutdown();
  }

  /// <summary>
  /// Everything going out does, minus deciding that it went out: the sound, the reset to ambient, the
  /// residue and the counters. Safe to call when the state has already been recomputed to
  /// <see cref="FurnaceState.Idle"/> by <see cref="DeriveState"/>.
  /// </summary>
  private void Shutdown() {
    ExSounds.Play(Api, ShaftCentrePos, ExSounds.Extinguish, 1f, 32f);

    _internalTemp = 20f;
    // A dead furnace is off, not starving: clear the flag so the serialized state (and the HUD) does not
    // report a stall on a cold hearth.
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
  /// Re-reads the gas-outlet and tuyere world cells for the current rotation off the layout's
  /// <see cref="CellRole.GasOutlet"/> and <see cref="CellRole.Tuyere"/> marks. Asked as roles, an absence
  /// needs no declaration: a drawing that carries no outlet or tuyere glyph answers empty.
  /// </summary>
  protected virtual void ScanForOutlets() {
    _gasOutlets = CellsWithRole(CellRole.GasOutlet);
    _tuyeres = CellsWithRole(CellRole.Tuyere);
  }

  /// <summary>
  /// Walks the charge once and returns an opaque handle the other charge hooks consume, so the tick does a
  /// single hearth walk. The handle is passed back to <see cref="ReadChargeMix"/>,
  /// <see cref="TryIgniteCharge"/> and <see cref="SmeltCycle"/>. Abstract rather than defaulted: a fallback
  /// walk would answer "no charge" for a branch that forgot to override, giving a furnace that never lights.
  /// </summary>
  protected abstract object CollectCharge();

  /// <summary>
  /// Reads the charge in one walk of the shaft. Returns the total charge count, family-blind, and hands back
  /// whether that total is full enough to fire, the summed composition of the whole column so the heat
  /// balance and the HUD read one coke fraction, and how much of the charge this furnace refuses - the count
  /// that drives the conversion block and the mismatch HUD line.
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
  /// Whether melting is evaluated every tick rather than on a fixed cycle. False by default. True on the
  /// shaft branch, where burden melts iff the temperature it carried down the shaft clears the line,
  /// evaluated at unit granularity over a raceway slice that usually spans a coke/burden boundary.
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

  // A dead furnace leaves the same residue whatever it is: the pool freezes onto the hearth floor and the
  // burden is left as spent, salvageable charge. A furnace supplies only which solid block its pool freezes
  // into and how much metal was in it. Nothing on this path makes slag.

  /// <summary>Block placed where the molten pool freezes - solidified iron, or cast iron for a cupola.
  /// Null on a furnace that melts nothing, which never reaches the freeze.</summary>
  protected virtual AssetLocation? SolidProductBlock => null;

  /// <summary>
  /// Residue behaviour shared by every furnace: the remaining burden is burned out by height rather than
  /// destroyed, so the player can dig it out, re-coke it in the mixer and charge it again.
  /// <para>
  /// The pool is no longer frozen here. It is already standing in the world as hearth blocks, whose own
  /// cells latch solid on their thermal update - so a furnace that dies leaves its metal exactly where it
  /// was, with nothing to stamp and nothing to zero.
  /// </para>
  /// </summary>
  protected virtual void ExtinguishResidue() {
    BurnOutCharge();
  }

  /// <summary>
  /// Lights this furnace from a flame held into the tap at <paramref name="tapPos"/>. False on every
  /// furnace but a shaft: the firebox machines have no tap-hole to reach through and catch by themselves
  /// once they are loaded, which is the ruled split - see docs/design/processes/ironmaking.md.
  /// </summary>
  /// <returns>Whether the flame took. A shaft returns true once it is blown in, whether or not the charge
  /// catches this tick: whether it does is the drawing's and the burden's business, not the torch's.</returns>
  public virtual bool TryLightFromTap(BlockPos tapPos) => false;

  /// <summary>
  /// The pool cells this furnace may write metal into, with <see cref="SolidProductBlock"/> placed in
  /// each one that was free. Free means empty, or a charge pile that was being consumed here: a pile
  /// holding rejected charge is not free, because writing over it would destroy salvage the player is
  /// owed. A cell already holding the product block is kept as it stands, so a campaign fills one block
  /// rather than replacing it every cycle.
  /// </summary>
  protected List<BlockPos> ClaimPoolCells() {
    var claimed = new List<BlockPos>();
    if (SolidProductBlock is not { } productCode)
      return claimed;

    Block? product = Api.World.GetBlock(productCode);
    if (product == null)
      return claimed;

    string pileCode = BlockChargePile.PileCode.Path;
    foreach (BlockPos pos in PoolCells) {
      Block occupant = Api.World.BlockAccessor.GetBlock(pos);
      if (occupant.Id == product.BlockId) {
        claimed.Add(pos);
        continue;
      }

      bool free =
        occupant.Id == 0
        || (occupant.Code?.Path == pileCode && !PileHoldsRejectedCharge(pos));
      if (!free)
        continue;

      Api.World.BlockAccessor.SetBlock(product.BlockId, pos);
      claimed.Add(pos);
    }
    return claimed;
  }

  private bool PileHoldsRejectedCharge(BlockPos pos) {
    if (ChargeColumnAt(pos, out int blockIndex) is not { } column)
      return false;

    int perBlock = Math.Max(1, ChargeUnitsPerBlock);
    int low = blockIndex * perBlock;
    int at = 0;
    foreach (ChargeSegment segment in column.Segments) {
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
  /// Rewrites the remaining charge in place as spent charge instead of destroying it: the carbon burns out,
  /// the ore/metal and flux survive, and the player digs the wreck out, re-cokes it in the mixer and charges
  /// it again. See <c>docs/design/mechanics/recoverability.md</c>.
  /// </summary>
  /// <remarks>
  /// Abstract, because the two branches store charge differently. A shaft's column is metres tall and the
  /// blast only reached the bottom, so what survives is interpolated by height
  /// (<see cref="BlockEntities.BlockEntityShaftFurnace"/>); a firebox is one course of cells all equally in
  /// the fire, so only the bottom fraction applies (<see cref="BlockEntities.BlockEntityFireboxFurnace"/>).
  /// </remarks>
  protected abstract void BurnOutCharge();

  #endregion

  #region Block lifecycle

  /// <summary>
  /// Puts a running furnace out before the block goes, so the pool freezes and the charge burns out to
  /// residue rather than vanishing with the block entity.
  /// removal-only teardown: a chunk unload leaves the furnace placed and still lit, and the away-catch-up
  /// in <see cref="ExpandedLib.Blocks.Machines.BEBehaviorProductionMachine"/> replays the time it spent
  /// unloaded. Extinguishing here would put out every furnace whose player walked away.
  /// </summary>
  public override void OnBlockRemoved() {
    if (Api?.Side == EnumAppSide.Server && State != FurnaceState.Idle)
      Extinguish();
    base.OnBlockRemoved();
  }

  #endregion

  #region Serialization

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldAccessForResolve
  ) {
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

  // The whole balance rides the tree, not only its result: GetBlockInfo runs on the client, which never
  // walks the charge or reads the pipes, so anything the HUD prints has to arrive here or it prints zeroes.

  private void ReadHeatBalance(ITreeAttribute tree) {
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

  private void WriteHeatBalance(ITreeAttribute tree) {
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

  public override void ToTreeAttributes(ITreeAttribute tree) {
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

  // The furnace HUD is spread across its component blocks: each scans up to this core, its anchor, and reads
  // the synced state below, showing only its own slice - the taps the pool they drain, the tall hopper the
  // burden in the shaft - while the core keeps the heat ledger. The formatting and lang keys live here with
  // the data, so a component stays a thin caller.

  /// <summary>How far a functional component scans to find its core: <see cref="ComponentScanBelow"/>
  /// cells down (the cold furnace's tall hopper sits six cells above the hearth, the taps one to two),
  /// <see cref="ComponentScanAbove"/> up, and <see cref="ComponentScanHorizontal"/> out per horizontal
  /// axis (the taps reach x=+/-2). Generous over the current layouts; <c>OwnsCell</c> rejects any
  /// non-owning core the wider box catches.</summary>
  public const int ComponentScanHorizontal = 3;
  public const int ComponentScanBelow = 8;
  public const int ComponentScanAbove = 1;

  /// <summary>
  /// World cell of the lower (metal) tap for the placed rotation, or null on a furnace whose drawing has no
  /// metal tap. A tap compares its own position against this to know it is the metal tap. Nullable because
  /// <see cref="CellRole.MetalTap"/> is <c>[SingleCell]</c>: a layout that declares it declares exactly one
  /// cell, but may decline to declare it at all, as the two hearths do.
  /// </summary>
  public BlockPos? MetalTapPos => SingleCellWithRole(CellRole.MetalTap);

  /// <summary>World cell of the higher (slag) tap for the placed rotation, or <c>null</c> on a furnace
  /// whose drawing has no slag tap. See <see cref="MetalTapPos"/>.</summary>
  public BlockPos? SlagTapPos => SingleCellWithRole(CellRole.SlagTap);

  /// <summary>
  /// The one world cell a <see cref="SingleCellAttribute">single-cell</see> role names, or null when the
  /// layout does not mark it. The build-time arity guard is what makes reading a point out of a cell set
  /// legal here; null means a layout declared nothing, not that it declared too much.
  /// </summary>
  private BlockPos? SingleCellWithRole(CellRole role) {
    IReadOnlyList<BlockPos> cells = CellsWithRole(role);
    return cells.Count == 1 ? cells[0] : null;
  }

  /// <summary>
  /// The tall hopper's slice: the burden loaded in the shaft against the fire threshold, plus the mismatch
  /// warning, shown at the hopper the player charges. Silent until the structure is complete, an incomplete
  /// furnace having no meaningful shaft count.
  /// </summary>
  public void AppendShaftChargeInfo(StringBuilder sb) {
    if (!StructureComplete)
      return;

    // No separate "mix loaded" line: ignition is positional rather than quantity-gated, so
    // `bf-info-shaftfull` below is the only remaining question.

    // A shaft can read full and still refuse to make metal; name the mismatch beside the count.
    AppendWrongBurdenInfo(sb);

    // The three lines below are the charging readout: a round is fuel then burden, laid in level courses.
    // All three are derived, with no field added or persisted for them. "fuel" rather than "coke", because
    // the shaft takes either and a charcoal band is half the carbon of a coke one.
    if (!ShaftHoldsLayeredCharge)
      return;
    AppendCourseInfo(sb);
    AppendChargeVerdict(sb);
    sb.AppendLine(
      Lang.Get(
        "iiex:bf-info-shaftfull",
        ShaftChargeUnits,
        ChargeableCells.Count * Math.Max(1, ChargeUnitsPerBlock)
      )
    );
  }

  /// <summary>
  /// Branch hook for the chill line - what a hung stockline says about itself. Empty on the core, a hang
  /// being a property of a descending column that only the shaft branch has. A hang has no other symptom: the
  /// furnace stays lit, the temperature stays high, the fuel keeps going down and the iron stops.
  /// </summary>
  protected virtual void AppendChillInfo(StringBuilder sb) { }

  /// <summary>
  /// The course being laid, as bands: how much of the top block is fuel and how much is burden, on the column
  /// the next load will land in. <c>Bands</c> is the block's own resolution, so the pair reads as "N of 16".
  /// <see cref="FuelMaterial"/> carries the fuel's own code rather than a counter per fuel.
  /// </summary>
  /// <remarks>
  /// Callers must read <see cref="FuelBands"/> and <see cref="FuelMaterial"/> as a pair.
  /// <c>FuelBands == 0</c> means no fuel in this course; <c>FuelBands &gt; 0</c> with a null material is a
  /// mixed course - two or more fuels in the same top block. A charcoal band carries half the carbon of a
  /// coke one (<see cref="CarbonPerUnit"/>), so naming them apart stops the readout overstating a round.
  /// </remarks>
  public readonly record struct ChargeCourse(
    int FuelBands,
    int BurdenBands,
    int Bands,
    string? FuelMaterial
  );

  /// <summary>
  /// The top course of the column the next load lands on - the lowest column, ties on ascending
  /// <c>(x, z)</c>, the same rule <see cref="NextChargeColumn"/> follows. Public because the headless lang
  /// service echoes a key and drops its arguments, so the numbers can only be pinned here.
  /// </summary>
  public ChargeCourse TopCourse {
    get {
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
        ) {
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
        if (IsFuelCode(run.Material)) {
          fuel += run.Bands;
          // The first fuel run names the course; a later run of a different fuel makes it unnameable. The
          // walk cannot stop at the first pair, because runs coalesce on material and a course can hold
          // coke, then charcoal, then coke again.
          var code = new AssetLocation(run.Material);
          if (fuelCode == null)
            (fuelCode, fuelMaterial) = (code, run.Material);
          else if (!fuelCode.Equals(code))
            mixedFuel = true;
        } else
          burden += run.Bands;

      return new ChargeCourse(
        fuel,
        burden,
        ChargeColumn.BandsPerBlock,
        // Compared as AssetLocations rather than strings: a segment stores whatever code was pushed, and
        // `coke` and `game:coke` are the same fuel spelled two ways.
        mixedFuel ? null : fuelMaterial
      );
    }
  }

  private void AppendCourseInfo(StringBuilder sb) {
    ChargeCourse course = TopCourse;
    if (course.FuelBands + course.BurdenBands <= 0)
      return;
    sb.AppendLine(
      Lang.Get(
        IiexLang.BfInfoCourse,
        course.FuelBands,
        CourseFuelName(course),
        course.BurdenBands,
        course.Bands
      )
    );
  }

  /// <summary>
  /// What the course line calls its fuel: the fuel's own item name when the course holds exactly one, "mixed
  /// fuel" when it holds several, and the bare word "fuel" when it holds none, so a burden-only course reads
  /// "0 fuel". The name comes off the item rather than a per-fuel lang key, so a fuel a third mod grants
  /// <see cref="Roles.Fuel"/> to names itself.
  /// </summary>
  private string CourseFuelName(in ChargeCourse course) {
    if (course.FuelBands <= 0)
      return Lang.Get(IiexLang.BfInfoCourseFuelnone);
    if (course.FuelMaterial is not { } material)
      return Lang.Get(IiexLang.BfInfoCourseFuelmixed);
    // An unresolvable code falls back to the generic word rather than rendering the raw code: a shaft
    // loaded from an old save can hold a code whose mod is gone.
    return MaterialDisplayName(material)
      ?? Lang.Get(IiexLang.BfInfoCourseFuelnone);
  }

  /// <summary>
  /// Display name for a charge material code, or null when nothing resolves. Items first, then blocks -
  /// <c>BlockEntityChargePile.StackOf</c>'s order, a segment storing a plain code string so the set of
  /// chargeable things stays open.
  /// </summary>
  private string? MaterialDisplayName(string material) {
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
  /// Whether this charge will melt, stated as the temperature it settles at against the melt line. The
  /// projection assumes full blast at ambient - the blowers running with no preheat, the cold reading - so
  /// the verdict never promises a melt the furnace cannot deliver. It reads
  /// <see cref="ComputeHeatBalance"/> rather than re-deriving a coke-fraction threshold of its own.
  /// </summary>
  private void AppendChargeVerdict(StringBuilder sb) {
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
        melts ? "iiex:bf-info-chargemelts" : "iiex:bf-info-chargechills",
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

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    long now = Api.World.ElapsedMilliseconds;
    if (now - _lastInfoUpdate > 1000) {
      StringBuilder sb = new StringBuilder();
      if (!StructureComplete) {
        sb.AppendLine(Lang.Get("iiex:bf-info-incomplete"));
      } else {
        // The core keeps only the temperature/threshold/heat-contributor ledger and the lit-state lines; the
        // burden slice is the tall hopper's and the molten pools belong to the taps that drain them.
        if (State != FurnaceState.Idle) {
          string stateName = Lang.Get(
            "iiex:bf-state-" + State.ToString().ToLowerInvariant()
          );
          sb.AppendLine(Lang.Get("iiex:bf-info-state", stateName));
          AppendHeatBalanceInfo(sb);

          if (
            State == FurnaceState.Firing
            && _internalTemp >= _ironMeltingPoint
            // A blocked furnace is at heat but will never cross into Melting, so the progress bar is
            // suppressed rather than left to climb to 100% and stall. The mismatch line at the hopper
            // reports the cause.
            && !ConversionBlocked
          ) {
            // Progress toward the Melting phase as a percentage, matching the Bessemer converter's
            // readout, rather than a raw seconds countdown.
            int pct = (int)
              GameMath.Clamp(
                100f
                  * _secondsAboveMelting
                  / System.Math.Max(1f, _meltStartDelay),
                0,
                100
              );
            sb.AppendLine(Lang.Get("iiex:bf-info-meltingin", pct));
          }

          // Name an air-starved stall so the countdown below reads as a cause - a dead blower, a cut main -
          // rather than an unexplained snuffing.
          if (_airStarved)
            sb.AppendLine(Lang.Get(IiexLang.BfInfoAirstarved));

          // And name a hang beside it: the two are the stalls a lit furnace can suffer, one starved of air
          // and one blocked by its own cold burden. It reads here rather than in the hopper's charging
          // slice because it is a condition of the fire.
          AppendChillInfo(sb);

          if (_extinguishSeconds > 0) {
            int remainingSeconds = (int)
              System.Math.Max(
                0f,
                ExtinguishThresholdDefault - _extinguishSeconds
              );
            sb.AppendLine(
              Lang.Get("iiex:bf-info-extinguishingin", remainingSeconds)
            );
          }
        } else {
          AppendNotLitInfo(sb);
        }
      }
      _cachedInfoText = sb.ToString();
      _lastInfoUpdate = now;
    }
    dsc.Append(_cachedInfoText);
  }

  /// <summary>
  /// Reports the heat balance: where the hearth is, where it needs to be, and which side of the ledger is at
  /// fault. The contributors are shown because the charge-mass loss term inverts the naive expectation -
  /// topping a marginal furnace up makes it cooler. Nothing is recalculated here.
  /// </summary>
  private void AppendHeatBalanceInfo(StringBuilder sb) {
    // The temperature/threshold/heat-in/heat-loss/blast ledger is the shared exlib formatter, which the
    // converter reuses with its own keys; the burden grade and melt rate below are the furnace's own.
    HeatBalanceHud.AppendLedger(
      sb,
      _lastHeatBalance,
      _internalTemp,
      _ironMeltingPoint,
      HeatLedgerKeys,
      FormatTemp
    );

    AppendHeatExtras(sb);

    sb.AppendLine(
      Lang.Get(
        IiexLang.BfInfoBurdengrade,
        Lang.Get(Burden.ProfileLangKey(_chargeMix))
      )
    );

    if (State == FurnaceState.Melting)
      sb.AppendLine(
        Lang.Get(
          IiexLang.BfInfoMeltrate,
          (int)System.Math.Round(MeltSpeedFactor() * 100f)
        )
      );
  }

  // The furnace's lang keys for the shared ledger, and the measurement formatter it uses. ExMeasure lives
  // downstream of exlib, so the formatter is passed to the helper rather than referenced by it.
  private static readonly HeatBalanceLedgerKeys HeatLedgerKeys = new(
    Temp: IiexLang.BfInfoTemp,
    HeatOk: IiexLang.BfInfoHeatok,
    HeatStall: IiexLang.BfInfoHeatstall,
    HeatIn: IiexLang.BfInfoHeatin,
    HeatLoss: IiexLang.BfInfoHeatloss,
    BlastNone: IiexLang.BfInfoNodraught,
    BlastHot: IiexLang.BfInfoBlasthot,
    BlastCold: IiexLang.BfInfoBlastcold
  );

  private static readonly System.Func<float, string> FormatTemp = t =>
    ExMeasure.Temperature(t);

  /// <summary>
  /// A branch's own heat-ledger lines, appended straight after the shared ledger. The shared formatter
  /// names only the losses every machine has, so a term one branch pays and another does not - the
  /// reverberatory transfer loss - would otherwise show up in the total with nothing accounting for it.
  /// </summary>
  protected virtual void AppendHeatExtras(StringBuilder sb) { }

  /// <summary>
  /// Appends the not-lit status line. The generic reasons (exhaust full, needs mix) are handled here; the
  /// subclass supplies the lit-readiness line via <see cref="AppendReadyInfo"/>.
  /// </summary>
  private void AppendNotLitInfo(StringBuilder sb) {
    if (IsChoked)
      sb.AppendLine(Lang.Get("iiex:bf-info-exhaustfull"));
    else if (!_cachedIsFull)
      sb.AppendLine(Lang.Get("iiex:bf-info-needsmix"));
    else
      AppendReadyInfo(sb);

    // No "burned out - re-coke it" line: fuel is not stamped on the burden item. Salvaged charge needs
    // fuel bands charged over it, which the layered-charge display already shows.
  }

  /// <summary>
  /// Names the stall: how many units in the shaft this furnace will not convert. Shown in every state,
  /// because a full shaft that refuses to make metal otherwise reads as a bug.
  /// </summary>
  private void AppendWrongBurdenInfo(StringBuilder sb) {
    if (_cachedRejectedCount <= 0)
      return;
    sb.AppendLine(Lang.Get(IiexLang.BfInfoWrongburden, _cachedRejectedCount));
  }

  /// <summary>Appends the charge-ready status line (full but not yet lit). Furnace-specific.</summary>
  protected abstract void AppendReadyInfo(StringBuilder sb);

  #endregion
}
