using IronworkingExpanded.BlockNetworkEnergy;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The bevel is drawn by composing a shaft body with one gear mesh per geared face, each gear rotated from the
/// shape's south-authored reference onto its world face. The mesh output can only be eyeballed in-game, but the
/// rotation table that places each gear is pure - these pin it so a flipped face is caught here, not in a screenshot.
/// </summary>
public class EnergyMeshesTests
{
  private static void AssertRot(BlockFacing face, float x, float y, float z)
  {
    Vec3f rot = EnergyMeshes.GearRotation(face);
    Assert.Equal(x, rot.X, 4);
    Assert.Equal(y, rot.Y, 4);
    Assert.Equal(z, rot.Z, 4);
  }

  [Fact]
  public void South_is_the_authoring_reference_and_needs_no_rotation()
  {
    AssertRot(BlockFacing.SOUTH, 0f, 0f, 0f);
  }

  [Fact]
  public void The_four_horizontal_faces_turn_about_Y_like_the_end_cap()
  {
    AssertRot(BlockFacing.NORTH, 0f, GameMath.PI, 0f);
    AssertRot(BlockFacing.EAST, 0f, GameMath.PIHALF, 0f);
    AssertRot(BlockFacing.WEST, 0f, GameMath.PI + GameMath.PIHALF, 0f);
  }

  [Fact]
  public void Up_and_down_tip_the_gear_about_X()
  {
    AssertRot(BlockFacing.UP, -GameMath.PIHALF, 0f, 0f);
    AssertRot(BlockFacing.DOWN, GameMath.PIHALF, 0f, 0f);
  }
}
