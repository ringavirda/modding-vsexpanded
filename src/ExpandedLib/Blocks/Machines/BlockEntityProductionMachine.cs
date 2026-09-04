using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Blocks.Machines;

/// <summary>
/// Base for a block entity whose whole reason to exist is periodic server-side production work - the
/// standalone steam engines and their sub-machines. A machine that is also something else hosts a
/// <see cref="BEBehaviorProductionMachine"/> of its own instead, and a multiblock does it through
/// <see cref="ExpandedLib.Blocks.Structures.BlockEntityMultiblockMachine"/>.
/// <para>
/// The tick lifecycle - registration, the gate, teardown and away-catch-up - is run by the process
/// this class hosts, so a concrete machine writes only its per-tick logic in
/// <see cref="OnProductionTick"/> plus the gate in <see cref="CanRunProduction"/>: the tick runs
/// every <see cref="ProductionTickMs"/> ms, and a <c>false</c> gate routes it to
/// <see cref="OnIdleProductionTick"/> instead. Network access is not part of being a machine - a
/// machine that reads a port calls the <see cref="MachinePorts"/> extensions on itself, exactly as a
/// plain block entity does.
/// </para>
/// </summary>
public abstract class BlockEntityProductionMachine
  : BlockEntity,
    IProductionReadiness {
  private readonly HostProcess _process;

  protected BlockEntityProductionMachine() {
    // Added here because BlockEntity fans both FromTreeAttributes and Initialize out over Behaviors,
    // and a process added any later misses whichever of the two has already run.
    _process = new HostProcess(this);
    Behaviors.Add(_process);
  }

  /// <summary>
  /// This machine's production process. It holds no copy of the machine's answers and reads each one
  /// off the block entity as the tick needs it, so a subclass override and a state change mid-tick are
  /// both seen at once.
  /// </summary>
  private sealed class HostProcess(BlockEntityProductionMachine owner)
    : BEBehaviorProductionMachine(owner) {
    protected override int ProductionTickMs => owner.ProductionTickMs;

    protected override bool AutoStartProduction => owner.AutoStartProduction;

    protected override int MaxAwayCatchupSteps => owner.MaxAwayCatchupSteps;

    protected override float AwayCatchupStepSeconds =>
      owner.AwayCatchupStepSeconds;

    protected override void OnProductionTick(float dt) =>
      owner.OnProductionTick(dt);

    protected override void OnIdleProductionTick(float dt) =>
      owner.OnIdleProductionTick(dt);
  }

  /// <summary>Interval (ms) of the production tick.</summary>
  protected virtual int ProductionTickMs => 1000;

  /// <summary>
  /// Whether the machine is in an operational state this tick (e.g. structure complete, finished
  /// construction). Returning <c>false</c> routes the tick to <see cref="OnIdleProductionTick"/>.
  /// This is the machine's own readiness answer; the process reads it as one publisher among any
  /// carried by behaviours (<see cref="ProductionReadiness"/>), and every one of them must agree.
  /// </summary>
  protected abstract bool CanRunProduction { get; }

  /// <summary>This machine's own readiness answer, for the process and anything else that asks. Not
  /// overridable: a subclass states its gate in <see cref="CanRunProduction"/>, so the two cannot
  /// drift apart.</summary>
  public bool IsReadyToProduce => CanRunProduction;

  /// <summary>
  /// Whether losing readiness also unregisters the production tick. A machine that must keep running
  /// while un-ready overrides this, not <see cref="CanRunProduction"/>: that gate is only consulted by
  /// a listener that still exists, so widening it alone leaves the machine frozen with its state held
  /// rather than stopped.
  /// </summary>
  public virtual bool StopsProductionWhenNotReady => true;

  /// <summary>
  /// Whether the process registers the production tick as soon as the machine loads. Default
  /// <c>true</c> (the machine self-gates each tick). A machine that registers/unregisters the tick
  /// on a state change (e.g. a multiblock that only ticks while complete) overrides this and drives
  /// <see cref="StartProductionTick"/>/<see cref="StopProductionTick"/> itself.
  /// Separate from readiness on purpose: a self-gating machine registers its tick while un-ready and
  /// idles until it is, and taking the answer from readiness would leave it with no listener to notice
  /// the change.
  /// </summary>
  protected virtual bool AutoStartProduction => true;

  /// <summary>Registers the production tick (idempotent, server-side only).</summary>
  protected void StartProductionTick() => _process.StartProductionTick();

  /// <summary>Unregisters the production tick.</summary>
  protected void StopProductionTick() => _process.StopProductionTick();

  #region Away catch-up (game time)

  /// <summary>
  /// How many bounded sub-ticks a machine replays to catch up the game time it spent unloaded. Default
  /// <c>0</c> disables away-catch-up, so the machine simply resumes. When overridden, the away interval
  /// is simulated as up to this many <see cref="AwayCatchupStepSeconds"/> sub-ticks, capping caught-up
  /// game time at <c>MaxAwayCatchupSteps x AwayCatchupStepSeconds</c> seconds; a longer absence is
  /// never replayed in full, which would stall the server and risk a grace-timer leap.
  /// </summary>
  protected virtual int MaxAwayCatchupSteps => 0;

  /// <summary>Sub-tick length (seconds) used while catching up; defaults to one normal tick, so a
  /// caught-up step is just another ordinary tick and needs no extra <c>dt</c> robustness.</summary>
  protected virtual float AwayCatchupStepSeconds => ProductionTickMs / 1000f;

  // The process holds the last-tick stamp and this class persists it: vanilla fans a behaviour's tree
  // into the block entity's own flat tree, so a key written on both sides has one silent winner.
  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetDouble("pm_lastHours", _process.LastTickHours);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    _process.RestoreLastTickHours(tree.GetDouble("pm_lastHours", -1));
  }

  #endregion

  /// <summary>Per-tick production logic; runs server-side only while <see cref="CanRunProduction"/>.</summary>
  protected abstract void OnProductionTick(float dt);

  /// <summary>Runs in place of <see cref="OnProductionTick"/> while the machine is not operational. Default: no-op.</summary>
  protected virtual void OnIdleProductionTick(float dt) { }
}
