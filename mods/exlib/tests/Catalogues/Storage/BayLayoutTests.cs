using System.Collections.Generic;
using ExpandedLib.Catalogues;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// A row of cells filled by length. The whole capacity model of a storage rack is here and it is pure, so
/// what a rack holds is answerable without a world, a block or a mesh - which is the point of declaring
/// piece size rather than measuring it off a drawn shape.
/// </summary>
public class BayLayoutTests {
  private const int Cells = 3;

  private static List<BayRun> Row(params (int Start, int Length)[] runs) {
    var list = new List<BayRun>();
    foreach (var (start, length) in runs)
      list.Add(new BayRun(start, length));
    return list;
  }

  #region The three arrangements of a three-cell row

  /// <summary>
  /// The owner's enumeration, which is the whole spec: three one-cell stacks, a two beside a one, or a
  /// single three. Driven rather than restated - each arrangement is laid a piece at a time through
  /// <see cref="BayLayout.Fit"/>, so a fit rule that happened to agree on the totals but packed wrongly
  /// still fails.
  /// </summary>
  [Theory]
  [InlineData(new[] { 1, 1, 1 }, new[] { 0, 1, 2 })]
  [InlineData(new[] { 2, 1 }, new[] { 0, 2 })]
  [InlineData(new[] { 1, 2 }, new[] { 0, 1 })]
  [InlineData(new[] { 3 }, new[] { 0 })]
  public void A_three_cell_row_takes_exactly_the_arrangements_the_spec_lists(
    int[] lengths,
    int[] starts
  ) {
    var row = new List<BayRun>();
    for (int i = 0; i < lengths.Length; i++) {
      int? start = BayLayout.Fit(row, Cells, lengths[i]);
      Assert.Equal(starts[i], start);
      row.Add(new BayRun(start!.Value, lengths[i]));
    }

    // Full, and full to the cell: an arrangement that left a gap would be a different arrangement.
    Assert.Equal(0, BayLayout.FreeCells(row, Cells));
    Assert.Null(BayLayout.Fit(row, Cells, 1));
  }

  [Theory]
  [InlineData(new[] { 1, 1, 1, 1 })]
  [InlineData(new[] { 2, 2 })]
  [InlineData(new[] { 3, 1 })]
  [InlineData(new[] { 2, 1, 1 })]
  public void A_full_row_refuses_the_next_piece(int[] lengths) {
    var row = new List<BayRun>();
    int refused = 0;
    foreach (int length in lengths) {
      if (BayLayout.Fit(row, Cells, length) is { } start)
        row.Add(new BayRun(start, length));
      else
        refused++;
    }

    Assert.Equal(1, refused);
    Assert.True(BayLayout.FreeCells(row, Cells) < 3);
  }

  [Fact]
  public void A_piece_longer_than_the_row_never_fits_however_empty_it_is() {
    Assert.Null(BayLayout.Fit([], Cells, 4));
    Assert.Null(BayLayout.Fit([], Cells, 0));
  }

  #endregion

  #region Where a piece lands

  [Fact]
  public void A_two_cell_piece_needs_two_cells_that_are_next_to_each_other() {
    // One free cell at each end, two free in total, and still no room: the run has to be contiguous. A
    // fit rule that counted free cells rather than placing a run would put a slab in two halves.
    List<BayRun> row = Row((1, 1));

    Assert.Equal(2, BayLayout.FreeCells(row, Cells));
    Assert.Null(BayLayout.Fit(row, Cells, 2));
    Assert.Equal(0, BayLayout.Fit(row, Cells, 1));
  }

  [Fact]
  public void A_piece_goes_in_the_lowest_gap_that_holds_it() {
    // Lowest-first rather than nearest-the-click: a row that packed toward whichever cell was clicked
    // would strand gaps a longer piece could have used.
    Assert.Equal(0, BayLayout.Fit(Row((2, 1)), 4, 1));
    Assert.Equal(1, BayLayout.Fit(Row((0, 1)), 4, 2));
  }

  #endregion

  #region Which piece a cell belongs to

  [Fact]
  public void Every_cell_of_a_run_names_that_run() {
    // What makes each cell of the rack work the whole of it: clicking any cell of a three-cell slab takes
    // the slab. Without this only the cell a piece started at would answer.
    List<BayRun> row = Row((0, 3));

    for (int cell = 0; cell < 3; cell++)
      Assert.Equal(0, BayLayout.IndexAt(row, cell));
  }

  [Fact]
  public void An_empty_cell_names_nothing_and_a_neighbour_does_not_answer_for_it() {
    List<BayRun> row = Row((0, 1), (2, 1));

    Assert.Equal(0, BayLayout.IndexAt(row, 0));
    Assert.Null(BayLayout.IndexAt(row, 1));
    Assert.Equal(1, BayLayout.IndexAt(row, 2));
  }

  #endregion

  #region Overlap

  [Fact]
  public void A_run_cannot_be_laid_over_one_that_is_already_there() {
    List<BayRun> row = Row((1, 1));

    Assert.False(BayLayout.Fits(row, Cells, 1, 1)); // exactly on it
    Assert.False(BayLayout.Fits(row, Cells, 0, 2)); // overlapping its start
    Assert.False(BayLayout.Fits(row, Cells, 1, 2)); // overlapping its end
    Assert.True(BayLayout.Fits(row, Cells, 0, 1));
    Assert.True(BayLayout.Fits(row, Cells, 2, 1));
  }

  [Fact]
  public void A_run_cannot_hang_off_either_end_of_the_row() {
    Assert.False(BayLayout.Fits([], Cells, -1, 1));
    Assert.False(BayLayout.Fits([], Cells, 2, 2));
    Assert.True(BayLayout.Fits([], Cells, 2, 1));
  }

  #endregion
}
