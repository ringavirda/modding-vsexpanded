using System;
using ExpandedLib;
using ExpandedLib.Blocks;
using ExpandedLib.Catalogues;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Helpers;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Machines;
using ExpandedLib.Networks;
using ExpandedLib.Structures;
using IronIndustryExpanded.BlockNetworkPipe;
using IronIndustryExpanded.BlockStructures.Furnaces;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockStructures.Boiler;

/// <summary>
/// Shared base for the steam boilers: a mega-block raised via the vanilla
/// <c>RightClickConstructable</c> behavior, which suppresses the default mesh so the vessel is drawn
/// through the animator (a permanent <c>idle</c> animation re-tessellated to the built elements as
/// construction progresses). Peripheral cells are reserved with invisible structure fillers, so the
/// whole vessel is self-contained: finishing the construction stages is the only gate on running it.
/// Per-variant stats come from the virtual hooks below.
/// <para>
/// The fire is one of two models, chosen by whether the leaf blocktype declares a
/// <see cref="BEBehaviorFirebox"/>. A vessel that does carries its bed inside its own shape and is
/// charged and lit through its main hatch; one that does not is walled into masonry the player builds,
/// and burns a vanilla coal pile the player tends in the firebox cell directly.
/// </para>
/// </summary>
public abstract partial class BlockEntityBoiler : BlockEntityProductionMachine {
  // Owns the RCC-suppressed-mesh animator triad shared by every constructed mega-block; the boiler
  // additionally swaps in its own renderer via the onAnimatorBuilt hook (see SwapBoilerRenderer).
  private ConstructedAnimator? _animator;

  // Client-side in-vessel water surface + a tick to keep its state fresh.
  private BoilerWaterRenderer? _waterRenderer;
  private long _clientTickId;

  // Throttle stamp for the client-side boiling hum loop.
  private long _boilHumMs;

  #region Per-variant stats

  /// <summary>Total internal capacity (L) shared between water and steam.</summary>
  protected abstract float Capacity { get; }

  /// <summary>Minimum water (L) before the boiler will start heating/boiling.</summary>
  protected abstract float MinBoilWater { get; }

  /// <summary>Maximum water (L) the boiler will hold/boil - the rest of the capacity is steam space.</summary>
  protected abstract float MaxBoilWater { get; }

  /// <summary>
  /// Water (L) the automatic pump intake tops up to (a fraction of capacity), so a piped
  /// supply reaches a safe level without overfilling. Manual pouring can still reach
  /// <see cref="MaxBoilWater"/>.
  /// </summary>
  protected virtual float MaxWaterIntakeFill =>
    Capacity * IiexValues.BoilerWaterIntakeFillFraction;

  /// <summary>Steam (L/s) produced while boiling at full tilt.</summary>
  protected abstract float SteamPerSecond { get; }

  /// <summary>Steam pressure (atm) the boiler chokes its output network at.</summary>
  protected abstract float MaxOutputPressure { get; }

  protected abstract int ExplosionRadius { get; }

  #endregion

  /// <summary>True once the player has finished the construction stages.</summary>
  public bool IsConstructed => _animator?.IsConstructed ?? false;

  /// <summary>A finished vessel is an operable one: the shell is the boiler's own footprint, so there
  /// is nothing further to verify around it. ExRightClickConstructable now publishes that readiness
  /// itself (IProductionReadiness), so nothing further gates the tick here.</summary>
  protected override bool CanRunProduction => true;

  /// <summary>Operating phase. Heating advances on a timer, not on a modelled temperature.</summary>
  public enum BoilerState {
    Idle,
    Heating,
    Boiling,
  }

  #region Operating state (serialized)

  /// <summary>Water held in the boiler (L).</summary>
  [Persist("waterVolume")]
  private float _waterVolume;

  /// <summary>Steam held internally (L); drives the internal pressure.</summary>
  [Persist("steamVolume")]
  private float _steamVolume;

  /// <summary>Current operating phase.</summary>
  [Persist("boilerState")]
  private BoilerState _state = BoilerState.Idle;

  /// <summary>Seconds spent in the Heating phase (boils once it reaches the heat-up time).</summary>
  [Persist("heatingSeconds")]
  private float _heatingSeconds;

  /// <summary>Seconds the boiler has been running without fire / with water out of range (drives the shutdown grace).</summary>
  [Persist("shutdownSeconds")]
  private float _shutdownSeconds;

  /// <summary>Whether the main (firing) hatch is open: the bed takes fuel and a light through it.</summary>
  [Persist("mainHatchOpen")]
  public bool MainHatchOpen { get; private set; }

  /// <summary>Whether the man hatch is open (held animation + venting + bucket fill).</summary>
  [Persist("manHatchOpen")]
  public bool ManHatchOpen { get; private set; }

  /// <summary>Whether the fire is lit. The bed itself has no lit state - it is fuel in a cell - so the
  /// vessel that fires it holds one.</summary>
  [Persist("lit")]
  private bool _lit;

  /// <summary>Seconds of burn credited against the charged fuel's own duration, carried between ticks
  /// so a fuel lasting longer than one tick is drawn down a whole unit at a time.</summary>
  [Persist("fuelSeconds")]
  private float _fuelSeconds;

  /// <summary>
  /// Transient, not serialized: set once a held right-click has acted on the main hatch, so the hold
  /// acts exactly once instead of firing every frame.
  /// </summary>
  public bool MainHatchToggled { get; set; }

  /// <summary>Transient counterpart of <see cref="MainHatchToggled"/> for the man hatch.</summary>
  public bool ManHatchToggled { get; set; }

  #endregion

