using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace ExpandedLib.Renderers;

/// <summary>
/// Rendering helper for a block entity that animates through <see cref="BEBehaviorAnimatable"/> and
/// <see cref="BlockEntityAnimationUtil"/> but is not raised via RightClickConstructable
/// (<c>ExpandedLib.Blocks.Construction.ConstructedAnimator</c> is the constructed equivalent). It resolves
/// the animatable behavior, holds the ready-guard against a null animator and gates every pose behind it;
/// the per-block animator build comes in as the <c>build</c> delegate. Hold one as a field, call
/// <see cref="Initialize"/> from the block entity's <c>Initialize</c>, route the machine's pose method
/// through <see cref="Pose"/>, and from <c>OnExchanged</c> on a wrench-rotatable block call
/// <see cref="Rebuild"/> then the pose again. Subscribes to nothing, so there is no <c>Dispose</c>.
/// </summary>
public sealed class ToggleAnimator {
  private readonly BlockEntity _be;
  private readonly Action<BEBehaviorAnimatable> _build;

  private BEBehaviorAnimatable? _animatable;
  private bool _ready;
  private Action? _repose;

  /// <param name="be">The owning block entity.</param>
  /// <param name="build">(Re)builds the animator on the resolved animatable behavior: mesh or shape plus the
  /// matching <c>InitializeAnimator</c> overload and any renderer <c>CustomTransform</c>. Runs client-side
  /// only. May leave the animator null, in which case the helper reports not-ready and never poses.</param>
  public ToggleAnimator(BlockEntity be, Action<BEBehaviorAnimatable> build) {
    _be = be;
    _build = build;
  }

  /// <summary>Whether a real animator currently exists (a client with a resolved shape). The gate on
  /// <see cref="Pose"/>.</summary>
  public bool Ready => _ready;

  /// <summary>The wrapped animation utility, or null off-client / before <see cref="Initialize"/>.</summary>
  public BlockEntityAnimationUtil? AnimUtil => _animatable?.animUtil;

  /// <summary>
  /// Resolves the animatable behavior, runs the initial build and applies the first pose; builds and poses
  /// on the client only. <paramref name="repose"/> is the machine's own pose selection, which must route
  /// through <see cref="Pose"/> and be invoked again by the consumer after a <see cref="Rebuild"/>.
  /// </summary>
  public void Initialize(Action repose) {
    _repose = repose;
    _animatable = _be.GetBehavior<BEBehaviorAnimatable>();
    if (_be.Api is not ICoreClientAPI || _animatable == null)
      return;

    Rebuild();
    _repose?.Invoke();
  }

  /// <summary>
  /// (Re)builds the animator through the build delegate and re-evaluates the ready-guard. Public so a
  /// wrench-rotatable block can rebuild in its new orientation from <c>OnExchanged</c>; re-pose after.
  /// </summary>
  public void Rebuild() {
    if (_be.Api is not ICoreClientAPI || _animatable == null)
      return;

    _build(_animatable);
    // A failed shape resolve leaves animUtil.animator null; posing against it NREs inside vanilla, so
    // Ready is set only when the animator exists.
    _ready = _animatable.animUtil.animator != null;
  }

  /// <summary>Runs <paramref name="pose"/> against the animation utility only on a client with a live
  /// animator. A no-op otherwise.</summary>
  public void Pose(Action<BlockEntityAnimationUtil> pose) {
    if (_be.Api is not ICoreClientAPI || _animatable == null || !_ready)
      return;
    pose(_animatable.animUtil);
  }
}
