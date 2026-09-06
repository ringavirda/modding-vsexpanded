using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Industry.MechanicalPower;

/// <summary>
/// Phase-lock math for mega-block parts (a rotor, a gear, a piston) that must turn in step with a
/// mechanical-power axle rather than merely at a proportional speed. A driven part reads the network's
/// rotation angle each render frame and advances its cyclic animation frame by the signed change in
/// that angle, so one full network revolution plays exactly one animation cycle and the part does not
/// drift out of phase with the axle.
/// </summary>
public static class MPAnim {
  /// <summary>
  /// Returns the next cyclic frame for an animation of <paramref name="totalFrames"/> frames, given
  /// the part's <paramref name="currentFrame"/> and the network angle on the previous frame
  /// (<paramref name="lastAngleRad"/>) and now (<paramref name="angleRad"/>). The result wraps within
  /// <c>[0, totalFrames)</c>; one full revolution (2π) advances exactly one cycle. Returns 0 when
  /// <paramref name="totalFrames"/> is 1 or less.
  /// </summary>
  public static float AdvanceFrame(
    float currentFrame,
    float lastAngleRad,
    float angleRad,
    int totalFrames
  ) {
    if (totalFrames <= 1)
      return 0f;
    // AngleRadDistance gives the signed shortest delta, so wrapping past 2π or reversing advances the
    // frame without a jump.
    float delta = GameMath.AngleRadDistance(lastAngleRad, angleRad);
    return GameMath.Mod(
      currentFrame + delta / GameMath.TWOPI * totalFrames,
      totalFrames
    );
  }

  /// <summary>
  /// Maps a network rotation angle straight onto a cyclic animation frame, so a driven part stays
  /// locked to the axle's absolute angle rather than merely spinning at the same rate. Angle
  /// <c>0..2π</c> maps onto frame <c>0..totalFrames</c> and wraps at <c>2π</c>. Use
  /// <see cref="AdvanceFrame"/> instead when only speed and direction matter, such as an oscillating
  /// piston that need not align to an absolute angle.
  /// </summary>
  /// <remarks>
  /// The span is the FULL frame count, not the last keyframe's number: the animator's live frame space
  /// is <c>[0, QuantityFrames)</c> and the stretch from the last keyframe back to the first is an
  /// ordinary interpolation segment. A clip driven from here is therefore authored the way vanilla
  /// authors one - last keyframe at <c>360 * (frames-1) / frames</c> with <c>rotShortestDistance</c>
  /// set, never a duplicate of frame 0. See docs/design/machines/engine-watt.md § Animation phase-lock.
  /// </remarks>
  public static float FrameFromAngle(float angleRad, int totalFrames) {
    if (totalFrames <= 1)
      return 0f;
    return GameMath.Mod(angleRad / GameMath.TWOPI * totalFrames, totalFrames);
  }

  /// <summary>
  /// Pins the running <paramref name="animCode"/> clip's frame to <paramref name="angleRad"/>, so the
  /// part it drives turns with the axle instead of merely at the same rate. Call once per render frame
  /// - a client tick is far too coarse and shows as stepping. A no-op until the clip is running and
  /// the animator has resolved, so it is safe to call unconditionally.
  /// Two conditions gate the write: the clip must start with a NON-ZERO <c>AnimationSpeed</c>, since
  /// the animator does not pose a zero-speed animation, and it must loop
  /// (<c>onAnimationEnd: Repeat</c>), or an animator-rendered block loses its mesh at the end. The
  /// clip's own advance runs after this write, at <see cref="EnumRenderStage.Opaque"/>, leading it by
  /// <c>30 * dt</c>.
  /// </summary>
  /// <param name="reverse">
  /// Set when the clip's shaft is keyframed turning the opposite way to the axle: a cycle rotating it
  /// through +360 plays with the axle, one authored the other way turns against it.
  /// </param>
  public static void LockFrameToAngle(
    AnimationUtil? animUtil,
    string animCode,
    float angleRad,
    bool reverse = false
  ) {
    if (reverse)
      angleRad = -angleRad;
    if (animUtil?.animator?.GetAnimationState(animCode) is not { } state)
      return;
    if (state.Animation == null)
      return;
    state.CurrentFrame = FrameFromAngle(
      angleRad,
      state.Animation.QuantityFrames
    );
  }
}
