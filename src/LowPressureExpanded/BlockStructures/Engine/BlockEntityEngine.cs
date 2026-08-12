using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Construction;
using ExpandedLib.Blocks.Machines;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using LowPressureExpanded.BlockNetworkPipe;
using LowPressureExpanded.BlockStructures.Engine.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LowPressureExpanded.BlockStructures.Engine;

/// <summary>
/// Shared base for the steam engines: a mega-block raised via the vanilla
/// <c>RightClickConstructable</c> behavior and drawn through the animator (RCC suppresses the default
/// mesh), with surrounding cells reserved by invisible structure fillers. The block at the
/// sub-machine cell <c>(0,0,2)</c> selects the pose - an <c>mpgenerator</c> uses the
/// <c>idlemp</c>/<c>cyclemp</c> animations, a blower, pump or empty cell uses
/// <c>idlepump</c>/<c>cyclepump</c>. Per-variant stats come from the virtual hooks below.
/// </summary>
public abstract class BlockEntityEngine : BlockEntityProductionMachine {
  // Owns the animator and construction (RCC) lifecycle shared by every constructed mega-block, and
  // the null-animator ready-guard. Fed a state-dependent cache key (broken vs intact); break/repair
  // rebuilds go through RebuildEngineAnimator.
  private ConstructedAnimator? _animator;

  // The sub-machine sits two cells away, not adjacent, so a change there never arrives through
  // OnNeighbourBlockChange. A light client poll re-poses when the attached type changes.
  private long _submachineWatchId;
  private bool _lastMP;

  // Client-side stroke-sound + over-pressure-steam watch.
  private long _engineClientTickId;
  private float _lastCycleFrame = -1f;
  private long _overSteamMs;

  // MP variant: the generator pushes this "axle is turning" flag each frame; ApplyPose reads it to
  // choose cyclemp vs idlemp. See DriveMpCycleFrame.
  private bool _mpTurning;

  // Constant low planetary-gear hum from the gear housing while the engine runs (client only).
  private ILoadedSound? _gearSound;

  /// <summary>Set true while the engine is driving its sub-machine (cycle animation).</summary>
  private bool _running;

  #region Per-variant stats

  /// <summary>Nominal maximum power (display reference; actual delivered power is <see cref="RunPower"/>).</summary>
  protected abstract float MaxPowerValue { get; }

  /// <summary>Inlet steam pressure (atm) at/above which the engine runs.</summary>
  protected abstract float EngagePressure { get; }

  /// <summary>Inlet pressure (atm) above which the engine wears toward a break.</summary>
  protected abstract float BreakPressure { get; }

  /// <summary>Steam (L/s) the engine draws while running at its current setting.</summary>
  protected abstract float RunSteamRate { get; }

  /// <summary>Power the engine delivers while running at its current setting.</summary>
  protected abstract float RunPower { get; }

  /// <summary>Hot condensed water (L/s) the engine spits out its outlet while running at its current setting.</summary>
  protected abstract float RunWaterOutput { get; }

  /// <summary>Particles vented out the cylinder top per power stroke; 0 suppresses the puff.
  /// Overridden per variant to scale with the throttle.</summary>
  protected virtual int CylinderSteamPuffCount => 2;

  /// <summary>Volume multiplier for the running sounds; 1 leaves them unchanged. The Cornish variant
  /// raises it when overclocked.</summary>
  protected virtual float SoundVolumeFactor => 1f;

  /// <summary>Pitch multiplier for the gear hum; 1 leaves it unchanged, below 1 deepens it.</summary>
  protected virtual float SoundPitchFactor => 1f;

  #endregion

  #region Break / repair

  /// <summary>Seconds the engine has run above its band; drives the break and resets when back in band.</summary>
  private GraceTimer _overPressure;

  /// <summary>True once the engine has burst from sustained over-pressure; it can't run until repaired.</summary>
  public bool IsBroken { get; private set; }

  /// <summary>Seconds of over-pressure left before the engine breaks (for the HUD warning).</summary>
  public float OverPressureRemaining =>
    _overPressure.Remaining(LpexValues.EngineOverPressureSeconds);

  /// <summary>Clears the broken state (called by the block's wrench repair).</summary>
  public void Repair() {
    if (!IsBroken)
      return;
    IsBroken = false;
    _overPressure.Reset();
    MarkDirty(true);
  }

