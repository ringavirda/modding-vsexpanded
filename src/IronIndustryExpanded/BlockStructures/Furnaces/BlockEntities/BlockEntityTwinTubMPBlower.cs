using System.Text;
using ExpandedLib;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// Mechanically driven pair of bellows that pushes cold ambient air into the blast main, the iron tier's
/// only air source. It is a pipe node that generates rather than a machine feeding a neighbouring network:
/// the block is a <see cref="ExpandedLib.Blocks.Networks.BlockPipe"/> and produces into its own network, the
/// way the fluid intake does for water. Drive comes from a <see cref="BEBehaviorMPFillerPort"/> on the
/// footprint's upper-rear cell.
/// <para>
/// <see cref="IiexValues.TwinTubBlowerMaxPressure"/> sits above the demand of a coke-rich burden and below
/// that of a coke-lean one (see <c>BlockEntityFurnaceCore.RequiredBlastPressureFor</c>), and under the
/// plated pipe's burst rating, so the bellows can blow only the fuel-hungry charge.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityTwinTubMPBlower : BlockEntityPipe {
  /// <summary>
  /// Structure-local cell hosting the mechanical-power port, in the block's north frame: the upper-rear
  /// cell of the 1x2x3 footprint. The port faces west relative to the placed rotation.
  /// </summary>
  private static readonly Vec3i MpPortCell = new(0, 1, 0);

  // Axle speed sampled on the last blow tick. Written server-side and serialized because the client
  // cannot read the port behaviour's live state and needs it for the HUD.
  private float _lastSpeed;

  private long _blowTickId;

  /// <summary>The placed rotation, read from the block so the port lookup and the footprint agree.</summary>
  private int Angle =>
    (Block as Blocks.BlockTwinTubMPBlower)?.StructureAngle ?? 0;

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    // One blow per second, server-side. The network tick runs at the same interval, so air is produced
    // and then distributed in the same beat.
    if (api.Side == EnumAppSide.Server)
      _blowTickId = RegisterGameTickListener(OnBlowTick, 1000);
  }

  public override void OnBlockRemoved() {
    base.OnBlockRemoved();
    if (_blowTickId != 0) {
      UnregisterGameTickListener(_blowTickId);
      _blowTickId = 0;
    }
  }

  public override void OnBlockUnloaded() {
    base.OnBlockUnloaded();
    if (_blowTickId != 0) {
      UnregisterGameTickListener(_blowTickId);
      _blowTickId = 0;
    }
  }

  /// <summary>
  /// Samples the axle and pushes one second of air into the network, scaled by
  /// <see cref="SpeedFraction"/>. Marks dirty only when the sampled speed changed.
  /// </summary>
  private void OnBlowTick(float dt) {
    float speed = PortSpeed();
    if (speed != _lastSpeed) {
      _lastSpeed = speed;
      MarkDirty();
    }
    ProduceAir(speed, dt);
  }

  /// <summary>
  /// Pushes <paramref name="dt"/> seconds of air into this blower's own network at axle speed
  /// <paramref name="speed"/>. Returns the litres actually produced, 0 when the bellows are below
  /// <see cref="IiexValues.TwinTubBlowerMinSpeed"/> or the line is at the pressure ceiling. Public so the
  /// balance can be driven without a mechanical network for the port to read.
  /// </summary>
  public float ProduceAir(float speed, float dt) {
    float fraction = SpeedFraction(speed);
    if (fraction <= 0f || dt <= 0f)
      return 0f;
    if (NetworkSystem?.GetNetworkAt(Pos) is not PipeNetwork net)
      return 0f;

    // Cold blast: the air enters at ambient temperature. Preheating is the cowper's job.
    // TryProduceGas reports only whether it accepted anything and clamps at the pressure ceiling, so the
    // litres that landed are the change in the pool, not the amount asked for.
    float before = net.State?.Volume ?? 0f;
    net.TryProduceGas(
      IiexValues.TwinTubBlowerOutputPerSecond * fraction * dt,
      AmbientTemperature,
      "Air",
      Api.World.BlockAccessor,
      maxOutputPressure: IiexValues.TwinTubBlowerMaxPressure
    );
    return GameMath.Max(0f, (net.State?.Volume ?? 0f) - before);
  }

  /// <summary>Ambient air temperature at the bellows in degrees Celsius, falling back to the configured
  /// world ambient where no climate is available.</summary>
  private float AmbientTemperature =>
    Api?.World?.BlockAccessor?.GetClimateAt(Pos)?.Temperature
    ?? ExlibValues.AmbientTemperature;

  /// <summary>
  /// Fraction of the rated output the bellows deliver at <paramref name="speed"/>: 0 at or below
  /// <see cref="IiexValues.TwinTubBlowerMinSpeed"/>, 1 at or above
  /// <see cref="IiexValues.TwinTubBlowerMaxSpeed"/>, linear between.
  /// </summary>
  public static float SpeedFraction(float speed) {
    float min = IiexValues.TwinTubBlowerMinSpeed;
    float max = IiexValues.TwinTubBlowerMaxSpeed;
    if (speed <= min)
      return 0f;
    if (max <= min)
      return 1f;
    return GameMath.Clamp((speed - min) / (max - min), 0f, 1f);
  }

  /// <summary>The driving axle's speed, or 0 when no axle is coupled to the port cell.</summary>
  private float PortSpeed() {
    BlockPos cell = ExOrientation.GlobalPos(
      Pos,
      MpPortCell.X,
      MpPortCell.Y,
      MpPortCell.Z,
      Angle
    );
    var port = Api
      .World.BlockAccessor.GetBlockEntity(cell)
      ?.GetBehavior<BEBehaviorMPFillerPort>();
    return port is { IsTurning: true } ? port.Speed : 0f;
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetFloat("blowerSpeed", _lastSpeed);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    _lastSpeed = tree.GetFloat("blowerSpeed");
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    // Pipe readout first (medium, throughput, pressure), then the bellows' own state.
    base.GetBlockInfo(forPlayer, dsc);

    float fraction = SpeedFraction(_lastSpeed);
    dsc.AppendLine(
      fraction <= 0f
        ? Lang.Get("iiex:blower-info-idle")
        : Lang.Get(
          "iiex:blower-info-blowing",
          ExMeasure.FlowRate(
            IiexValues.TwinTubBlowerOutputPerSecond * fraction
          ),
          (int)(fraction * 100f)
        )
    );
  }
}
