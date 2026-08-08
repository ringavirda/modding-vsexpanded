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

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The twin-tub blower: the <b>iron tier's only air source</b>. A mechanically driven pair of bellows
/// that pushes cold air into the blast main, so an iron-age blast furnace can be blown long before the
/// steam tiers exist. Its steam-age successors are lpex's fluid-pump-style sub-machines and smex's
/// engine air blower - this one needs nothing but an axle.
/// <para>
/// It is a <b>pipe node that generates</b> rather than a machine that reaches into a neighbouring
/// network: the principal sits in the blast main itself (the block is a <see cref="BlockNetworkPipe.Blocks.BlockPipe"/>),
/// so it produces into its own network the same way the fluid intake does for water. The mechanical
/// power comes from a <see cref="BEBehaviorMPFillerPort"/> hosted on the footprint's upper-rear cell,
/// so an axle on that face drives it - the same coupling the rolling mill uses.
/// </para>
/// <para>
/// Output is deliberately modest, and it is what draws the line between the iron and steam tiers. A
/// furnace's blast demand is not a constant - it comes from the burden's coke fraction (see
/// <c>BlockEntityFurnaceCore.RequiredBlastPressureFor</c>): coke is the permeable skeleton of the charge
/// column, so a coke-rich burden blows easily but eats fuel and drinks air, while a coke-lean one packs
/// dense and needs far more pressure. <see cref="IwexValues.TwinTubBlowerMaxPressure"/> sits above what
/// a rich burden asks and <b>below what a lean one does</b>, and under the plated pipe's burst rating.
/// </para>
/// <para>
/// So the tier gate is a consequence, not a rule: bellows will run an iron furnace all day on a
/// fuel-hungry charge, and can never run the fuel-efficient charge - that one needs pressure only a
/// steam blower raises, carried by pipe only the steam tier can build. The air also goes in at ambient,
/// so this is cold blast; preheating it needs a cowper, which needs steam as well.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityTwinTubMPBlower : BlockEntityPipe
{
  /// <summary>
  /// Structure-local cell hosting the mechanical-power port, in the block's north frame: the upper-rear
  /// cell of the 1x2x3 footprint. Its port faces (rotation-relative) west, so an axle on that side of
  /// the bellows housing drives them.
  /// </summary>
  private static readonly Vec3i MpPortCell = new(0, 1, 0);

  // Server-side: the axle speed sampled on the last production tick, kept for the HUD readout (the
  // client cannot see the port behaviour's live state).
  private float _lastSpeed;

  private long _blowTickId;

  /// <summary>The placed rotation, read from the block so the port lookup and the footprint agree.</summary>
  private int Angle => (Block as Blocks.BlockTwinTubMPBlower)?.StructureAngle ?? 0;

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    // One blow per second, server-side: the network's own tick is also per second, so the air is
    // produced and then distributed in the same beat.
    if (api.Side == EnumAppSide.Server)
      _blowTickId = RegisterGameTickListener(OnBlowTick, 1000);
  }

  public override void OnBlockRemoved()
  {
    base.OnBlockRemoved();
    if (_blowTickId != 0)
    {
      UnregisterGameTickListener(_blowTickId);
      _blowTickId = 0;
    }
  }

  public override void OnBlockUnloaded()
  {
    base.OnBlockUnloaded();
    if (_blowTickId != 0)
    {
      UnregisterGameTickListener(_blowTickId);
      _blowTickId = 0;
    }
  }

  /// <summary>
  /// Pushes one second of air into this blower's own network, scaled by how fast the axle turns.
  /// Below <see cref="IwexValues.TwinTubBlowerMinSpeed"/> the bellows barely move and deliver nothing;
  /// at or above <see cref="IwexValues.TwinTubBlowerMaxSpeed"/> they deliver the full rate.
  /// </summary>
  private void OnBlowTick(float dt)
  {
    float speed = PortSpeed();
    if (speed != _lastSpeed)
    {
      _lastSpeed = speed;
      MarkDirty();
    }
    ProduceAir(speed, dt);
  }

  /// <summary>
  /// Pushes <paramref name="dt"/> seconds of air into this blower's own network at axle speed
  /// <paramref name="speed"/>, and returns the litres actually produced (0 when the bellows are too slow
  /// or the line is already at the pressure ceiling). Split out of the tick so the balance can be driven
  /// without standing up a mechanical network for the port to read.
  /// </summary>
  public float ProduceAir(float speed, float dt)
  {
    float fraction = SpeedFraction(speed);
    if (fraction <= 0f || dt <= 0f)
      return 0f;
    if (NetworkSystem?.GetNetworkAt(Pos) is not PipeNetwork net)
      return 0f;

    // Cold blast: the air goes in at ambient. Heating it is the cowper's job, which needs steam - so
    // the iron tier is capped at cold blast by construction, not by a rule.
    // TryProduceGas reports only whether it accepted anything, and it clamps at the pressure ceiling,
    // so the litres that actually landed are the change in the pool - not the amount asked for.
    float before = net.State?.Volume ?? 0f;
    net.TryProduceGas(
      IwexValues.TwinTubBlowerOutputPerSecond * fraction * dt,
      AmbientTemperature,
      "Air",
      Api.World.BlockAccessor,
      maxOutputPressure: IwexValues.TwinTubBlowerMaxPressure
    );
    return GameMath.Max(0f, (net.State?.Volume ?? 0f) - before);
  }

  /// <summary>Ambient air temperature at the bellows; falls back to the configured world ambient
  /// (<see cref="ExlibConfig.AmbientTemperature"/>) where no climate is available.</summary>
  private float AmbientTemperature =>
    Api?.World?.BlockAccessor?.GetClimateAt(Pos)?.Temperature
    ?? ExlibValues.AmbientTemperature;

  /// <summary>
  /// How much of the rated output the bellows deliver at <paramref name="speed"/>: 0 at or below the
  /// minimum, 1 at or above the maximum, linear between. Public so the balance can be asserted without
  /// standing up a mechanical network.
  /// </summary>
  public static float SpeedFraction(float speed)
  {
    float min = IwexValues.TwinTubBlowerMinSpeed;
    float max = IwexValues.TwinTubBlowerMaxSpeed;
    if (speed <= min)
      return 0f;
    if (max <= min)
      return 1f;
    return GameMath.Clamp((speed - min) / (max - min), 0f, 1f);
  }

  /// <summary>The driving axle's speed, or 0 when no axle is coupled to the port cell.</summary>
  private float PortSpeed()
  {
    BlockPos cell = ExOrientation.GlobalPos(
      Pos,
      MpPortCell.X,
      MpPortCell.Y,
      MpPortCell.Z,
      Angle
    );
    var port = Api.World.BlockAccessor.GetBlockEntity(cell)
      ?.GetBehavior<BEBehaviorMPFillerPort>();
    return port is { IsTurning: true } ? port.Speed : 0f;
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetFloat("blowerSpeed", _lastSpeed);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    _lastSpeed = tree.GetFloat("blowerSpeed");
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    // The pipe readout first (medium, throughput, pressure), then what the bellows themselves are doing.
    base.GetBlockInfo(forPlayer, dsc);

    float fraction = SpeedFraction(_lastSpeed);
    dsc.AppendLine(
      fraction <= 0f
        ? Lang.Get("iwex:blower-info-idle")
        : Lang.Get(
          "iwex:blower-info-blowing",
          ExMeasure.FlowRate(IwexValues.TwinTubBlowerOutputPerSecond * fraction),
          (int)(fraction * 100f)
        )
    );
  }
}
