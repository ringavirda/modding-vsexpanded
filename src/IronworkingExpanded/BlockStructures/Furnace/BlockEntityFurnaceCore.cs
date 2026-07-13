using ExpandedLib.Blocks.Networks;
using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using PipesAndPowerExpanded.BlockNetworkPipe;
using PipesAndPowerExpanded.BlockNetworkPipe.BlockEntities;
using PipesAndPowerExpanded.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.Furnace;

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
  /// <summary>Whether the exhaust network is full, stalling production.</summary>
  public bool IsChoked { get; protected set; }

  /// <summary>Current operating state of the furnace.</summary>
  public FurnaceState State { get; protected set; } = FurnaceState.Idle;

  protected int _cachedMixCount = 0;
  protected bool _cachedIsFull = false;
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

  /// <summary>Base yaw (radians) of the furnace door, used to orient the multiblock structure.</summary>
  public float BaseAngleRad { get; set; } = -1f;

  // Sound throttles (world-elapsed ms): the furnace fire ambience and the molten
  // tap-pour hiss are looping, gated so the per-second tick doesn't spam audio.
  protected long _lastFireSoundMs;
  protected long _lastTapSoundMs;

  private string _cachedInfoText = "";
  private long _lastInfoUpdate = 0;

  // Block attributes cached at init instead of re-parsing the JsonObject every tick / HUD refresh.
  protected float _naturalMaxTemp;
  protected float _boostedMaxTemp;
  protected float _blastBoostThreshold;
  protected float _ironMeltingPoint;
  protected int _maxFuelBurnTime;
  protected float _meltStartDelay;
  protected float _meltIntervalSec;
  protected float _tuyereIntakeVolume;

  protected override int CompletionTickMs => 3000;

  #region Tunables

  /// <summary>Internal temperature ceiling without a hot blast.</summary>
  protected abstract float NaturalMaxTemp { get; }

  /// <summary>Internal temperature ceiling when a hot blast above the boost threshold is fed.</summary>
  protected abstract float BoostedMaxTemp { get; }

  /// <summary>Hot-blast temperature at or above which the boosted ceiling applies.</summary>
  protected abstract float BlastBoostThreshold { get; }

  /// <summary>Temperature at which the charge melts and the furnace can enter Melting.</summary>
  protected abstract float MeltingPoint { get; }

  /// <summary>Maximum Firing time (seconds) before the fuel burns out and the furnace extinguishes.</summary>
  protected abstract int MaxFuelBurnTime { get; }

  /// <summary>Soak time (seconds) above the melt point before transitioning to Melting.</summary>
  protected abstract float MeltStartDelay { get; }

  /// <summary>Interval (seconds) between melt cycles.</summary>
  protected abstract float MeltIntervalSec { get; }

  /// <summary>Volume drawn from each tuyere per tick.</summary>
  protected abstract float TuyereIntakeVolume { get; }

  /// <summary>Air-pressure threshold (atm) a tuyere must read to count as receiving blast.</summary>
  protected abstract float BlastPressureThreshold { get; }

  /// <summary>Hearth mix total at or above which the furnace reads as full (can fire) and for the HUD.</summary>
  protected abstract int BlastMixRequiredToFire { get; }

  /// <summary>Internal temperature the furnace snaps to on ignition.</summary>
  protected virtual float IgnitionTemp => 900f;

  /// <summary>Mix floor below which a lit furnace counts a disruption toward extinguish.</summary>
  protected virtual int DisruptionMixFloor => 144;

  /// <summary>Exhaust volume vented through each gas outlet per tick.</summary>
  protected virtual float ExhaustVolumePerTick => 24f;

  /// <summary>Factor applied to the internal temperature for vented exhaust temperature.</summary>
  protected virtual float ExhaustTempFactor => 0.8f;

  /// <summary>Extinguish threshold (seconds) with a single non-door disruption.</summary>
  protected virtual int ExtinguishThresholdDefault => 30;

  /// <summary>Extinguish threshold (seconds) when the only disruption is an open door.</summary>
  protected virtual int ExtinguishThresholdDoor => 10;

  /// <summary>Extinguish threshold (seconds) with two or more concurrent disruptions.</summary>
  protected virtual int ExtinguishThresholdSevere => 0;

  /// <summary>Cold-soak time (seconds) below the melt point before Melting reverts to Firing.</summary>
  protected virtual int BelowMeltingReset => 30;

  #endregion

  #region Abstract method implementations

  protected override void UpdateStructureRotation()
  {
    if (Block == null)
      return;

    if (BaseAngleRad < 0)
    {
      var doorBehavior = GetBehavior<BEBehaviorDoor>();
      BaseAngleRad = doorBehavior != null ? doorBehavior.RotateYRad : 0;
    }

    float angleDeg = BaseAngleRad * GameMath.RAD2DEG % 360;
    if (angleDeg < 0)
      angleDeg += 360;
    int snappedAngle = (int)System.Math.Round(angleDeg / 90.0) * 90 % 360;
    if (snappedAngle < 0)
      snappedAngle += 360;

    SetStructureAngle(snappedAngle % 360);
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

  /// <summary>Forces the structure rotation to be recomputed (call after placement).</summary>
  public void Init() => UpdateStructureRotation();

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
    _naturalMaxTemp = NaturalMaxTemp;
    _boostedMaxTemp = BoostedMaxTemp;
    _blastBoostThreshold = BlastBoostThreshold;
    _ironMeltingPoint = MeltingPoint;
    _maxFuelBurnTime = MaxFuelBurnTime;
    _meltStartDelay = MeltStartDelay;
    _meltIntervalSec = MeltIntervalSec;
    _tuyereIntakeVolume = TuyereIntakeVolume;
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
    int mixCount = ReadChargeMix(chargeHandle, out bool isFull);
    if (_cachedMixCount != mixCount || _cachedIsFull != isFull)
      dirty = true;
    _cachedMixCount = mixCount;
    _cachedIsFull = isFull;

    bool tuyeresReceiveExhaust = false;
    float hotBlastTemp = 20f;
    bool receivingBlast = false;

    foreach (var pos in _tuyeres)
    {
      if (Api.World.BlockAccessor.GetBlockEntity(pos) is IPipeNode tuyere)
      {
        float consumed = tuyere.TryConsume(_tuyereIntakeVolume);
        if (tuyere is BlockEntityPipe pipe)
        {
          if (pipe.Medium == "Exhaust")
            tuyeresReceiveExhaust = true;

          if (pipe.Medium == "Air" && pipe.Pressure >= BlastPressureThreshold)
          {
            hotBlastTemp = Math.Max(hotBlastTemp, pipe.Temperature);
            receivingBlast = true;
          }
        }
      }
      else
      {
        ScanForOutlets();
      }
    }

    bool isDoorOpen = GetBehavior<BEBehaviorDoor>()?.Opened == true;
    bool isLiquidCapacityReached = LiquidCapacityReached;

    if (
      State == FurnaceState.Idle
      && StructureComplete
      && _cachedIsFull
      && !IsChoked
      && !isDoorOpen
    )
    {
      if (TryIgniteCharge(chargeHandle))
      {
        State = FurnaceState.Firing;
        _fuelBurnSeconds = 0;
        _internalTemp = IgnitionTemp;
        dirty = true;
        // Whoosh as the charge catches.
        ExSounds.Play(Api, GetGlobalPos(0, 0, 2), ExSounds.Ignite, 1f, 32f);
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
      if (isDoorOpen)
        disruptionCount++;
      if (isLiquidCapacityReached)
        disruptionCount++;

      if (disruptionCount > 0)
      {
        _extinguishSeconds += dt;
        dirty = true;

        int extinguishThreshold = ExtinguishThresholdDefault;
        if (disruptionCount >= 2)
          extinguishThreshold = ExtinguishThresholdSevere;
        else if (isDoorOpen)
          extinguishThreshold = ExtinguishThresholdDoor;

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
        GetGlobalPos(0, 0, 2),
        ExSounds.Fire,
        ref _lastFireSoundMs,
        5000,
        0.6f,
        32f
      );

      float targetTemp =
        hotBlastTemp >= _blastBoostThreshold ? _boostedMaxTemp : _naturalMaxTemp;
      float oldTemp = _internalTemp;
      // Heating/cooling rates are per-second; scale by dt for tick-independence.
      float heatRate = receivingBlast ? 4f : 2f;

      if (_internalTemp < targetTemp)
        _internalTemp = Math.Min(_internalTemp + heatRate * dt, targetTemp);
      else if (_internalTemp > targetTemp)
        _internalTemp = Math.Max(_internalTemp - 4f * dt, targetTemp);

      _internalTemp = GameMath.Clamp(_internalTemp, 20f, 1700f);
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
          if (_secondsAboveMelting >= _meltStartDelay)
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

          if (!isLiquidCapacityReached)
          {
            _meltSeconds += dt;
            if (_meltSeconds >= _meltIntervalSec)
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
      ExSounds.Play(Api, GetGlobalPos(0, 0, 2), ExSounds.Extinguish, 1f, 32f);

    State = FurnaceState.Idle;
    _internalTemp = 20f;

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
  protected abstract void ScanForOutlets();

  /// <summary>
  /// Walks the charge once and returns an opaque handle the other charge hooks consume, so the tick
  /// does a single hearth walk per tick. The returned handle is passed back to
  /// <see cref="ReadChargeMix"/>, <see cref="TryIgniteCharge"/>, and <see cref="SmeltCycle"/>.
  /// </summary>
  protected abstract object CollectCharge();

  /// <summary>
  /// Reads the total mix in the charge handle and whether it is full enough to fire. While lit this
  /// is also where the furnace keeps its charge managed/burning (the side effect the original
  /// <c>GetBlastMixCount</c> ran).
  /// </summary>
  protected abstract int ReadChargeMix(object chargeHandle, out bool isFull);

  /// <summary>Returns whether the charge is fully lit (all piles burning), igniting it as needed.</summary>
  protected abstract bool TryIgniteCharge(object chargeHandle);

  /// <summary>Consumes from the charge and accumulates molten product for one melt cycle.</summary>
  protected abstract void SmeltCycle(object chargeHandle);

  /// <summary>Whether any molten product pool has reached its capacity.</summary>
  protected abstract bool LiquidCapacityReached { get; }

  /// <summary>Drains the molten products into their taps; sets <paramref name="dirty"/> when state changed.</summary>
  protected abstract void DrainProducts(ref bool dirty);

  /// <summary>
  /// Leaves the product-specific residue behind when the furnace extinguishes (e.g. solidified iron
  /// dropped in the hearth, piles converted to slag). The generic <see cref="Extinguish"/> handles
  /// the sound, state reset, temperature reset, and timer resets around this.
  /// </summary>
  protected abstract void ExtinguishResidue();

  /// <summary>Appends the product-specific HUD lines shown while Melting.</summary>
  protected abstract void AppendProductInfo(StringBuilder sb);

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
    BaseAngleRad = tree.GetFloat("baseAngleRad", -1f);
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
    tree.SetFloat("baseAngleRad", BaseAngleRad);
  }

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
        sb.AppendLine(
          Lang.Get("iwex:bf-info-mixloaded", _cachedMixCount, BlastMixRequiredToFire)
        );

        if (State != FurnaceState.Idle)
        {
          string stateName = Lang.Get(
            "iwex:bf-state-" + State.ToString().ToLowerInvariant()
          );
          sb.AppendLine(Lang.Get("iwex:bf-info-state", stateName));
          sb.AppendLine(
            Lang.Get("iwex:bf-info-temp", ExMeasure.Temperature(_internalTemp))
          );

          if (State == FurnaceState.Melting)
          {
            AppendProductInfo(sb);
          }
          else if (
            State == FurnaceState.Firing && _internalTemp >= _ironMeltingPoint
          )
          {
            // Progress toward the Melting phase as a percentage (matches the
            // Bessemer converter's readout) rather than a raw seconds countdown.
            int pct = (int)
              GameMath.Clamp(
                100f * _secondsAboveMelting / System.Math.Max(1f, _meltStartDelay),
                0,
                100
              );
            sb.AppendLine(Lang.Get("iwex:bf-info-meltingin", pct));
          }

          if (_extinguishSeconds > 0)
          {
            int maxExtinguish =
              (GetBehavior<BEBehaviorDoor>()?.Opened == true)
                ? ExtinguishThresholdDoor
                : ExtinguishThresholdDefault;
            int remainingSeconds = (int)
              System.Math.Max(0f, maxExtinguish - _extinguishSeconds);
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
  /// Appends the not-lit status line. The generic reasons (exhaust full, door open, needs mix) are
  /// handled here; the subclass supplies the lit-readiness line via <see cref="AppendReadyInfo"/>.
  /// </summary>
  private void AppendNotLitInfo(StringBuilder sb)
  {
    if (IsChoked)
      sb.AppendLine(Lang.Get("iwex:bf-info-exhaustfull"));
    else if (GetBehavior<BEBehaviorDoor>()?.Opened == true)
      sb.AppendLine(Lang.Get("iwex:bf-info-doorclosed"));
    else if (!_cachedIsFull)
      sb.AppendLine(Lang.Get("iwex:bf-info-needsmix"));
    else
      AppendReadyInfo(sb);
  }

  /// <summary>Appends the charge-ready status line (full but not yet lit). Furnace-specific.</summary>
  protected abstract void AppendReadyInfo(StringBuilder sb);

  #endregion
}