  /// <summary>Bursts the engine: it stops and stays inert until repaired.</summary>
  private void Break() {
    IsBroken = true;
    AvailablePower = 0f;
    _running = false;
    // Clear the timer so the broken engine stops venting the warning plume: the client vent is
    // gated on it, and the broken tick returns early without resetting it.
    _overPressure.Reset();
    if (Api is { Side: EnumAppSide.Server }) {
      // Server particles replicate to clients.
      ExParticles.SteamPlume(Api.World, Pos, 120);
      // Smoke blasts out the cylinder top, where spent steam normally vents.
      Vec3d vent =
        EngineBlock?.CylinderVentPos(Pos).AddCopy(new Vec3d(0, -0.5, 0))
        ?? Pos.ToVec3d().Add(0.5, 0.5, 0.5);
      ExParticles.SmokeCloud(Api.World, vent, 80);
      ExSounds.PlayAt(
        Api.World,
        Pos,
        ExSounds.MediumExplosion,
        null,
        randomizePitch: false,
        range: 24f,
        volume: 0.5f
      );
    }
    MarkDirty(true);
  }

  #endregion

  /// <summary>True once the player has finished the construction stages.</summary>
  public bool IsConstructed => _animator?.IsConstructed ?? false;

  /// <summary>Available mechanical power (0..<see cref="MaxPower"/>), from inlet steam pressure.</summary>
  public float AvailablePower { get; private set; }

  /// <summary>Inlet steam pressure (atm) read last tick. Sub-machines set their output
  /// pressure to this times <see cref="LpexValues.SteamEngineEfficiency"/>.</summary>
  public float InletPressure { get; private set; }

  /// <summary>Maximum power this engine can deliver to its sub-machine at rated pressure.</summary>
  public float MaxPower => MaxPowerValue;

  #region MP generator drive

  /// <summary>
  /// MP load the generator holds at <see cref="LpexValues.MpRatedSpeed"/> with the engine at full
  /// power. The network slows past this and the engine stalls past double it. Derived from
  /// <see cref="MaxPower"/> rather than current output, so the stall check cannot latch off once
  /// the engine has stopped.
  /// </summary>
  public float MpRatedLoad => MaxPower * LpexValues.MpLoadPerEnginePower;

  /// <summary>
  /// Mechanical-power budget the generator delivers at the engine's current output. As a
  /// constant-power source (torque = budget / speed) the network settles at
  /// <c>speed = budget / load</c>: rated speed at rated load, slower under more.
  /// </summary>
  public float MpPowerBudget =>
    AvailablePower * LpexValues.MpLoadPerEnginePower * LpexValues.MpRatedSpeed;

  /// <summary>True when MP <paramref name="load"/> exceeds what keeps the network above half rated
  /// speed; the engine then stalls until load is shed.</summary>
  public bool IsMpOverstressed(float load) => load > 2f * MpRatedLoad;

  #endregion

  /// <summary>Where the inlet steam pressure sits against the operating band, as a lang-key
  /// fragment: <c>over</c> (above break, wearing toward a burst), <c>nominal</c> (in band),
  /// <c>under</c> (some steam, below the engage pressure), or <c>idle</c> (no steam).</summary>
  private string ClockState =>
    InletPressure > BreakPressure ? "over"
    : InletPressure >= EngagePressure ? "nominal"
    : InletPressure > 0.01f ? "under"
    : "idle";

  public float AnimationSpeed { get; private set; } = 1f;

  /// <summary>True while the engine is running (sub-machine being driven).</summary>
  public bool IsRunning => _running;

  private BlockEngine? EngineBlock => Block as BlockEngine;

  /// <summary>The production tick runs once construction is finished. The gate is construction, not
  /// running: a broken engine still ticks to keep its power zeroed and to render the break.</summary>
  protected override bool CanRunProduction => IsConstructed;

  /// <summary>The attached sub-machine block entity at the engine's sub-machine cell, if any.</summary>
  public BlockEntityEngineSubmachine? SubmachineBE =>
    EngineBlock != null
      ? Api.World.BlockAccessor.GetBlockEntity(EngineBlock.SubmachinePos(Pos))
        as BlockEntityEngineSubmachine
      : null;

  #region Lifecycle

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    // The cache key is state-dependent (see EngineCacheKey); the helper re-reads it on each build.
    _animator = new ConstructedAnimator(this, EngineCacheKey);
    _animator.Initialize(ApplyPose);

