using System;
using System.Text;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkEnergy.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

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
/// twin-tub blower and ore mixer use. In-game tuning sets the bridge torque and rated axle speed.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityFlywheel
  : BlockEntityNetworkNode,
    IMpEnergyStorage,
    IMpEnergyProducer
{
  public override string NetworkType
  {
    get => "mpenergy";
    set { }
  }

  private bool IsLarge => Block?.Variant["type"] == "large";

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

  /// <summary>The placed rotation, read from the block so the hub lookup and the footprint agree.</summary>
  private int Angle => (Block as BlockFlywheel)?.StructureAngle ?? 0;

  /// <summary>North-frame hub cells (disc centre) hosting the MP ports; mirrors <see cref="BlockFlywheel"/>'s
  /// footprint. The normal wheel has one; the large wheel has one on each shaft face (z 0 and 1) so an axle
  /// couples from either side.</summary>
  private static readonly (int X, int Y, int Z)[] NormalHubs = [(0, 1, 0)];
  private static readonly (int X, int Y, int Z)[] LargeHubs = [(0, 2, 0), (0, 2, 1)];

  private (int X, int Y, int Z)[] HubCells => IsLarge ? LargeHubs : NormalHubs;

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
