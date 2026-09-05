using ExpandedLib.Blocks;
using ExpandedLib.Blocks.Machines;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace IronIndustryExpanded.BlockStructures.Forming.BlockEntities;

/// <summary>
/// What every mechanical-energy bench in the forming shop has in common: a place on the mpenergy graph, a
/// stroke clock, and the reads that turn the live run into a speed and a torque. A stroke is one bite -
/// something goes under the tooling, the clock runs it down, and the products leave.
/// <para>
/// The benches differ only in what they read the job off. The shear takes a <c>MachineTool</c> and looks
/// its jobs up in the declared table; the fastener benches take a die, which carries its job itself. That
/// split is the design's and it is load-bearing - a bench with no die has no work at all, while a bench
/// with no tool has work it cannot reach. See docs/design/mechanics/machining-line.md § Tooling.
/// </para>
/// </summary>
public abstract class BlockEntityMpBench
  : BlockEntityMachineStation,
    IMpEnergyConsumer,
    IProductionReadiness {
  private readonly HostMembership _membership;

  protected BlockEntityMpBench() {
    // Added in the constructor for the reason the mill's are: BlockEntity fans FromTreeAttributes and
    // Initialize out over Behaviors, so either one added later misses whichever has already run.
    _membership = new HostMembership(this);
    Behaviors.Add(_membership);
    Behaviors.Add(new HostProcess(this));
  }

  /// <summary>The network manager this bench is registered with. Held by the membership, which is what
  /// registers with it; the setter writes through so a fixture can inject a test graph.</summary>
  public BlockNetworkModSystem? NetworkSystem {
    get => _membership.NetworkSystem;
    protected set => _membership.NetworkSystem = value;
  }

  /// <summary>The bench's place on the mechanical-energy graph. It carries no state of its own: a stroke
  /// reads the live network each tick rather than caching a broadcast.</summary>
  private sealed class HostMembership(BlockEntityMpBench owner)
    : BEBehaviorNetworkMember(owner) {
    public override string NetworkType {
      get => "mpenergy";
      protected set { }
    }
  }

  // The stroke advances on its own 250 ms clock rather than the network's 1 s tick, so the tooling reads
  // as continuous motion; the speed it samples is whatever the run settled at on the last tick.
  private const int StrokeTickMs = 250;

  /// <summary>The stroke clock. It keeps no copy of the bench's state and reads the gate off the block
  /// entity each tick, so a piece offered between two ticks is worked on the next one.</summary>
  // no away-catch-up: a stroke is drawn by the run that is turning now, so an absence has nothing to
  // replay. A piece left mid-stroke is found exactly as it was left.
  private sealed class HostProcess(BlockEntityMpBench owner)
    : BEBehaviorProductionMachine(owner) {
    protected override int ProductionTickMs => StrokeTickMs;

    protected override void OnProductionTick(float dt) =>
      owner.AdvanceStroke(dt);
  }

  #region Readiness

  /// <summary>Whether the bench has work this tick. A stroke is the only thing its clock advances.</summary>
  public bool IsReadyToProduce => IsStroking;

  /// <summary>Always <c>false</c>: an idle bench keeps its clock, or a machine that gave the tick up when
  /// it emptied would never finish the next stroke.</summary>
  public bool StopsProductionWhenNotReady => false;

  #endregion

  #region The stroke

  // Seconds of stroke left; 0 means idle. Persisted so a stroke survives a reload.
  private float _remaining;

  /// <summary>Whether a stroke is under way.</summary>
  public bool IsStroking => _remaining > 0f;

  /// <summary>Seconds of stroke left, for the block info readout.</summary>
  public float Remaining => _remaining;

  /// <summary>Starts a stroke of <paramref name="seconds"/>. The caller has already decided the work is
  /// legal; this only arms the clock.</summary>
  protected void BeginStroke(float seconds) => _remaining = seconds;

  /// <summary>Abandons the stroke without completing it, leaving whatever is under the tooling to the
  /// caller.</summary>
  protected void AbandonStroke() => _remaining = 0f;

  /// <summary>
  /// Advances the stroke by <paramref name="dt"/> seconds. A stroke is drawn from a turning run, so a run
  /// that stops holds the tooling where it is rather than finishing the job: it resumes when the run does,
  /// and nothing is lost.
  /// </summary>
  /// <returns>True when the stroke completed on this advance.</returns>
  public bool AdvanceStroke(float dt) {
    if (!IsStroking || Speed <= 0f)
      return false;

    _remaining -= dt;
    if (_remaining > 0f) {
      MarkDirty();
      return false;
    }
    _remaining = 0f;
    CompleteStroke();
    return true;
  }

  /// <summary>What the bench does when the clock runs out. Called with the stroke already cleared.</summary>
  protected abstract void CompleteStroke();

  #endregion

  #region Handing things back

  /// <summary>
  /// The stack a job's output code names, or null when it names nothing. A dead code is a table fault
  /// rather than a machine fault, so the stroke still consumes its input and the miss is logged once.
  /// </summary>
  protected ItemStack? Resolve(string code, int quantity) {
    var location = new AssetLocation(code);
    Item? item = Api!.World.GetItem(location);
    if (item != null)
      return new ItemStack(item, quantity);

    Block? block = Api.World.GetBlock(location);
    if (block != null)
      return new ItemStack(block, quantity);

    Api.Logger.Warning(
      "[iiex] {0} job output '{1}' names no item or block; the stroke produced nothing.",
      Block?.Code?.Path ?? "bench",
      code
    );
    return null;
  }

  /// <summary>Drops <paramref name="stack"/> at the bench, which is how every product and every returned
  /// piece leaves one.</summary>
  protected void Eject(ItemStack? stack) {
    if (stack == null || Api?.World == null || Pos == null)
      return;
    Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
  }

  #endregion

  #region Network

  private MpEnergyNetworkState? RunState =>
    (NetworkSystem?.GetNetworkAt(Pos) as MpEnergyNetwork)?.State;

  /// <summary>Shaft speed of the run this tick. Zero means at rest, which no stored energy overcomes.</summary>
  protected float Speed => RunState?.Speed ?? 0f;

  /// <summary>
  /// Drive torque the run is putting out this tick, recovered as <c>P / ω</c> from the supply power the
  /// network already publishes. Neither a new state field nor a stored-energy proxy is needed:
  /// <c>SupplyPower</c> is written as <c>driveTorque * Speed</c> each tick
  /// (<c>MpEnergyNetworkState.Step</c>), so the division is exact, and a stopped run is refused before this
  /// is read - which is also what keeps it defined.
  /// </summary>
  protected float AvailableTorque =>
    RunState is { Speed: > 0f } run ? run.SupplyPower / run.Speed : 0f;

  /// <summary>What the bench draws off the run: its stroke's own demand while it is working, and nothing
  /// when it is idle. An idle bench is a free passthrough on the line shaft.</summary>
  public abstract float LoadTorque(float speed);

  #endregion

  #region Persistence

  private const string RemainingKey = "benchRemaining";

  // What the shear wrote before the stroke clock moved onto this base. Read as a fallback so a shear
  // caught mid-stroke by the upgrade finishes its cut instead of stranding the piece in its slot with no
  // clock to clear it.
  private const string LegacyShearKey = "shearRemaining";

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetFloat(RemainingKey, _remaining);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    _remaining = tree.GetFloat(RemainingKey, tree.GetFloat(LegacyShearKey));
  }

  #endregion
}
