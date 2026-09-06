using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Structures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The grid core every layout DSL draws over: the axis mapping per plane, the origin, blank-line
/// trimming, the space and empty-glyph rules, the anchor and the cross-call duplicate check.
/// </summary>
public class CellGridTests {
  [Fact]
  public void Horizontal_maps_rows_to_z_and_columns_to_x() {
    var grid = new CellGrid(GridPlane.Horizontal, originA: -1, originB: 0);
    grid.Add(
      0,
      """
      . A .
      B . C
      """
    );

    Assert.Equal(3, grid.Cells.Count);
    Assert.Contains(new LayoutCell(0, 0, 0, 'A'), grid.Cells); // col1,row0
    Assert.Contains(new LayoutCell(-1, 0, 1, 'B'), grid.Cells); // col0,row1
    Assert.Contains(new LayoutCell(1, 0, 1, 'C'), grid.Cells); // col2,row1
  }

  [Fact]
  public void SliceX_maps_rows_down_in_y_and_columns_to_z() {
    var grid = new CellGrid(GridPlane.SliceX, originA: 0, originB: 2);
    grid.Add(
      0,
      """
      A . B
      . C .
      """
    );

    // depth=0 is the fixed X; row0 -> y=2 (top), row1 -> y=1; columns run +Z from originA.
    Assert.Equal(3, grid.Cells.Count);
    Assert.Contains(new LayoutCell(0, 2, 0, 'A'), grid.Cells);
    Assert.Contains(new LayoutCell(0, 2, 2, 'B'), grid.Cells);
    Assert.Contains(new LayoutCell(0, 1, 1, 'C'), grid.Cells);
  }

  [Fact]
  public void FaceZ_maps_rows_down_in_y_and_columns_to_x() {
    var grid = new CellGrid(GridPlane.FaceZ, originA: -1, originB: 2);
    grid.Add(
      0,
      """
      A . B
      . C .
      """
    );

    // depth=0 is the fixed Z; row0 -> y=2 (top), row1 -> y=1; columns run +X from originA.
    Assert.Equal(3, grid.Cells.Count);
    Assert.Contains(new LayoutCell(-1, 2, 0, 'A'), grid.Cells);
    Assert.Contains(new LayoutCell(1, 2, 0, 'B'), grid.Cells);
    Assert.Contains(new LayoutCell(0, 1, 0, 'C'), grid.Cells);
  }

  [Fact]
  public void Add_trims_surrounding_blank_lines() {
    var grid = new CellGrid(GridPlane.Horizontal, 0, 5);
    grid.Add(2, "\n\n  X  \n\n");

    var cell = Assert.Single(grid.Cells);
    Assert.Equal(new LayoutCell(0, 2, 5, 'X'), cell);
  }

  [Fact]
  public void Space_does_not_advance_the_column_by_default() {
    var grid = new CellGrid(GridPlane.Horizontal, 0, 0);
    grid.Add(0, "A B");

    Assert.Equal(2, grid.Cells.Count);
    Assert.Contains(new LayoutCell(0, 0, 0, 'A'), grid.Cells);
    Assert.Contains(new LayoutCell(1, 0, 0, 'B'), grid.Cells);
  }

  [Fact]
  public void Space_advances_the_column_when_the_option_is_set() {
    var grid = new CellGrid(
      GridPlane.Horizontal,
      0,
      0,
      new GridOptions(SpaceAdvancesColumn: true)
    );
    grid.Add(0, "A B");

    // The space itself becomes a drawn cell (a legend simply never maps it, as a scene's does not);
    // what the option changes is where the following glyph lands.
    Assert.Equal(3, grid.Cells.Count);
    Assert.Contains(new LayoutCell(0, 0, 0, 'A'), grid.Cells);
    Assert.Contains(new LayoutCell(2, 0, 0, 'B'), grid.Cells);
  }

  [Fact]
  public void The_empty_glyph_advances_the_column_but_draws_nothing() {
    var grid = new CellGrid(GridPlane.Horizontal, 0, 0);
    grid.Add(0, ".A");

    var cell = Assert.Single(grid.Cells);
    Assert.Equal(new LayoutCell(1, 0, 0, 'A'), cell);
  }

  [Fact]
  public void The_empty_glyph_is_configurable() {
    var grid = new CellGrid(GridPlane.Horizontal, 0, 0, new GridOptions(Empty: '_'));
    grid.Add(0, "_A.");

    // '_' is now empty (skipped); '.' is now an ordinary drawn glyph.
    Assert.Equal(2, grid.Cells.Count);
    Assert.Contains(new LayoutCell(1, 0, 0, 'A'), grid.Cells);
    Assert.Contains(new LayoutCell(2, 0, 0, '.'), grid.Cells);
  }

  [Fact]
  public void AnchorCell_is_null_when_no_anchor_glyph_is_configured() {
    var grid = new CellGrid(GridPlane.Horizontal, 0, 0);
    grid.Add(0, "AB");
    Assert.Null(grid.AnchorCell);
  }

  [Fact]
  public void AnchorCell_reports_where_the_anchor_glyph_was_drawn() {
    var grid = new CellGrid(
      GridPlane.Horizontal,
      0,
      0,
      new GridOptions(Anchor: 'C')
    );
    grid.Add(0, "AC");
    Assert.Equal((1, 0, 0), grid.AnchorCell);
  }

  [Fact]
  public void Drawn_reports_every_symbol_the_grid_has_drawn() {
    var grid = new CellGrid(GridPlane.Horizontal, 0, 0);
    grid.Add(0, "AB");
    Assert.True(grid.Drawn('A'));
    Assert.True(grid.Drawn('B'));
    Assert.False(grid.Drawn('C'));
  }

  [Fact]
  public void Add_rejects_two_cells_at_the_same_position() {
    var grid = new CellGrid(GridPlane.Horizontal, 0, 0);
    grid.Add(0, "A");
    Assert.Throws<InvalidOperationException>(() => grid.Add(0, "B"));
  }
}
