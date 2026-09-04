using ExpandedLib;
using IronIndustryExpanded.BlockNetworkEnergy;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Motion conventions of the cast-iron transmission: a spin clip's playback rate is the shaft's revolutions
/// per second, and a bevel branch turns with or against the driving sense depending on which side of its
/// axis it sits. The animation itself is verified in-game.
/// </summary>
public class EnergyAnimTests {
  #region Spin speed (clip rate = revolutions per second)

  [Fact]
  public void A_clip_is_one_revolution_so_its_rate_is_revolutions_per_second() {
    // The clip is authored as one revolution, so a shaft at 2*pi rad/s (one revolution a second) plays
    // it at 1x.
    Assert.Equal(1f, EnergyAnim.SpinSpeed(GameMath.TWOPI), 4);
    Assert.Equal(2f, EnergyAnim.SpinSpeed(2f * GameMath.TWOPI), 4);
    Assert.Equal(0.5f, EnergyAnim.SpinSpeed(GameMath.PI), 4);
  }

  [Fact]
  public void A_stopped_shaft_has_no_playback_rate() {
    Assert.Equal(0f, EnergyAnim.SpinSpeed(0f));
    Assert.Equal(0f, EnergyAnim.SpinSpeed(-1f)); // never negative, whatever the caller hands over
  }

  [Fact]
  public void A_barely_creeping_shaft_reads_as_stopped() {
    float max = ExlibValues.MpMaxSpeed;
    Assert.False(EnergyAnim.IsTurning(0f, max));
    Assert.False(EnergyAnim.IsTurning(max * 0.005f, max)); // under the threshold: rest pose, not a crawl
    Assert.True(EnergyAnim.IsTurning(max * 0.5f, max));
    Assert.True(EnergyAnim.IsTurning(max, max));
  }

  #endregion

  #region Bevel branch direction

  [Fact]
  public void The_two_branches_of_one_bevel_turn_opposite_ways() {
    // The mitre pair reverses on the positive side of the axis and keeps the sense on the negative side, so
    // a west branch and an east branch off the same shaft counter-rotate.
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
  public void A_branch_on_the_negative_side_of_its_axis_keeps_the_driving_sense() {
    Assert.Equal(1, EnergyAnim.BranchSpinSign(BlockFacing.WEST)); // -X
    Assert.Equal(1, EnergyAnim.BranchSpinSign(BlockFacing.DOWN)); // -Y
    Assert.Equal(1, EnergyAnim.BranchSpinSign(BlockFacing.NORTH)); // -Z
  }

  [Fact]
  public void A_branch_on_the_positive_side_of_its_axis_reverses() {
    Assert.Equal(-1, EnergyAnim.BranchSpinSign(BlockFacing.EAST)); // +X
    Assert.Equal(-1, EnergyAnim.BranchSpinSign(BlockFacing.UP)); // +Y
    Assert.Equal(-1, EnergyAnim.BranchSpinSign(BlockFacing.SOUTH)); // +Z
  }

  #endregion
}
