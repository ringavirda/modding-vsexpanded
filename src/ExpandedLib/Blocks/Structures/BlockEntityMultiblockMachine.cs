using ExpandedLib.Blocks.Machines;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// A multiblock that also runs a production process: the hand-built pattern of
/// <see cref="BlockEntityMultiblockStructure"/> plus a server-side tick, gated by the readiness that
/// form publishes and started and stopped by its monitor tick. Deriving from this rather than from the
/// form alone is how a multiblock takes the process on, so a structure that only has to be built - a
/// decorative assembly, a housing - costs nothing to express.
/// <para>
/// The tick mechanism is a <see cref="BEBehaviorProductionMachine"/> this class hosts, exactly as
/// <see cref="BlockEntityProductionMachine"/> hosts one for machines that are not multiblocks. A
/// concrete machine writes its per-tick logic in <see cref="OnProductionTick"/> and its gate in
/// <see cref="BlockEntityMultiblockStructure.CanRunProduction"/>.
/// </para>
/// </summary>
public abstract class BlockEntityMultiblockMachine
  : BlockEntityMultiblockStructure {
  private readonly HostProcess _process;

  protected BlockEntityMultiblockMachine() {
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
  private sealed class HostProcess(BlockEntityMultiblockMachine owner)
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

  /// <summary>Whether the production tick is registered as soon as the machine loads. A multiblock
  /// registers it only when the structure is already complete, and the monitor tick starts and stops it
  /// across completion transitions; the answer therefore has to come from the save tree, which
  /// <c>FromTreeAttributes</c> has restored by the time <c>Initialize</c> asks.</summary>
  protected virtual bool AutoStartProduction => StructureComplete;

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

  /// <summary>Per-tick production logic; runs server-side only while
  /// <see cref="BlockEntityMultiblockStructure.CanRunProduction"/>.</summary>
  protected abstract void OnProductionTick(float dt);

  /// <summary>Runs in place of <see cref="OnProductionTick"/> while the machine is not operational. Default: no-op.</summary>
  protected virtual void OnIdleProductionTick(float dt) { }
}