  /// <summary>
  /// Internal pressure (atm): steam over the tank space not occupied by water.
  /// e.g. 400 L steam, 400 L water, 1200 L vessel = 400 / (1200-400) = 0.5 atm.
  /// </summary>
  public float InternalPressure =>
    _steamVolume / Math.Max(1f, Capacity - _waterVolume);

  /// <summary>
  /// True while boiling at or above 90% of choke pressure. Derived from synced state, so it can drive
  /// the warning particles client-side.
  /// </summary>
  public bool InDangerZone =>
    _state == BoilerState.Boiling
    && InternalPressure >= 0.9f * MaxOutputPressure;

  /// <summary>Heating progress 0..1 (for the HUD); only meaningful in the Heating phase. Shares
  /// <see cref="EffectiveHeatUpSeconds"/> with the Heating-to-Boiling gate, so a cool fire cannot read
  /// 100% before the vessel it describes has actually reached Boiling.</summary>
  public float HeatProgress =>
    GameMath.Clamp(_heatingSeconds / EffectiveHeatUpSeconds, 0f, 1f);

  // In-game day stamp for natural water evaporation; unloaded time is not charged.
  private double _lastEvapDays = -1;

  // Client-display mirror, synced via the tree.
  [Persist("burning")]
  private bool _burning;

  /// <summary>Set server-side when steam is escaping the outlet with no pipe attached;
  /// synced to drive the leak particle plume.</summary>
  [Persist("steamLeaking")]
  private bool _steamLeaking;

  #region Lifecycle

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    // The animator (and IsConstructed) resolves on both sides; it only builds and poses on the client.
    // The boiler swaps in its own renderer after each build via SwapBoilerRenderer.
    _animator = new ConstructedAnimator(
      this,
      () => AnimCacheKey,
      SwapBoilerRenderer,
      () => this,
      DrawnElements
    );
    _animator.Initialize(ApplyPose);

