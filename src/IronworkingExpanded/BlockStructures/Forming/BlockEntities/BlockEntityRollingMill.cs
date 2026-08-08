using System.Text;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Forming.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Forming.BlockEntities;

/// <summary>
/// Block entity for the rolling mill, a consumer on the mechanical-energy network. A pass is a bite of finite
/// length rather than a toggle: the stock is drawn through at roll surface speed (<c>v = ωR</c>), and while it
/// is under the rolls the mill imposes <see cref="LoadTorque"/>. That load is temperature-driven, so cold stock
/// drags ω down and can stall the run mid-pass. See docs/design/machines/rolling-mill.md and
/// docs/design/mechanics/mp-energy.md.
/// </summary>
[BlockEntityRegister]
public class BlockEntityRollingMill : BlockEntityNetworkNode, IMpEnergyConsumer {
  public override string NetworkType {
    get => "mpenergy";
    set { }
  }

  private const int PassTickMs = 250;

  // The pass currently under the rolls; _remaining == 0 means idle. Persisted so a bite survives a reload.
  private float _draft;
  private float _width;
  private float _tempC; // degrees Celsius
  private float _remaining; // stock still to draw through, in block-space units
  private bool _stalled;

  // The piece in transit between the two decks, held only for the duration of a pass.
  private ItemStack? _piece;

  // The fitted roll set. Decides what the mill makes; without one the stand cannot roll.
  private ItemStack? _rollSet;

