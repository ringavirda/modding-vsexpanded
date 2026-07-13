using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ExpandedLib.Blocks.Construction;

/// <summary>
/// Composable helper that owns the animator + <see cref="ExRightClickConstructable"/> lifecycle shared
/// by every constructed, animator-rendered mega-block (boiler, engine, converter vessel, ore mixer/bunker).
/// The RCC behavior suppresses the block's default mesh, so these blocks are only visible through a
/// permanent animation re-tessellated to the currently-built construction elements - a ~80-line triad
/// that was copy-pasted byte-for-byte across all of them (audit Theme A).
/// <para>
/// It is a <b>helper</b>, not a base class, on purpose: the consumers sit on four different block-entity
/// bases (plain <see cref="BlockEntity"/>, <c>BlockEntityProductionMachine</c>,
/// <c>BlockEntityMultiblockStructure</c>, <c>BlockEntityContainer</c>), and C# single inheritance cannot
/// give them one shared leaf base. Composing this helper instead lets each keep its own base while
/// sharing the scaffold - and, crucially, the one place that owns the <c>_animatorReady</c> null-guard,
/// so a machine can never drift into the null-animator <c>GetBlockInfo</c> NRE the way one hand-rolled
/// copy did.
/// </para>
/// <para>
/// Usage: hold one as a field, call <see cref="Initialize"/> from the block entity's <c>Initialize</c>
/// (passing the machine's pose method), route the machine's pose method through <see cref="Pose"/>, and
/// call <see cref="Dispose"/> from both <c>OnBlockRemoved</c> and <c>OnBlockUnloaded</c>.
/// </para>
/// </summary>
public sealed class ConstructedAnimator
{
  private readonly BlockEntity _be;
  private readonly Func<string> _cacheKey;
  private readonly Action<BlockEntityAnimationUtil, MeshData>? _onAnimatorBuilt;

  private BEBehaviorAnimatable? _animatable;
  private ExRightClickConstructable? _rcc;
  private bool _ready;
  private Action? _repose;

  /// <param name="be">The owning block entity.</param>
  /// <param name="cacheKey">The per-block animator/shape cache key (e.g. <c>"orebunker-" + side</c>);
  /// evaluated lazily so a wrench-rotate that changes the variant is picked up.</param>
  /// <param name="onAnimatorBuilt">Optional hook run after each successful (re)build, receiving the anim
  /// util and the freshly-built mesh - used to swap in a custom <see cref="AnimatableRenderer"/> (the
  /// boiler relights its mesh from a body cell). Runs before the pose, so a renderer that seeds its
  /// visibility from the active-animation set sees the same state the old inline swap did.</param>
  public ConstructedAnimator(
    BlockEntity be,
    Func<string> cacheKey,
    Action<BlockEntityAnimationUtil, MeshData>? onAnimatorBuilt = null
  )
  {
    _be = be;
    _cacheKey = cacheKey;
    _onAnimatorBuilt = onAnimatorBuilt;
  }

  /// <summary>True once the player has finished the construction stages. Valid on the server too (the
  /// RCC behavior is resolved on both sides), so it can gate production.</summary>
  public bool IsConstructed => _rcc?.IsComplete ?? false;

  /// <summary>Whether a real animator currently exists (a client with a resolved shape). The single gate
  /// on <see cref="Pose"/>.</summary>
  public bool Ready => _ready;

  /// <summary>The wrapped animation utility, or null off-client / before <see cref="Initialize"/>.</summary>
  public BlockEntityAnimationUtil? AnimUtil => _animatable?.animUtil;

  /// <summary>The construction behavior, or null when the block has none (a purely-animated machine).</summary>
  public ExRightClickConstructable? Rcc => _rcc;

  /// <summary>
  /// Resolves the animatable + construction behaviors, wires construction-stage re-tessellation, builds
  /// the initial mesh and applies the first pose. Safe to call on any side - it only builds/poses on the
  /// client. <paramref name="repose"/> is the machine's own pose selection; it is invoked here and after
  /// every construction stage, and should itself route through <see cref="Pose"/>.
  /// </summary>
  public void Initialize(Action repose)
  {
    _repose = repose;
    _animatable = _be.GetBehavior<BEBehaviorAnimatable>();
    _rcc = _be.GetBehavior<ExRightClickConstructable>();

    if (_be.Api is not ICoreClientAPI || _animatable == null)
      return;

    // Re-render whenever a construction stage adds/removes elements.
    if (_rcc != null)
      _rcc.OnShapeChanged += OnShapeChanged;

    Rebuild(_rcc?.shape?.SelectiveElements);
    _repose?.Invoke();
  }

  private void OnShapeChanged(CompositeShape cs)
  {
    Rebuild(cs?.SelectiveElements);
    _repose?.Invoke();
  }

  /// <summary>
  /// (Re)builds the animator to render exactly the currently-built elements (only the mesh is filtered
  /// to <paramref name="selectiveElements"/>; the animator hierarchy stays the full shape). Public so a
  /// wrench-rotatable machine can rebuild in its new orientation from <c>OnExchanged</c>.
  /// </summary>
  public void Rebuild(string[]? selectiveElements)
  {
    if (_be.Api is not ICoreClientAPI || _animatable == null)
      return;

    BlockEntityAnimationUtil util = _animatable.animUtil;

    // CreateMesh resolves a FRESH shape each call; reusing one re-maps UVs into atlas space and
    // stretches textures. Rotation is applied by the renderer, not baked into the mesh.
    MeshData meshData = util.CreateMesh(
      _cacheKey(),
      null,
      out Shape resolvedShape,
      null,
      new TesselationMetaData { SelectiveElements = selectiveElements }
    );

    util.InitializeAnimator(
      _cacheKey(),
      meshData,
      resolvedShape,
      new Vec3f(0, _be.Block.Shape.rotateY, 0)
    );

    // A failed shape resolve leaves animUtil.animator null; only mark ready when it truly exists, so a
    // pose is never queued against a null animator (vanilla GetBlockInfo would then NRE).
    _ready = util.animator != null;
    if (_ready)
      _onAnimatorBuilt?.Invoke(util, meshData);
  }

  /// <summary>
  /// Runs <paramref name="pose"/> against the animation utility only on a client that has a live
  /// animator - the one guard every constructed machine shares, so none can drift into the null-animator
  /// <c>GetBlockInfo</c> NRE. A no-op otherwise.
  /// </summary>
  public void Pose(Action<BlockEntityAnimationUtil> pose)
  {
    if (_be.Api is not ICoreClientAPI || _animatable == null || !_ready)
      return;
    pose(_animatable.animUtil);
  }

  /// <summary>Unsubscribes from construction-stage events. Call from both <c>OnBlockRemoved</c> and
  /// <c>OnBlockUnloaded</c>.</summary>
  public void Dispose()
  {
    if (_rcc != null)
      _rcc.OnShapeChanged -= OnShapeChanged;
  }
}