    if (api is ICoreClientAPI capi) {
      InitWaterRenderer(capi);
      // Keep the water level / glow current despite push-based state syncing.
      _clientTickId = RegisterGameTickListener(OnClientTick, 250);
    }
    // The base (BlockEntityProductionMachine) registers the server production tick.
  }

  private BlockBoiler? BoilerBlock => Block as BlockBoiler;

  /// <summary>Per-variant animator cache key (also the shape selector); unique per block code + side.</summary>
  protected virtual string AnimCacheKey => Block.Code.Path;

  /// <summary>
  /// What the animator draws, given the element set <paramref name="built"/> the construction stages have
  /// raised. A construction stage owns the coal group as a whole - a finished vessel has a grate, an
  /// unfinished one does not - while how much of it is standing is the bed's, so the group's entry is
  /// narrowed to one course per four charged units and drops out entirely on an empty bed. A vessel with
  /// no bed of its own burns a pile in a cell outside the mesh and takes the stage set unchanged.
  /// Internal so <c>BoilerBedRenderTests</c> can read the drawn set without a render client.
  /// </summary>
  internal string[]? DrawnElements(string[]? built) =>
    Bed?.ComposeOver(built) ?? built;

  /// <summary>The element set the animator is currently rendering, at the construction stage it stands
  /// at.</summary>
  internal string[]? DrawnElements() =>
    DrawnElements(_animator?.Rcc?.shape?.SelectiveElements);

  public override void OnBlockRemoved() {
    _animator?.Dispose();
    DisposeClient();
    // Base stops the production tick.
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded() {
    _animator?.Dispose();
    DisposeClient();
    base.OnBlockUnloaded();
  }

  private void DisposeClient() {
    if (_clientTickId != 0) {
      UnregisterGameTickListener(_clientTickId);
      _clientTickId = 0;
    }
    _waterRenderer?.Dispose();
    _waterRenderer = null;
  }

  /// <summary>
  /// Swaps vanilla's renderer for one that lights the vessel from a body cell rather than the
  /// firebox-adjacent master cell (see <see cref="BoilerAnimatableRenderer"/>). Run by the animator
  /// helper after each build, before the pose is applied, so the ShouldRender seeding below reads the
  /// active-animation state as it stands at build time.
  /// </summary>
  private void SwapBoilerRenderer(
    BlockEntityAnimationUtil util,
    MeshData meshData
  ) {
    if (Api is not ICoreClientAPI capi || BoilerBlock == null)
      return;

    util.renderer?.Dispose();
    util.renderer = new BoilerAnimatableRenderer(
      capi,
      Pos.ToVec3d(),
      new Vec3f(0, Block.Shape.rotateY, 0),
      util.animator!,
      util.activeAnimationsByAnimCode,
      meshData
    ) {
      LightPos = BoilerBlock.LightSampleWorldPos(Pos).ToVec3d(),
      // Seed visibility from whether a pose is already running. The 1.22 AnimatableRenderer ctor does
      // this itself; the legacy (1.20/1.21) ctor leaves ShouldRender false and only flips it via
      // OnAnimationsStateChange, which StartAnimation skips when the pose ("idle") is already active.
      // Without this, the rebuild on each construction step produces an invisible renderer.
      ShouldRender = util.activeAnimationsByAnimCode.Count > 0,
    };
  }

  /// <summary>Clip that holds the main (firing) hatch open. A vessel whose art draws one door under
  /// another name overrides this.</summary>
  protected virtual string MainHatchAnimation => "mainhatchopen";

  /// <summary>Clip that holds the man hatch open.</summary>
  protected virtual string ManHatchAnimation => "manhatchopen";

  /// <summary>Clip that holds the vessel at rest. It poses the same hatch elements the hatch clips do,
  /// so it yields to them rather than layering under them.</summary>
  private const string IdleAnimation = "idle";

  private void ApplyPose() =>
    _animator?.Pose(util => {
      // Animatable only draws while an animation runs, so one clip is always held. An open hatch holds
      // its own; a hatch whose clip is not running sits at the shape's authored rest, which is the
      // closed pose. The hatch clips are started before idle stops, so the mesh never goes unposed.
      HoldClip(util, MainHatchAnimation, MainHatchOpen);
      HoldClip(util, ManHatchAnimation, ManHatchOpen);
      HoldClip(util, IdleAnimation, !MainHatchOpen && !ManHatchOpen);
    });

  /// <summary>Runs <paramref name="code"/> while <paramref name="running"/> and stops it otherwise.
  /// Starting an already-active clip is a no-op, so this is safe to call on every repose.</summary>
  private static void HoldClip(
    BlockEntityAnimationUtil util,
    string code,
    bool running
  ) {
    if (!running) {
      util.StopAnimation(code);
      return;
    }
    util.StartAnimation(
      new AnimationMetaData {
        Animation = code,
        Code = code,
        AnimationSpeed = 1f,
        EaseInSpeed = 6f,
        EaseOutSpeed = 6f,
      }.Init()
    );
  }

  #endregion

  #region Production

  /// <summary>Grace timer: how long the boiler has sat at its output ceiling while still boiling (drives the explosion).</summary>
  private GraceTimer _overpressure;

  /// <summary>Grace timer: how long the boiler has sat choked - fire lit but its exhaust outlet backed up (drives snuffing the fuel pile).</summary>
  private GraceTimer _chokeTimer;

  /// <summary>Whether the boiler is currently choked (can't expel exhaust). Synced for the HUD line.</summary>
  [Persist("choked")]
  private bool _choked;

  protected override void OnProductionTick(float dt) {
    if (!IsConstructed)
      return;

    var ba = Api.World.BlockAccessor;

    ApplyEvaporation();

    BlockPos fuelPos = BoilerBlock?.FuelWorldPos(Pos) ?? Pos;
    bool fireOn =
      Bed != null ? _lit && Bed.Units > 0 : PileIsBurning(ba, fuelPos);

    PipeNetwork? exhaustNet = ExhaustNetwork();
    bool draughtBlocked =
      (exhaustNet?.State?.Pressure ?? 0f)
      >= IiexValues.ExhaustMaxOutputPressure;
    bool burning = fireOn && !draughtBlocked;

    if (burning)
      BurnBedDown(dt);

    // Fire lit but exhaust outlet backed up to the vent cap means choked: combustion gas cannot
    // escape. Held choked past the grace, the fire is put out.
    _choked = fireOn && draughtBlocked;
    if (
      _chokeTimer.Update(_choked, dt, IiexValues.BoilerChokeExtinguishSeconds)
    ) {
      Snuff(ba, fuelPos);
      ExSounds.Play(Api, fuelPos, ExSounds.Extinguish, 0.7f);
      _choked = false;
    }

    PipeNetwork? waterNet = this.ConnectedNetwork<PipeNetwork>(
      BoilerBlock?.FeedwaterWorldFace ?? BlockFacing.DOWN
    );
    if (waterNet != null && _waterVolume < MaxWaterIntakeFill) {
      float feedPressure = waterNet.State?.Pressure ?? 0f;
      // Cap the draw at the intake rate so a piped supply trickles in instead of taking the whole
      // remaining headroom in a single tick.
      float request = Math.Min(
        MaxWaterIntakeFill - _waterVolume,
        IiexValues.BoilerWaterIntakeRate * dt
      );
      float drawn = waterNet.TryConsumeLiquid(request, ba);
      _waterVolume += drawn;

      // The pressurised-feed steam boost is gated on the same ceiling as BoilStep; ungated it is the one
      // path that can lift steam past MaxOutputPressure on every boiling tick.
      if (
        drawn > 0f
        && feedPressure > 1f
        && _state == BoilerState.Boiling
        && InternalPressure < MaxOutputPressure
      )
        _steamVolume +=
          drawn * (feedPressure - 1f) * IiexValues.WaterPressureSteamBoost;
    }

    // Only a lower water bound gates boiling; there is no "too full" cutoff, because the fill paths
    // (auto intake, manual pour, condensation) already cap water at MaxBoilWater.
    bool enoughWater = _waterVolume >= MinBoilWater;
    float grace = IiexValues.BoilerShutdownDelaySeconds;

    switch (_state) {
      case BoilerState.Idle:
        CondenseInternal(dt);
        if (burning && enoughWater) {
          _state = BoilerState.Heating;
          _heatingSeconds = 0f;
          _shutdownSeconds = 0f;
        }
        break;

      case BoilerState.Heating:
        if (!burning || !enoughWater) {
          _shutdownSeconds += dt;
          if (_shutdownSeconds >= grace)
            ShutDown();
        } else {
          _shutdownSeconds = 0f;
          _heatingSeconds += dt;
          // A cool fire (low FuelRateMultiplier) stretches the heat-up the same way it throttles
          // boiling: the vessel is heating-surface limited either way, not flame limited.
          if (_heatingSeconds >= EffectiveHeatUpSeconds)
            _state = BoilerState.Boiling;
        }
        break;

      case BoilerState.Boiling:
        if (burning && enoughWater)
          _shutdownSeconds = 0f;
        else {
          _shutdownSeconds += dt;
          if (_shutdownSeconds >= grace) {
            ShutDown();
            break;
          }
        }

        if (
          enoughWater
          && (burning || _shutdownSeconds < grace)
          && InternalPressure < MaxOutputPressure
        )
          BoilStep(dt);
        break;
    }

    CapSteamToCeiling();

    _burning = burning && _state != BoilerState.Idle;

    if (ManHatchOpen) {
      VentExcessSteam(dt);
      _overpressure.Reset();
      _steamLeaking = false; // vents through the man hatch, not the outlet
    } else {
      // PushSteam reports back when the outlet is open to air (no pipe) and steam is jetting out
      // instead of pressurising, which drives the leak particles.
      _steamLeaking = _state != BoilerState.Idle && PushSteam(ba, dt);

      bool overPressure =
        _state == BoilerState.Boiling
        && burning
        && InternalPressure >= MaxOutputPressure;
      if (
        _overpressure.Update(
          overPressure,
          dt,
          IiexValues.BoilerOverpressureSeconds
        )
      ) {
        Explode();
        return;
      }
    }

    if (burning && exhaustNet != null)
      exhaustNet.TryProduceGas(
        IiexValues.BoilerExhaustPerSecond * dt,
        SteamTemperature() * 0.6f,
        "Exhaust",
        ba,
        maxOutputPressure: IiexValues.ExhaustMaxOutputPressure
      );

    MarkDirty(true);
  }

  #endregion

  #region Fire

  /// <summary>
  /// The exhaust network the flue gas leaves through. A vessel whose footprint declares a port on the
  /// outlet cell reads across that port's face, since a port is a connector and not a graph node of its
  /// own; one whose outlet is a real node block the player set there reads the network at the cell.
  /// </summary>
  private PipeNetwork? ExhaustNetwork() {
    if (BoilerBlock is not { } block)
      return null;
    BlockPos outlet = block.ExhaustOutletWorldPos(Pos);
    return block.ExhaustWorldFace is { } face
      ? this.ConnectedNetworkAt<PipeNetwork>(outlet, face)
      : this.NetworkAt<PipeNetwork>(outlet);
  }

  /// <summary>Whether the vanilla coal pile in the firebox cell is alight and still holds fuel - the
  /// fire of a vessel walled into masonry, which has no bed of its own.</summary>
  private static bool PileIsBurning(IBlockAccessor ba, BlockPos fuelPos) =>
    ba.GetBlockEntity(fuelPos)
      is BlockEntityCoalPile { IsBurning: true, inventory: { Count: > 0 } } pile
    && !pile.inventory[0].Empty;

  /// <summary>Puts the fire out, whichever model this vessel burns.</summary>
  private void Snuff(IBlockAccessor ba, BlockPos fuelPos) {
    if (Bed != null) {
      _lit = false;
      return;
    }
    (ba.GetBlockEntity(fuelPos) as BlockEntityCoalPile)?.Extinguish();
  }

  /// <summary>
  /// Draws the bed down by the whole units this tick's burn has paid for. The fuel's own duration is
  /// seconds per unit, so a long-burning coal costs the same bed far more running time than a short one
  /// - which is the whole difference between the fuels a boiler can take.
  /// </summary>
  private void BurnBedDown(float dt) {
    if (Bed is not { Units: > 0 })
      return;
    _fuelSeconds += dt;
    float perUnit = Math.Max(1f, BurnSecondsPerUnit);
    while (_fuelSeconds >= perUnit && Bed is { Units: > 0 }) {
      Bed.Consume(1);
      _fuelSeconds -= perUnit;
    }
    if (Bed is { Units: <= 0 }) {
      _lit = false;
      _fuelSeconds = 0f;
    }
  }

  // One stack of the charged fuel, held against the code it was built for: a bed holds one fuel, so the
  // stack is stable for the whole bed, and the production tick runs every second per boiler.
  private string? _bedStackCode;
  private ItemStack? _bedStack;

  // Set once a code has been looked up, so a code that resolves to nothing - a fuel whose mod has been
  // removed from a save that still holds a charged bed - is answered from the cache too, rather than
  // re-searching the item and block registries on every tick for as long as the bed stands.
  private bool _bedStackResolved;

  /// <summary>The stack the bed is holding, or null when it is empty or its code no longer resolves.</summary>
  private ItemStack? BedStack {
    get {
      string? code = Bed?.FuelCode;
      if (code == null || Api == null) {
        _bedStackCode = null;
        _bedStackResolved = false;
        return _bedStack = null;
      }
      if (_bedStackResolved && _bedStackCode == code)
        return _bedStack;
      var loc = new AssetLocation(code);
      CollectibleObject? collectible =
        Api.World.GetItem(loc) ?? (CollectibleObject?)Api.World.GetBlock(loc);
      _bedStackCode = code;
      _bedStackResolved = true;
      return _bedStack =
        collectible == null ? null : new ItemStack(collectible);
    }
  }

  /// <summary>Seconds one unit of the charged fuel burns for, from its own combustibleProps.</summary>
  private float BurnSecondsPerUnit =>
    BEBehaviorFirebox.BurnDurationOf(BedStack);

  /// <summary>Flame temperature (°C) of the charged fuel, or 0 when the bed is empty or unlit.</summary>
  protected float BedBurnTemperature =>
    _lit ? BEBehaviorFirebox.BurnTemperatureOf(BedStack) : 0f;

  #endregion

  #region Production arithmetic

  /// <summary>The boiler's feed liquid. Held untagged as <see cref="_waterVolume"/> and named here so the
  /// water-to-steam phase change reads its output medium and expansion factor from the medium taxonomy
  /// rather than hardcoding them.</summary>
  private const string FeedLiquid = "Water";

  /// <summary>The gas the feed water boils into, read from <see cref="ExLiquids.Taxonomy"/> (Water to
  /// Steam by default; a mod can retarget it). <paramref name="expansionFactor"/> is the vaporisation
  /// volume multiplier; the taxonomy returns 0 when it leaves the factor to the caller, in which case the
  /// iiex steam-expansion constant applies.</summary>
  private static string BoiledMedium(out float expansionFactor) {
    if (
      ExLiquids.Taxonomy.VaporisationTarget(
        FeedLiquid,
        out string gas,
        out float factor
      )
      && gas.Length > 0
    ) {
      expansionFactor = factor > 0f ? factor : IiexValues.SteamExpansionFactor;
      return gas;
    }
    expansionFactor = IiexValues.SteamExpansionFactor;
    return "Steam";
  }

  /// <summary>Converts water to steam for one tick: 1 L of water becomes the taxonomy's water-vaporisation
  /// expansion (<see cref="IiexValues.SteamExpansionFactor"/> by default) in litres of steam.</summary>
  private void BoilStep(float dt) {
    BoiledMedium(out float expansion);
    float rate = SteamPerSecond * FuelRateMultiplier;
    float waterUse = Math.Min(_waterVolume, rate * dt / expansion);
    if (waterUse <= 0f)
      return;
    _waterVolume -= waterUse;
    _steamVolume += waterUse * expansion;
  }

  /// <summary>
  /// How much of the vessel's rated output the burning fuel supports, 0..1. A boiler is limited by its
  /// heating surface rather than by its flame: every coal is far hotter than the water, so the term
  /// saturates at <see cref="IiexValues.BoilerFuelDesignTemp"/> and fuel choice is felt mainly as how
  /// long a bed lasts. 1 with no bed (or an unlit one), so an empty boiler's arithmetic is the rated one
  /// rather than collapsing to zero.
  /// </summary>
  public float FuelRateMultiplier {
    get {
      float flame = BedBurnTemperature;
      if (flame <= 0f)
        return 1f;
      float sat = SteamTemperature();
      float head = Math.Max(1f, IiexValues.BoilerFuelDesignTemp - sat);
      return GameMath.Clamp((flame - sat) / head, 0f, 1f);
    }
  }

  /// <summary>
  /// Seconds this bed's fire actually needs to reach Boiling: the rated
  /// <see cref="IiexValues.BoilerHeatUpSeconds"/> stretched by how far <see cref="FuelRateMultiplier"/>
  /// falls short of 1. The single member both the Heating-to-Boiling gate and <see cref="HeatProgress"/>
  /// read, so a cool fire cannot show complete before the vessel it describes has actually finished
  /// heating.
  /// </summary>
  private float EffectiveHeatUpSeconds =>
    IiexValues.BoilerHeatUpSeconds / FuelRateMultiplier;

  /// <summary>
  /// Saturated-steam temperature (°C): T = boiling point x absolutePressure^exponent. Boiler and pipe
  /// pressure are gauge (0 atm = atmospheric), so 1 atm is added to reach absolute pressure; at 0 atm
  /// gauge the steam reads exactly the boiling point.
  /// </summary>
  private float SteamTemperature() {
    float absPressure = Math.Max(0f, InternalPressure) + 1f;
    return IiexValues.BoilingPoint
      * (float)Math.Pow(absPressure, IiexValues.SteamSaturationExponent);
  }

  /// <summary>
  /// Pushes internal steam into the steam network, capped at the choke pressure. With no
  /// connected steam pipe at the outlet, the neck is open: steam bleeds to atmosphere at
  /// <see cref="IiexValues.BoilerSteamLeakRate"/> and the method returns <c>true</c> to
  /// drive the leak particles.
  /// </summary>
  private bool PushSteam(IBlockAccessor ba, float dt) {
    if (BoilerBlock is not { } block || _steamVolume <= 0f)
      return false;

    // The steam connector is a port filler on the body; the network it feeds sits across that port's
    // own face, read off the footprint the way ExhaustNetwork reads the outlet's, so the face the cell
    // couples on and the face the vessel pushes through are one declaration rather than two. A cell
    // declaring no port has no outlet to couple to, which reads the same as an unpiped neck below.
    BlockFacing? face = block.SteamWorldFace;
    BlockPos? pipePos =
      face == null ? null : block.SteamPipeWorldPos(Pos).AddCopy(face);

    bool pipeAttached =
      pipePos != null
      && ba.GetBlock(pipePos) is BlockNetworkNode steamPipe
      && steamPipe.HasConnectorAt(face!.Opposite);

    if (!pipeAttached) {
      // Open neck - steam jets out instead of building pressure.
      float leaked = Math.Min(
        _steamVolume,
        IiexValues.BoilerSteamLeakRate * dt
      );
      _steamVolume = Math.Max(0f, _steamVolume - leaked);
      return leaked > 0f;
    }

    PipeNetwork? steamNet = this.NetworkAt<PipeNetwork>(pipePos!);
    if (steamNet == null)
      return false;

    // A freshly built run has no PipeNetworkState; it is created lazily on the first TryProduceGas.
    // Treat that as an empty network at full node capacity so the boiler can charge it.
    var st = steamNet.State;
    float netVolume = st?.Volume ?? 0f;
    float netMaxVolume =
      st?.MaxVolume ?? steamNet.Nodes.Count * ExlibValues.LitresPerPipe;
    if (netMaxVolume <= 0f)
      return false;

    // Boiler and pipe run are connected vessels: steam moves until pressures equalise, so it stays in
    // both and the boiler never empties into the run. Transfer is the boiler steam above the shared
    // equilibrium pressure, with free vessel space F and pipe capacity V:
    //   eqP = (Sboiler + Spipe) / (F + V);  transfer = Sboiler - eqP*F.
    float freeSpace = Math.Max(1f, Capacity - _waterVolume);
    float eqPressure = (_steamVolume + netVolume) / (freeSpace + netMaxVolume);
    float transfer = _steamVolume - eqPressure * freeSpace;
    if (transfer <= 0.001f)
      return false; // pipe already at/above the boiler's pressure - hold the steam in

    float accepted = steamNet.ProduceGasMeasured(
      transfer,
      SteamTemperature(),
      BoiledMedium(out _),
      ba,
      maxOutputPressure: InternalPressure
    );
    if (accepted > 0f)
      _steamVolume = Math.Max(0f, _steamVolume - accepted);
    return false;
  }

  /// <summary>
  /// Hard-caps steam so <see cref="InternalPressure"/> can never exceed <see cref="MaxOutputPressure"/>:
  /// the vessel has an implied safety valve, so any excess above the choke ceiling is vented. Runs at the
  /// end of every production tick. The cap sits at the ceiling rather than below it, so a closed burning
  /// boiler still trips the over-pressure burst grace, which fires at <c>&gt;= MaxOutputPressure</c>.
  /// </summary>
  private void CapSteamToCeiling() {
    float steamCeiling =
      MaxOutputPressure * Math.Max(1f, Capacity - _waterVolume);
    if (_steamVolume > steamCeiling)
      _steamVolume = steamCeiling;
  }

  /// <summary>
  /// Bleeds steam out through the open lid at <see cref="IiexValues.BoilerLidVentRate"/>, so a
  /// pressurised vessel blows off gradually. While running it stops at atmospheric (1 atm), since boiling
  /// keeps adding steam underneath; once idle it empties the trapped pocket down to 0 atm.
  /// </summary>
  private void VentExcessSteam(float dt) {
    float floor =
      _state == BoilerState.Idle ? 0f : Math.Max(0f, Capacity - _waterVolume);
    if (_steamVolume <= floor)
      return;
    float vent = Math.Min(
      _steamVolume - floor,
      IiexValues.BoilerLidVentRate * dt
    );
    _steamVolume = Math.Max(floor, _steamVolume - vent);
  }

  /// <summary>
  /// Condenses leftover internal steam back into water after a shutdown, but only while the resulting
  /// water stays below the boil-water ceiling (<see cref="MaxBoilWater"/>). Once the vessel is that full
  /// the remaining steam is trapped and stops condensing, and must be vented through the lid.
  /// </summary>
  private void CondenseInternal(float dt) {
    if (_steamVolume <= 0f)
      return;
    // Condensing removes one expansion factor's worth of steam but frees only 1 L of headspace, so in a
    // nearly-full, high-pressure vessel each step concentrates the remainder and raises pressure instead
    // of lowering it. The crossover is exactly at the expansion factor; above it condensation is refused
    // and the trapped steam must be bled through the lid first.
    if (InternalPressure >= IiexValues.SteamExpansionFactor)
      return;
    float waterRoom = MaxBoilWater - _waterVolume;
    if (waterRoom <= 0f)
      return;
    float cond = Math.Min(
      Math.Min(_steamVolume, IiexValues.BoilerShutdownCondenseRate * dt),
      waterRoom * IiexValues.SteamExpansionFactor
    );
    _steamVolume -= cond;
    _waterVolume += cond / IiexValues.SteamExpansionFactor;
  }

  /// <summary>Shuts the boiler down: back to Idle, reset timers (leftover steam condenses in Idle).</summary>
  private void ShutDown() {
    _state = BoilerState.Idle;
    _heatingSeconds = 0f;
    _shutdownSeconds = 0f;
    _burning = false;
  }

  /// <summary>Natural water evaporation, measured in in-game days; time the chunk spent unloaded is not
  /// charged.</summary>
  private void ApplyEvaporation() {
    double nowDays = Api.World.Calendar?.TotalDays ?? -1;
    if (nowDays < 0)
      return;
    if (_lastEvapDays >= 0 && _waterVolume > 0f) {
      float evap = (float)(
        ExlibValues.EvaporationLitresPerDay * (nowDays - _lastEvapDays)
      );
      if (evap > 0f)
        _waterVolume = Math.Max(0f, _waterVolume - evap);
    }
    _lastEvapDays = nowDays;
  }

  private void Explode() {
    BlockPos pos = Pos.Copy();
    var world = Api.World;
    // Centre the blast on the vessel body, not the master cell (at the firebox end).
    BlockPos center = BoilerBlock?.ExplosionCenterPos(pos) ?? pos;

    if (BoilerBlock != null) {
      // A burst skips the structure's normal break path, so a salvageable fraction of the build
      // materials is pulled straight from the RightClickConstructable behavior.
      foreach (
        var ds in ConstructionMaterialDrops(IiexValues.BoilerExplosionDropRatio)
      )
        world.SpawnItemEntity(ds, pos.ToVec3d().Add(0.5, 0.5, 0.5));
      BoilerBlock.RemoveStructure(world, pos);
    }
    world.BlockAccessor.SetBlock(0, pos);

    // The built-in explosion supplies particles, sound, drops and entity damage but spares the mod's
    // low-resistance machinery, so the fragile blocks (pipes, ports, coal piles, soft terrain) are
    // flattened first. Radius is per variant. Runs in the server tick, so the server-world cast holds.
    float r = ExplosionRadius;
    ShatterFragileBlocks(
      world,
      center,
      r,
      IiexValues.BoilerBlastResistanceThreshold
    );
    (world as IServerWorldAccessor)?.CreateExplosion(
      center,
      EnumBlastType.EntityBlast,
      r,
      r + 2f
    );
  }

  /// <summary>
  /// The build materials this boiler would drop at <paramref name="ratio"/> (0..1) of the consumed
  /// stacks. The construction behavior scatters these from its own <c>OnBlockBroken</c> at a fixed ratio
  /// only, hence the dedicated accessor. Returns empty if the behavior is missing; never throws.
  /// </summary>
  private ItemStack[] ConstructionMaterialDrops(float ratio) =>
    _animator?.Rcc?.GetConstructionDrops(ratio, Api.World.Rand) ?? [];

  /// <summary>
  /// Breaks every block within <paramref name="radius"/> of <paramref name="center"/> whose resistance is
  /// below <paramref name="maxResistance"/> (pipes, ports, coal piles, soft terrain), leaving sturdier
  /// blocks standing. Uses <c>BreakBlock</c> so pipe nodes detach cleanly.
  /// </summary>
  private static void ShatterFragileBlocks(
    IWorldAccessor world,
    BlockPos center,
    float radius,
    float maxResistance
  ) {
    var ba = world.BlockAccessor;
    int ri = (int)Math.Ceiling(radius);
    float r2 = radius * radius;
    for (int dx = -ri; dx <= ri; dx++)
      for (int dy = -ri; dy <= ri; dy++)
        for (int dz = -ri; dz <= ri; dz++) {
          if (dx * dx + dy * dy + dz * dz > r2)
            continue;
          BlockPos p = center.AddCopy(dx, dy, dz);
          Block block = ba.GetBlock(p);
          if (block.Id == 0 || block.Resistance >= maxResistance)
            continue;
          ba.BreakBlock(p, null, 0.25f);
        }
  }

  #endregion

  #region Hatches, firing and manual fill

  /// <summary>Swings the main (firing) hatch.</summary>
  public void ToggleMainHatch() {
    MainHatchOpen = !MainHatchOpen;
    PlayHatchSound(BoilerBlock?.MainHatchWorldPos(Pos), MainHatchOpen);
    MarkDirty(true);
  }

  /// <summary>Swings the man hatch, through which the vessel is filled by hand and vented.</summary>
  public void ToggleManHatch() {
    ManHatchOpen = !ManHatchOpen;
    PlayHatchSound(BoilerBlock?.ManHatchWorldPos(Pos), ManHatchOpen);
    MarkDirty(true);
  }

  // Reuses the coke-oven door's metal hatch open/close sound.
  private void PlayHatchSound(BlockPos? at, bool opening) =>
    ExSounds.PlayAt(
      Api.World,
      at ?? Pos,
      opening ? ExSounds.CokeOvenDoorOpen : ExSounds.CokeOvenDoorClose,
      null,
      range: 32f
    );

  /// <summary>
  /// Whether the next empty-handed hold at the main hatch lights the fire rather than swinging the
  /// door: an open door over a bed charged to capacity and not already alight. A part-charged bed is
  /// refused for the same reason a furnace refuses one - a fire is lit once, on a full bed.
  /// </summary>
  public bool CanLightBed => MainHatchOpen && !_lit && Bed is { IsFull: true };

  /// <summary>Lights the charged bed. Does nothing when <see cref="CanLightBed"/> is false.</summary>
  public void LightBed() {
    if (!CanLightBed)
      return;
    _lit = true;
    _fuelSeconds = 0f;
    ExSounds.Play(Api, BoilerBlock?.FuelWorldPos(Pos) ?? Pos, ExSounds.Ignite);
    MarkDirty(true);
  }

  /// <summary>
  /// Charges the bed from <paramref name="slot"/> and deducts what it took, refusing with the same
  /// three messages a firebox does: not fuel at all, a different fuel already in the bed, or full.
  /// </summary>
  public bool TryChargeBed(IPlayer byPlayer, ItemSlot? slot) {
    if (Bed is not { } bed || slot?.Itemstack is not { } stack)
      return false;

    if (!BEBehaviorFirebox.IsFuel(stack)) {
      (byPlayer as IServerPlayer)?.SendIngameError("iiex-firebox-notfuel");
      return true;
    }

    int taken = bed.TryAdd(stack, bed.Free);
    if (taken == 0) {
      (byPlayer as IServerPlayer)?.SendIngameError(
        bed.Accepts(stack) ? "iiex-firebox-full" : "iiex-firebox-wrongfuel"
      );
      return true;
    }

    slot.TakeOut(taken);
    slot.MarkDirty();
    MarkDirty(true);
    return true;
  }

  /// <summary>
  /// Pours water from a held liquid container into the boiler (RMB while the lid is open), capped by the
  /// boil-water ceiling. Both sides are metered in litres, so no conversion is needed.
  /// </summary>
  public bool TryManualFill(IPlayer byPlayer, ItemSlot slot) {
    if (slot.Itemstack?.Collectible is not BlockLiquidContainerBase cont)
      return false;

    ItemStack? content = cont.GetContent(slot.Itemstack);
    if (content?.Collectible?.Code?.Path?.Contains("water") != true)
      return false;

    float space = MaxBoilWater - _waterVolume;
    if (space < 0.01f)
      return false;

    // Empty the container, capped by the space left. The amount moved is measured as a litre delta so
    // transfer-size rounding cannot desync the two sides.
    float before = cont.GetCurrentLitres(slot.Itemstack);
    if (before <= 0f)
      return false;

    cont.TryTakeLiquid(slot.Itemstack, Math.Min(before, space));
    float removed = before - cont.GetCurrentLitres(slot.Itemstack);
    if (removed <= 0f)
      return false;
    slot.MarkDirty();

    _waterVolume += removed;

    BlockPos pourPos = BoilerBlock?.ManHatchWorldPos(Pos) ?? Pos;
    ExSounds.PlayAt(Api.World, pourPos, ExSounds.WaterPour, null, range: 16f);

    MarkDirty(true);
    return true;
  }

  /// <summary>
  /// Bails water out of the boiler into a held liquid container (RMB with an empty or water-holding
  /// bucket while the lid is open). Only water above <see cref="MinBoilWater"/> is reachable, so manual
  /// draining stops at the operating floor. The amount moved is measured as a litre delta so
  /// transfer-size rounding cannot desync the two sides.
  /// </summary>
  public bool TryManualDrain(IPlayer byPlayer, ItemSlot slot) {
    if (slot.Itemstack?.Collectible is not BlockLiquidContainerBase cont)
      return false;

    // Empty, or already holding water; do not mix into another liquid.
    ItemStack? content = cont.GetContent(slot.Itemstack);
    if (
      content != null
      && content.Collectible?.Code?.Path?.Contains("water") != true
    )
      return false;

    // Only water above the operating floor can be bailed out with a bucket.
    float reachable = _waterVolume - MinBoilWater;
    if (reachable < 0.01f)
      return false;

    float before = cont.GetCurrentLitres(slot.Itemstack);
    float space = cont.CapacityLitres - before;
    if (space < 0.01f)
      return false;

    float want = Math.Min(reachable, space);
    var waterStack = new ItemStack(
      Api.World.GetItem(new AssetLocation("game:waterportion"))
    );
    // TryPutLiquid also caps the transfer at the liquid stack's item count, and a fresh portion stack is
    // one item (0.01 L). Setting the source stack size to int.MaxValue leaves the transfer bounded only
    // by `want`: the bucket's free space, capped by the reachable boiler water.
    waterStack.StackSize = int.MaxValue;
    cont.TryPutLiquid(slot.Itemstack, waterStack, want);
    float added = cont.GetCurrentLitres(slot.Itemstack) - before;
    if (added <= 0f)
      return false;
    slot.MarkDirty();

    _waterVolume -= added;

    BlockPos drainPos = BoilerBlock?.ManHatchWorldPos(Pos) ?? Pos;
    ExSounds.PlayAt(Api.World, drainPos, ExSounds.WaterPour, null, range: 16f);

    MarkDirty(true);
    return true;
  }

  #endregion

  #region Serialization

  // GraceTimer.ToTree/FromTree write one flat float under the key they are given, not a nested
  // sub-tree, so this is a Tree entry rather than an ExpandedLib.Blocks.IPersistable member.
  protected override void DeclareState(ExBlockState state) =>
    state.Tree(
      "overpressure",
      tree => _overpressure.ToTree(tree, "overpressure"),
      (tree, _) => _overpressure.FromTree(tree, "overpressure")
    );

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    // The bed reads its own charge out of the same tree, through the base fan-out below, so its
    // previous fuel and course count have to be taken before that runs.
    string? prevFuel = Bed?.FuelCode;
    int prevCourses = Bed?.LayerCount ?? 0;
    bool prevMain = MainHatchOpen;
    bool prevMan = ManHatchOpen;

    base.FromTreeAttributes(tree, worldForResolving);

    if (Api?.Side == EnumAppSide.Client) {
      // The coal courses are drawn in whatever fuel is charged and one course per four units standing,
      // and both are resolved while the mesh is built. A charge and a burn-down only change the tree, so
      // the mesh is rebuilt here; the construction event that otherwise triggers a rebuild fires on a
      // finished stage and never again.
      if (prevFuel != Bed?.FuelCode || prevCourses != (Bed?.LayerCount ?? 0))
        _animator?.Refresh();
      // Hatch poses are push-based: replay them whenever a synced state flips.
      else if (prevMain != MainHatchOpen || prevMan != ManHatchOpen)
        ApplyPose();
    }
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(
    IPlayer forPlayer,
    System.Text.StringBuilder dsc
  ) {
    base.GetBlockInfo(forPlayer, dsc);
    if (!IsConstructed)
      return;

    if (Bed is { } bed)
      dsc.AppendLine(bed.InfoLine());

    dsc.AppendLine(
      Lang.Get(
        "iiex:boiler-info-water",
        ExMeasure.VolumeRange(_waterVolume, MaxBoilWater)
      )
    );
    dsc.AppendLine(
      Lang.Get(
        "iiex:boiler-info-steam",
        ExMeasure.Volume(_steamVolume),
        ExMeasure.Pressure(InternalPressure)
      )
    );

    if (_state == BoilerState.Boiling)
      dsc.AppendLine(
        Lang.Get(
          "iiex:boiler-info-boiling",
          // The rate actually being made, not the vessel's rated ceiling: with no bed (or an unlit
          // one) FuelRateMultiplier is 1 and this reads the same as before.
          ExMeasure.FlowRate(SteamPerSecond * FuelRateMultiplier, "F0"),
          ExMeasure.Temperature(SteamTemperature())
        )
      );
    else if (_state == BoilerState.Heating)
      dsc.AppendLine(Lang.Get("iiex:boiler-info-heating", HeatProgress * 100f));
    else if (_waterVolume < MinBoilWater)
      dsc.AppendLine(
        Lang.Get("iiex:boiler-info-needswater", ExMeasure.Volume(MinBoilWater))
      );
    else
      dsc.AppendLine(Lang.Get("iiex:boiler-info-idle"));

    if (MainHatchOpen)
      dsc.AppendLine(Lang.Get("iiex:boiler-info-mainhatchopen"));

    if (ManHatchOpen)
      dsc.AppendLine(Lang.Get("iiex:boiler-info-manhatchopen"));

    if (_choked)
      dsc.AppendLine(Lang.Get("iiex:boiler-info-choked"));

    if (_overpressure.IsCounting)
      dsc.AppendLine(
        Lang.Get(
          "iiex:boiler-info-overpressure",
          _overpressure.Remaining(IiexValues.BoilerOverpressureSeconds)
        )
      );
  }

  #endregion
}
