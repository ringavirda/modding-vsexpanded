using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Molten;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Casting;
using IronIndustryExpanded.BlockStructures.Casting.Blocks;
using IronIndustryExpanded.Items;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The 1×2 long cell: where its filler lands at each facing, and that its pattern-size guard is the
/// mirror of the 1×1 cell's.
/// </summary>
public class LongCellTests {
  private static readonly BlockPos At = new(48, 12, 48, 0);

  private static BlockSandCastingLongCell Block(string side) {
    var block = TestBlocks.Configure(
      new BlockSandCastingLongCell(),
      $"iiex:casting-sandlongcell-fire-{side}",
      210,
      ("brick", "fire"),
      ("side", side)
    );
    // The footprint the code-first def emits; the golden covers that the def really carries it.
    block.Attributes = new JsonObject(
      JToken.Parse("""{ "fillerOffsets": [ { "x": 0, "y": 0, "z": 1 } ] }""")
    );
    return block;
  }

  #region Footprint

  [Fact]
  public void The_layout_declares_exactly_one_filler_beside_the_principal() {
    FillerCellSpec cell = Assert.Single(LongCellLayout.Footprint());

    Assert.Equal((0, 0, 1), (cell.X, cell.Y, cell.Z));
    Assert.Equal(2, LongCellLayout.CellCount);
  }

  [Theory]
  [InlineData("n")]
  [InlineData("e")]
  [InlineData("s")]
  [InlineData("w")]
  public void The_filler_is_always_one_cell_away_horizontally(string side) {
    // Asserted as a property rather than four fixed positions, so it survives a re-based rotation
    // convention and still catches a filler on the principal, two cells out, or on another Y.
    var block = Block(side);

    FillerCell filler = Assert.Single(
      StructureFillers.FootprintCells(block, At, block.StructureAngle)
    );

    Assert.Equal(At.Y, filler.Pos.Y);
    Assert.Equal(
      1,
      System.Math.Abs(filler.Pos.X - At.X)
        + System.Math.Abs(filler.Pos.Z - At.Z)
    );
  }

  [Fact]
  public void The_four_facings_put_the_filler_on_four_different_sides() {
    // A StructureAngle that ignored the side variant would place the filler one cell away every time
    // and pass the property test above, with every long cell pointing the same way.
    string[] sides = ["n", "e", "s", "w"];

    var positions = sides
      .Select(s => {
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
  public void The_body_extends_away_from_the_side_the_cell_faces(string side) {
    // A placed cell runs away from the player, so the filler sits on the facing side, not behind the
    // block.
    var block = Block(side);
    BlockFacing facing = ExOrientation.FacingFromSide(side)!;

    FillerCell filler = Assert.Single(
      StructureFillers.FootprintCells(block, At, block.StructureAngle)
    );

    Assert.Equal(At.AddCopy(facing), filler.Pos);
  }

  [Fact]
  public void The_structure_angle_is_the_side_angle_plus_the_half_turn() {
    // The shape spins by the side angle alone; the +180 here reconciles the footprint's declared cell
    // with the body the shape draws on the opposite side of it.
    foreach (string side in new[] { "n", "e", "s", "w" })
      Assert.Equal(
        ExOrientation.AngleFromSide(side) + 180,
        Block(side).StructureAngle
      );
  }

  #endregion

  #region Pattern size

  [Fact]
  public void A_longcell_pattern_is_accepted_by_the_long_cell() {
    var rig = CastingCellScenes.RammedFullLongCell();

    bool handled = rig.Interact(
      rig.PatternStack("castslab", size: "longcell", capacity: 3000)
    );

    Assert.True(handled);
    Assert.True(rig.Cell.HasImpression);
    Assert.Null(rig.LastError);
  }

  [Fact]
  public void A_cell_sized_pattern_is_refused_by_the_long_cell() {
    // The mirror of the 1x1 cell's guard, with its own error code so the message names which station
    // the pattern belongs to.
    var rig = CastingCellScenes.RammedFullLongCell();

    bool handled = rig.Interact(rig.PatternStack("heavyplate"));

    Assert.True(handled);
    Assert.False(rig.Cell.HasImpression);
    Assert.Equal("iiex-longcell-wrongsize", rig.LastError);
  }

  [Fact]
  public void The_impressed_capacity_is_the_whole_impression_not_one_lane() {
    // Capacity is the whole impression: a three-lane billet pattern holds 1800 u, not 600, so one
    // charge cannot shake out three billets' worth of metal.
    var rig = CastingCellScenes.RammedFullLongCell();

    rig.Interact(
      rig.PatternStack("castbillet", size: "longcell", capacity: 1800)
    );

    Assert.Equal(
      1800,
      rig.Cell.GetBehavior<BEBehaviorMoltenCell>()!.MaxUnitCapacity
    );
  }

  #endregion

  #region Yield

  [Fact]
  public void A_full_billet_pour_harvests_all_three_lanes() {
    // Capacity is the whole impression (1800 u), and the three-lane pattern's output carries the lane
    // count, so the harvest is three 600 u billets, not one.
    var rig = CastingCellScenes.RammedFullLongCell();
    rig.Interact(
      rig.PatternStack(
        "castbillet",
        size: "longcell",
        capacity: 1800,
        outputQuantity: 3
      )
    );
    rig.PourUntilFull(1200f).CoolToHardened();

    rig.InteractEmptyHanded();

    ItemStack part = Assert.Single(rig.Harvested);
    Assert.Equal(3, part.StackSize);
    Assert.Equal(1800, part.StackSize * CastStockItemDefinitions.BilletUnits);
  }

  #endregion

  #region Intake

  [Fact]
  public void The_long_cell_pulls_metal_fed_at_its_launder_face() {
    // The launder face is the block entity's own reckoning, not a literal offset: a filler at the wrong
    // face would leave the cell fed by nothing, which is the bug this pins.
    var rig = CastingCellScenes.RammedFullLongCell();
    rig.Interact(
      rig.PatternStack("castbillet", size: "longcell", capacity: 1800)
    );

    rig.PourUntilFull(1200f);

    Assert.Equal(
      1800,
      rig.Cell.GetBehavior<BEBehaviorMoltenCell>()!.CellAmount
    );
  }

  #endregion
}