    if (api is ICoreClientAPI && _animator.AnimUtil != null) {
      // Loaded already broken: the helper's first build used the intact construction elements, so
      // rebuild without the piston subtree and re-pose against the fresh animator.
      if (IsBroken) {
        RebuildEngineAnimator();
        ApplyPose();
      }
      _lastMP = IsMPGenerator();

      _submachineWatchId = RegisterGameTickListener(OnSubmachineWatch, 500);
      // Fast client tick: per-stroke piston sounds at keyframe crossings, plus cylinder steam
      // while the engine runs over pressure.
      _engineClientTickId = RegisterGameTickListener(OnEngineClientTick, 50);
    }
    // The server production tick is registered by the base (BlockEntityProductionMachine).
  }

  #region Power

  protected override void OnProductionTick(float dt) {
    if (EngineBlock == null)
      return;

    var ba = Api.World.BlockAccessor;

    // A broken engine is inert until repaired.
    if (IsBroken) {
      AvailablePower = 0f;
      if (_running) {
        _running = false;
        MarkDirty(true);
      }
      return;
    }

    var inlet = this.ConnectedNetwork<PipeNetwork>(EngineBlock.SteamInletFace);
    float pressure =
      inlet?.State?.MediumType == "Steam" ? inlet.State.Pressure : 0f;
    InletPressure = pressure;

    // Running above the band wears the engine out; sustained long enough it bursts and must be
    // repaired. Back inside the band it recovers.
    bool overPressure = pressure > BreakPressure;
    bool wasCounting = _overPressure.IsCounting;
    if (
      _overPressure.Update(
        overPressure,
        dt,
        LpexValues.EngineOverPressureSeconds
      )
    ) {
      Break();
      return;
    }
    if (overPressure || wasCounting != _overPressure.IsCounting)
      MarkDirty(true); // refresh the HUD countdown / clear it on recovery

    // Fixed steam draw while engaged - inlet pressure only gates on/off. Power scales with
    // the steam the network can actually supply, so a starved line yields less power.
    float demand = SubmachineBE?.PowerDemand ?? 0f;
    bool engaged = pressure >= EngagePressure && demand > 0f;
    float power = 0f;

    if (engaged && inlet != null) {
      float want = RunSteamRate * demand * dt;
      float used = inlet.TryConsumeGas(want, ba);
      float frac = want > 0f ? used / want : 0f;
      power = RunPower * demand * frac;
      // Condensate is a fixed per-engine rate scaled by how hard the engine runs, not the raw
      // steam volume consumed.
      float waterOut = RunWaterOutput * demand * frac * dt;
      if (waterOut > 0f)
        OutputCondensate(waterOut, ba);
    }

    AvailablePower = power;
    bool run = power > 0.001f;

    float newSpeed = run ? 0.5f + power : 1f;
    if (run != _running || Math.Abs(newSpeed - AnimationSpeed) > 0.05f) {
      AnimationSpeed = newSpeed;
      _running = run;
      MarkDirty(true); // sync running + speed to clients for the cycle animation
    }
  }

  /// <summary>
  /// Sends condensed water out the outlet. A connected pipe network with room takes it at no
  /// pressure (only the pump pressurises water); otherwise it spills with a splash particle.
  /// </summary>
  private void OutputCondensate(float amount, IBlockAccessor ba) {
    var outNet = this.ConnectedNetwork<PipeNetwork>(
      EngineBlock!.WaterOutletFace
    );
    bool piped = outNet?.TryProduceLiquid(amount, 90f, 0f, ba) == true;
    if (!piped)
      SpawnWaterSpill();
  }

  /// <summary>Jets water out of the outlet when the condensate has nowhere to go.</summary>
  private void SpawnWaterSpill() {
    if (Api is { Side: EnumAppSide.Server }) {
      ExParticles.WaterJet(Api.World, Pos, EngineBlock!.WaterOutletFace);
      ExSounds.SplashSound(Api.World, Pos);
    }
  }

  #endregion

  /// <summary>Per-variant animator cache key (also the shape selector); unique per block code + side.</summary>
  protected virtual string AnimCacheKey => Block.Code.Path;

  // Re-pose when the sub-machine attached at (0,0,2) changes type between mpgenerator and
  // blower/pump, which switches the engine between its mp and pump animations.
  private void OnSubmachineWatch(float dt) {
    bool mp = IsMPGenerator();
    if (mp == _lastMP)
      return;
    _lastMP = mp;
    ApplyPose();
  }

  /// <summary>Shared teardown for removal and unload.</summary>
  private void Cleanup() {
    _animator?.Dispose();
    if (_submachineWatchId != 0)
      UnregisterGameTickListener(_submachineWatchId);
    if (_engineClientTickId != 0)
      UnregisterGameTickListener(_engineClientTickId);
    // The server production tick is owned and torn down by the base.
    DisposeGearHum();
  }

  public override void OnBlockRemoved() {
    Cleanup();
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded() {
    Cleanup();
    base.OnBlockUnloaded();
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetBool("running", _running);
    tree.SetFloat("animSpeed", AnimationSpeed);
    tree.SetFloat("availPower", AvailablePower);
    tree.SetFloat("inletPressure", InletPressure);
    tree.SetBool("broken", IsBroken);
    _overPressure.ToTree(tree, "overPressure");
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    bool wasRunning = _running;
    bool wasBroken = IsBroken;
    float wasSpeed = AnimationSpeed;
    _running = tree.GetBool("running");
    AnimationSpeed = tree.GetFloat("animSpeed", 1f);
    AvailablePower = tree.GetFloat("availPower");
    InletPressure = tree.GetFloat("inletPressure");
    IsBroken = tree.GetBool("broken");
    _overPressure.FromTree(tree, "overPressure");
    if (Api is ICoreClientAPI && _animator is { Ready: true }) {
      // Breaking/repairing swaps the rendered mesh (piston subtree on/off).
      if (wasBroken != IsBroken) {
        RebuildEngineAnimator();
        ApplyPose();
      }
      // Re-pose when the run state or speed arrives so the cycle plays at the right tempo and the
      // sub-machine is re-synced to match (see ApplyPose).
      else if (
        wasRunning != _running
        || Math.Abs(wasSpeed - AnimationSpeed) > 0.05f
      )
        ApplyPose();
    }
  }

  public override void GetBlockInfo(
    IPlayer forPlayer,
    System.Text.StringBuilder dsc
  ) {
    base.GetBlockInfo(forPlayer, dsc);
    if (!IsConstructed)
      return;

    if (IsBroken) {
      dsc.AppendLine(Lang.Get(LpexLang.EngineInfoBroken));
      return;
    }

    // Inlet pressure against the operating band, plus the steam drawn while running.
    dsc.AppendLine(Lang.Get("lpex:engine-info-clock-" + ClockState));
    if (IsRunning)
      dsc.AppendLine(
        Lang.Get(
          "lpex:engine-info-steam",
          ExMeasure.FlowRate(RunSteamRate, "F0")
        )
      );
    if (_overPressure.IsCounting)
      dsc.AppendLine(
        Lang.Get("lpex:engine-info-overpressure", OverPressureRemaining)
      );
  }

  /// <summary>Animator cache key for the current state. A broken engine renders a distinct
  /// "-broken" mesh with the piston/cylinder subtree hidden, so it must not share the intact key.
  /// The shared helper reads this on every build.</summary>
  private string EngineCacheKey() =>
    IsBroken ? AnimCacheKey + "-broken" : AnimCacheKey;

  /// <summary>Rebuilds the animator through the shared helper with the broken or intact
  /// selective-element set, matched to the cache key from <see cref="EngineCacheKey"/>. Covers the
  /// break/repair mesh swap only; construction-stage re-tessellation goes through the helper
  /// directly and never runs while broken.</summary>
  private void RebuildEngineAnimator() =>
    _animator?.Rebuild(
      IsBroken
        ? GetBrokenSelectiveElements()
        : _animator.Rcc?.shape?.SelectiveElements
    );

  /// <summary>Elements whose subtrees are hidden while the engine is broken (the piston and part of the cylinder).</summary>
  protected virtual string[] BrokenHiddenElements => ["Cube21", "Piston"];

  private string[]? _brokenSelectiveElements;

  /// <summary>
  /// Selective-element list rendering the whole engine except the
  /// <see cref="BrokenHiddenElements"/> subtree, built by walking the shape. Cached.
  /// </summary>
  private string[]? GetBrokenSelectiveElements() {
    if (_brokenSelectiveElements != null)
      return _brokenSelectiveElements;
    if (Api is not ICoreClientAPI capi || Block.Shape?.Base == null)
      return null;

    Shape? shape = ExMeshCache.LoadShape(capi, ExMeshCache.ShapePathOf(Block));
    if (shape?.Elements == null)
      return null;

    var list = new List<string>();
    foreach (var root in shape.Elements)
      if (root.Name != null)
        CollectVisible(root, root.Name, list);
    _brokenSelectiveElements = [.. list];
    return _brokenSelectiveElements;
  }

  // Collects the whole shape except the hidden subtrees. SelectiveElements matches per path
  // segment, and "<path>/*" renders the element plus its subtree: emit one "<path>/*" per maximal
  // clean subtree, and recurse into the non-hidden children of any ancestor of a hidden element
  // (which still renders via the deeper entries naming it as a prefix).
  private void CollectVisible(
    ShapeElement el,
    string path,
    List<string> outList
  ) {
    if (BrokenHiddenElements.Any(broken => broken == el.Name))
      return;
    if (!BrokenHiddenElements.Any(broken => SubtreeContains(el, broken))) {
      outList.Add(path + "/*");
      return;
    }
    if (el.Children != null)
      foreach (var c in el.Children)
        if (c.Name != null)
          CollectVisible(c, path + "/" + c.Name, outList);
  }

  private static bool SubtreeContains(ShapeElement el, string name) {
    if (el.Name == name)
      return true;
    if (el.Children != null)
      foreach (var c in el.Children)
        if (SubtreeContains(c, name))
          return true;
    return false;
  }

  #endregion

  #region Pose

  /// <summary>
  /// True when the sub-machine attached at the engine's sub-machine cell is an
  /// MP generator (which uses the alternate <c>idlemp</c>/<c>cyclemp</c> animations).
  /// </summary>
  private bool IsMPGenerator() {
    if (EngineBlock == null)
      return false;
    BlockPos cell = EngineBlock.SubmachinePos(Pos);
    string path = Api.World.BlockAccessor.GetBlock(cell).Code?.Path ?? "";
    return path.Contains("mpgenerator");
  }

  /// <summary>Refreshes the engine pose. Call after the running state or the sub-machine changes.</summary>
  public void RefreshPose() => ApplyPose();

  /// <summary>Sets whether the engine is actively running (drives the cycle animation).</summary>
  public void SetRunning(bool running) {
    if (_running == running)
      return;
    _running = running;
    ApplyPose();
  }

  /// <summary>
  /// Run state and cycle speed driving the rendered animation. For the MP generator the cycle is
  /// locked frame-for-frame to the axle (see <see cref="DriveMpCycleFrame"/>) and keeps turning
  /// while the flywheel coasts, so the speed returned is nominal and only the turning flag matters.
  /// Every other sub-machine follows the engine's synced
  /// <see cref="IsRunning"/>/<see cref="AnimationSpeed"/>.
  /// </summary>
  private (bool running, float speed) CyclePose() {
    if (IsMPGenerator())
      return (_mpTurning, 1f);
    return (_running, AnimationSpeed);
  }

  /// <summary>
  /// Drives the <c>cyclemp</c> animation from the MP generator's axle: one revolution maps to one
  /// cycle, so the beam and piston stay locked to the visible axle at any speed and keep cycling
  /// while the flywheel coasts. Client-side; pushed every render frame by
  /// <see cref="BlockEntityEngineMPGenerator"/>.
  /// </summary>
  /// <param name="turning">Whether the mechanical network is moving.</param>
  /// <param name="angleRad">The axle's render angle, 0..2π.</param>
  public void DriveMpCycleFrame(bool turning, float angleRad) {
    if (
      Api is not ICoreClientAPI
      || _animator is not { Ready: true }
      || _animator.AnimUtil?.animator is not { } animator
    )
      return;

    // Switch idlemp <-> cyclemp only on a state flip (ApplyPose reads _mpTurning).
    if (turning != _mpTurning) {
      _mpTurning = turning;
      ApplyPose();
    }
    if (!turning)
      return;

    var st = animator.GetAnimationState("cyclemp");
    if (st?.Animation == null)
      return;

    // Frame comes from the axle's absolute angle, not an accumulated delta, so the beam's phase
    // relative to the visible crank is deterministic: the rod's big-end is pinned to the crank, and
    // both rest connected at frame 0 / axle angle 0, tracking in the same direction from there.
    st.CurrentFrame = MPAnim.FrameFromAngle(
      angleRad,
      st.Animation.QuantityFrames
    );
  }

  private void ApplyPose() {
    _animator?.Pose(util => {
      util.StopAnimation("idlepump");
      util.StopAnimation("idlemp");
      util.StopAnimation("cyclepump");
      util.StopAnimation("cyclemp");

      bool mp = IsMPGenerator();
      var (run, speed) = CyclePose();
      string code = run
        ? (mp ? "cyclemp" : "cyclepump")
        : (mp ? "idlemp" : "idlepump");

      util.StartAnimation(
        new AnimationMetaData {
          Animation = code,
          Code = code,
          AnimationSpeed = run ? speed : 1f,
          EaseInSpeed = 3f,
          EaseOutSpeed = 3f,
        }.Init()
      );

      // Start the sub-machine's cycle from the same call so the two start together; it then
      // phase-locks via CycleAnimProgress. No-op for the MP generator, whose motion is the axle
      // that drives this pose instead.
      SubmachineBE?.SyncAnimation(run, speed);
    });
  }

  /// <summary>
  /// Progress (0..1) through the engine's currently-running cycle animation, read by the
  /// attached sub-machine to phase-lock its own cycle. 0 when not animating client-side.
  /// </summary>
  /// <remarks>
  /// Divides by the whole frame count, not the last keyframe's number: the animator's live frame space
  /// is <c>[0, QuantityFrames)</c>, so a frame past the last keyframe would otherwise read above 1.
  /// </remarks>
  public float CycleAnimProgress {
    get {
      var (frame, total) = ReadCycleFrame();
      return total > 1 ? frame / total : 0f;
    }
  }

  /// <summary>Current frame + total frames of the engine's running cycle animation (client-side).</summary>
  private (float frame, int total) ReadCycleFrame() {
    if (
      Api is not ICoreClientAPI
      || _animator?.AnimUtil?.animator is not { } animator
    )
      return (0f, 0);
    string code = IsMPGenerator() ? "cyclemp" : "cyclepump";
    var st = animator.GetAnimationState(code);
    if (st?.Animation == null)
      return (0f, 0);
    return (st.CurrentFrame, st.Animation.QuantityFrames);
  }

  /// <summary>
  /// Fast client tick: fires per-stroke piston sounds as the cycle crosses its up/down keyframes,
  /// and vents cylinder steam while running above break pressure.
  /// </summary>
  private void OnEngineClientTick(float dt) {
    // The MP cycle frame is driven by the generator's axle (see DriveMpCycleFrame); only the run
    // state is read here, for the gear hum and per-stroke sounds.
    var (running, _) = CyclePose();

    if (!running) {
      _lastCycleFrame = -1f;
      StopGearHum();
    } else {
      StartGearHum();

      var (frame, total) = ReadCycleFrame();
      if (total > 1) {
        if (_lastCycleFrame >= 0f) {
          PistonCycleSounds.Fire(
            Api.World,
            Pos,
            _lastCycleFrame,
            frame,
            total,
            SoundVolumeFactor
          );
          // Steam puff out the cylinder top at each power stroke, gated on _running so a flywheel
          // coasting on MP does not puff. Count comes from the per-variant hook.
          if (
            _running
            && CylinderSteamPuffCount > 0
            && PistonCycleSounds.CrossedUpStroke(_lastCycleFrame, frame, total)
          )
            ExParticles.SteamPuff(
              Api.World,
              EngineBlock!.CylinderVentPos(Pos),
              CylinderSteamPuffCount
            );
        }
        _lastCycleFrame = frame;
      }
    }

    // Over break pressure: vent a constant plume independent of stroke timing, throttled so the
    // 50ms tick does not flood particles.
    if (
      _overPressure.IsCounting
      && Api.World.ElapsedMilliseconds - _overSteamMs >= 200
    ) {
      _overSteamMs = Api.World.ElapsedMilliseconds;
      ExParticles.SteamPuff(Api.World, EngineBlock!.CylinderVentPos(Pos), 3);
    }
  }

  /// <summary>Lazily creates and starts the gear hum at the gear housing.</summary>
  private void StartGearHum() {
    _gearSound ??= ExSounds.CreateLoop(
      Api,
      EngineBlock?.GearHousingPos(Pos) ?? Pos,
      ExSounds.PlanetaryGears,
      0.5f,
      16f,
      // Pitched down to sit under the per-stroke piston sounds.
      0.65f
    );
    if (_gearSound is { IsPlaying: false })
      _gearSound.Start();
    // Applied every running tick so a throttle change is heard without restarting the loop.
    _gearSound?.SetVolume(0.5f * SoundVolumeFactor);
    _gearSound?.SetPitch(0.65f * SoundPitchFactor);
  }

  /// <summary>Stops the gear hum (kept allocated so it can resume when the engine restarts).</summary>
  private void StopGearHum() {
    if (_gearSound is { IsPlaying: true })
      _gearSound.Stop();
  }

  /// <summary>Stops and releases the gear hum on block removal/unload.</summary>
  private void DisposeGearHum() {
    _gearSound?.Stop();
    _gearSound?.Dispose();
    _gearSound = null;
  }

  #endregion
}
