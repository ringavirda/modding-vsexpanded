using ExpandedLib.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LowPressureExpanded.BlockStructures.Engine;

/// <summary>
/// Keyframe-driven piston sounds shared by the engines and sub-machines. All run the same cycle
/// animation (piston top at <see cref="UpFrame"/>, bottom at <see cref="DownFrame"/>); this fires a
/// swoosh per up-stroke and a metal impact per down-stroke as the cycle frame crosses those
/// thresholds. Client-side, so each stroke matches the locally rendered animation.
/// </summary>
public static class PistonCycleSounds {
  /// <summary>Cycle frame where the piston tops out (torch un-equip whoosh).</summary>
  public const int UpFrame = 45;

  /// <summary>Cycle frame where the piston bottoms out (anvil merge clang).</summary>
  public const int DownFrame = 15;

  /// <summary>
  /// Plays the stroke sounds for any threshold the cycle animation crossed between
  /// <paramref name="lastFrame"/> and <paramref name="currentFrame"/> this tick, wrap included, at
  /// <paramref name="pos"/>. <paramref name="volumeMul"/> scales stroke loudness and carry range.
  /// </summary>
  public static void Fire(
    IWorldAccessor world,
    BlockPos pos,
    float lastFrame,
    float currentFrame,
    int totalFrames,
    float volumeMul = 1f
  ) {
    if (Crossed(lastFrame, currentFrame, totalFrames, UpFrame))
      ExSounds.PlayLocal(
        world,
        pos,
        ExSounds.TorchUnequip,
        1.5f * volumeMul,
        16f * volumeMul,
        true
      );
    if (Crossed(lastFrame, currentFrame, totalFrames, DownFrame))
      ExSounds.PlayLocal(
        world,
        pos,
        ExSounds.AnvilMergeHit,
        0.2f * volumeMul,
        16f * volumeMul,
        true
      );
  }

  /// <summary>True when the cycle crossed the top-of-stroke frame (<see cref="UpFrame"/>) this tick,
  /// the point at which the cylinder vents its spent steam.</summary>
  public static bool CrossedUpStroke(float last, float cur, int totalFrames) =>
    Crossed(last, cur, totalFrames, UpFrame);

  /// <summary>True when the cycle swept across <paramref name="frame"/> this tick, wrap included; for
  /// sub-machines watching their own keyframes, such as the air blower's intake and compression.</summary>
  public static bool CrossedFrame(
    float last,
    float cur,
    int totalFrames,
    int frame
  ) => Crossed(last, cur, totalFrames, frame);

  /// <summary>
  /// True when <paramref name="threshold"/> falls in the half-open interval the frame swept
  /// from <paramref name="last"/> to <paramref name="cur"/> this tick, accounting for the
  /// animation looping back to 0.
  /// </summary>
  private static bool Crossed(
    float last,
    float cur,
    int totalFrames,
    float threshold
  ) {
    if (totalFrames <= 1)
      return false;
    if (cur >= last) // normal advance within the loop
      return last < threshold && threshold <= cur;
    // Wrapped past the end this tick: crossed if the threshold is after `last` or up to `cur`.
    return threshold > last || threshold <= cur;
  }
}
