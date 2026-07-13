using System;
using System.Text;
using ExpandedLib.Blocks.Construction;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.OreBunker.BlockEntities;
using IronworkingExpanded.BlockStructures.OreMixer.Blocks;
using IronworkingExpanded.Compat;
using IronworkingExpanded.Items;
using IronworkingExpanded.Rendering;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.OreMixer.BlockEntities;

/// <summary>
/// The 3×2×1 ore mixer. Like the bunker it is a <c>RightClickConstructable</c> mega-block that
/// renders through a permanent <c>idle</c> animation re-tessellated to the built elements. Its two
/// upper-side footprint cells host an <see cref="BEBehaviorMPFillerPort"/> each (west + east), so the
/// mixer is driven by a mechanical axle on either side; the visible <c>Rotor</c> element is
/// <b>phase-locked</b> to the driving axle - every render frame the rotor's <c>cycle</c> animation
/// frame is advanced by the network's rotation angle (<see cref="MPAnim.AdvanceFrame"/>), so one
/// revolution of the axle turns the rotor exactly once and the two never drift apart.
/// <para>
/// Gameplay is a small state machine: right-clicking with crushed iron / lime / crushed coke charges
/// the matching raw part; while the rotor is driven the charge mixes (time scales with its size); a
/// finished batch becomes burden stamped with the charge's proportions (its grade). Right-clicking the
/// principal empty-handed opens the <c>open</c> lid pose and drains that burden down into the container
/// directly below (a bunker, crate or chest). An off-spec burden can be right-clicked back in to an
/// un-drained mixer to retune by adding more material.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityOreMixer : BlockEntity, IRenderer
{
  // MP intake cells in the principal's north layout: upper-left couples west, upper-right couples east.
  private static readonly Vec3i[] PortOffsets =
  [
    new Vec3i(-1, 1, 0),
    new Vec3i(1, 1, 0),
  ];

  // Owns the RCC-suppressed-mesh animator triad (shared by every constructed mega-block). See
  // ConstructedAnimator: it holds the _animatorReady null-guard so a pose can never NRE.
  private ConstructedAnimator? _animator;
  private ICoreClientAPI? _capi;

  // Visual heap of the charge/finished burden inside the bowl: a flat ore surface rising with how
  // full the mixer is. Client only.
  private OreSurfaceRenderer? _oreRenderer;

  // The charge visibly fills the upper bowl between these pixel heights (y=16..28 of the 2-tall block).
  private const float OreSurfaceYMin = 16f / 16f;
  private const float OreSurfaceYMax = 28f / 16f;

  // Lids: true while draining (the open pose is held); persisted so the pose survives reload.
  private bool _draining;

  // Rotor phase-lock state (client render only): whether the cycle animation is currently running.
  private bool _cycleRunning;

  // Gameplay state machine (server-authoritative, synced):
  //   EMPTY -> raw accumulating (_iron/_flux/_fuel) -> powered mixing (_mixProgress 0..1) ->
  //   READY burden buffer (_burdenCount + _burdenMix) -> draining into the container below -> EMPTY.
  // Raw is the unmixed charge whose proportions become the burden grade; the buffer is the finished
  // burden waiting to drain. Adding raw is only allowed while no finished burden is waiting.
  // _fuel is the carbon part in coke-equivalent VALUE (coke = 1.0/item, charcoal = a fraction), so it
  // is a float; iron and flux are plain item counts.
  private int _iron;
  private int _flux;
  private float _fuel;
  // Mixing completion 0..1. Stored as a fraction (not elapsed seconds) because the required time
  // depends on the live axle speed, which can change mid-batch.
  private float _mixProgress;
  private int _burdenCount;
  private BurdenMix _burdenMix;

  private long _tickId;
  private Item? _burdenItem;

  // Render before the opaque pass so the rotor frame is set in step with the axle.
  public double RenderOrder => 0.0;
  public int RenderRange => 64;

  /// <summary>True once the player has finished the construction stages.</summary>
  public bool IsConstructed => _animator?.IsConstructed ?? false;

  private int Angle => (Block as BlockOreMixer)?.StructureAngle ?? 0;

  #region Lifecycle

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    // The animator (and IsConstructed) is resolved on both sides; it only builds/poses on the client.
    _animator = new ConstructedAnimator(this, () => AnimCacheKey);
    _animator.Initialize(ApplyPose);

    if (api.Side == EnumAppSide.Server)
    {
      _burdenItem = api.World.GetItem(new AssetLocation("iwex", "burden"));
      // Drives mixing (while powered) and draining; cheap, so a slow 250 ms tick is plenty.
      _tickId = RegisterGameTickListener(OnServerTick, 250);
    }

    if (api is ICoreClientAPI capi)
    {
      _capi = capi;
      // Drives the rotor's cycle frame off the axle angle every render frame.
      capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "iwex-mixer-rotor");

      InitOreRenderer(capi);
      UpdateOreLevel();
    }
  }

  #region Ore surface render

  /// <summary>Interior footprint of the charge, north-default frame (3 wide × 1 deep), inset 1px from
  /// the bowl walls. Rotated by the structure angle to match the placed orientation.</summary>
  private static readonly Cuboidf[] OreSurfaceBoxes =
  [
    new Cuboidf(-15f, 16f, 1f, 31f, 28f, 15f),
  ];

  private void InitOreRenderer(ICoreClientAPI capi)
  {
    // Boxes are in the structure-offset frame, so they rotate by the same structure angle the fillers
    // use (the mixer's StructureAngle = AngleFromSide, no +180), not Shape.rotateY.
    float rot = (float)(Angle * Math.PI / 180.0);
    _oreRenderer = new OreSurfaceRenderer(
      Pos,
      capi,
      OreSurfaceBoxes,
      rot,
      OreSurfaceYMin,
      OreSurfaceYMax,
      new AssetLocation("game:textures/block/coal/orecoalmix.png")
    );
    capi.Event.RegisterRenderer(_oreRenderer, EnumRenderStage.Opaque);
  }

  /// <summary>Raises the charge surface to match how full the bowl is (raw charge plus finished burden,
  /// out of one full batch).</summary>
  private void UpdateOreLevel()
  {
    if (_oreRenderer != null)
      _oreRenderer.Fill = IsConstructed
        ? (TotalRaw + ReadyBurden) / (float)IwexValues.MixerMaxRaw
        : 0f;
  }

  #endregion

  private string AnimCacheKey => "oremixer-" + Block.Variant["side"];

  public override void OnBlockRemoved()
  {
    Dispose();
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded()
  {
    Dispose();
    base.OnBlockUnloaded();
  }

  public void Dispose()
  {
    _animator?.Dispose();
    _capi?.Event.UnregisterRenderer(this, EnumRenderStage.Before);
    _oreRenderer?.Dispose();
    _oreRenderer = null;
    if (_tickId != 0)
    {
      UnregisterGameTickListener(_tickId);
      _tickId = 0;
    }
  }

  /// <summary>Holds the mixer visible via the permanent idle pose, then reflects the current lid state.</summary>
  private void ApplyPose()
  {
    _animator?.Pose(util =>
      util.StartAnimation(
        new AnimationMetaData
        {
          Animation = "idle",
          Code = "idle",
          AnimationSpeed = 1f,
          EaseInSpeed = 3f,
          EaseOutSpeed = 3f,
        }.Init()
      )
    );
    UpdateLidPose();
  }

  #endregion

  #region Rotor phase-lock

  /// <summary>Per render frame: locks the rotor's <c>cycle</c> animation to the driving axle's angle.</summary>
  public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
  {
    if (
      _animator is not { Ready: true }
      || _animator.AnimUtil is not { animator: { } animator } util
    )
      return;

    BEBehaviorMPFillerPort? port = ActivePort();
    bool turning = port?.IsTurning == true;

    if (turning != _cycleRunning)
    {
      _cycleRunning = turning;
      if (turning)
      {
        // Must start with a NON-ZERO speed: a zero-speed animation is not posed by the animator, so
        // the manual CurrentFrame writes below would have no visible effect (the rotor wouldn't turn).
        // The frame is then overwritten each render frame to phase-lock it to the axle - the same
        // pattern the steam engine uses for its cyclemp animation (BlockEntityEngine.DriveMpCycleFrame).
        util.StartAnimation(
          new AnimationMetaData
          {
            Animation = "cycle",
            Code = "cycle",
            AnimationSpeed = 1f,
            EaseInSpeed = 3f,
            EaseOutSpeed = 3f,
          }.Init()
        );
      }
      else
        util.StopAnimation("cycle");
    }

    if (!turning || port == null)
      return;

    var state = animator.GetAnimationState("cycle");
    if (state?.Animation == null)
      return;

    // Map the axle's ABSOLUTE angle straight to the rotor frame: the rotor lines up with the axle
    // (not just spins at the same rate) and the loop is seamless. A full-turn cycle animation
    // (0deg -> 360deg) makes this an exact phase match.
    //
    // The `cycle` clip is authored in the north frame, so the rotor co-rotates with the driving axle
    // only after correcting for the placed orientation. TWO things flip the rotor's world spin relative
    // to the axle: the axle's per-axis AxisSign (X-axis axles render reversed vs Z), AND the 180deg
    // model rotation between opposite orientations (north<->south, west<->east) that mirrors the rotor
    // mesh's world spin for a fixed clip direction. PortFacing can't distinguish a 0deg mixer from a
    // 180deg one - both expose W/E ports, so an axle from the west reads PortFacing=WEST at either - so
    // the sign is taken from the unambiguous structure angle: negate at north(0)/east(270), keep at
    // west(90)/south(180). All four orientations were confirmed against the axle in-game.
    int structAngle = GameMath.Mod(Angle, 360);
    float rotorSign = structAngle is 0 or 270 ? -1f : 1f;
    state.CurrentFrame = MPAnim.FrameFromAngle(
      rotorSign * port.CurrentAngleRad,
      state.Animation.QuantityFrames
    );
  }

  /// <summary>
  /// The MP port currently delivering power (preferring a turning one), or the first present port for a
  /// stopped-angle readout. Reads the hosted <see cref="BEBehaviorMPFillerPort"/> on each upper-side cell.
  /// </summary>
  private BEBehaviorMPFillerPort? ActivePort()
  {
    BEBehaviorMPFillerPort? firstPresent = null;
    foreach (Vec3i off in PortOffsets)
    {
      BlockPos pos = ExOrientation.GlobalPos(Pos, off.X, off.Y, off.Z, Angle);
      var port = Api.World.BlockAccessor.GetBlockEntity(pos)
        ?.GetBehavior<BEBehaviorMPFillerPort>();
      if (port == null)
        continue;
      if (port.IsTurning)
        return port;
      firstPresent ??= port;
    }
    return firstPresent;
  }

  #endregion

  #region Charge / mixing

  /// <summary>The grade-defining proportions of the current raw charge (fuel in coke-equivalent value).</summary>
  public BurdenMix Mix => new(_iron, _flux, _fuel);

  /// <summary>Raw charge volume in units (iron + flux item counts + fuel value), as a whole number.</summary>
  public int TotalRaw => (int)MathF.Round(RawUnits);

  /// <summary>Raw charge volume as the exact float (iron + flux + fuel value); drives the cap and timing.</summary>
  private float RawUnits => _iron + _flux + _fuel;

  /// <summary>Finished burden waiting to be drained.</summary>
  public int ReadyBurden => _burdenCount;

  /// <summary>True once a batch has finished mixing and is waiting to drain (the lids may open).</summary>
  public bool HasReadyBurden => _burdenCount > 0;

  /// <summary>True when the raw charge has reached the batch cap and can take no more.</summary>
  public bool IsFull => RawUnits >= IwexValues.MixerMaxRaw;

  /// <summary>True while the lids are open and draining.</summary>
  public bool IsDraining => _draining;

  /// <summary>
  /// Whether the mixer accepts <paramref name="stack"/> at all: crushed iron / lime / crushed coke /
  /// charcoal as raw input, or burden to reload (split back into raw so its proportions can be retuned).
  /// </summary>
  public static bool AcceptsAsInput(ItemStack? stack)
  {
    string? path = stack?.Collectible?.Code?.Path;
    if (path == null)
      return false;
    return IsIronInput(path)
      || IsFluxInput(path)
      || IsFuelInput(path)
      || IsBurden(stack);
  }

  /// <summary>
  /// Seconds the current raw charge needs to fully mix at axle speed <paramref name="axleSpeed"/>.
  /// Faster axle → less time (interpolating between the slow- and fast-speed full-batch times); more
  /// material → more time (linear in how full the mixer is). Returns 0 with no charge.
  /// </summary>
  public float MixSecondsRequired(float axleSpeed)
  {
    if (RawUnits <= 0f)
      return 0f;

    float min = IwexValues.MixerMinSpeed;
    float max = IwexValues.MixerMaxSpeed;
    float t = max > min ? GameMath.Clamp((axleSpeed - min) / (max - min), 0f, 1f) : 0f;
    float fullTime = GameMath.Lerp(
      IwexValues.MixerFullMixSecondsSlow,
      IwexValues.MixerFullMixSecondsFast,
      t
    );
    return fullTime * (RawUnits / IwexValues.MixerMaxRaw);
  }

  /// <summary>Mixing progress 0..1 of the current raw charge (0 with no charge).</summary>
  public float MixProgress => TotalRaw <= 0 ? 0f : Math.Min(1f, _mixProgress);

  private static bool IsIronInput(string path) =>
    IronOreCompat.IsCrushedIronOre(path);

  private static bool IsFluxInput(string path) => path == "lime";

  private static bool IsCokeInput(string path) => path == "crushed-coke";

  private static bool IsCharcoalInput(string path) => path == "charcoal";

  /// <summary>Any carbon reductant: coke or charcoal.</summary>
  private static bool IsFuelInput(string path) =>
    IsCokeInput(path) || IsCharcoalInput(path);

  /// <summary>Coke-equivalent carbon value of one item of the given fuel input.</summary>
  private static float FuelValuePerItem(string path) =>
    IsCharcoalInput(path) ? IwexValues.MixerCharcoalFuelValue : 1f;

  private static bool IsBurden(ItemStack? stack) =>
    stack?.Collectible?.Code is { Domain: "iwex", Path: "burden" };

  /// <summary>
  /// Adds a held crushed-iron / lime / crushed-coke stack to the matching raw part (up to the batch
  /// cap), consuming from <paramref name="slot"/>. With <paramref name="wholeStack"/> the whole held
  /// stack is taken (sneak+right-click); otherwise a single unit (plain right-click). Rejected while a
  /// finished batch is waiting to drain or the lids are open. Adding material re-opens the batch (it
  /// must mix again). Server-side.
  /// </summary>
  public bool TryAddInput(ItemSlot slot, bool wholeStack = true)
  {
    if (Api.Side != EnumAppSide.Server || _draining || HasReadyBurden)
      return false;

    ItemStack? s = slot.Itemstack;
    if (s?.Collectible?.Code == null)
      return false;

    string path = s.Collectible.Code.Path;
    bool iron = IsIronInput(path);
    bool flux = IsFluxInput(path);
    bool fuel = IsFuelInput(path);
    if (!iron && !flux && !fuel)
      return false;

    // Cap by remaining charge VOLUME. Fuel items count for their carbon value (charcoal < coke), so a
    // bowl with little room left takes proportionally more charcoal items than coke.
    float space = IwexValues.MixerMaxRaw - RawUnits;
    float perItem = fuel ? FuelValuePerItem(path) : 1f;
    int maxByCap = (int)MathF.Floor(space / perItem + 1e-4f);
    if (maxByCap <= 0)
      return false;
    int take = Math.Min(maxByCap, wholeStack ? s.StackSize : 1);
    if (take <= 0)
      return false;

    if (iron)
      _iron += take;
    else if (flux)
      _flux += take;
    else
      _fuel += take * perItem;

    slot.TakeOut(take);
    slot.MarkDirty();
    _mixProgress = 0f; // a fresh addition un-mixes the batch
    MarkDirty(true);
    return true;
  }

  /// <summary>
  /// Reloads an off-spec (or any) burden stack back into the raw charge, splitting its units into
  /// iron/flux/coke by the burden's own proportions so a little more material can adjust the grade.
  /// Only into a mixer that holds no finished burden. Server-side.
  /// </summary>
  public bool TryReloadBurden(ItemSlot slot, bool wholeStack = true)
  {
    if (Api.Side != EnumAppSide.Server || _draining || HasReadyBurden)
      return false;

    ItemStack? s = slot.Itemstack;
    if (!IsBurden(s))
      return false;

    int space = (int)MathF.Floor(IwexValues.MixerMaxRaw - RawUnits + 1e-4f);
    if (space <= 0)
      return false;
    int count = Math.Min(space, wholeStack ? s.StackSize : 1);

    BurdenMix mix = Burden.Read(s);
    int iron = (int)MathF.Round(count * mix.IronFrac);
    int flux = (int)MathF.Round(count * mix.FluxFrac);
    float fuel = Math.Max(0f, count - iron - flux); // fuel takes the remaining value
    _iron += iron;
    _flux += flux;
    _fuel += fuel;

    slot.TakeOut(count);
    slot.MarkDirty();
    _mixProgress = 0f;
    MarkDirty(true);
    return true;
  }

  private void OnServerTick(float dt)
  {
    if (!IsConstructed)
      return;

    if (_draining)
    {
      DrainStep(dt);
      return;
    }
    // Mix only while the rotor is driven and there's an unfinished raw charge; faster axle = faster mix.
    if (RawUnits > 0f && !HasReadyBurden && ActivePort() is { IsTurning: true } port)
      AdvanceMixing(dt, port.Speed);
  }

  /// <summary>
  /// Advances the mixing of the current raw charge by <paramref name="dt"/> seconds at axle speed
  /// <paramref name="axleSpeed"/>; once the charge has mixed long enough (see
  /// <see cref="MixSecondsRequired"/>) its proportions become graded burden and the raw clears. Public
  /// so the server tick - and tests - can drive it; the power/charge gating is the caller's job.
  /// </summary>
  public void AdvanceMixing(float dt, float axleSpeed)
  {
    if (RawUnits <= 0f || HasReadyBurden)
      return;

    float required = MixSecondsRequired(axleSpeed);
    if (required <= 0f)
      return;

    float before = _mixProgress;
    _mixProgress += dt / required;
    if (_mixProgress >= 1f)
    {
      // Batch done: the raw proportions become the burden grade; raw clears.
      _burdenMix = Mix;
      _burdenCount = TotalRaw;
      _iron = _flux = 0;
      _fuel = 0f;
      _mixProgress = 0f;
      MarkDirty(true);
    }
    // Sync ~every 5% for the progress readout without spamming the network.
    else if ((int)(_mixProgress * 20f) != (int)(before * 20f))
      MarkDirty(true);
  }

  /// <summary>Moves finished burden down into the container below, a slice per call, stamped with the
  /// batch grade. Stops draining when the buffer empties or nothing fits. Public for the server tick
  /// and tests; only acts while draining (set by <see cref="ToggleDrain"/>).</summary>
  public void DrainStep(float dt)
  {
    if (!HasReadyBurden || _burdenItem == null)
    {
      _draining = false;
      MarkDirty(true);
      return;
    }

    int amount = Math.Min(
      _burdenCount,
      Math.Max(1, (int)MathF.Ceiling(IwexValues.MixerDrainPerSecond * dt))
    );
    var stack = new ItemStack(_burdenItem, amount);
    Burden.Write(stack, _burdenMix);

    int accepted = DepositBelow(stack);
    if (accepted <= 0)
      return; // no container / full: hold the lids open, keep the burden waiting

    _burdenCount -= accepted;
    if (_burdenCount <= 0)
    {
      _burdenCount = 0;
      _burdenMix = default;
      _draining = false;
    }
    MarkDirty(true);
  }

  /// <summary>
  /// Merges <paramref name="stack"/> into the container directly below (a bunker, crate or chest):
  /// matching-grade stacks first (compared by composition, so grades stay separate), then empty slots.
  /// Returns how many units were accepted.
  /// </summary>
  private int DepositBelow(ItemStack stack)
  {
    if (ContainerBelow() is not { } container)
      return 0;

    // An ore bunker enforces its own one-grade pooling rule, so route through its deposit path (it
    // pools the drained burden into the bunker's weighted-average grade and declines a mismatched load).
    if (container is BlockEntityOreBunker bunker)
    {
      int held = stack.StackSize;
      var src = new DummySlot(stack);
      bunker.TryDeposit(src, wholeStack: true);
      return held - (src.Itemstack?.StackSize ?? 0);
    }

    if (container.Inventory is not { } inv)
      return 0;

    int before = stack.StackSize;
    int max = stack.Collectible.MaxStackSize;
    BurdenMix mix = Burden.Read(stack);

    foreach (ItemSlot slot in inv)
    {
      if (stack.StackSize <= 0)
        break;
      if (
        slot.Itemstack is not { } existing
        || existing.Collectible != stack.Collectible
        || !Burden.Read(existing).Equals(mix)
      )
        continue;
      int put = Math.Min(max - slot.StackSize, stack.StackSize);
      if (put <= 0)
        continue;
      slot.Itemstack.StackSize += put;
      stack.StackSize -= put;
      slot.MarkDirty();
    }
    foreach (ItemSlot slot in inv)
    {
      if (stack.StackSize <= 0)
        break;
      if (!slot.Empty)
        continue;
      int put = Math.Min(max, stack.StackSize);
      ItemStack placed = stack.Clone();
      placed.StackSize = put;
      slot.Itemstack = placed;
      stack.StackSize -= put;
      slot.MarkDirty();
    }

    int moved = before - stack.StackSize;
    if (moved > 0)
      container.MarkDirty(true);
    return moved;
  }

  /// <summary>
  /// The container the mixer drains into: the block entity directly below, or - when that cell is the
  /// invisible filler of a mega-block (e.g. the bunker's footprint, where only the principal carries the
  /// container) - the principal that filler belongs to. Returns null when there's no container below.
  /// </summary>
  private BlockEntityContainer? ContainerBelow()
  {
    BlockPos below = Pos.DownCopy();
    BlockEntity? be = Api.World.BlockAccessor.GetBlockEntity(below);

    // Drain straight into a container sitting below; otherwise hop through a structure filler to the
    // principal cell that actually holds the container.
    if (be is BlockEntityContainer direct)
      return direct;
    if (
      be is BlockEntityStructureFiller { Principal: { } principal }
      && Api.World.BlockAccessor.GetBlockEntity(principal)
        is BlockEntityContainer viaFiller
    )
      return viaFiller;
    return null;
  }

  #endregion

  #region Lids / drain

  /// <summary>
  /// Toggles the dropping lids (the <c>open</c> pose): opening starts draining finished burden into the
  /// container below (only allowed once a batch has mixed), closing stops it. Server-authoritative; the
  /// pose and drain progress sync to clients through the save tree. Returns false when opening is
  /// refused because nothing is mixed yet.
  /// </summary>
  public bool ToggleDrain()
  {
    if (Api.Side != EnumAppSide.Server)
      return false;
    if (!_draining && !HasReadyBurden)
      return false; // nothing finished to drain
    _draining = !_draining;
    MarkDirty(true);
    return true;
  }

  /// <summary>Plays or clears the lid-open pose to match <see cref="_draining"/> (client visual).</summary>
  private void UpdateLidPose() =>
    _animator?.Pose(util =>
    {
      if (_draining)
        util.StartAnimation(
          new AnimationMetaData
          {
            Animation = "open",
            Code = "open",
            AnimationSpeed = 1f,
            EaseInSpeed = 3f,
            EaseOutSpeed = 3f,
          }.Init()
        );
      else
        util.StopAnimation("open");
    });

  #endregion

  #region Persistence / info

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetBool("draining", _draining);
    tree.SetInt("iron", _iron);
    tree.SetInt("flux", _flux);
    tree.SetFloat("fuel", _fuel);
    tree.SetFloat("mixProgress", _mixProgress);
    tree.SetInt("burdenCount", _burdenCount);
    tree.SetFloat("bmIron", _burdenMix.Iron);
    tree.SetFloat("bmFlux", _burdenMix.Flux);
    tree.SetFloat("bmFuel", _burdenMix.Fuel);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    _draining = tree.GetBool("draining", false);
    _iron = tree.GetInt("iron");
    _flux = tree.GetInt("flux");
    _fuel = tree.GetFloat("fuel");
    _mixProgress = tree.GetFloat("mixProgress");
    _burdenCount = tree.GetInt("burdenCount");
    _burdenMix = new BurdenMix(
      tree.GetFloat("bmIron"),
      tree.GetFloat("bmFlux"),
      tree.GetFloat("bmFuel")
    );
    // A client receiving a lid-state change re-applies the pose (animator may not exist yet on load).
    UpdateLidPose();
    // ...and re-heights the visible charge heap to the synced amount.
    UpdateOreLevel();
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
  {
    base.GetBlockInfo(forPlayer, sb);
    if (!IsConstructed)
      return;

    if (HasReadyBurden)
    {
      sb.AppendLine(
        Lang.Get(
          "iwex:mixer-ready",
          _burdenCount,
          Lang.Get(Burden.ProfileLangKey(_burdenMix))
        )
      );
      sb.AppendLine(
        Lang.Get(_draining ? "iwex:mixer-draining" : "iwex:mixer-drain-hint")
      );
      return;
    }

    if (RawUnits > 0f)
    {
      sb.AppendLine(
        Lang.Get("iwex:mixer-charge", _iron, _flux, (int)MathF.Round(_fuel))
      );
      sb.AppendLine(
        Lang.Get("iwex:mixer-grade", Lang.Get(Burden.ProfileLangKey(Mix)))
      );
      sb.AppendLine(
        ActivePort()?.IsTurning == true
          ? Lang.Get("iwex:mixer-mixing", (int)(MixProgress * 100f))
          : Lang.Get("iwex:mixer-needs-power")
      );
      return;
    }

    sb.AppendLine(Lang.Get("iwex:mixer-empty"));
  }

  #endregion
}