  // The reduction this pass will make, held until the piece clears the rolls. Nothing is committed mid-pass,
  // so an interruption cannot leave a piece half-rolled.
  private float _pendingGap;
  private int _pendingStrip;

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    // The pass advances on its own 250 ms clock rather than the network's 1 s tick, so motion reads as
    // continuous; the speed it samples is whatever the run settled at on the last network tick.
    if (api.Side == EnumAppSide.Server)
      RegisterGameTickListener(OnPassTick, PassTickMs);
  }

  // Draws the stock on by however far the rolls turned. Reads the live network rather than the cached
  // broadcast, so progress stays in step with the torque balance this mill is loading.
  private void OnPassTick(float dt) {
    if (!IsRolling)
      return;
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
  /// <param name="draft">Thickness the chosen gap removes.</param>
  /// <param name="width">Stock width.</param>
  /// <param name="length">Distance the piece must travel through the rolls.</param>
  /// <param name="tempC">Stock temperature.</param>
  /// <param name="piece">The stack held under the rolls until the pass completes.</param>
  public bool BeginPass(
    float draft,
    float width,
    float length,
    float tempC,
    ItemStack? piece = null
  ) {
    if (IsRolling || length <= 0f || width <= 0f)
      return false;
    if (
      !RollingPass.CanBite(
        draft,
        IwexValues.RollingRollRadius,
        tempC,
        IwexValues.RollingTempC
      )
    )
      return false;

    _piece = piece;
    _draft = draft;
    _width = width;
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
  /// Offers <paramref name="stack"/> to the rolls at gap <paramref name="gapIndex"/> on strip
  /// <paramref name="strip"/>. On acceptance the piece goes under the rolls and lands on the output deck when
  /// it clears. The verdict is returned on refusal too, so the caller can report which mistake was made.
  /// </summary>
  public FeedDecision TryFeed(ItemStack? stack, int gapIndex, int strip) {
    if (IsRolling)
      return new FeedDecision(FeedVerdict.NoReduction, 0f);

    float tempC =
      stack != null && Api != null
        ? stack.Collectible.GetTemperature(Api.World, stack)
        : 0f;

    // Re-divide the piece for this barrel first: a wide set takes it whole, a narrow one a side at a time.
    WorkPiece? piece = WorkPiece.FromStack(stack);
    if (piece != null && RollSet != null) {
      piece = piece.Resplit(
        WorkPiece.SidesFor(piece.Width, RollSet.BarrelWidth)
      );
      piece.ToStack(stack!);
    }

    FeedDecision decision = MillFeed.Decide(
      RollSet,
      piece,
      gapIndex,
      strip,
      tempC,
      IwexValues.RollingRollRadius,
      IwexValues.RollingTempC
    );
    if (!decision.Accepted || piece == null || stack == null)
      return decision;

    // The reduction is not applied yet: it is committed in CompletePass. An interrupted pass therefore leaves
    // the stock exactly as it went in.
    _pendingGap = RollSet!.Gaps[gapIndex];
    _pendingStrip = strip;

    float gap = _pendingGap;
    BeginPass(
      decision.Draft,
      piece.StripWidth(gap),
      piece.StripLength(gap),
      tempC,
      stack
    );
    return decision;
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

  // The two decks sit either side of the roll stand, at z = +/-1 in the mill's own frame.
  private BlockPos Deck(bool far) =>
    ExOrientation.GlobalPos(
      Pos,
      0,
      0,
      far ? 1 : -1,
      (Block as BlockRollingMill)?.StructureAngle ?? 0
    );

  /// <summary>Whether <paramref name="cell"/> is the deck the mill can currently be fed from.</summary>
  public bool IsInputDeck(BlockPos cell) => cell.Equals(InputDeck);

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
    )
      piece.Fed(_pendingStrip, _pendingGap).ToStack(_piece);
    _pendingGap = 0f;
    EjectPiece();
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
  /// The resisting torque this mill imposes on its run: zero when idle, otherwise
  /// <see cref="RollingPass.LoadTorque"/> for the current draft, width and stock temperature.
  /// <para>
  /// Independent of <paramref name="speed"/>, unlike the network's friction term which eases off as ω falls.
  /// That asymmetry is what lets a pass drag a run to a stall rather than to a slower equilibrium.
  /// </para>
  /// </summary>
  public float LoadTorque(float speed) =>
    IsRolling
      ? RollingPass.LoadTorque(
        _draft,
        _width,
        IwexValues.RollingRollRadius,
        _tempC,
        IwexValues.RollingTempC,
        IwexValues.RollingColdStressMultiplier,
        IwexValues.RollingColdSpanC,
        IwexValues.RollingTorqueScale
      )
      : 0f;

  /// <summary>
  /// Advances the bite by how far the rolls turned over <paramref name="dt"/> seconds (<c>v = ωR</c>). A
  /// stopped run makes no progress and flags the pass <see cref="IsStalled"/> rather than losing it.
  /// </summary>
  /// <returns>True when the pass completed on this advance.</returns>
  public bool AdvancePass(float dt, float speed) {
    if (!IsRolling)
      return false;

    // The piece cools whether or not it is moving, so a stall is self-worsening: a stalled piece keeps
    // stiffening and demands more torque the longer it sits.
    _tempC = RollingPass.Cool(
      _tempC,
      IwexValues.RollingAmbientC,
      IwexValues.RollingCoolRate,
      dt
    );

    // No progress if the drive stops or the stock drops below rolling heat. Either way the pass halts where it
    // is; the reduction is not part-applied.
    float travelled =
      _tempC >= IwexValues.RollingTempC
        ? speed * IwexValues.RollingRollRadius * dt
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
      dsc.AppendLine(Lang.Get("iwex:rollingmill-info-idle"));
      return;
    }

    dsc.AppendLine(
      Lang.Get(
        _stalled
          ? "iwex:rollingmill-info-stalled"
          : "iwex:rollingmill-info-rolling",
        ExMeasure.Temperature(_tempC)
      )
    );
  }

  #endregion

  #region Persistence

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetFloat("rmDraft", _draft);
    tree.SetFloat("rmWidth", _width);
    tree.SetFloat("rmTemp", _tempC);
    tree.SetFloat("rmRemaining", _remaining);
    tree.SetBool("rmStalled", _stalled);
    tree.SetFloat("rmPendingGap", _pendingGap);
    tree.SetInt("rmPendingStrip", _pendingStrip);
    if (_piece != null)
      tree.SetItemstack("rmPiece", _piece);
    if (_rollSet != null)
      tree.SetItemstack("rmRollSet", _rollSet);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    _draft = tree.GetFloat("rmDraft");
    _width = tree.GetFloat("rmWidth");
    _tempC = tree.GetFloat("rmTemp");
    _remaining = tree.GetFloat("rmRemaining");
    _stalled = tree.GetBool("rmStalled");
    _pendingGap = tree.GetFloat("rmPendingGap");
    _pendingStrip = tree.GetInt("rmPendingStrip");
    _piece = tree.GetItemstack("rmPiece");
    _rollSet = tree.GetItemstack("rmRollSet");
    // A stack read from a tree has no resolved Collectible until it is resolved against the world.
    _piece?.ResolveBlockOrItem(worldForResolving);
    _rollSet?.ResolveBlockOrItem(worldForResolving);
  }

  #endregion
}
