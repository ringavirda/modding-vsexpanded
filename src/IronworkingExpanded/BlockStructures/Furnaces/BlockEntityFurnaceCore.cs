using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Materials;
using ExpandedLib.Networks;
using ExpandedLib.Process;
using IronworkingExpanded.Items;
using IronworkingExpanded.Patches;
using IronworkingExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.Furnaces;

/// <summary>
/// Generic furnace core for the mod's furnace multiblocks. Drives the firing/melting state
/// machine: vents exhaust through the gas outlets, draws air/blast through the tuyeres, ramps the
/// internal temperature, and runs the melt-cycle that turns a charge into molten product through
/// the taps. The charge handling, the smelt-cycle conversion, the molten product accumulation and
/// drain, and the residue left on extinguish are furnace-specific and supplied by the subclass
/// behind the protected hooks below; everything else (orchestration, timers, serialization, HUD) is
/// shared. Tunable values come from the protected properties so variants can override them.
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

  /// <summary>Current operating state of the furnace.</summary>
  public FurnaceState State { get; protected set; } = FurnaceState.Idle;

  protected int _cachedMixCount = 0;
  protected bool _cachedIsFull = false;

  // Wrong-family charge in the shaft: it still burns (and burns out), but it blocks the conversion to
  // molten while present, and the HUD names the mismatch. Cached (and serialized) because GetBlockInfo
  // runs client-side and the client never walks the charge - the same reason _cachedMixCount rides the tree.
  protected int _cachedRejectedCount = 0;
  protected string? _cachedRejectedFamily;

  protected List<BlockPos> _gasOutlets = [];
  protected List<BlockPos> _tuyeres = [];

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
  //   AIR: air is the oxidant for coke. A coke-rich burden burns more fuel per ton of iron and needs
  //        proportionally more air to do it. (Hot blast's real historical value was cutting coke per
  //        ton - and with it, the blast volume per ton.)
  //
  //   PRESSURE: coke is the permeable skeleton of the charge column, the coarse non-fusing component
  //        that holds gas channels open through the stack. A coke-LEAN burden packs denser, so the
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

  /// <summary>Hearth mix total at or above which the furnace reads as full (can fire) and for the HUD.</summary>
  protected abstract int BlastMixRequiredToFire { get; }

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

  /// <summary>Extinguish threshold (seconds) with a single disruption.</summary>
  protected virtual int ExtinguishThresholdDefault => 30;

  /// <summary>Extinguish threshold (seconds) with two or more concurrent disruptions.</summary>
  protected virtual int ExtinguishThresholdSevere => 0;

  /// <summary>Cold-soak time (seconds) below the melt point before Melting reverts to Firing.</summary>
  protected virtual int BelowMeltingReset => 30;

  #endregion

  #region Structure geometry

  // Every cell the furnace reads is declared here as a structure-local offset in the anchor's north
  // frame - the same frame the core block's multiblock layout is drawn in, so a tuyere offset must
  // land on the layout's 'Y', a tap offset on its 'T', the shaft centre inside its 'c' column. The
  // defaults are the blast furnace's; a furnace with different plumbing overrides the one member it
  // differs in. Keeping them here (rather than inline in each subclass's scan/drain) is what lets
  // the offset-vs-layout test check the correspondence the compiler cannot.

  /// <summary>
  /// Structure-local cell at the middle of the shaft - where the ignition whoosh, the fire ambience
  /// and the extinguish hiss play, and the centre of the charge walk.
  /// </summary>
  protected virtual Vec3i ShaftCentre => new(0, 3, 0);

  /// <summary>Structure-local tuyere cells the furnace draws its blast through.</summary>
  protected virtual Vec3i[] TuyereCells => [new(0, 1, -1), new(0, 1, 1)];

  /// <summary>Structure-local exhaust-outlet cells. Empty when the open top is the chimney.</summary>
  protected virtual Vec3i[] GasOutletCells => [new(0, 6, -1), new(0, 6, 1)];

  /// <summary>Structure-local cell of the lower tap, which drains the metal product.</summary>
  protected virtual Vec3i MetalTapCell => new(2, 1, 0);

  /// <summary>Structure-local cell of the higher tap, which drains slag.</summary>
  protected virtual Vec3i SlagTapCell => new(-2, 2, 0);

  /// <summary>Structure-local low corner (inclusive) of the charge column - every cell the layout
  /// marks as chargeable lies inside <see cref="ShaftMin"/>..<see cref="ShaftMax"/>.</summary>
  protected virtual Vec3i ShaftMin => new(-1, 1, -1);

  /// <summary>Structure-local high corner (inclusive) of the charge column.</summary>
  protected virtual Vec3i ShaftMax => new(1, 5, 1);

  /// <summary>
  /// Structure-local cells of the bottommost layer of the shaft - the hearth floor the molten pool
  /// freezes across when the furnace is extinguished. These are the layout's chargeable cells at the
  /// lowest Y of the shaft (which is why the blast furnace lists two, not a full 3x3: the rest of
  /// that layer is brick and tuyere). The geometry test derives the same set from the layout data.
  /// </summary>
  protected virtual Vec3i[] SolidifyCells => [new(0, 1, 0), new(1, 1, 0)];

  /// <summary>World cell of a structure-local offset for the placed rotation.</summary>
  protected BlockPos GlobalOf(Vec3i local) =>
    GetGlobalPos(local.X, local.Y, local.Z);

  /// <summary>World cell of <see cref="ShaftCentre"/> for the placed rotation.</summary>
  protected BlockPos ShaftCentrePos => GlobalOf(ShaftCentre);

  /// <summary>
  /// The shaft box in world space for the placed rotation. Rotating the two structure-local corners
  /// can swap either horizontal axis, so the corners are re-sorted per component - a box walk needs a
  /// true min/max, not "whatever the local low corner rotated into".
  /// </summary>
  protected (BlockPos min, BlockPos max) ShaftBounds()
  {
    BlockPos a = GlobalOf(ShaftMin);
    BlockPos b = GlobalOf(ShaftMax);
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

  #region Charge family gate

  // A furnace burns exactly one burden family (the blast furnace: ore burden; the cupola: remelt
  // burden). Wrong-family charge is a legible mistake, not a silent one: it still lights and burns in
  // the shaft - and burns out to salvageable spent burden on extinguish, like any charge - but the
  // furnace will not render molten metal out of it, and the HUD names what it is holding versus what it
  // burns. Deriving the family from the item's identity (Burden.FamilyOf) keeps the two burdens distinct
  // items that can never merge into one pile. Every gate below reads the same shaft walk the tick does,
  // so every route charge can enter a shaft by (hand-placed pile, hopper drop, in-situ regrade, chute)
  // is covered at the one seam - the charge read - not per entry point.

  /// <summary>Burden families this furnace will convert to molten. Null/empty = unrestricted (the
  /// pre-gate behaviour), so a furnace that declares no families - or a third-party subclass - is never
  /// gated. The blast furnace overrides this to ore; the cupola will override it to remelt.</summary>
  protected virtual IReadOnlyList<string>? AcceptedFamilies => null;

  /// <summary>Item-level charge identity, family-blind: anything the shaft counts as chargeable at all
  /// (prepared burden of either family, or the legacy count-only blast mix). The family gate is layered
  /// on top by <see cref="AcceptsCharge"/>; a furnace whose charge is items rather than burden overrides this.</summary>
  protected virtual bool IsChargeItem(ItemStack? stack) =>
    Items.Burden.IsAny(stack)
    || MaterialRoleRegistry.IsRole(Roles.Charge, stack);

  /// <summary>Whether this furnace will actually convert this stack - it is charge, and of an accepted family.</summary>
  protected bool AcceptsCharge(ItemStack? stack) =>
    IsChargeItem(stack) && FamilyAccepted(Items.Burden.FamilyOf(stack));

  /// <summary>Whether <paramref name="family"/> is one this furnace burns (unrestricted when it declares none).</summary>
  protected bool FamilyAccepted(string family)
  {
    if (AcceptedFamilies is not { Count: > 0 } fams)
      return true;
    foreach (string f in fams)
      if (f == "*" || f == family)
        return true;
    return false;
  }

  /// <summary>True while wrong-family charge in the shaft is blocking the melt-to-molten conversion.</summary>
  protected bool ConversionBlocked => _cachedRejectedCount > 0;

  /// <summary>The family token the HUD names as "what this furnace burns" (the first accepted family,
  /// or "any" when unrestricted).</summary>
  protected string AcceptedFamilyName =>
    AcceptedFamilies is { Count: > 0 } fams && fams[0] != "*" ? fams[0] : "any";

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

  protected override void OnStructureLost()
  {
    if (State != FurnaceState.Idle)
      Extinguish();
  }

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

  protected override void OnProductionTick(float dt)
  {
    if (!StructureComplete)
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
            !outlet.TryProduce(
              ExhaustVolumePerTick,
              _internalTemp * ExhaustTempFactor,
              "Exhaust"
            )
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
      out int rejectedCount,
      out string? rejectedFamily
    );
    if (
      _cachedMixCount != mixCount
      || _cachedIsFull != isFull
      || _cachedRejectedCount != rejectedCount
      || _cachedRejectedFamily != rejectedFamily
    )
      dirty = true;
    _cachedMixCount = mixCount;
    _cachedIsFull = isFull;
    _cachedRejectedCount = rejectedCount;
    _cachedRejectedFamily = rejectedFamily;
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
          float consumed = tuyere.TryConsume(perTuyereDraw);
          if (tuyere is BlockEntityPipe pipe)
          {
            if (pipe.Medium == "Exhaust")
              tuyeresReceiveExhaust = true;

            if (pipe.Medium == "Air" && pipe.Pressure >= requiredPressure)
            {
              blastTemp = Math.Max(blastTemp, pipe.Temperature);
              // How much air actually arrived, not merely whether a line is attached: an
              // under-supplied tuyere slides the furnace back toward natural draught (cooler T_in),
              // and a near-dry one starves it out entirely (the disruption below).
              blastSupplied += consumed;
            }
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

    if (
      State == FurnaceState.Idle
      && StructureComplete
      && _cachedIsFull
      && !IsChoked
    )
    {
      if (TryIgniteCharge(chargeHandle))
      {
        State = FurnaceState.Firing;
        _fuelBurnSeconds = 0;
        _internalTemp = IgnitionTemp;
        dirty = true;
        // Whoosh as the charge catches.
        ExSounds.Play(Api, ShaftCentrePos, ExSounds.Ignite, 1f, 32f);
      }
    }

    if (State != FurnaceState.Idle)
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
      _lastHeatBalance = ComputeHeatBalance(
        _chargeMix,
        blastSupplyFrac,
        blastTemp,
        mixCount
      );
      float targetTemp = _lastHeatBalance.TProcess;

      float oldTemp = _internalTemp;
      // Heating/cooling rates are per-second; scale by dt for tick-independence. Blast now raises
      // the target rather than the rate - a hot furnace is hot because it makes more heat.
      if (_internalTemp < targetTemp)
        _internalTemp = Math.Min(
          _internalTemp + IwexValues.BfHeatRatePerSecond * dt,
          targetTemp
        );
      else if (_internalTemp > targetTemp)
        _internalTemp = Math.Max(
          _internalTemp - IwexValues.BfCoolRatePerSecond * dt,
          targetTemp
        );

      // No absolute clamp - TProcess is already floored at ambient, and a ceiling here silently
      // capped the advertised hot-blast temperature.
      if (Math.Abs(_internalTemp - oldTemp) > 0.1f)
        dirty = true;

      if (State == FurnaceState.Firing)
      {
        _fuelBurnSeconds += dt;
        if (_fuelBurnSeconds >= _maxFuelBurnTime)
        {
          Extinguish();
          return;
        }

        if (_internalTemp >= _ironMeltingPoint)
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
        if (_internalTemp < _ironMeltingPoint)
        {
          _belowMeltingSeconds += dt;
          dirty = true;
          if (_belowMeltingSeconds >= BelowMeltingReset)
          {
            State = FurnaceState.Firing;
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
            _meltSeconds += dt;
            // The harder the furnace is being blown past the melt line, the faster the burden
            // renders - which is what makes a hot blast out-produce a cold one without either
            // carrying its own per-cycle yield constant.
            if (_meltSeconds >= _meltIntervalSec / MeltSpeedFactor())
            {
              _meltSeconds = 0;
              SmeltCycle(chargeHandle);
              dirty = true;
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

    int requiredMix = Math.Max(1, BlastMixRequiredToFire);
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
    State = FurnaceState.Melting;
    _meltSeconds = 0;
    _fuelBurnSeconds = 0;
    MarkDirty(true);
  }

  private void Extinguish()
  {
    if (State != FurnaceState.Idle)
      ExSounds.Play(Api, ShaftCentrePos, ExSounds.Extinguish, 1f, 32f);

    State = FurnaceState.Idle;
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

  /// <summary>Recomputes the gas-outlet and tuyere world cells for the current rotation.</summary>
  protected virtual void ScanForOutlets()
  {
    _gasOutlets = Resolve(GasOutletCells);
    _tuyeres = Resolve(TuyereCells);
  }

  private List<BlockPos> Resolve(Vec3i[] cells)
  {
    var list = new List<BlockPos>(cells.Length);
    foreach (Vec3i cell in cells)
      list.Add(GlobalOf(cell));
    return list;
  }

  /// <summary>
  /// Walks the charge once and returns an opaque handle the other charge hooks consume, so the tick
  /// does a single hearth walk per tick. The returned handle is passed back to
  /// <see cref="ReadChargeMix"/>, <see cref="TryIgniteCharge"/>, and <see cref="SmeltCycle"/>.
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
    out int rejectedCount,
    out string? rejectedFamily
  );

  /// <summary>Returns whether the charge is fully lit (all piles burning), igniting it as needed.</summary>
  protected abstract bool TryIgniteCharge(object chargeHandle);

  /// <summary>Consumes from the charge and accumulates molten product for one melt cycle.</summary>
  protected abstract void SmeltCycle(object chargeHandle);

  /// <summary>Whether any molten product pool has reached its capacity.</summary>
  protected abstract bool LiquidCapacityReached { get; }

  /// <summary>Drains the molten products into their taps; sets <paramref name="dirty"/> when state changed.</summary>
  protected abstract void DrainProducts(ref bool dirty);

  #endregion

  #region Extinguish residue

  // What a dead furnace leaves behind is the same story for every furnace in the mod - the pool
  // freezes onto the hearth floor and the burden is left as spent, salvageable charge - so it lives
  // here once. A furnace supplies only the two facts that are genuinely its own: which solid block
  // its pool freezes into and how much metal was in it. Nothing on this path makes slag: a furnace
  // that goes out is a setback, not a total loss of the charge.

  /// <summary>Block placed where the molten pool freezes (solidified iron; cast iron for a cupola).</summary>
  protected abstract AssetLocation SolidProductBlock { get; }

  /// <summary>Units of molten metal in the pool being frozen.</summary>
  protected abstract float DrainedMetalUnits { get; }

  /// <summary>Stamps the frozen product's nugget count onto the block entity placed at <paramref name="pos"/>.</summary>
  protected abstract void StampSolidProduct(BlockPos pos, int units);

  /// <summary>Zeroes the furnace's molten pools once the residue has been placed.</summary>
  protected abstract void ClearMoltenPools();

  /// <summary>Every charge pile in the shaft, with its world cell. Reuses the tick's charge walk.</summary>
  protected abstract IEnumerable<(
    BlockPos pos,
    BlockEntityCoalPile pile
  )> EnumerateChargePiles();

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
  /// Freezes the molten pool onto the hearth floor, spread evenly over whichever bottom-layer cells
  /// are actually free. The split is deterministic (remainder to the first cells, no world RNG) so
  /// the residue is testable and so two identical furnaces leave identical wrecks.
  /// </summary>
  private void SolidifyBottomLayer()
  {
    float units = DrainedMetalUnits;
    if (units <= 0f)
      return;

    Block? solid = Api.World.GetBlock(SolidProductBlock);
    if (solid == null)
      return;

    var cells = new List<BlockPos>();
    foreach (Vec3i local in SolidifyCells)
    {
      BlockPos pos = GlobalOf(local);
      Block occupant = Api.World.BlockAccessor.GetBlock(pos);
      // Free = empty, or a charge pile that was being consumed here. A pile holding wrong-family charge
      // is NOT free: freezing the pool over it would silently destroy the salvage the player is owed
      // (this furnace never converted that charge, so the metal was made from the accepted charge only).
      if (occupant.Id == 0)
        cells.Add(pos);
      else if (
        occupant.Code?.Path.StartsWith("coalpile") == true
        && !PileHoldsRejectedCharge(pos)
      )
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

  /// <summary>Whether the coal pile at <paramref name="pos"/> holds charge this furnace refused to
  /// convert - the pile the solidify walk must not overwrite so the wrong-family salvage survives.</summary>
  private bool PileHoldsRejectedCharge(BlockPos pos)
  {
    if (
      Api.World.BlockAccessor.GetBlockEntity(pos)
        is not BlockEntityCoalPile pile
      || pile.inventory == null
    )
      return false;
    foreach (var slot in pile.inventory)
      if (
        !slot.Empty
        && IsChargeItem(slot.Itemstack)
        && !AcceptsCharge(slot.Itemstack)
      )
        return true;
    return false;
  }

  /// <summary>
  /// Rewrites the remaining burden in place as spent charge instead of destroying it. Coke burns out
  /// by height: piles sitting on the tuyeres are stripped to <c>BfBurnoutFuelRetainedBottom</c>, piles
  /// at the top of the shaft - which the blast never reached - keep
  /// <c>BfBurnoutFuelRetainedTop</c>. Iron and flux are preserved verbatim, so the player digs the
  /// column out, re-cokes it in the mixer and charges it again.
  /// </summary>
  private void BurnOutCharge()
  {
    int yMin = GlobalOf(ShaftMin).Y;
    int yMax = GlobalOf(ShaftMax).Y;
    float span = Math.Max(1, yMax - yMin);

    foreach (var (pos, pile) in EnumerateChargePiles())
    {
      // Hand the pile back to its own burn timer, and reset that timer: a pile released mid-countdown
      // would otherwise slag itself minutes later and quietly take the salvage with it.
      BlastmixPiles.ReleaseFromFurnace(pile);

      float height = GameMath.Clamp((pos.Y - yMin) / span, 0f, 1f);
      float retained = GameMath.Lerp(
        IwexValues.BfBurnoutFuelRetainedBottom,
        IwexValues.BfBurnoutFuelRetainedTop,
        height
      );

      if (pile.inventory == null)
        continue;

      bool changed = false;
      foreach (var slot in pile.inventory)
      {
        // Both families burn out the same way: strip the coke, keep the iron/metal + flux as salvage.
        // Wrong-family charge that a furnace refused to convert is still burned out (not destroyed) -
        // it is the player's mistake to dig out and re-coke, not the furnace's to eat. Legacy count-only
        // blast mix carries no composition to burn out; it is left as it is and still never slagged here.
        if (slot.Empty || !Burden.IsAny(slot.Itemstack))
          continue;
        BurdenMix mix = Burden.Read(slot.Itemstack);
        if (!mix.HasContent)
          continue;

        Burden.Write(slot.Itemstack, mix with { Fuel = mix.Fuel * retained });
        slot.MarkDirty();
        changed = true;
      }

      if (changed)
        pile.MarkDirty(true);
    }
  }

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
    State = (FurnaceState)tree.GetInt("bfState", 0);
    _internalTemp = tree.GetFloat("internalTemp", 20f);
    _secondsAboveMelting = tree.GetFloat("secondsAboveMelting", 0);
    _meltSeconds = tree.GetFloat("meltSeconds", 0);
    _extinguishSeconds = tree.GetFloat("extinguishSeconds", 0);
    _belowMeltingSeconds = tree.GetFloat("belowMeltingSeconds", 0);
    _fuelBurnSeconds = tree.GetFloat("fuelBurnSeconds", 0);
    _cachedMixCount = tree.GetInt("cachedMixCount", 0);
    _cachedIsFull = tree.GetBool("cachedIsFull", false);
    _cachedRejectedCount = tree.GetInt("cachedRejectedCount", 0);
    _cachedRejectedFamily = tree.GetString("cachedRejectedFamily", null);
    _airStarved = tree.GetBool("airStarved", false);
    ReadHeatBalance(tree);
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
    if (_cachedRejectedFamily != null)
      tree.SetString("cachedRejectedFamily", _cachedRejectedFamily);
    tree.SetBool("airStarved", _airStarved);
    WriteHeatBalance(tree);
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

  /// <summary>World cell of the lower (metal) tap for the placed rotation. A tap compares its own
  /// position against this to know it is the metal tap - and shows the metal pool rather than the slag.</summary>
  public BlockPos MetalTapPos
  {
    get
    {
      EnsureStructureLoaded();
      return GlobalOf(MetalTapCell);
    }
  }

  /// <summary>World cell of the higher (slag) tap for the placed rotation.</summary>
  public BlockPos SlagTapPos
  {
    get
    {
      EnsureStructureLoaded();
      return GlobalOf(SlagTapCell);
    }
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
    sb.AppendLine(
      Lang.Get(
        "iwex:bf-info-mixloaded",
        _cachedMixCount,
        BlastMixRequiredToFire
      )
    );
    // A shaft can read full and still refuse to make metal; name the mismatch beside the count.
    AppendWrongBurdenInfo(sb);
  }

  /// <summary>The lower tap's slice: the molten metal pool it drains. No-op on a furnace with no metal
  /// pool; <see cref="BlockEntities.BlockEntityBlastFurnace"/> overrides it.</summary>
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

    // A furnace that went out leaves its burden spent rather than destroyed, which is only useful if
    // the player is told the salvage needs re-coking instead of re-lighting.
    if (Burden.ProfileLangKey(_chargeMix) == BurnedOutProfileKey)
      sb.AppendLine(Lang.Get(IwexLang.BfInfoBurnedout));
  }

  /// <summary>
  /// Names the wrong-family stall: what the shaft is holding, what this furnace burns, and how many
  /// units will not convert. Shown in every state, because the failure it explains (a full shaft that
  /// refuses to make metal) reads as a bug otherwise - the same house rule as the heat balance.
  /// </summary>
  private void AppendWrongBurdenInfo(StringBuilder sb)
  {
    if (_cachedRejectedCount <= 0)
      return;
    sb.AppendLine(
      Lang.Get(
        IwexLang.BfInfoWrongburden,
        Lang.Get("iwex:burden-family-" + (_cachedRejectedFamily ?? Items.Burden.FamilyOre)),
        Lang.Get("iwex:burden-family-" + AcceptedFamilyName),
        _cachedRejectedCount
      )
    );
  }

  /// <summary>Grade key a fully burned-out burden classifies as (see <see cref="IwexConfig.BurdenProfiles"/>).</summary>
  private const string BurnedOutProfileKey = "iwex:burden-profile-burnedout";

  /// <summary>Appends the charge-ready status line (full but not yet lit). Furnace-specific.</summary>
  protected abstract void AppendReadyInfo(StringBuilder sb);

  #endregion
}
