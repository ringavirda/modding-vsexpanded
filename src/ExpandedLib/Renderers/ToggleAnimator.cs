using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace ExpandedLib.Renderers;

/// <summary>
/// Composable helper for a non-constructed animated block entity - one that renders through a permanent
/// or toggled animation (<see cref="BEBehaviorAnimatable"/> + <see cref="BlockEntityAnimationUtil"/>) but
/// is not raised via RightClickConstructable. Its constructed sibling is
/// <c>ExpandedLib.Blocks.Construction.ConstructedAnimator</c>; this is the lighter variant for the pour
/// taps, gas/pressure valves, converter control, engine sub-machines and the manual pump.
/// <para>
/// It owns exactly the scaffold those five hand-rolled identically: resolving the animatable behavior,
/// the one null-animator ready-guard (so no consumer can drift into the null-animator <c>GetBlockInfo</c>
/// NRE - two copies set the flag <c>true</c> unconditionally and were exposed to it), and the pose gate.
/// The animator build itself varies too much to own - two <c>InitializeAnimator</c> overloads (mesh-based
/// vs shape+texsource), per-block cache keys, Y-only vs full X/Y/Z rotation, an optional renderer
/// <c>CustomTransform</c> - so the consumer supplies it as the <paramref name="build"/> delegate.
/// </para>
/// <para>
/// Usage: hold one as a field, call <see cref="Initialize"/> from the block entity's <c>Initialize</c>
/// (passing the machine's pose method), route the pose method through <see cref="Pose"/>, call
/// <see cref="Rebuild"/> then the pose again from <c>OnExchanged</c> if the block is wrench-rotatable.
/// There is no <c>Dispose</c>: unlike the constructed helper it subscribes to nothing.
/// </para>
/// </summary>
public sealed class ToggleAnimator
{
  private readonly BlockEntity _be;
  private readonly Action<BEBehaviorAnimatable> _build;

  private BEBehaviorAnimatable? _animatable;
  private bool _ready;
  private Action? _repose;

  /// <param name="be">The owning block entity.</param>
  /// <param name="build">(Re)builds the animator on the resolved animatable behavior: load or tessellate
  /// the mesh/shape and call the matching <c>InitializeAnimator</c> overload (plus any renderer
  /// <c>CustomTransform</c>). Runs only on a client that has the behavior. Leaving the animator null (e.g.
  /// a shape that fails to resolve) is honoured - the helper then reports not-ready and never poses it.</param>
  public ToggleAnimator(BlockEntity be, Action<BEBehaviorAnimatable> build)
  {
    _be = be;
    _build = build;
  }

  /// <summary>Whether a real animator currently exists (a client with a resolved shape). The single gate
  /// on <see cref="Pose"/>.</summary>
  public bool Ready => _ready;

  /// <summary>The wrapped animation utility, or null off-client / before <see cref="Initialize"/>.</summary>
  public BlockEntityAnimationUtil? AnimUtil => _animatable?.animUtil;

  /// <summary>
  /// Resolves the animatable behavior, runs the initial build and applies the first pose. Safe on any
  /// side - it only builds/poses on the client. <paramref name="repose"/> is the machine's own pose
  /// selection; it is invoked here (and should be invoked again by the consumer after a
  /// <see cref="Rebuild"/>), and should itself route through <see cref="Pose"/>.
  /// </summary>
  public void Initialize(Action repose)
  {
    _repose = repose;
    _animatable = _be.GetBehavior<BEBehaviorAnimatable>();
    if (_be.Api is not ICoreClientAPI || _animatable == null)
      return;

    Rebuild();
    _repose?.Invoke();
  }

  /// <summary>
  /// (Re)builds the animator via the consumer's build delegate and re-evaluates the ready-guard. Public so
  /// a wrench-rotatable block can rebuild in its new orientation from <c>OnExchanged</c> (re-pose after).
  /// </summary>
  public void Rebuild()
  {
    if (_be.Api is not ICoreClientAPI || _animatable == null)
      return;

    _build(_animatable);
    // A failed shape resolve leaves animUtil.animator null; only mark ready when it truly exists, so a
    // pose is never queued against a null animator (vanilla GetBlockInfo would then NRE).
    _ready = _animatable.animUtil.animator != null;
  }

  /// <summary>
  /// Runs <paramref name="pose"/> against the animation utility only on a client that has a live animator
  /// - the one guard every animated machine shares. A no-op otherwise.
  /// </summary>
  public void Pose(Action<BlockEntityAnimationUtil> pose)
  {
    if (_be.Api is not ICoreClientAPI || _animatable == null || !_ready)
      return;
    pose(_animatable.animUtil);
  }
}
