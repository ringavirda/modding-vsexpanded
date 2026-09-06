using System;
using System.Collections.Generic;
using ExpandedLib.Structures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The code-first footprint computer: a mega-block's <c>fillerOffsets</c> table generated from a compact
/// description and validated at build time. Covers the exact cell set and order produced for the ore-bunker
/// footprint, hosted-behaviour glyphs, and the validation rejections.
/// </summary>
public class StructureFootprintTests {
  #region Rectangle generation

  [Fact]
  public void Rectangle_reproduces_the_bunker_footprint_in_order() {
    // 3 wide (x = -1,0,1) x 6 deep (z = 0..5), origin skipped, flanking columns attachable: 17 cells,
    // ordered rows z-outer, columns 0,+1,-1.
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

    Assert.Equal(
      expected,
      StructureFootprint.Rectangle(halfWidth: 1, depth: 6)
    );
  }

  [Fact]
  public void Rectangle_skips_only_the_principal_origin() {
    var cells = StructureFootprint.Rectangle(halfWidth: 1, depth: 6);
    Assert.DoesNotContain(cells, c => c is { X: 0, Y: 0, Z: 0 });
    Assert.Equal(17, cells.Count); // 3*6 - 1 origin
  }

  [Fact]
  public void Rectangle_marks_flanking_columns_attachable_and_centre_not() {
    foreach (FillerCellSpec cell in StructureFootprint.Rectangle(1, 6))
      Assert.Equal(cell.X != 0, cell.AllowAttach);
  }

  [Fact]
  public void Rectangle_of_zero_halfwidth_is_a_single_forward_column() {
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
  public void Rectangle_rejects_degenerate_dimensions(int halfWidth, int depth) {
    Assert.Throws<ArgumentOutOfRangeException>(() =>
      StructureFootprint.Rectangle(halfWidth, depth)
    );
  }

  #endregion

  #region Hosted behaviours

  [Fact]
  public void Host_attaches_behaviours_to_every_cell_drawn_with_its_glyph() {
    // The twin-tub blower's shape: a 1x2x3 elevation whose upper-rear cell hosts the MP port an axle
    // couples to.
    var port = new FillerBehaviorSpec("exlib.BEBehaviorMPFillerPort", "west");
    IReadOnlyList<FillerCellSpec> cells = StructureFootprint.Layout(f =>
      f.Host('M', port)
        .Origin(0, 1)
        .Slice(
          0,
          """
          M##
          0##
          """
        )
    );

    Assert.Equal(5, cells.Count); // 6 drawn cells minus the skipped principal
    FillerCellSpec hosted = Assert.Single(cells, c => c.Behaviors != null);
    Assert.Equal((0, 1, 0), (hosted.X, hosted.Y, hosted.Z));
    Assert.Equal(port, Assert.Single(hosted.Behaviors!));
    // A hosted cell always allows attach: something has to couple to it.
    Assert.True(hosted.AllowAttach);
  }

  [Fact]
  public void A_plain_glyph_hosts_nothing() {
    IReadOnlyList<FillerCellSpec> cells = StructureFootprint.Layout(f =>
      f.Slice(0, "0#")
    );
    Assert.All(cells, c => Assert.Null(c.Behaviors));
  }

  #endregion

  #region Validation

  [Fact]
  public void Validate_rejects_a_cell_at_the_principal_origin() {
    var ex = Assert.Throws<ArgumentException>(() =>
      StructureFootprint.Validate([new FillerCellSpec(0, 0, 0)])
    );
    Assert.Contains("origin", ex.Message);
  }

  [Fact]
  public void Validate_rejects_a_duplicate_cell() {
    var ex = Assert.Throws<ArgumentException>(() =>
      StructureFootprint.Validate([
        new FillerCellSpec(1, 0, 0),
        new FillerCellSpec(1, 0, 0, true),
      ])
    );
    Assert.Contains("duplicate", ex.Message);
  }

  [Fact]
  public void Validate_accepts_a_clean_footprint() {
    StructureFootprint.Validate([
      new FillerCellSpec(1, 0, 0),
      new FillerCellSpec(-1, 0, 0),
    ]);
  }

  #endregion
}
