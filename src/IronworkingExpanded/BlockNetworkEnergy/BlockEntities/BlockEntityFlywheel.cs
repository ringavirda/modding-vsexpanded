using System;
using System.Text;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Renderers;
using IronworkingExpanded.BlockNetworkEnergy.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockNetworkEnergy.BlockEntities;

/// <summary>
/// Block entity for the <see cref="BlockFlywheel"/>: a storage node of the mechanical-energy network that
/// doubles as the <b>vanilla-MP bridge</b>. It contributes <see cref="Inertia"/> (which sets the run's
/// capacity, spin-up, and buffering), and - when an axle is coupled to its hub - it also applies
/// <see cref="DriveTorque"/>, turning water-wheel/windmill/engine torque into shaft spin. The shaft dynamics
/// live in <see cref="MpEnergyNetwork"/>; the base <see cref="BlockEntityNetworkNode"/> handles graph membership.
/// <para>
/// The bridge reuses the mega-block MP-port machinery: each hub footprint cell hosts a
/// <see cref="BEBehaviorMPFillerPort"/> (declared by <see cref="BlockFlywheel"/>'s per-size footprint), which
/// is what actually joins the vanilla MP network and presents a load. This block entity reads back the port
/// axle speed each tick and converts it to a drive torque - the same "read the port speed" pattern the
/// twin-tub blower and the rolling mill use. In-game tuning sets the bridge torque and rated axle speed.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityFlywheel
  : BlockEntityNetworkNode,
    IMpEnergyStorage,
    IMpEnergyProducer,
    IMpEnergyDirection
{
  public override string NetworkType
  {
    get => "mpenergy";
    set { }
  }

  private bool IsLarge => Block?.Variant["size"] == "large";

  /// <summary>Rotational inertia this wheel adds to its run, by size (large ≈ 15× normal). Read live from
  /// iwex config so it can be retuned without a rebuild.</summary>
  public float Inertia =>
    IsLarge ? IwexValues.FlywheelInertiaLarge : IwexValues.FlywheelInertiaNormal;

  /// <summary>The drive torque the bridge applies to the mpenergy shaft this tick: derived from the coupled
  /// axle's speed at the hub port, 0 when nothing drives it. See <see cref="BridgeDriveTorque"/>. The shaft
  /// speed is unused for now (the network's ω_max clamp is the governor); a speed-droop is a later refinement.</summary>
  public float DriveTorque(float shaftSpeed) =>
    BridgeDriveTorque(
      HubAxleSpeed(),
      IwexValues.FlywheelBridgeChargePower,
      IwexValues.FlywheelBridgeRatedAxleSpeed
    );

  /// <summary>
  /// The drive torque for a coupled vanilla-MP axle turning at <paramref name="hubSpeed"/>: linear from 0 up
  /// to <paramref name="maxTorque"/> at <paramref name="ratedHubSpeed"/>, then flat (an over-driven axle never
  /// pushes harder than rated). At rest this is full torque, so the bridge can start a stopped load; if it is
  /// below the load + idle resistance, the shaft never spins up (the "not a battery" gate). Pure, so the bridge
  /// response pins without a mechanical network.
  /// </summary>
  public static float BridgeDriveTorque(
    float hubSpeed,
    float maxTorque,
    float ratedHubSpeed
  ) =>
    ratedHubSpeed <= 0f || hubSpeed <= 0f
      ? 0f
      : maxTorque * GameMath.Clamp(hubSpeed / ratedHubSpeed, 0f, 1f);

  /// <summary>Which way the coupled vanilla axle turns, handed on to the run. The bridge is where direction
  /// enters the mpenergy network at all, since our own speed is unsigned.</summary>
  public bool IsReversed
  {
    get
    {
      if (Api?.World == null)
        return false;
      foreach ((int hx, int hy, int hz) in HubCells)
      {
        BlockPos cell = ExOrientation.GlobalPos(Pos, hx, hy, hz, Angle);
        var port = Api.World.BlockAccessor.GetBlockEntity(cell)
          ?.GetBehavior<BEBehaviorMPFillerPort>();
        if (port is { IsTurning: true })
          return port.IsReversed;
      }
      return false;
    }
  }

  /// <summary>The placed rotation, read from the block so the hub lookup and the footprint agree.</summary>
  private int Angle => (Block as BlockFlywheel)?.StructureAngle ?? 0;

  /// <summary>North-frame hub cells (disc centre) hosting the MP ports; mirrors <see cref="BlockFlywheel"/>'s
  /// footprint. The normal wheel has one; the large wheel has one on each shaft face (z 0 and 1) so an axle
  /// couples from either side.</summary>
  private static readonly (int X, int Y, int Z)[] NormalHubs = [(0, 1, 0)];
  private static readonly (int X, int Y, int Z)[] LargeHubs = [(0, 2, 0), (0, 2, 1)];

  private (int X, int Y, int Z)[] HubCells => IsLarge ? LargeHubs : NormalHubs;

  #region Spin animation (the disc speed IS the charge gauge)

  private ToggleAnimator? _spin;
  private float _posedSpeed = -1f;

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    if (api.Side != EnumAppSide.Client)
      return;
    _spin = new ToggleAnimator(this, BuildAnimator);
    _spin.Initialize(ApplySpin);
  }

  private void BuildAnimator(BEBehaviorAnimatable animatable)
  {
    MeshData mesh = animatable.animUtil.CreateMesh(
      Block.Code.Path,
      null,
      out Shape resolvedShape,
      null,
      new TesselationMetaData()
    );
    animatable.animUtil.InitializeAnimator(
      Block.Code.Path,
      mesh,
      resolvedShape,
      new Vec3f(0, Block.Shape.rotateY, 0)
    );
  }

  /// <summary>
  /// Holds exactly one clip: <c>cycle</c> at the run's speed while the wheel turns, <c>idle</c> at rest.
  /// Keeping one always active is what renders the wheel at all - the animator only suppresses the static
  /// mesh while a clip is running, so dropping both would make the disc flicker back to its unposed shape.
  /// <para>
  /// The clip is authored as one revolution, so its playback rate is the wheel's revolutions per second
  /// (<see cref="EnergyAnim.SpinSpeed"/>) - the disc is then a direct readout of the reservoir's charge,
  /// visible in-world at a glance (docs/design/conventions.md R7: nothing is hidden).
  /// </para>
  /// </summary>
  private void ApplySpin()
  {
    float speed = (_savedNetworkState as MpEnergyNetworkState)?.Speed ?? 0f;
    bool turning = EnergyAnim.IsTurning(speed, ExpandedLib.ExlibValues.MpMaxSpeed);

    _spin?.Pose(util =>
    {
      if (turning)
      {
        util.StopAnimation("idle");
        util.StartAnimation(
          new AnimationMetaData
          {
            Animation = "cycle",
            Code = "cycle",
            AnimationSpeed = EnergyAnim.SpinSpeed(speed),
            EaseInSpeed = 3f,
            EaseOutSpeed = 3f,
          }.Init()
        );
      }
      else
      {
        util.StopAnimation("cycle");
        util.StartAnimation(
          new AnimationMetaData
          {
            Animation = "idle",
            Code = "idle",
            AnimationSpeed = 1f,
            EaseInSpeed = 3f,
            EaseOutSpeed = 3f,
          }.Init()
        );
      }
    });
    _posedSpeed = speed;
  }

  /// <summary>A wrench rotation exchanges the block but keeps this entity, so the animator would keep posing
  /// the pre-rotation shape. Rebuild it, then restore the spin.</summary>
  public override void OnExchanged(Block block)
  {
    base.OnExchanged(block);
    if (Api is not ICoreClientAPI)
      return;
    _spin?.Rebuild();
    ApplySpin();
  }

  #endregion

  /// <summary>Fastest axle speed among the hub ports, or 0 when none is turning (the two large-wheel hubs
  /// are the same shaft line, so an axle drives whichever side it is coupled to).</summary>
  private float HubAxleSpeed()
  {
    if (Api?.World == null)
      return 0f;
    float best = 0f;
    foreach ((int hx, int hy, int hz) in HubCells)
    {
      BlockPos cell = ExOrientation.GlobalPos(Pos, hx, hy, hz, Angle);
      var port = Api.World.BlockAccessor.GetBlockEntity(cell)
        ?.GetBehavior<BEBehaviorMPFillerPort>();
      if (port is { IsTurning: true } && port.Speed > best)
        best = port.Speed;
    }
    return best;
  }

  #region Block-info (the run's charge / speed / power, synced to clients)

  // The network broadcast only reaches server BEs, so the run's state is round-tripped onto this BE's tree
  // and pushed to clients on a throttled MarkDirty - the look-at HUD then reads it from _savedNetworkState.
  private float _lastSyncedSpeed = -1f;

  protected override void SerializeNetworkState(ITreeAttribute tree, object? state)
  {
    if (state is not MpEnergyNetworkState s)
      return;
    tree.SetFloat("mpSpeed", s.Speed);
    tree.SetFloat("mpInertia", s.Inertia);
    tree.SetFloat("mpEnergy", s.StoredEnergy);
    tree.SetFloat("mpSupply", s.SupplyPower);
    tree.SetFloat("mpDemand", s.DemandPower);
  }

  protected override object? DeserializeNetworkState(ITreeAttribute tree) =>
    tree.HasAttribute("mpSpeed")
      ? new MpEnergyNetworkState
      {
        Speed = tree.GetFloat("mpSpeed"),
        Inertia = tree.GetFloat("mpInertia"),
        StoredEnergy = tree.GetFloat("mpEnergy"),
        SupplyPower = tree.GetFloat("mpSupply"),
        DemandPower = tree.GetFloat("mpDemand"),
      }
      : null;

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving); // refreshes _savedNetworkState
    // The throttled sync below is what reaches clients; re-pose off it rather than adding a second channel,
    // so the disc's speed and the block-info readout can never disagree.
    if (
      Api?.Side == EnumAppSide.Client
      && (_savedNetworkState as MpEnergyNetworkState)?.Speed is { } speed
      && speed != _posedSpeed
    )
      ApplySpin();
  }

  public override void OnNetworkUpdate(object? state)
  {
    base.OnNetworkUpdate(state);
    if (Api?.Side != EnumAppSide.Server || state is not MpEnergyNetworkState s)
      return;
    // Sync only when the displayed speed shifts by ≥2% of full scale (or on a stop), not every tick.
    float step = 0.02f * ExpandedLib.ExlibValues.MpMaxSpeed;
    if (
      _lastSyncedSpeed < 0f
      || MathF.Abs(s.Speed - _lastSyncedSpeed) >= step
      || (s.Speed == 0f && _lastSyncedSpeed != 0f)
    )
    {
      _lastSyncedSpeed = s.Speed;
      MarkDirty(true);
    }
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);

    if (_savedNetworkState is not MpEnergyNetworkState s || s.Inertia <= 0f)
    {
      dsc.AppendLine(Lang.Get("iwex:flywheel-info-idle"));
      return;
    }

    float capacity = MpEnergyNetworkState.CapacityFor(
      s.Inertia,
      ExpandedLib.ExlibValues.MpMaxSpeed
    );
    dsc.AppendLine(
      Lang.Get("iwex:flywheel-info-charge", ExMeasure.Charge(s.StoredEnergy, capacity))
    );
    dsc.AppendLine(Lang.Get("iwex:flywheel-info-speed", ExMeasure.Speed(s.Speed)));
    if (s.SupplyPower > 1f || s.DemandPower > 1f)
      dsc.AppendLine(
        Lang.Get(
          "iwex:flywheel-info-power",
          ExMeasure.Power(s.SupplyPower),
          ExMeasure.Power(s.DemandPower)
        )
      );
  }

  #endregion
}
