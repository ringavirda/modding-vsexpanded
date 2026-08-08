using System;
using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;

namespace ExpandedLib.Networks;

/// <summary>
/// Concrete <see cref="BlockNetwork"/> for the mechanical-<b>energy</b> system: one shared reservoir that
/// <see cref="IMpEnergyProducer"/> engines fill (pulsed, stroke by stroke), <see cref="IMpEnergyStorage"/>
/// flywheels buffer, and <see cref="IMpEnergyConsumer"/> heavy machines draw in per-op pulses. The physics
/// is <see cref="MpEnergyNetworkState"/>; this class only walks the node set each tick, sums the supply and
/// inertia, integrates, and hands out demand pulses. See <c>docs/design/mp-energy-network.md</c>.
/// <para>
/// Phase-1 scope: the reservoir core (supply → friction → clamp → demand → derive speed) with the surplus
/// shed by the governor (the clamp). The over-speed <b>burst</b>, the vanilla-MP bridge, and pulsed-supply
/// phase live with the flywheel/engine blocks in a later increment. The friction/burst constants are read
/// live from <c>ExlibValues</c> (framework tunables), the same way <see cref="MoltenNetwork"/> reads its
/// flow rate; per-flywheel inertia is content (iwex's <c>FlywheelInertia*</c>) and arrives via the nodes.
/// </para>
/// </summary>
public class MpEnergyNetwork : BlockNetwork
{
  public override string NetworkType => "mpenergy";

  // Framework tunables, read live from config (like MoltenNetwork's flow rate). FrictionCoeff is the windage
  // coefficient (τ ∝ ω), IdleTorque the standing-resistance floor, MaxSpeed the burst speed ω_max (rad/s) that
  // sets capacity = ½·I·ω_max².
  private static float FrictionCoeff => ExlibValues.MpFrictionCoeff;
  private static float IdleTorque => ExlibValues.MpIdleTorque;
  private static float MaxSpeed => ExlibValues.MpMaxSpeed;

  public MpEnergyNetwork(BlockNetworkModSystem system)
    : base(system) { }

  /// <summary>Live reservoir state, or <c>null</c> until the first storage/supply node ticks it. Backed by
  /// the base <see cref="BlockNetwork.State"/> so the typed accessor and base code share one object.</summary>
  public new MpEnergyNetworkState? State
  {
    get => base.State as MpEnergyNetworkState;
    private set => base.State = value;
  }

  public override void RestoreState(object? state) => State = state as MpEnergyNetworkState;

  public override void InheritStateFrom(BlockNetwork source)
  {
    if (source is MpEnergyNetwork other)
      State = other.State;
  }

  #region Tick

  public override void OnTick(IBlockAccessor blockAccessor, float dt, BlockNetworkModSystem manager)
  {
    // One walk of the node set at the current shaft speed: sum the inertia (storage), the drive torque
    // (producers, evaluated on their torque-speed curve at ω), and the load torque (working consumers).
    float speed = State?.Speed ?? 0f;
    float inertia = 0f;
    float driveTorque = 0f;
    float loadTorque = 0f;
    bool reversed = false;

    foreach (var pos in Nodes)
    {
      var be = blockAccessor.GetBlockEntity(pos);
      if (be == null)
        continue;
      if (be is IMpEnergyStorage storage)
        inertia += Math.Max(0f, storage.Inertia);
      if (be is IMpEnergyProducer producer)
        driveTorque += Math.Max(0f, producer.DriveTorque(speed));
      if (be is IMpEnergyConsumer consumer)
        loadTorque += Math.Max(0f, consumer.LoadTorque(speed));
      // Any driver that knows its rotation sets the run's direction; the last one wins, which is fine because
      // two drives fighting each other is already a build error rather than a state to model.
      if (be is IMpEnergyDirection { IsReversed: true })
        reversed = true;
    }

    // A run with no inertia has nothing to spin - drop the reservoir (nothing to simulate).
    if (inertia <= 0f)
    {
      if (State != null)
      {
        State = null;
        BroadcastUpdate(blockAccessor);
      }
      return;
    }

    State ??= new MpEnergyNetworkState();
    State.Inertia = inertia;
    State.Reversed = reversed;

    // Integrate the shaft: ω += (τ_drive − τ_load − τ_fric)/I · dt, clamped to [0, ω_max]. Torque governs -
    // an under-torqued run decelerates to a stall rather than buffering its way to a pulse.
    MpEnergyNetworkState.Step(
      State,
      dt,
      driveTorque,
      loadTorque,
      FrictionCoeff,
      IdleTorque,
      MaxSpeed
    );

    BroadcastUpdate(blockAccessor);
  }

  #endregion

  #region Merge / split

  public override void OnMerge(BlockNetwork other, IBlockAccessor world)
  {
    if (other is not MpEnergyNetwork o || o.State == null)
      return;
    if (State == null)
    {
      State = o.State;
      return;
    }
    // Pool the stored energy and inertia; the derived speed follows. A merged reservoir can hold both runs'
    // energy (capacity = ½·(I₁+I₂)·ω_max²), so there is nothing to clamp beyond the usual capacity ceiling.
    State.Inertia += o.State.Inertia;
    State.StoredEnergy += o.State.StoredEnergy;
    float cap = MpEnergyNetworkState.CapacityFor(State.Inertia, MaxSpeed);
    if (State.StoredEnergy > cap)
      State.StoredEnergy = cap;
    State.Speed = MpEnergyNetworkState.DeriveSpeed(State.StoredEnergy, State.Inertia);
  }

  public override void OnSplitFragment(BlockNetwork original, IBlockAccessor world)
  {
    if (original is not MpEnergyNetwork orig || orig.State == null || orig.State.Inertia <= 0f)
    {
      State = null;
      return;
    }
    // Each fragment keeps its proportional share of the stored energy by inertia fraction; its own inertia
    // is recomputed from its nodes on the next tick, so seed only the energy here.
    float fragInertia = FragmentInertia(world);
    if (fragInertia <= 0f)
    {
      State = null;
      return;
    }
    float share = orig.State.StoredEnergy * (fragInertia / orig.State.Inertia);
    State = new MpEnergyNetworkState
    {
      Inertia = fragInertia,
      StoredEnergy = share,
      Speed = MpEnergyNetworkState.DeriveSpeed(share, fragInertia),
    };
  }

  // Sum this fragment's storage inertia straight off its nodes (used only at split time, before the first tick).
  private float FragmentInertia(IBlockAccessor world)
  {
    float inertia = 0f;
    foreach (var pos in Nodes)
      if (world.GetBlockEntity(pos) is IMpEnergyStorage s)
        inertia += Math.Max(0f, s.Inertia);
    return inertia;
  }

  #endregion
}
