using ExpandedLib;
using IronworkingExpanded.BlockNetworkEnergy;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The motion conventions of the cast-iron transmission. The animation itself is in-game-only, but the two rules
/// behind it are pure: a spin clip's playback rate is the shaft's revolutions per second, and a bevel branch turns
/// the same way or the opposite way depending on which side of its axis it sits. These pin both, so a shaft
/// running backwards is caught here rather than in a screenshot.
/// </summary>
public class EnergyAnimTests
{
  #region Spin speed (clip rate = revolutions per second)

  [Fact]
  public void A_clip_is_one_revolution_so_its_rate_is_revolutions_per_second()
  {
    // A shaft turning at 2*pi rad/s makes exactly one revolution a second, and the clip is authored as one
    // revolution - so it plays at 1x. That equivalence is the whole mapping.
    Assert.Equal(1f, EnergyAnim.SpinSpeed(GameMath.TWOPI), 4);
    Assert.Equal(2f, EnergyAnim.SpinSpeed(2f * GameMath.TWOPI), 4);
    Assert.Equal(0.5f, EnergyAnim.SpinSpeed(GameMath.PI), 4);
  }

  [Fact]
  public void A_stopped_shaft_has_no_playback_rate()
  {
    Assert.Equal(0f, EnergyAnim.SpinSpeed(0f));
    Assert.Equal(0f, EnergyAnim.SpinSpeed(-1f)); // never negative, whatever the caller hands over
  }

  [Fact]
  public void A_barely_creeping_shaft_reads_as_stopped()
  {
    float max = ExlibValues.MpMaxSpeed;
    Assert.False(EnergyAnim.IsTurning(0f, max));
    Assert.False(EnergyAnim.IsTurning(max * 0.005f, max)); // under the threshold: rest pose, not a crawl
    Assert.True(EnergyAnim.IsTurning(max * 0.5f, max));
    Assert.True(EnergyAnim.IsTurning(max, max));
  }

  #endregion

  #region Bevel branch direction

  [Fact]
  public void The_two_branches_of_one_bevel_turn_opposite_ways()
  {
    // The mitre pair reverses on the positive side of the axis and keeps the sense on the negative side, so a
    // west branch and an east branch off the same shaft counter-rotate. This is the rule the user called out.
    Assert.NotEqual(
      EnergyAnim.BranchSpinSign(BlockFacing.WEST),
      EnergyAnim.BranchSpinSign(BlockFacing.EAST)
    );
    Assert.NotEqual(
      EnergyAnim.BranchSpinSign(BlockFacing.UP),
      EnergyAnim.BranchSpinSign(BlockFacing.DOWN)
    );
    Assert.NotEqual(
      EnergyAnim.BranchSpinSign(BlockFacing.NORTH),
      EnergyAnim.BranchSpinSign(BlockFacing.SOUTH)
    );
  }

  [Fact]
  public void A_branch_on_the_negative_side_of_its_axis_keeps_the_driving_sense()
  {
    Assert.Equal(1, EnergyAnim.BranchSpinSign(BlockFacing.WEST)); // -X
    Assert.Equal(1, EnergyAnim.BranchSpinSign(BlockFacing.DOWN)); // -Y
    Assert.Equal(1, EnergyAnim.BranchSpinSign(BlockFacing.NORTH)); // -Z
  }

  [Fact]
  public void A_branch_on_the_positive_side_of_its_axis_reverses()
  {
    Assert.Equal(-1, EnergyAnim.BranchSpinSign(BlockFacing.EAST)); // +X
    Assert.Equal(-1, EnergyAnim.BranchSpinSign(BlockFacing.UP)); // +Y
    Assert.Equal(-1, EnergyAnim.BranchSpinSign(BlockFacing.SOUTH)); // +Z
  }

  #endregion
}
