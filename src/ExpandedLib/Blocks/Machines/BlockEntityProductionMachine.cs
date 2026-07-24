using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Machines;

/// <summary>
/// Base for any block entity that does periodic server-side production work, whether or not it is a
/// multiblock (<see cref="ExpandedLib.Blocks.Structures.BlockEntityMultiblockStructure"/> derives
/// from this; so do the standalone steam engines and their sub-machines). It owns the production
/// tick lifecycle - registration, the operational gate, and teardown - so a concrete machine writes
/// only its per-tick logic in <see cref="OnProductionTick"/> plus the gate in
/// <see cref="CanRunProduction"/>, and reads networks through the inherited port helpers.
///
/// The tick runs every <see cref="ProductionTickMs"/> ms on the server. Each tick is gated by
/// <see cref="CanRunProduction"/>: when it returns <c>false</c> the machine is idle and
/// <see cref="OnIdleProductionTick"/> runs instead (default no-op).
/// </summary>
public abstract class BlockEntityProductionMachine : BlockEntity
{
  private long _productionTickId;

  /// <summary>Interval (ms) of the production tick.</summary>
  protected virtual int ProductionTickMs => 1000;

  /// <summary>
  /// Whether the machine is in an operational state this tick (e.g. structure complete, finished
  /// construction). Returning <c>false</c> routes the tick to <see cref="OnIdleProductionTick"/>.
  /// </summary>
  protected abstract bool CanRunProduction { get; }

  /// <summary>
  /// Whether <see cref="Initialize"/> should register the production tick immediately. Default
  /// <c>true</c> (the machine self-gates each tick). A machine that registers/unregisters the tick
  /// on a state change (e.g. a multiblock that only ticks while complete) overrides this and drives
  /// <see cref="StartProductionTick"/>/<see cref="StopProductionTick"/> itself.
  /// </summary>
  protected virtual bool AutoStartProduction => true;

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    if (api.Side == EnumAppSide.Server && AutoStartProduction)
      StartProductionTick();
  }

  /// <summary>Registers the production tick (idempotent, server-side only).</summary>
  protected void StartProductionTick()
  {
    if (_productionTickId == 0 && Api?.Side == EnumAppSide.Server)
      _productionTickId = RegisterGameTickListener(
        RunProductionTick,
        ProductionTickMs
      );
  }

  /// <summary>Unregisters the production tick.</summary>
  protected void StopProductionTick()
  {
    if (_productionTickId != 0)
    {
      UnregisterGameTickListener(_productionTickId);
      _productionTickId = 0;
    }
  }

  /// <summary>Upper bound on a single production <c>dt</c>, as a multiple of the tick interval. After a
  /// chunk reload or a server hitch the engine can hand us one oversized catch-up <c>dt</c>; an unclamped
  /// grace timer (e.g. the boiler over-pressure burst) would leap its whole window in that single step -
  /// the "detonates right after rejoining" bug. Capping at 2x the interval loses at most ~1 tick of
  /// simulation on a genuine hitch. Away-catch-up (below) replays the unloaded interval as many bounded
  /// sub-ticks, each still passing through this clamp.</summary>
  private const float MaxCatchupTickMultiple = 2f;

  #region Away catch-up (game time)

  // Calendar time (game hours) of the last simulated tick; -1 until the first tick / a fresh machine.
  // Persisted, so on reload the gap to "now" is the game time the machine spent unloaded.
  private double _lastTickHours = -1;
  private bool _pendingCatchup;

  /// <summary>
  /// How many bounded sub-ticks a machine replays to catch up the game time it spent unloaded. Default
  /// <c>0</c> disables away-catch-up (the machine simply resumes, as before). A machine opts in by
  /// overriding this; the away interval is then simulated as up to this many
  /// <see cref="AwayCatchupStepSeconds"/> sub-ticks - so the maximum caught-up game time is
  /// <c>MaxAwayCatchupSteps × AwayCatchupStepSeconds</c> seconds, and a longer absence is capped there
  /// (never replayed in full, which would both stall the server and risk a grace-timer leap).
  /// </summary>
  protected virtual int MaxAwayCatchupSteps => 0;

  /// <summary>Sub-tick length (seconds) used while catching up; defaults to one normal tick, so a
  /// caught-up step is just another ordinary tick and needs no extra <c>dt</c> robustness.</summary>
  protected virtual float AwayCatchupStepSeconds => ProductionTickMs / 1000f;

  private void RunProductionTick(float dt)
  {
    // First tick after a reload: replay the unloaded game-time gap before this real tick.
    if (_pendingCatchup)
    {
      _pendingCatchup = false;
      RunAwayCatchup();
    }

    RunOneTick(dt);
    if (Api?.World != null)
      _lastTickHours = Api.World.Calendar.TotalHours;
  }

  private void RunAwayCatchup()
  {
    if (MaxAwayCatchupSteps <= 0 || _lastTickHours < 0 || Api?.World == null)
      return;

    double away = GameTime.SecondsBetween(
      _lastTickHours,
      Api.World.Calendar.TotalHours
    );
    GameTime.CatchUp(away, AwayCatchupStepSeconds, MaxAwayCatchupSteps, RunOneTick);
  }

  private void RunOneTick(float dt)
  {
    dt = GameMath.Min(dt, ProductionTickMs / 1000f * MaxCatchupTickMultiple);
    if (CanRunProduction)
      OnProductionTick(dt);
    else
      OnIdleProductionTick(dt);
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetDouble("pm_lastHours", _lastTickHours);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    _lastTickHours = tree.GetDouble("pm_lastHours", -1);
    // A saved timestamp means this machine was previously simulated, so the gap to now is unloaded time.
    _pendingCatchup = _lastTickHours >= 0;
  }

  #endregion

  /// <summary>Per-tick production logic; runs server-side only while <see cref="CanRunProduction"/>.</summary>
  protected abstract void OnProductionTick(float dt);

  /// <summary>Runs in place of <see cref="OnProductionTick"/> while the machine is not operational. Default: no-op.</summary>
  protected virtual void OnIdleProductionTick(float dt) { }

  public override void OnBlockRemoved()
  {
    base.OnBlockRemoved();
    StopProductionTick();
  }

  public override void OnBlockUnloaded()
  {
    base.OnBlockUnloaded();
    StopProductionTick();
  }

  #region Network ports

  // NOTE: these forward to the MachinePorts extension methods by their fully-qualified static form.
  // Writing `this.ConnectedNetwork(...)` would bind to THIS instance method (instance methods shadow
  // extension methods), recursing forever - so the static call is required, not stylistic.

  /// <summary>The <typeparamref name="TNet"/> across <paramref name="face"/>, or <c>null</c> if not plumbed in.</summary>
  protected TNet? ConnectedNetwork<TNet>(BlockFacing face)
    where TNet : BlockNetwork =>
    MachinePorts.ConnectedNetwork<TNet>(this, face);

  /// <summary>The <typeparamref name="TNet"/> owning <paramref name="pos"/>, or <c>null</c>.</summary>
  protected TNet? NetworkAt<TNet>(BlockPos pos)
    where TNet : BlockNetwork => MachinePorts.NetworkAt<TNet>(this, pos);

  #endregion
}
