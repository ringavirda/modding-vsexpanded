using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks;
using ExpandedLib.Catalogues;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.MechanicalPower;
using ExpandedLib.Machines;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockStructures.Forming.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Forming.BlockEntities;

/// <summary>
/// Block entity for the rolling mill, a consumer on the mechanical-energy network. A pass is a bite of finite
/// length rather than a toggle: the stock is drawn through at roll surface speed (<c>v = ωR</c>), and while it
/// is under the rolls the mill imposes <see cref="LoadTorque"/>. That load is temperature-driven, so cold stock
/// drags ω down and can stall the run mid-pass. See docs/design/machines/rolling-mill.md and
/// docs/design/mechanics/mp-energy.md.
/// </summary>
[BlockEntityRegister]
public class BlockEntityRollingMill
  : BlockEntityMachineStation,
    IMpEnergyConsumer,
    IProductionReadiness {
  private readonly HostMembership _membership;

  public BlockEntityRollingMill() {
    // Both are added here because BlockEntity fans FromTreeAttributes and Initialize out over
    // Behaviors, and either added later misses whichever of the two has already run. The mill's base
    // slot goes to what it is - a container the player fits tooling into - while the two things it
    // has, a graph membership and a pass clock, are behaviours.
    _membership = new HostMembership(this);
    Behaviors.Add(_membership);
    Behaviors.Add(new HostProcess(this));
  }

  /// <summary>The network manager the mill is registered with. Held by the membership, which is what
  /// registers with it; the setter writes through so the fixtures can inject a test graph.</summary>
  public BlockNetworkModSystem? NetworkSystem {
    get => _membership.NetworkSystem;
    protected set => _membership.NetworkSystem = value;
  }

  /// <summary>The mill's place on the mechanical-energy graph. It carries no state of its own: the
  /// mill reads the live network each pass tick rather than caching a broadcast.</summary>
  private sealed class HostMembership(BlockEntityRollingMill owner)
    : BEBehaviorNetworkMember(owner) {
    public override string NetworkType {
      get => "mpenergy";
      protected set { }
    }
  }

  #region Inventory

  /// <summary>The fitted roll set - the tooling that decides what this mill makes.</summary>
  public const int RollSetSlot = 0;

  /// <summary>The piece under the rolls, held only for the duration of a pass.</summary>
  public const int PieceSlot = 1;

  public override string InventoryClassName => "rollingmill";

  protected override MachineSlotSpec[] SlotSpecs =>
    [MachineSlotSpec.Input(IsRollSet, "#6A5A3A"), MachineSlotSpec.AnyInput()];

  /// <summary>Whether the stack is a roll set, i.e. carries a parseable set spec.</summary>
  public static bool IsRollSet(ItemStack? stack) =>
    RollSetSpec.TryParse(
      stack?.Collectible?.Attributes?[RollSetSpec.AttributeKey],
      out _,
      out _
    );

  // The two stacks the mill holds. Slot-backed so the container base carries their persistence and
  // their collectible id mappings; before A3 both were bare fields with all of that hand-rolled.
  // Neither setter marks its slot dirty: every caller already dirties the whole block entity, whose
  // tree carries the inventory subtree, and a slot-level MarkDirty needs an inventory that has been
  // through LateInitialize - which a mill placed but never initialised has not.
  private ItemStack? _piece {
    get => Inventory[PieceSlot].Itemstack;
    set => Inventory[PieceSlot].Itemstack = value;
  }

  private ItemStack? _rollSet {
    get => Inventory[RollSetSlot].Itemstack;
    set => Inventory[RollSetSlot].Itemstack = value;
  }

  #endregion

  // The pass advances on its own 250 ms clock rather than the network's 1 s tick, so motion reads as
  // continuous; the speed it samples is whatever the run settled at on the last network tick.
  private const int PassTickMs = 250;

  /// <summary>
  /// The pass clock. It keeps no copy of the mill's state and reads the gate off the block entity each
  /// tick, so a piece fed between two ticks is drawn on the next one.
  /// </summary>
  // no away-catch-up: the stock is drawn through by the run that is turning now, so an absence has
  // nothing to replay and there is no last-tick stamp worth persisting. A piece left in the rolls is
  // found exactly as it was left, which is what the wrench recovery already assumes.
  private sealed class HostProcess(BlockEntityRollingMill owner)
    : BEBehaviorProductionMachine(owner) {
    protected override int ProductionTickMs => PassTickMs;

    protected override void OnProductionTick(float dt) => owner.OnPassTick(dt);
  }

  #region Readiness

  /// <summary>Whether the mill has work this tick. A pass is the only thing its clock advances, so an
  /// empty stand produces nothing rather than idling through the physics.</summary>
  public bool IsReadyToProduce => IsRolling;

  /// <summary>Always <c>false</c>: an idle mill keeps its clock. Nothing else watches the rolls, so a
  /// stand that gave the tick up when it emptied would never draw the next piece through.</summary>
  public bool StopsProductionWhenNotReady => false;

  #endregion

  // The pass currently under the rolls; _remaining == 0 means idle. Persisted so a bite survives a reload.
  // Neither the draft nor the width is kept: the draft is spent at the bite test and the load the stand puts
  // on its run is a declared demand, so nothing downstream of BeginPass reads the pass's geometry.
  [Persist("rmTemp")]
  private float _tempC; // degrees Celsius

  [Persist("rmRemaining")]
  private float _remaining; // stock still to draw through, in block-space units

  [Persist("rmStalled")]
  private bool _stalled;

  // The reduction this pass will make, held until the piece clears the rolls. Nothing is committed mid-pass,
  // so an interruption cannot leave a piece half-rolled. The gap rather than the gauge it lands on: which
  // of the gap's two rounds this is belongs to the piece, and it is the piece that applies it.
  [Persist("rmPendingGap")]
  private float _pendingGap;

  [Persist("rmPendingSide")]
  private int _pendingSide;

  // Draws the stock on by however far the rolls turned. Reads the live network rather than the cached
  // broadcast, so progress stays in step with the torque balance this mill is loading. The dt is the
  // process's, so it is bounded at twice the pass interval: an unbounded one would draw a whole piece
  // through in a single step after a hitch, and cool it by the same leap.
  private void OnPassTick(float dt) {
    float speed =
      (NetworkSystem?.GetNetworkAt(Pos) as MpEnergyNetwork)?.State?.Speed ?? 0f;
    AdvancePass(dt, speed);
  }

  /// <summary>Whether stock is under the rolls right now.</summary>
  public bool IsRolling => _remaining > 0f;

  /// <summary>Whether the pass is jammed: the run could not carry the load and ω fell to a stop. The pass is
  /// not lost and resumes where it stopped once the run spins back up.</summary>
  public bool IsStalled => _stalled;

  /// <summary>Stock still to draw through, in block-space units. Zero when idle.</summary>
  public float Remaining => _remaining;

  /// <summary>
  /// Puts a piece under the rolls. Lengths and widths are in block-space units,
  /// <paramref name="tempC"/> in degrees Celsius. Refused when the rolls cannot bite it - a draft deeper than
  /// <c>δ_max = μ²R</c>, or cold stock - so an impossible pass is rejected up front instead of stalling the run.
  /// </summary>
  /// <param name="draft">Thickness the chosen gap removes. Read once, at the bite test.</param>
  /// <param name="length">Distance the piece must travel through the rolls.</param>
  /// <param name="tempC">Stock temperature.</param>
  /// <param name="piece">The stack held under the rolls until the pass completes.</param>
  public bool BeginPass(
    float draft,
    float length,
    float tempC,
    ItemStack? piece = null
  ) {
    if (IsRolling || length <= 0f)
      return false;
    if (
      !RollingPass.CanBite(
        draft,
        IiexValues.RollingRollRadius,
        tempC,
        IiexValues.RollingTempC
      )
    )
      return false;

    _piece = piece;
    _tempC = tempC;
    _remaining = length;
    _stalled = false;
    MarkDirty(true);
    return true;
  }

  #region Roll set (the tooling that decides what this mill makes)

  /// <summary>The fitted set's spec, or null when the stand is bare.</summary>
  public RollSetSpec? RollSet =>
    RollSetSpec.TryParse(
      _rollSet?.Collectible?.Attributes?[RollSetSpec.AttributeKey],
      out RollSetSpec? spec,
      out _
    )
      ? spec
      : null;

  /// <summary>Whether a set is fitted.</summary>
  public bool HasRollSet => RollSet != null;

  /// <summary>The stage catalogue this mill reads its routes from - the process-wide one, which every mod's
  /// routes load into. Settable so a fixture can stand a route up without writing to the shared
  /// catalogue.</summary>
  public ProcessRouteRegistry Routes { get; protected set; } =
    ProcessRouteRegistry.Shared;

  /// <summary>
  /// What the fitted set can do to <paramref name="piece"/>: its branch of that stock family's stage
  /// route. Null when the stand is bare, when the tooling will not bite this stock, or when nothing has
  /// declared a stage this set's family accepts.
  /// </summary>
  public MillSchedule? ScheduleFor(WorkPiece? piece) =>
    piece == null ? null : MillSchedule.For(RollSet, piece.Form.Name, Routes);

  /// <summary>
  /// Fits <paramref name="set"/> to the stand and hands back through <paramref name="previous"/> whatever was
  /// there. Refused while stock is under the rolls; the tooling cannot change mid-pass.
  /// </summary>
  public bool TryFitRollSet(ItemStack? set, out ItemStack? previous) {
    previous = null;
    if (IsRolling)
      return false;
    if (
      set != null
      && RollSetSpec.TryParse(
        set.Collectible?.Attributes?[RollSetSpec.AttributeKey],
        out _,
        out _
      )
        is false
    )
      return false;

    previous = _rollSet;
    _rollSet = set;
    MarkDirty(true);
    return true;
  }

  #endregion

  #region Feeding

  /// <summary>
  /// Offers <paramref name="stack"/> to the rolls at gap <paramref name="gapIndex"/> on side
  /// <paramref name="side"/>. On acceptance the piece goes under the rolls and lands on the output deck when
  /// it clears. The verdict is returned on refusal too, so the caller can report which mistake was made.
  /// </summary>
  public FeedDecision TryFeed(ItemStack? stack, int gapIndex, int side) {
    if (IsRolling)
      return new FeedDecision(FeedVerdict.NoReduction, 0f);

    stack = Admit(stack);

    float tempC =
      stack != null && Api != null
        ? stack.Collectible.GetTemperature(Api.World, stack)
        : 0f;

    // Divide the piece for this barrel first: a wide set takes it whole, a narrow one a side at a time.
    WorkPiece? piece = WorkPiece.FromStack(stack);
    if (piece != null && RollSet != null)
      piece = piece.ForSides(
        WorkPiece.SidesFor(piece.Width, RollSet.BarrelWidth)
      );

    MillSchedule? schedule = ScheduleFor(piece);
    FeedDecision decision = MillFeed.Decide(
      RollSet,
      schedule,
      piece,
      gapIndex,
      side,
      tempC,
      IiexValues.RollingRollRadius,
      IiexValues.RollingTempC
    );
    if (!decision.Accepted || piece == null || stack == null)
      return decision;

    // Written back only on acceptance: a refused offer must leave the held stack exactly as it was, or
    // measuring a piece against the wrong barrel would silently drop the round it is part way through.
    piece.ToStack(stack);

    // The reduction is not applied yet: it is committed in CompletePass. An interrupted pass therefore leaves
    // the stock exactly as it went in.
    _pendingGap = schedule!.Gaps[gapIndex];
    _pendingSide = side;

    // The pass is measured at the gauge this round lands on. A piece too wide for the barrel presents a side
    // at a time, and every side is the full length, so the travel is the same whichever side this is.
    float target = piece.RoundTarget(_pendingGap);
    BeginPass(decision.Draft, piece.LengthAt(target), tempC, stack);
    return decision;
  }

  /// <summary>
  /// The stack the rolls should see for <paramref name="offered"/>: itself when it is already a work piece
  /// or nothing admits it, and otherwise the stock item it enters as, at the same heat.
  /// <para>
  /// This is how a product that already exists is re-rolled without a second copy of it being minted -
  /// vanilla's <c>game:rod-iron</c> enters as <c>iiex:stock-rod</c>, so the 30-odd call sites that ask for
  /// a rod by name keep working and an anvil-made rod can be rolled into nail plate. Idempotent, so the
  /// deck mapping and the feed itself may both call it.
  /// </para>
  /// </summary>
  public ItemStack? Admit(ItemStack? offered) {
    if (offered == null || Api == null || WorkPiece.FromStack(offered) != null)
      return offered;

    if (
      StockForm.EntersAs(offered.Collectible?.Code?.ToString())
        is not { } entersAs
      || Api.World.GetItem(new AssetLocation(entersAs)) is not { } item
    )
      return offered;

    // One piece at a time: stock is MaxStackSize 1, and the rolls take one piece per feed however many the
    // player is holding.
    var admitted = new ItemStack(item);
    // The rod comes off an anvil hot and is walked to the mill, so the heat it arrives with is the heat it
    // enters at. Minting it cold would make the first pass refuse on TooCold for no reason the player can see.
    admitted.Collectible.SetTemperature(
      Api.World,
      admitted,
      offered.Collectible!.GetTemperature(Api.World, offered)
    );
    return admitted;
  }

  /// <summary>
  /// Takes a stuck piece back out of the rolls, backing the wrench interaction. The stack is returned
  /// unchanged, since the interrupted reduction was never committed. Returns null when nothing is stuck.
  /// </summary>
  public ItemStack? ReleaseStuckPiece() {
    if (!IsRolling)
      return null;
    ItemStack? piece = _piece;
    _piece = null;
    _remaining = 0f;
    _stalled = false;
    _pendingGap = 0f;
    MarkDirty(true);
    return piece;
  }

  #endregion

  #region Which deck is which (the feed side follows the rolls)

  /// <summary>Whether the run currently turns in reverse, which swaps the two decks.</summary>
  private bool DriveReversed =>
    (NetworkSystem?.GetNetworkAt(Pos) as MpEnergyNetwork)?.State?.Reversed
    ?? false;

  /// <summary>
  /// The deck the piece is fed from. A two-high stand can only be fed from the side the rolls turn towards, so
  /// this follows the drive direction and swaps when the run is reversed.
  /// </summary>
  public BlockPos InputDeck => Deck(DriveReversed);

  /// <summary>The deck the piece lands on, always the opposite one.</summary>
  public BlockPos OutputDeck => Deck(!DriveReversed);

  // The two decks sit either side of the roll stand, at z = +/-1 in the mill's own frame. This is the
  // cell a finished piece lands on; the deck the player feeds from is the whole row - see DeckRow.
  private BlockPos Deck(bool far) =>
    ExOrientation.GlobalPos(
      Pos,
      0,
      0,
      far ? 1 : -1,
      (Block as BlockRollingMill)?.StructureAngle ?? 0
    );

  /// <summary>
  /// Every cell of one deck row: local x from 0 back to <c>-(MillFeed.DeckCells - 1)</c>, rotated into the
  /// placed orientation. The row is how far along the barrel the player can reach, and which gap a click
  /// selects is read from where along it the click landed.
  /// </summary>
  private IEnumerable<BlockPos> DeckRow(bool far) {
    int angle = (Block as BlockRollingMill)?.StructureAngle ?? 0;
    for (int x = 0; x > -MillFeed.DeckCells; x--)
      yield return ExOrientation.GlobalPos(Pos, x, 0, far ? 1 : -1, angle);
  }

  /// <summary>
  /// Whether <paramref name="cell"/> is one of the cells the mill can currently be fed from.
  /// <para>
  /// The whole row, not just the cell beside the stand. <see cref="MillFeed.AlongBarrel"/> maps a click
  /// across all <see cref="MillFeed.DeckCells"/> of it, so accepting only one cell confined every click
  /// to the last third of the barrel - and the widest gap, the only one fresh stock can enter at, sits
  /// in the first third. No click could reach it at any gap count above one.
  /// </para>
  /// </summary>
  public bool IsInputDeck(BlockPos cell) {
    foreach (BlockPos c in DeckRow(DriveReversed))
      if (c.Equals(cell))
        return true;
    return false;
  }

  #endregion

  /// <summary>Pulls the stock back out, abandoning the pass.</summary>
  public void CancelPass() {
    if (!IsRolling)
      return;
    _remaining = 0f;
    _stalled = false;
    _piece = null;
    MarkDirty(true);
  }

  /// <summary>
  /// Applies the reduction the pass just completed and drops the piece onto the output deck. The only place
  /// the stock changes, which is what makes an interrupted pass safe.
  /// </summary>
  private void CompletePass() {
    if (
      _piece != null
      && _pendingGap > 0f
      && WorkPiece.FromStack(_piece) is { } piece
    ) {
      // The branch is recorded with the reduction: a stage is addressed by (thickness, family), so a
      // piece that did not carry the family it was worked on could not be drawn at a fork.
      WorkPiece rolled = piece.Feed(_pendingSide, _pendingGap) with {
        Family = RollSet?.Family ?? piece.Family,
      };
      rolled.ToStack(_piece);
      // Only the feed that completes a round moves the gauge, and only a moved gauge can have arrived at a
      // stopping point.
      if (rolled.Thickness < piece.Thickness)
        ClaimFinishedPiece(rolled);
    }
    _pendingGap = 0f;
    EjectPiece();
  }

  /// <summary>
  /// Swaps the piece for the finished item its stage names, when that stage needs no shear cut. A stage the
  /// route names no code for stays stock and leaves the mill to be cropped elsewhere.
  /// <para>
  /// One piece in, one piece out: a claim changes what the piece is, never how many there are. A stage
  /// whose yield is several items is a shear crop instead, which is what the shear's own table declares.
  /// A half-step is never a stopping point, so a piece can only be claimed where a whole gap has landed.
  /// </para>
  /// </summary>
  private void ClaimFinishedPiece(WorkPiece rolled) {
    if (
      _piece == null
      || Api == null
      || ScheduleFor(rolled)?.OutputAt(rolled.Thickness) is not { } code
    )
      return;

    // A product may be an item or a block, and the set names only its code, so both are tried.
    var loc = new AssetLocation(code);
    ItemStack? finished =
      Api.World.GetItem(loc) is { } item ? new ItemStack(item, _piece.StackSize)
      : Api.World.GetBlock(loc) is { } block
        ? new ItemStack(block, _piece.StackSize)
      : null;
    if (finished == null) {
      Api.Logger.Warning(
        "[iiex] Rolling mill: the stage route names output \"{0}\" at gap {1}, which resolves to no item. "
          + "The piece stays stock.",
        code,
        rolled.Thickness
      );
      return;
    }

    // Heat carries across: the piece came off the rolls hot and the item it became is the same metal at the
    // same moment. Without this a finished piece reads as cold and cannot be worked on.
    finished.Collectible.SetTemperature(
      Api.World,
      finished,
      _piece.Collectible.GetTemperature(Api.World, _piece)
    );
    _piece = finished;
  }

  private void EjectPiece() {
    ItemStack? piece = _piece;
    _piece = null;
    if (piece == null || Api?.Side != EnumAppSide.Server)
      return;

    // Near-zero velocity so the piece settles on the output deck instead of bouncing off behind the mill.
    BlockPos deck = OutputDeck;
    Api.World.SpawnItemEntity(
      piece,
      deck.ToVec3d().Add(0.5, 0.6, 0.5),
      new Vec3d(0, 0.02, 0)
    );
  }

  /// <summary>
  /// The resisting torque this mill imposes on its run. The stand has two states and the load follows them:
  /// zero with the rolls empty, <see cref="IiexValues.RollingLoadTorque"/> with stock between them, times
  /// whatever the stock's flow stress has climbed to as it cooled.
  /// <para>
  /// Independent of <paramref name="speed"/>, unlike the network's friction term which eases off as ω falls.
  /// That asymmetry is what lets a pass drag a run to a stall rather than to a slower equilibrium.
  /// </para>
  /// </summary>
  public float LoadTorque(float speed) =>
    IsRolling
      ? RollingPass.LoadTorque(
        IiexValues.RollingLoadTorque,
        _tempC,
        IiexValues.RollingTempC,
        IiexValues.RollingColdStressMultiplier,
        IiexValues.RollingColdSpanC
      )
      : 0f;

  /// <summary>
  /// Surface area per unit volume of whatever is between the rolls, which is what paces its cooling. A
  /// stand holding nothing readable falls back to <c>1</c>, so the rate is the bare configured one and a
  /// piece is never left uncooled by a state the mill cannot describe.
  /// </summary>
  private float PieceAreaOverVolume =>
    WorkPiece.FromStack(_piece) is { } piece
      ? RollingPass.AreaOverVolume(piece.Width, piece.Thickness)
      : 1f;

  /// <summary>
  /// Advances the bite by how far the rolls turned over <paramref name="dt"/> seconds (<c>v = ωR</c>). A
  /// stopped run makes no progress and flags the pass <see cref="IsStalled"/> rather than losing it.
  /// </summary>
  /// <returns>True when the pass completed on this advance.</returns>
  public bool AdvancePass(float dt, float speed) {
    if (!IsRolling)
      return false;

    // The piece cools whether or not it is moving, so a stall is self-worsening: a stalled piece keeps
    // stiffening and demands more torque the longer it sits. The rate is the piece's own - heat leaves at
    // its surface, so the thinner it is rolled the faster it goes cold, and the last gap of a schedule is
    // the one the player has least time to finish.
    _tempC = RollingPass.Cool(
      _tempC,
      IiexValues.RollingAmbientC,
      IiexValues.RollingCoolRate * PieceAreaOverVolume,
      dt
    );

    // No progress if the drive stops or the stock drops below rolling heat. Either way the pass halts where it
    // is; the reduction is not part-applied.
    float travelled =
      _tempC >= IiexValues.RollingTempC
        ? speed * IiexValues.RollingRollRadius * dt
        : 0f;
    if (travelled <= 0f) {
      if (!_stalled) {
        _stalled = true;
        MarkDirty(true);
      }
      return false;
    }

    _stalled = false;
    _remaining -= travelled;
    if (_remaining > 0f)
      return false;

    _remaining = 0f;
    CompletePass();
    MarkDirty(true);
    return true;
  }

  /// <summary>Drops any piece stuck in the rolls and the fitted roll set, so neither is lost with the
  /// mill.</summary>
  public override void OnBlockBroken(IPlayer? byPlayer = null) {
    EjectPiece();
    if (_rollSet != null && Api?.Side == EnumAppSide.Server) {
      Api.World.SpawnItemEntity(_rollSet, Pos.ToVec3d().Add(0.5, 0.6, 0.5));
      _rollSet = null;
    }
    base.OnBlockBroken(byPlayer);
  }

  #region Block info

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);

    if (!IsRolling) {
      dsc.AppendLine(Lang.Get("iiex:rollingmill-info-idle"));
      return;
    }

    dsc.AppendLine(
      Lang.Get(
        _stalled
          ? "iiex:rollingmill-info-stalled"
          : "iiex:rollingmill-info-rolling",
        ExMeasure.Temperature(_tempC)
      )
    );
  }

  #endregion

  #region Persistence

  // The two stacks are absent from DeclareState on purpose: they are the container's now, written into
  // its "inventory" subtree with their id mappings.
  protected override void DeclareState(ExBlockState state) { }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    MigrateLooseStacks(tree, worldForResolving);
  }

  /// <summary>
  /// Moves a pre-A3 mill's two loose stacks into the inventory. Both were written at the tree root as
  /// <c>rmPiece</c>/<c>rmRollSet</c>; the container writes them under <c>inventory</c> instead, so a
  /// saved mill would otherwise come back with its roll set gone and a piece dropped mid-pass.
  /// <para>
  /// One-way and self-healing: the keys are never written again, so the next save carries only the
  /// container's form and this does nothing thereafter. It runs on the client too, which is harmless -
  /// a synced tree has no such keys.
  /// </para>
  /// </summary>
  private void MigrateLooseStacks(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    Adopt("rmRollSet", RollSetSlot);
    Adopt("rmPiece", PieceSlot);

    void Adopt(string key, int slot) {
      if (tree[key] == null)
        return;
      ItemStack? stack = tree.GetItemstack(key);
      if (stack == null)
        return;
      // A stack read from a tree has no resolved Collectible until it is resolved against the world.
      stack.ResolveBlockOrItem(worldForResolving);
      // Only into an empty slot: a tree carrying both forms means the inventory is already the
      // authority, and overwriting it would restore a stale copy over the live one.
      if (Inventory[slot].Empty)
        Inventory[slot].Itemstack = stack;
    }
  }

  #endregion
}
