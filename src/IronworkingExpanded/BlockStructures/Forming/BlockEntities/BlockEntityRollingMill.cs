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
/// Block entity for the rolling mill: the <b>first consumer</b> of the mechanical-energy network
/// (<c>docs/design/mp-energy-network.md</c>). Everything upstream - the flywheel's stored inertia, the shaft
/// run, the transmissions' ratios - exists to deliver the torque a pass demands here, so this is where that
/// whole chain finally becomes visible to a player.
/// <para>
/// A pass is a <b>bite of finite length</b>, not a toggle: the stock is drawn through at the roll surface speed
/// (<c>v = ωR</c>), so a strong run rolls it briskly and a labouring one crawls. While it is under the rolls the
/// mill imposes <see cref="LoadTorque"/> (<see cref="RollingPass"/>), which is temperature-driven - cold stock
/// resists far harder, drags ω down, and can stall the run mid-pass. That is the design's "keep it hot or it
/// jams" loop, and it is emergent rather than special-cased: the load simply outweighs the drive.
/// </para>
/// <para>
/// Scope: the pass <em>load and progress</em> are live, so the network has real demand end to end. The roll-set
/// tooling item, work-item form/thickness tracking and the staged render are the next increment; until they land
/// a pass is begun through <see cref="BeginPass"/> and reports itself in block info.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityRollingMill : BlockEntityNetworkNode, IMpEnergyConsumer
{
  public override string NetworkType
  {
    get => "mpenergy";
    set { }
  }

  private const int PassTickMs = 250;

  // The pass currently under the rolls; _remaining == 0 means idle. Persisted so a bite survives a reload.
  private float _draft;
  private float _width;
  private float _tempC;
  private float _remaining; // stock still to draw through, in block-space units
  private bool _stalled;

  // The piece itself, in transit between the two decks. It is only ever here for the few seconds a pass takes -
  // the rest of the time it is on the ground or in the player's hands, because a two-high stand cannot be fed
  // backwards and the piece has to be carried back around.
  private ItemStack? _piece;

  // The fitted roll set. Swapping it is what changes what the mill makes, so it is the machine's one real
  // configuration - and without one the stand has nothing to roll with.
  private ItemStack? _rollSet;

  // The reduction this pass will make, held until the piece actually clears the rolls. Nothing is committed
  // mid-pass, so an interruption can never leave a piece half-rolled.
  private float _pendingGap;
  private int _pendingStrip;

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    // The pass advances on its own clock rather than the network's 1 s tick, so a bite reads as continuous
    // motion; the speed it reads is whatever the run settled at on the last network tick.
    if (api.Side == EnumAppSide.Server)
      RegisterGameTickListener(OnPassTick, PassTickMs);
  }

  // Draw the stock on by however far the rolls turned. Reading the live network (rather than the cached
  // broadcast) keeps the progress in step with the torque balance the mill itself is loading.
  private void OnPassTick(float dt)
  {
    if (!IsRolling)
      return;
    float speed =
      (NetworkSystem?.GetNetworkAt(Pos) as MpEnergyNetwork)?.State?.Speed ?? 0f;
    AdvancePass(dt, speed);
  }

  /// <summary>Whether stock is under the rolls right now.</summary>
  public bool IsRolling => _remaining > 0f;

  /// <summary>Whether the pass is jammed - the run could not carry the load and ω fell to a stop. The pass is
  /// <b>not</b> lost; it resumes where it stopped once the run spins back up.</summary>
  public bool IsStalled => _stalled;

  /// <summary>Stock still to draw through, in block-space units. Zero when idle.</summary>
  public float Remaining => _remaining;

  /// <summary>
  /// Puts a piece under the rolls: <paramref name="draft"/> is the reduction this gap takes,
  /// <paramref name="width"/> the stock width, <paramref name="length"/> how far it must travel, and
  /// <paramref name="tempC"/> its temperature. Refused when the rolls cannot <b>bite</b> it - too deep a draft
  /// for <c>δ_max = μ²R</c>, or stock gone cold - so an impossible pass is rejected up front rather than
  /// silently stalling the whole run.
  /// </summary>
  public bool BeginPass(
    float draft,
    float width,
    float length,
    float tempC,
    ItemStack? piece = null
  )
  {
    if (IsRolling || length <= 0f || width <= 0f)
      return false;
    if (!RollingPass.CanBite(draft, IwexValues.RollingRollRadius, tempC, IwexValues.RollingTempC))
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
  /// Fits <paramref name="set"/> to the stand, handing back whatever was there. Refused while stock is under
  /// the rolls - you cannot change the tooling mid-pass.
  /// </summary>
  public bool TryFitRollSet(ItemStack? set, out ItemStack? previous)
  {
    previous = null;
    if (IsRolling)
      return false;
    if (set != null && RollSetSpec.TryParse(set.Collectible?.Attributes?[RollSetSpec.AttributeKey], out _, out _) is false)
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
  /// <paramref name="strip"/>. On acceptance the strip is reduced, the updated piece goes under the rolls, and
  /// it will land on the output deck when it clears. The verdict is returned either way so the caller can tell
  /// the player <em>why</em> nothing happened - a skid and a too-wide gap are very different mistakes.
  /// </summary>
  public FeedDecision TryFeed(ItemStack? stack, int gapIndex, int strip)
  {
    if (IsRolling)
      return new FeedDecision(FeedVerdict.NoReduction, 0f);

    float tempC =
      stack != null && Api != null
        ? stack.Collectible.GetTemperature(Api.World, stack)
        : 0f;

    // Re-divide the piece for THIS barrel first: a wide set takes it whole, a narrow one a side at a time.
    WorkPiece? piece = WorkPiece.FromStack(stack);
    if (piece != null && RollSet != null)
    {
      piece = piece.Resplit(WorkPiece.SidesFor(piece.Width, RollSet.BarrelWidth));
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

    // The reduction is NOT applied yet. The metal only reaches the gap once it has actually been through, so a
    // pass that is interrupted - power lost, or the piece gone cold - leaves the stock exactly as it went in.
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
  /// Takes a stuck piece back out of the rolls - the wrench interaction. Its state is <b>unchanged</b>: an
  /// interrupted reduction never happened, so the player starts that gap over rather than getting a
  /// half-rolled piece. Returns the stack, or null when there is nothing stuck.
  /// </summary>
  public ItemStack? ReleaseStuckPiece()
  {
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

  /// <summary>Whether the run currently turns in reverse, which swaps the mill end for end.</summary>
  private bool DriveReversed =>
    (NetworkSystem?.GetNetworkAt(Pos) as MpEnergyNetwork)?.State?.Reversed ?? false;

  /// <summary>
  /// The deck the piece is fed from. The rolls only turn one way, so a two-high stand can only be fed from one
  /// side - and which side that is follows the drive. Reverse the drive and the mill runs the other way, which
  /// is exactly why a reversing mill was worth building: it spares the crew the walk back around.
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
  public void CancelPass()
  {
    if (!IsRolling)
      return;
    _remaining = 0f;
    _stalled = false;
    _piece = null;
    MarkDirty(true);
  }

  /// <summary>
  /// Drops the finished piece onto the output deck: it comes out the far side of the stand and lands there for
  /// the player to walk round and collect. A near-zero velocity keeps it on the deck rather than bouncing off
  /// into whatever is behind the mill - it is still a loose item you have to go and fetch, just not a lottery.
  /// </summary>
  // Applies the reduction the pass just completed, then drops the piece. This is the only place the stock
  // changes, which is what makes every interruption safe.
  private void CompletePass()
  {
    if (_piece != null && _pendingGap > 0f && WorkPiece.FromStack(_piece) is { } piece)
      piece.Fed(_pendingStrip, _pendingGap).ToStack(_piece);
    _pendingGap = 0f;
    EjectPiece();
  }

  private void EjectPiece()
  {
    ItemStack? piece = _piece;
    _piece = null;
    if (piece == null || Api?.Side != EnumAppSide.Server)
      return;

    BlockPos deck = OutputDeck;
    Api.World.SpawnItemEntity(
      piece,
      deck.ToVec3d().Add(0.5, 0.6, 0.5),
      new Vec3d(0, 0.02, 0)
    );
  }

  /// <summary>
  /// The resisting torque this mill imposes on its run: zero when idle, otherwise the full
  /// <see cref="RollingPass.LoadTorque"/> for the draft, width and stock temperature.
  /// <para>
  /// Deliberately <b>independent of <paramref name="speed"/></b>: plastic deformation resists the same however
  /// fast the rolls turn, unlike the network's friction term which eases off as ω falls. That asymmetry is what
  /// lets a pass drag a run all the way to a stall instead of settling at a slower equilibrium.
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
  /// Advances the bite by how far the rolls turned this tick (<c>v = ωR</c>), so a pass takes real time and a
  /// labouring run visibly crawls through it. A stopped run makes no progress and the pass is flagged
  /// <see cref="IsStalled"/> rather than lost. Returns whether the pass completed on this advance.
  /// </summary>
  public bool AdvancePass(float dt, float speed)
  {
    if (!IsRolling)
      return false;

    // The piece cools whether or not it is moving - which is what makes a jam self-worsening: a stalled piece
    // keeps stiffening, so the longer it sits the more torque it needs, and past the bite threshold the answer
    // is the reheat furnace rather than more power.
    _tempC = RollingPass.Cool(
      _tempC,
      IwexValues.RollingAmbientC,
      IwexValues.RollingCoolRate,
      dt
    );

    // Frozen if the drive stops OR the stock drops below rolling heat: either way the pass simply halts where
    // it is, and the player restarts the line or wrenches the piece out. There is no partial credit.
    float travelled =
      _tempC >= IwexValues.RollingTempC ? speed * IwexValues.RollingRollRadius * dt : 0f;
    if (travelled <= 0f)
    {
      if (!_stalled)
      {
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

  /// <summary>A piece jammed in the rolls is handed back rather than lost when the mill is broken.</summary>
  public override void OnBlockBroken(IPlayer? byPlayer = null)
  {
    EjectPiece();
    if (_rollSet != null && Api?.Side == EnumAppSide.Server)
    {
      Api.World.SpawnItemEntity(_rollSet, Pos.ToVec3d().Add(0.5, 0.6, 0.5));
      _rollSet = null;
    }
    base.OnBlockBroken(byPlayer);
  }

  #region Block info

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);

    if (!IsRolling)
    {
      dsc.AppendLine(Lang.Get("iwex:rollingmill-info-idle"));
      return;
    }

    dsc.AppendLine(
      Lang.Get(
        _stalled ? "iwex:rollingmill-info-stalled" : "iwex:rollingmill-info-rolling",
        ExMeasure.Temperature(_tempC)
      )
    );
  }

  #endregion

  #region Persistence

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
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
  )
  {
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
    // A stack loaded from a tree has no resolved Collectible until it is re-resolved against the world.
    _piece?.ResolveBlockOrItem(worldForResolving);
    _rollSet?.ResolveBlockOrItem(worldForResolving);
  }

  #endregion
}
