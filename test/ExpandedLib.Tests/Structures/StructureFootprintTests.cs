using System;
using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The code-first footprint computer: a mega-block's <c>fillerOffsets</c> table is generated from a compact
/// description and validated at build time, instead of hand-typing a coordinate array. Pins the exact cell
/// set/order the ore-bunker footprint produces (so the migrated def stays byte-identical to the old JSON) and
/// that a bad footprint fails loudly rather than silently clobbering a filler.
/// </summary>
public class StructureFootprintTests
{
  #region Rectangle generation

  [Fact]
  public void Rectangle_reproduces_the_bunker_footprint_in_order()
  {
    // 3 wide (x = -1,0,1) x 6 deep (z = 0..5), origin skipped, flanking columns attachable - the exact
    // 17 cells the hand-written ore/bunker.json listed, in the same order (rows z-outer, columns 0,+1,-1).
    var expected = new List<FillerCellSpec>
    {
      new(1, 0, 0, AllowAttach: true),
      new(-1, 0, 0, AllowAttach: true),
      new(0, 0, 1),
      new(1, 0, 1, AllowAttach: true),
      new(-1, 0, 1, AllowAttach: true),
      new(0, 0, 2),
      new(1, 0, 2, AllowAttach: true),
      new(-1, 0, 2, AllowAttach: true),
      new(0, 0, 3),
      new(1, 0, 3, AllowAttach: true),
      new(-1, 0, 3, AllowAttach: true),
      new(0, 0, 4),
      new(1, 0, 4, AllowAttach: true),
      new(-1, 0, 4, AllowAttach: true),
      new(0, 0, 5),
      new(1, 0, 5, AllowAttach: true),
      new(-1, 0, 5, AllowAttach: true),
    };

    Assert.Equal(expected, StructureFootprint.Rectangle(halfWidth: 1, depth: 6));
  }

  [Fact]
  public void Rectangle_skips_only_the_principal_origin()
  {
    var cells = StructureFootprint.Rectangle(halfWidth: 1, depth: 6);
    Assert.DoesNotContain(cells, c => c is { X: 0, Y: 0, Z: 0 });
    Assert.Equal(17, cells.Count); // 3*6 - 1 origin
  }

  [Fact]
  public void Rectangle_marks_flanking_columns_attachable_and_centre_not()
  {
    foreach (FillerCellSpec cell in StructureFootprint.Rectangle(1, 6))
      Assert.Equal(cell.X != 0, cell.AllowAttach);
  }

  [Fact]
  public void Rectangle_of_zero_halfwidth_is_a_single_forward_column()
  {
    // No flanking columns: just the forward cells past the origin, none attachable.
    var cells = StructureFootprint.Rectangle(halfWidth: 0, depth: 3);
    Assert.Equal(
      new List<FillerCellSpec> { new(0, 0, 1), new(0, 0, 2) },
      cells
    );
  }

  [Theory]
  [InlineData(-1, 4)]
  [InlineData(1, 0)]
  public void Rectangle_rejects_degenerate_dimensions(int halfWidth, int depth)
  {
    Assert.Throws<ArgumentOutOfRangeException>(
      () => StructureFootprint.Rectangle(halfWidth, depth)
    );
  }

  #endregion

  #region Validation

  [Fact]
  public void Validate_rejects_a_cell_at_the_principal_origin()
  {
    var ex = Assert.Throws<ArgumentException>(
      () => StructureFootprint.Validate([new FillerCellSpec(0, 0, 0)])
    );
    Assert.Contains("origin", ex.Message);
  }

  [Fact]
  public void Validate_rejects_a_duplicate_cell()
  {
    var ex = Assert.Throws<ArgumentException>(
      () =>
        StructureFootprint.Validate(
          [new FillerCellSpec(1, 0, 0), new FillerCellSpec(1, 0, 0, true)]
        )
    );
    Assert.Contains("duplicate", ex.Message);
  }

  [Fact]
  public void Validate_accepts_a_clean_footprint()
  {
    StructureFootprint.Validate(
      [new FillerCellSpec(1, 0, 0), new FillerCellSpec(-1, 0, 0)]
    );
  }

  #endregion
}
