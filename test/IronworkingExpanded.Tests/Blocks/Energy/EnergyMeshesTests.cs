using IronworkingExpanded.BlockNetworkEnergy;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The bevel mesh is a shaft body plus one gear mesh per geared face, each gear rotated from the shape's
/// south-authored reference onto its world face. Covers that rotation table; the assembled mesh is
/// verified in-game.
/// </summary>
public class EnergyMeshesTests {
  private static void AssertRot(BlockFacing face, float x, float y, float z) {
    Vec3f rot = EnergyMeshes.GearRotation(face);
    Assert.Equal(x, rot.X, 4);
    Assert.Equal(y, rot.Y, 4);
    Assert.Equal(z, rot.Z, 4);
  }

  [Fact]
  public void South_is_the_authoring_reference_and_needs_no_rotation() {
    AssertRot(BlockFacing.SOUTH, 0f, 0f, 0f);
  }

  [Fact]
  public void The_four_horizontal_faces_turn_about_Y_like_the_end_cap() {
    AssertRot(BlockFacing.NORTH, 0f, GameMath.PI, 0f);
    AssertRot(BlockFacing.EAST, 0f, GameMath.PIHALF, 0f);
    AssertRot(BlockFacing.WEST, 0f, GameMath.PI + GameMath.PIHALF, 0f);
  }

  [Fact]
  public void Up_and_down_tip_the_gear_about_X() {
    AssertRot(BlockFacing.UP, -GameMath.PIHALF, 0f, 0f);
    AssertRot(BlockFacing.DOWN, GameMath.PIHALF, 0f, 0f);
  }
}
