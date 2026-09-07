using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Casting.Blocks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Ties the sand casting long cell's drawn mesh to the volume it reserves, at every orientation it can be
/// laid at, and ties both cells' drawn launder spout to the face the block entity pulls metal from. The
/// footprint rule is <see cref="MegablockFrames"/>'s, same as the Cornish boiler's guard; the spout rule is
/// that a canal feeding the block entity's <c>LaunderFace</c> is feeding the face the art actually opens.
/// </summary>
public class CastingCellFootprintGuards {
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_drawn_mesh_lands_inside_the_reserved_footprint(string side) {
    ExBlockDef def = BlockSandCastingLongCell.Definitions("iiex").Single();
    JObject json = def.ToJson();

    var block = TestBlocks.Configure(
      new BlockSandCastingLongCell(),
      $"iiex:casting-sandlongcell-fire-{side[0]}",
      210,
      ("brick", "fire"),
      ("side", side)
    );
    block.Attributes = new JsonObject((JObject)json["attributes"]!);

    JToken shape = json["shape"]!;
    string? misfit = MegablockFrames.Misfit(
      MegablockFrames.ShapeFile((string)shape["base"]!),
      (int)shape["rotateYByType"]![$"*-{side[0]}"]!,
      block,
      block.StructureAngle
    );

    Assert.True(misfit == null, $"A '{side}' long cell {misfit}");
  }

  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_1x1_cell_pulls_from_the_face_the_spout_is_drawn_on(
    string side
  ) =>
    AssertSpoutFacesLaunder(
      BlockSandCastingCell.Definitions("iiex").First(),
      side
    );

  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_long_cell_pulls_from_the_face_the_spout_is_drawn_on(
    string side
  ) =>
    AssertSpoutFacesLaunder(
      BlockSandCastingLongCell.Definitions("iiex").Single(),
      side
    );

  /// <summary>
  /// Rotates the spout's drawn centre (Cube8, on the principal cell both shapes share) by the spin the
  /// shipped definition declares for that side, about the cell centre (8, y, 8) with the same
  /// quarter-turn handedness <see cref="MegablockFrames"/> uses, and checks the result falls on the
  /// launder face's side of the centre. The block entity cannot be stood headless in this fixture, so
  /// the launder face is computed the way <c>BlockEntitySandCastingCell.LaunderFace</c> does: the
  /// block's facing, turned around.
  /// </summary>
  private static void AssertSpoutFacesLaunder(ExBlockDef def, string side) {
    string letter = side[0].ToString();
    JToken shape = def.ToJson()["shape"]!;
    (float[] min, float[] max) = ShapeExtents.Bounds(
      MegablockFrames.ShapeFile((string)shape["base"]!),
      "Cube8"
    );
    float x = (min[0] + max[0]) / 2f;
    float z = (min[2] + max[2]) / 2f;

    int spin = (int)shape["rotateYByType"]![$"*-{letter}"]!;
    (double rx, double rz) = ExOrientation.RotateXZ(x - 8, z - 8, spin);

    BlockFacing launderFace = ExOrientation.FacingFromSide(letter)!.Opposite;

    double offset = launderFace.Normali.X * rx + launderFace.Normali.Z * rz;
    Assert.True(
      offset > 0,
      $"The '{side}' spout rotates to ({rx:0.###}, {rz:0.###}) about the "
        + $"cell centre, which does not lie on the {launderFace.Code} "
        + "launder face."
    );
  }
}
