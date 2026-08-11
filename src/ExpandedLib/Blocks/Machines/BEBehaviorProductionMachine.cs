using ExpandedLib.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Machines;

/// <summary>
/// The periodic work a machine does: a server-side tick on a fixed interval, gated each time by
/// <see cref="CanRunProduction"/>, with a bounded <c>dt</c> and an opt-in replay of the game time the
/// machine spent unloaded. Held as a behaviour so a block entity spends its one base class on what it
/// is - a multiblock, a graph node, a container - rather than on the fact that it runs.
/// See docs/design/mechanics/framework-composition.md.
/// </summary>
public abstract class BEBehaviorProductionMachine(BlockEntity blockentity)
  : BlockEntityBehavior(blockentity) {
  // The tick listener handle; 0 means no tick is registered, which is what the start guard keys on.
  // RegisterGameTickListener also answers 0 for a position the engine refuses to tick at all (the
  // block-preview mini-dimension), so a machine standing there registers nothing and re-attempts on
  // the next explicit start. Nothing loops on that: the same rule refuses every other listener the
  // machine would have, so no tick is left running to re-attempt from.
  private long _productionTickId;

  /// <summary>Interval (ms) of the production tick.</summary>
  protected virtual int ProductionTickMs => 1000;

  /// <summary>
  /// Whether the machine is in an operational state this tick (e.g. structure complete, finished
  /// construction). Answered by the readiness its host publishes, so a process never names the type
  /// that knows. Returning <c>false</c> routes the tick to <see cref="OnIdleProductionTick"/>.
  /// </summary>
  // A host publishes readiness from its own state. One that answered by asking its process back would
  // arrive here again and recurse until the stack goes.
  protected virtual bool CanRunProduction =>
    ProductionReadiness.IsReady(Blockentity);

  /// <summary>
  /// Whether <see cref="Initialize"/> should register the production tick immediately. Default
  /// <c>true</c> (the machine self-gates each tick). A machine that registers/unregisters the tick
  /// on a state change (e.g. a multiblock that only ticks while complete) overrides this and drives
  /// <see cref="StartProductionTick"/>/<see cref="StopProductionTick"/> itself.
  /// </summary>
  protected virtual bool AutoStartProduction => true;

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
    if (api.Side == EnumAppSide.Server && AutoStartProduction)
      StartProductionTick();
  }

  /// <summary>Registers the production tick (idempotent, server-side only).</summary>
  public void StartProductionTick() {
    // The block entity's api rather than this behaviour's: a behaviour is handed one only by its own
    // Initialize, while a block entity can be given one without being initialised - the shape the
    // fixtures use - and a host may drive the tick from either state.
    if (_productionTickId == 0 && Blockentity.Api?.Side == EnumAppSide.Server)
      _productionTickId = Blockentity.RegisterGameTickListener(
        RunProductionTick,
        ProductionTickMs
      );
  }

  /// <summary>Unregisters the production tick.</summary>
  public void StopProductionTick() {
    if (_productionTickId != 0) {
      Blockentity.UnregisterGameTickListener(_productionTickId);
      _productionTickId = 0;
    }
  }

  /// <summary>Upper bound on a single production <c>dt</c>, as a multiple of the tick interval. After a
  /// chunk reload or a server hitch the engine can deliver one oversized catch-up <c>dt</c>, which an
  /// unclamped grace timer (such as the boiler over-pressure burst) would cross in that single step.
  /// Capping at 2x the interval loses at most about one tick of simulation on a genuine hitch.
  /// Away-catch-up below replays the unloaded interval as many bounded sub-ticks, each still passing
  /// through this clamp.</summary>
  private const float MaxCatchupTickMultiple = 2f;

  #region Away catch-up (game time)

  // Calendar time (game hours) of the last simulated tick; -1 on a fresh machine, until its first
  // tick. The host persists it, so on reload the gap to now is the game time the machine spent
  // unloaded.
  private double _lastTickHours = -1;
  private bool _pendingCatchup;

  /// <summary>Calendar time (game hours) of the last simulated tick, or <c>-1</c> before the first one.
  /// The host writes it into its own save tree; a behaviour tree would land in the same flat tree and
  /// one of the two writers would win silently.</summary>
  public double LastTickHours => _lastTickHours;

  /// <summary>Takes <paramref name="hours"/> as the calendar time of the last simulated tick, as read
  /// back from a save. A stamp means the machine was simulated before, so the gap from it to now is
  /// game time spent unloaded and the next tick replays it.</summary>
  public void RestoreLastTickHours(double hours) {
    _lastTickHours = hours;
    _pendingCatchup = hours >= 0;
  }

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

  private void RunProductionTick(float dt) {
    // First tick after a reload: replay the unloaded game-time gap before this real tick.
    if (_pendingCatchup) {
      _pendingCatchup = false;
      RunAwayCatchup();
    }

    RunOneTick(dt);
    if (Blockentity.Api?.World != null)
      _lastTickHours = Blockentity.Api.World.Calendar.TotalHours;
  }

  private void RunAwayCatchup() {
    if (
      MaxAwayCatchupSteps <= 0
      || _lastTickHours < 0
      || Blockentity.Api?.World == null
    )
      return;

    double away = GameTime.SecondsBetween(
      _lastTickHours,
      Blockentity.Api.World.Calendar.TotalHours
    );
    GameTime.CatchUp(
      away,
      AwayCatchupStepSeconds,
      MaxAwayCatchupSteps,
      RunOneTick
    );
  }

  private void RunOneTick(float dt) {
    dt = GameMath.Min(dt, ProductionTickMs / 1000f * MaxCatchupTickMultiple);
    if (CanRunProduction)
      OnProductionTick(dt);
    else
      OnIdleProductionTick(dt);
  }

  #endregion

  /// <summary>Per-tick production logic; runs server-side only while <see cref="CanRunProduction"/>.</summary>
  protected abstract void OnProductionTick(float dt);

  /// <summary>Runs in place of <see cref="OnProductionTick"/> while the machine is not operational. Default: no-op.</summary>
  protected virtual void OnIdleProductionTick(float dt) { }

  /// <summary>
  /// Forgets the tick handle so a later start registers a fresh listener. Both teardown paths do it:
  /// the block entity drops every listener it holds before either call reaches its behaviours, so a
  /// handle kept here would name a listener that no longer exists and block the next start.
  /// <para>
  /// Hosts differ on where they call <c>base</c> in their own teardown, so this runs at either end of
  /// a host's shutdown and assumes nothing about which: zeroing the handle is idempotent and has no
  /// side effect. A machine that must finish work before its tick goes - putting a fire out, flushing
  /// a buffer - does that before calling <c>base</c>, because every listener it holds is already gone
  /// once the call returns.
  /// </para>
  /// </summary>
  public override void OnBlockRemoved() {
    base.OnBlockRemoved();
    StopProductionTick();
  }

  public override void OnBlockUnloaded() {
    base.OnBlockUnloaded();
    StopProductionTick();
  }
}
