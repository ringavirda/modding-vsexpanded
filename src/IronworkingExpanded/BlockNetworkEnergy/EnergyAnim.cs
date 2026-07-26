using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockNetworkEnergy;

/// <summary>
/// The <b>motion</b> half of the cast-iron mpenergy transmission (<see cref="EnergyMeshes"/> owns the mesh half):
/// how a run's shaft speed <c>ω</c> becomes an animation playback rate, and which way a bevel branch turns. Both
/// are pure so the convention is pinned headless - the visuals themselves are in-game-only.
/// </summary>
public static class EnergyAnim
{
  /// <summary>Below this fraction of <c>ω_max</c> a shaft reads as stopped: the spin clip is dropped for the rest
  /// pose rather than creeping imperceptibly. Matches the flywheel's 2% block-info sync step.</summary>
  private const float StoppedFraction = 0.01f;

  /// <summary>
  /// The animation playback multiplier for a shaft turning at <paramref name="omega"/> rad/s, for a clip authored
  /// as <b>one revolution</b> of its reference shaft. A clip plays at 1× in one second, so the multiplier is
  /// simply the shaft's <b>revolutions per second</b>, <c>ω / 2π</c> - the animation is therefore a direct readout
  /// of the run's speed rather than a tuned constant, and the only knob is the physical <c>MpMaxSpeed</c> itself.
  /// Never negative.
  /// </summary>
  public static float SpinSpeed(float omega) =>
    omega > 0f ? omega / GameMath.TWOPI : 0f;

  /// <summary>Whether a shaft at <paramref name="omega"/> is turning fast enough to animate, given the run's
  /// ceiling <paramref name="maxSpeed"/>. See <see cref="StoppedFraction"/>.</summary>
  public static bool IsTurning(float omega, float maxSpeed) =>
    omega > maxSpeed * StoppedFraction;

  /// <summary>
  /// Which way a bevel branch onto <paramref name="branchFace"/> turns, relative to the driving shaft: <c>+1</c>
  /// same sense, <c>-1</c> reversed. Both spins are expressed right-handed about their own <b>positive</b> axis.
  /// <para>
  /// It falls out of the mitre pair rather than being a lookup: the two pitch cones touch on the bisector between
  /// the driver's axis and the branch, and matching the contact velocity there gives
  /// <c>ω_branch = −sign(branchFace · its own axis) · ω_driver</c>. So a branch on the <b>positive</b> side of its
  /// axis (east, up, south) reverses and one on the <b>negative</b> side (west, down, north) keeps the sense —
  /// which is why the two branches of a single bevel turn <b>opposite ways</b>, exactly as a real crown-and-pinion
  /// pair does. Independent of which axis the driving shaft runs on.
  /// </para>
  /// </summary>
  public static int BranchSpinSign(BlockFacing branchFace) =>
    branchFace.Normali.X + branchFace.Normali.Y + branchFace.Normali.Z > 0
      ? -1
      : 1;
}
