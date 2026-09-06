using System;
using ExpandedLib;
using ExpandedLib.Blocks;
using ExpandedLib.Machines;
using ExpandedLib.Networks;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.MechanicalPower;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockNetworkEnergy.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockNetworkEnergy.BlockEntities;

/// <summary>
/// Block entity for a <see cref="Blocks.BlockTransmission"/>: drives the RightClickConstructable build, holds
/// the finished machine's pose, and each tick reads the south and north mpenergy networks through
/// <c>GetNetworkAt</c> to impose <c>ω_north = ω_south / r</c> without merging them (<see cref="TryCouple"/>).
/// The clutch variant engages and disengages that coupling from its lever cell (<see cref="ToggleEngaged"/>).
/// <para>
/// The coupler is not a graph node and receives no network broadcast: the server records the two side speeds
/// it read and pushes them to clients on a throttled <c>MarkDirty</c>, and the client animates from those.
/// Ratio blocks author one north revolution per clip with the south gear geared up inside it; the clutch
/// drives each shaft from its own side, so a disengaged clutch shows one shaft turning and the other still.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityTransmission : BlockEntityProductionMachine {
  // RCC-suppressed-mesh animator shared by every constructed mega-block; it null-guards poses issued
  // before the animator is built.
  private ConstructedAnimator? _animator;

  // Resolved once: the graph manager the coupler reads the south/north networks from.
  private BlockNetworkModSystem? _networks;

  // Clutch coupling state (persisted). x2/x4 are always coupled; the clutch only transfers while engaged.
  [Persist("engaged")]
  private bool _engaged;

  // The two side speeds (rad/s) the coupler last saw, synced to clients so the gear train can animate; the
  // transmission is not a graph node and gets no network broadcast of its own (see SyncSideSpeeds).
  [Persist("southSpeed")]
  private float _southSpeed;

  [Persist("northSpeed")]
  private float _northSpeed;

  /// <summary>True once the player has finished the three construction stages.</summary>
  public bool IsConstructed => _animator?.IsConstructed ?? false;

  private string Type => Block?.Variant["kind"] ?? "x2";

  /// <summary>Gear reduction south to north: the north (output) run turns at <c>ω_south / Ratio</c>. The
  /// clutch is a straight 1:1 coupling.</summary>
  private float Ratio =>
    Type switch {
      "x2" => 2f,
      "x4" => 4f,
      _ => 1f,
    };

  /// <summary>Whether the two sides are coupled right now: always for the ratio blocks; only when engaged for the
  /// clutch (a disengaged clutch leaves the two runs fully independent).</summary>
  private bool ShouldCouple => Type != "clutch" || _engaged;

  /// <summary>True for the clutch variant (the only one the engage/disengage interaction applies to).</summary>
  public bool IsClutch => Type == "clutch";

  /// <summary>Whether the clutch coupling is engaged; unused by the ratio blocks.</summary>
  public bool IsEngaged => _engaged;

  protected override int ProductionTickMs => 250;

  // ExRightClickConstructable now publishes readiness itself (IProductionReadiness), so the tick
  // already waits for construction; nothing else gates it.
  protected override bool CanRunProduction => true;

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    _networks = api.ModLoader.GetModSystem<BlockNetworkModSystem>();
    _animator = new ConstructedAnimator(
      this,
      () => $"transmission-{Type}-{Block?.Variant["side"]}"
    );
    _animator.Initialize(ApplyPose);
  }

  #region Clutch engage / disengage

  /// <summary>Whether <paramref name="clicked"/> is the clutch's lever cell: the quarter-block filler at the
  /// footprint's <c>(1,1,0)</c>, rotated to this orientation. The interaction is localised there.</summary>
  public bool IsLeverCell(BlockPos clicked) {
    int angle = (Block as BlockTransmission)?.StructureAngle ?? 0;
    return clicked.Equals(ExOrientation.GlobalPos(Pos, 1, 1, 0, angle));
  }

  /// <summary>Throws the clutch lever: toggles the coupling so <see cref="ShouldCouple"/> follows, and re-poses
  /// the lever. Server-side and clutch-only; the pose reaches clients through the save tree.</summary>
  public bool ToggleEngaged() {
    if (Api?.Side != EnumAppSide.Server || !IsClutch)
      return false;
    _engaged = !_engaged;
    MarkDirty(true); // sync the flag + re-pose on clients
    return true;
  }

  #endregion

  #region Animation (the gear train turns at the speed of the runs it couples)

  /// <summary>Re-applies every clip this machine should be holding: the clutch's lever pose and the spin clips at
  /// the current side speeds. Construction stages, lever throws and speed changes all route through here, so no
  /// caller has to know which clips a variant has.</summary>
  private void ApplyPose() {
    if (IsClutch)
      UpdateClutchPose();
    UpdateSpin();
  }

  /// <summary>Swaps the clutch lever between the engaged (<c>connected</c>) and disengaged (<c>disconnected</c>)
  /// poses, stopping the other so they never overlap. The disengaged pose also slides the side shaft out of mesh,
  /// which is why <c>sideshaftcycle</c> must not run alongside it (see <see cref="UpdateSpin"/>).</summary>
  private void UpdateClutchPose() =>
    _animator?.Pose(util => {
      util.StopAnimation(_engaged ? "disconnected" : "connected");
      util.StartAnimation(PoseMeta(_engaged ? "connected" : "disconnected"));
    });

  /// <summary>
  /// Drives the spin clips off the two side speeds. Ratio blocks author one north revolution per <c>cycle</c>
  /// with the south shaft geared up and counter-rotating inside the clip, so the rigid train is one clip played
  /// at ω_north; below the turning threshold they rest on <c>idle</c>, which keeps the RCC-suppressed mesh
  /// visible. The clutch has no rigid train, so <c>mainshaft1cycle</c> runs at ω_north and
  /// <c>mainshaft2cycle</c> at ω_south, and <c>sideshaftcycle</c> only while engaged (disengaged, the lever pose
  /// has moved it out of mesh); it needs no rest pose because the lever pose is always active.
  /// </summary>
  private void UpdateSpin() {
    float max = ExlibValues.MpMaxSpeed;
    bool northTurning = EnergyAnim.IsTurning(_northSpeed, max);
    bool southTurning = EnergyAnim.IsTurning(_southSpeed, max);

    _animator?.Pose(util => {
      if (!IsClutch) {
        Drive(util, "cycle", northTurning, _northSpeed);
        // One of the two must always run, or the suppressed mesh has nothing to render.
        if (northTurning)
          util.StopAnimation("idle");
        else
          util.StartAnimation(PoseMeta("idle"));
        return;
      }

      Drive(util, "mainshaft1cycle", northTurning, _northSpeed);
      Drive(util, "mainshaft2cycle", southTurning, _southSpeed);
      // Engaged, the two sides are held at one speed by the coupling; drive the link off whichever is turning.
      Drive(
        util,
        "sideshaftcycle",
        _engaged && (northTurning || southTurning),
        MathF.Max(_northSpeed, _southSpeed)
      );
    });
  }

  // Runs one spin clip at the playback rate for `omega`, or stops it. A new speed takes effect by restarting an
  // already-running clip, so the throttle in SyncSideSpeeds is what keeps this off every tick.
  private static void Drive(
    BlockEntityAnimationUtil util,
    string clip,
    bool turning,
    float omega
  ) {
    if (!turning) {
      util.StopAnimation(clip);
      return;
    }
    util.StartAnimation(
      new AnimationMetaData {
        Animation = clip,
        Code = clip,
        AnimationSpeed = EnergyAnim.SpinSpeed(omega),
        EaseInSpeed = 3f,
        EaseOutSpeed = 3f,
      }.Init()
    );
  }

  private static AnimationMetaData PoseMeta(string clip) =>
    new AnimationMetaData {
      Animation = clip,
      Code = clip,
      AnimationSpeed = 1f,
      EaseInSpeed = 3f,
      EaseOutSpeed = 3f,
    }.Init();

  /// <summary>
  /// Records the side speeds the coupler just read and pushes them to clients when either has moved by at least
  /// 2% of full scale or has stopped. The transmission is not a graph node and never receives the network's own
  /// broadcast, so this stands in for it; the threshold is what stops a per-tick <c>MarkDirty</c> and a per-tick
  /// clip restart on the client. Returns whether a sync was pushed.
  /// </summary>
  public bool SyncSideSpeeds(float southSpeed, float northSpeed) {
    float step = 0.02f * ExlibValues.MpMaxSpeed;
    bool changed =
      MathF.Abs(southSpeed - _southSpeed) >= step
      || MathF.Abs(northSpeed - _northSpeed) >= step
      || (southSpeed == 0f && _southSpeed != 0f)
      || (northSpeed == 0f && _northSpeed != 0f);

    _southSpeed = southSpeed;
    _northSpeed = northSpeed;
    if (changed)
      MarkDirty(true);
    return changed;
  }

  #endregion

  /// <summary>
  /// Couples the two mpenergy runs across the gear, then publishes the resulting side speeds so the gear train
  /// animates. The speeds are published whether or not a coupling happened, so a disengaged clutch still shows
  /// each shaft turning at its own side's speed.
  /// </summary>
  protected override void OnProductionTick(float dt) {
    TryCouple(dt);
    var (south, north) = ReadSides();
    SyncSideSpeeds(south?.Speed ?? 0f, north?.Speed ?? 0f);
  }

  /// <summary>
  /// Resolves the south (input, +Z) and north (output, −Z) port cells for this orientation and reads the mpenergy
  /// state on each. The transmission is not a graph node, so the two stay separate: this reads two independent
  /// runs, either of which may be absent. Server-only, since the graph lives there.
  /// </summary>
  private (MpEnergyNetworkState? South, MpEnergyNetworkState? North) ReadSides() {
    if (Api?.Side != EnumAppSide.Server || _networks == null)
      return (null, null);

    int angle = (Block as BlockTransmission)?.StructureAngle ?? 0;
    // North-default run axis is Z: the south port faces +Z, the north (output) port −Z.
    BlockPos southPort = ExOrientation.GlobalPos(Pos, 0, 0, 1, angle);
    BlockPos northPort = ExOrientation.GlobalPos(Pos, 0, 0, -1, angle);

    return (
      (_networks.GetNetworkAt(southPort) as MpEnergyNetwork)?.State,
      (_networks.GetNetworkAt(northPort) as MpEnergyNetwork)?.State
    );
  }

  /// <summary>
  /// Projects the two runs onto the gear constraint <c>ω_north = ω_south / Ratio</c> less the mesh loss over
  /// <paramref name="dt"/> seconds. Returns whether a coupling happened: false when disengaged, when a side has
  /// no shaft, or when both ports resolve to the same run (a loop, with nothing to couple).
  /// </summary>
  public bool TryCouple(float dt) {
    if (Api?.Side != EnumAppSide.Server || !ShouldCouple || _networks == null)
      return false;

    int angle = (Block as BlockTransmission)?.StructureAngle ?? 0;
    BlockPos southPort = ExOrientation.GlobalPos(Pos, 0, 0, 1, angle);
    BlockPos northPort = ExOrientation.GlobalPos(Pos, 0, 0, -1, angle);

    if (
      _networks.GetNetworkAt(southPort) is not MpEnergyNetwork south
      || _networks.GetNetworkAt(northPort) is not MpEnergyNetwork north
      || ReferenceEquals(south, north) // a run looped back through the transmission - nothing to couple
      || south.State is not { } southState
      || north.State is not { } northState
    )
      return false;

    float retention = 1f - ExlibValues.MpGearMeshLoss * dt;
    MpEnergyNetworkState.CoupleRatio(
      southState,
      northState,
      Ratio,
      ExlibValues.MpMaxSpeed,
      retention
    );
    return true;
  }

  protected override void DeclareState(ExBlockState state) { }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    bool wasEngaged = _engaged;
    float wasSouth = _southSpeed;
    float wasNorth = _northSpeed;

    base.FromTreeAttributes(tree, worldForResolving);

    // A client receiving a lever throw or a speed sync re-poses. The animator may not exist yet on first load
    // and Initialize applies the initial pose, so only an actual change is acted on here.
    if (Api?.Side != EnumAppSide.Client)
      return;
    if (_engaged != wasEngaged)
      UpdateClutchPose();
    if (
      _engaged != wasEngaged
      || _southSpeed != wasSouth
      || _northSpeed != wasNorth
    )
      UpdateSpin();
  }

  public override void OnBlockRemoved() {
    _animator?.Dispose();
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded() {
    _animator?.Dispose();
    base.OnBlockUnloaded();
  }
}
