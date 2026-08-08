using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockNetworkEnergy;

/// <summary>
/// The motion half of the cast-iron mpenergy transmission, with <see cref="EnergyMeshes"/> owning the mesh half:
/// how a run's shaft speed <c>ω</c> becomes an animation playback rate, and which way a bevel branch turns. Both
/// are pure functions, so the convention is testable headless while the visuals are in-game only.
/// </summary>
public static class EnergyAnim {
  /// <summary>Fraction of <c>ω_max</c> below which a shaft reads as stopped and the spin clip gives way to the
  /// rest pose rather than creeping imperceptibly. Matches the flywheel's block-info sync step.</summary>
  private const float StoppedFraction = 0.01f;

  /// <summary>
  /// Animation playback multiplier for a shaft turning at <paramref name="omega"/> rad/s, for a clip authored as
  /// one revolution of its reference shaft. A clip plays once per second at 1×, so the multiplier is the shaft's
  /// revolutions per second, <c>ω / 2π</c>: the animation reads the run's speed directly and the only tuning knob
  /// is the physical <c>MpMaxSpeed</c>. Never negative.
  /// </summary>
  public static float SpinSpeed(float omega) =>
    omega > 0f ? omega / GameMath.TWOPI : 0f;

  /// <summary>Whether a shaft at <paramref name="omega"/> is turning fast enough to animate, given the run's
  /// ceiling <paramref name="maxSpeed"/>. See <see cref="StoppedFraction"/>.</summary>
  public static bool IsTurning(float omega, float maxSpeed) =>
    omega > maxSpeed * StoppedFraction;

  /// <summary>
  /// Which way a bevel branch onto <paramref name="branchFace"/> turns relative to the driving shaft: <c>+1</c>
  /// same sense, <c>-1</c> reversed. Both spins are expressed right-handed about their own positive axis, and the
  /// result is independent of which axis the driving shaft runs on.
  /// <para>
  /// Matching contact velocity on the mitre pair's pitch cones gives
  /// <c>ω_branch = -sign(branchFace · its own axis) · ω_driver</c>, so a branch on the positive side of its axis
  /// (east, up, south) reverses while one on the negative side (west, down, north) keeps the sense. The two
  /// branches of a single bevel therefore turn opposite ways.
  /// </para>
  /// </summary>
  public static int BranchSpinSign(BlockFacing branchFace) =>
    branchFace.Normali.X + branchFace.Normali.Y + branchFace.Normali.Z > 0
      ? -1
      : 1;
}
