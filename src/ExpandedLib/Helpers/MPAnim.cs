using Vintagestory.API.MathTools;

namespace ExpandedLib.Helpers;

/// <summary>
/// Phase-lock math shared by mega-blocks whose visible parts (a rotor, a gear, a piston) must turn in
/// step with a mechanical-power axle rather than merely at a proportional speed. A driven part reads
/// the network's rotation angle each render frame and advances its cyclic animation frame by the
/// signed change in that angle, so one full network revolution plays exactly one animation cycle and
/// the part never drifts out of phase with the axle (the same approach the steam engine uses to drive
/// its MP cycle).
/// </summary>
public static class MPAnim
{
  /// <summary>
  /// Returns the next cyclic frame for an animation of <paramref name="totalFrames"/> frames, given
  /// the part's <paramref name="currentFrame"/>, the network angle on the previous frame
  /// (<paramref name="lastAngleRad"/>) and now (<paramref name="angleRad"/>). The result wraps within
  /// <c>[0, totalFrames)</c>; one full revolution (2π) advances exactly one whole cycle. Returns 0 for
  /// a degenerate animation (<paramref name="totalFrames"/> ≤ 1).
  /// </summary>
  public static float AdvanceFrame(
    float currentFrame,
    float lastAngleRad,
    float angleRad,
    int totalFrames
  )
  {
    if (totalFrames <= 1)
      return 0f;
    // AngleRadDistance gives the signed shortest delta, so wrapping past 2π (or reversing) advances
    // the frame smoothly without a jump.
    float delta = GameMath.AngleRadDistance(lastAngleRad, angleRad);
    return GameMath.Mod(
      currentFrame + delta / GameMath.TWOPI * totalFrames,
      totalFrames
    );
  }

  /// <summary>
  /// Maps a network rotation angle <em>directly</em> to a cyclic animation frame, so a driven part
  /// stays phase-locked to the axle's absolute angle (it lines up with the axle, not merely spins at
  /// the same rate) and loops seamlessly. Angle <c>0..2π</c> maps onto frame <c>0..(totalFrames-1)</c>
  /// and wraps at <c>2π</c>; because a full-turn animation's first and last keyframes are the same
  /// orientation, the wrap is invisible. Use this for a part that should align with the axle (e.g. a
  /// rotor on the same axis); use <see cref="AdvanceFrame"/> when only the speed/direction matters
  /// (e.g. an oscillating piston, which need not align to any absolute angle).
  /// </summary>
  public static float FrameFromAngle(float angleRad, int totalFrames)
  {
    if (totalFrames <= 1)
      return 0f;
    // Span over the keyframe range [0, total-1]: the last keyframe is the end of the turn, and the
    // wrap (total-1 -> 0) is the same orientation, so it never interpolates backward through the loop.
    int span = totalFrames - 1;
    return GameMath.Mod(angleRad / GameMath.TWOPI * span, span);
  }
}
