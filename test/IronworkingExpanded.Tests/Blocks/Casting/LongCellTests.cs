using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Casting;
using IronworkingExpanded.BlockStructures.Casting.Blocks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The 1×2 long cell: where its filler lands at each facing, and that its pattern-size guard is the
/// mirror of the 1×1 cell's.
/// </summary>
public class LongCellTests
{
  private static readonly BlockPos At = new(48, 12, 48, 0);

  private static BlockSandCastingLongCell Block(string side)
  {
    var block = TestBlocks.Configure(
      new BlockSandCastingLongCell(),
      $"iwex:casting-sandlongcell-fire-{side}",
      210,
      ("brick", "fire"),
      ("side", side)
    );
    // The footprint the code-first def emits. Read from the layout rather than restated, so a change to
    // one is a change to both - the golden covers that the def really carries it.
    block.Attributes = new JsonObject(
      JToken.Parse(
        """{ "fillerOffsets": [ { "x": 0, "y": 0, "z": 1 } ] }"""
      )
    );
    return block;
  }

  #region Footprint

  [Fact]
  public void The_layout_declares_exactly_one_filler_beside_the_principal()
  {
    FillerCellSpec cell = Assert.Single(LongCellLayout.Footprint());

    Assert.Equal((0, 0, 1), (cell.X, cell.Y, cell.Z));
    Assert.Equal(2, LongCellLayout.CellCount);
  }

  [Theory]
  [InlineData("n")]
  [InlineData("e")]
  [InlineData("s")]
  [InlineData("w")]
  public void The_filler_is_always_one_cell_away_horizontally(string side)
  {
    // At 1x2 the only thing that can be wrong is where the filler lands, so this is the whole test.
    // Asserting the property rather than four hard-coded positions means it still holds if the rotation
    // convention is ever re-based - and it would still catch a filler landing on the principal, two cells
    // out, or on a different Y.
    var block = Block(side);

    FillerCell filler = Assert.Single(
      StructureFillers.FootprintCells(block, At, block.StructureAngle)
    );

    Assert.Equal(At.Y, filler.Pos.Y);
    Assert.Equal(
      1,
      System.Math.Abs(filler.Pos.X - At.X) + System.Math.Abs(filler.Pos.Z - At.Z)
    );
  }

  [Fact]
  public void The_four_facings_put_the_filler_on_four_different_sides()
  {
    // The check the property test above cannot make on its own: a StructureAngle that ignored the side
    // variant would place the filler one cell away every time and pass, while every long cell in the world
    // pointed the same way.
    string[] sides = ["n", "e", "s", "w"];

    var positions = sides
      .Select(s =>
      {
        var block = Block(s);
        return StructureFillers
          .FootprintCells(block, At, block.StructureAngle)
          .Single()
          .Pos.ToString();
      })
      .ToList();

    Assert.Equal(4, positions.Distinct().Count());
  }

  [Theory]
  [InlineData("n")]
  [InlineData("e")]
  [InlineData("s")]
  [InlineData("w")]
  public void The_body_extends_away_from_the_side_the_cell_faces(string side)
  {
    // The +180 convention, stated as behaviour rather than as an arithmetic identity: a placed cell runs
    // away from the player, so the filler sits on the facing side - not behind the block.
    var block = Block(side);
    BlockFacing facing = ExOrientation.FacingFromSide(side)!;

    FillerCell filler = Assert.Single(
      StructureFillers.FootprintCells(block, At, block.StructureAngle)
    );

    Assert.Equal(At.AddCopy(facing), filler.Pos);
  }

  [Fact]
  public void The_structure_angle_is_the_side_angle_plus_the_half_turn()
  {
    // Pinned because it must equal the angle the shape is spun by (ShapeSpunPerOrientation offset 180).
    // The two are declared in different files and a mismatch does not fail to compile - it produces a
    // structure whose filler is on the opposite side from its model.
    foreach (string side in new[] { "n", "e", "s", "w" })
      Assert.Equal(
        ExOrientation.AngleFromSide(side) + 180,
        Block(side).StructureAngle
      );
  }

  #endregion

  #region Pattern size

  [Fact]
  public void A_longcell_pattern_is_accepted_by_the_long_cell()
  {
    var rig = CastingCellScenes.RammedFullLongCell();

    bool handled = rig.Interact(rig.PatternStack("castslab", size: "longcell", capacity: 3000));

    Assert.True(handled);
    Assert.True(rig.Cell.HasImpression);
    Assert.Null(rig.LastError);
  }

  [Fact]
  public void A_cell_sized_pattern_is_refused_by_the_long_cell()
  {
    // The exact mirror of the 1x1 cell's guard, and it needs its own error code: "this pattern is for the
    // other station" can only be acted on if the message names which station.
    var rig = CastingCellScenes.RammedFullLongCell();

    bool handled = rig.Interact(rig.PatternStack("heavyplate"));

    Assert.True(handled);
    Assert.False(rig.Cell.HasImpression);
    Assert.Equal("iwex-longcell-wrongsize", rig.LastError);
  }

  [Fact]
  public void The_impressed_capacity_is_the_whole_impression_not_one_lane()
  {
    // A multi-lane pattern yields more than one item out of one charge. If capacity were per lane, a
    // three-lane billet pattern would fill after 600 u and shake out three billets from one billet's worth
    // of metal - creating matter, which is the one thing the casting suite pins everywhere else.
    var rig = CastingCellScenes.RammedFullLongCell();

    rig.Interact(rig.PatternStack("castbillet", size: "longcell", capacity: 1800));

    Assert.Equal(
      1800,
      rig.Cell.GetBehavior<BEBehaviorMoltenCell>()!.MaxUnitCapacity
    );
  }

  #endregion
}
