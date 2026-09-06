using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace ExpandedLib.Structures;

/// <summary>
/// Which plane a text grid draws: rows and columns map to two world axes, the depth argument to the
/// third. <see cref="Horizontal"/> is a floor plan (one grid per Y level, rows +Z, columns +X);
/// <see cref="SliceX"/> is a front elevation at a fixed X (rows -Y from the top, columns +Z);
/// <see cref="FaceZ"/> is a front elevation at a fixed Z, looking along -Z (rows -Y from the top,
/// columns +X).
/// </summary>
public enum GridPlane {
  Horizontal,
  SliceX,
  FaceZ,
}

/// <summary>
/// How a text grid is read. <paramref name="SpaceAdvancesColumn"/> is false for a code-first layout (a
/// space is a spacer between cells, so <c>"# C"</c> draws two adjacent cells) and true for a test-scene
/// diagram (a space is a gap, so <c>"# C"</c> draws them two columns apart). <paramref name="Empty"/> is
/// the glyph that draws nothing but still advances the column. <paramref name="Anchor"/>, when set, is
/// the glyph <see cref="CellGrid.AnchorCell"/> reports the position of; it draws a normal cell like any
/// other glyph and carries no meaning inside this type - a caller such as a layout builder gives it one.
/// </summary>
public sealed record GridOptions(
  bool SpaceAdvancesColumn = false,
  char Empty = '.',
  char? Anchor = null
);

/// <summary>
/// Turns rows of symbols into a cell list: the one loop <see cref="ExpandedLib.Definitions.StructureLayout"/>,
/// the multiblock and filler layout builders and the testing harness's scene diagrams all drew separately.
/// One instance draws one plane (<see cref="GridPlane"/>); <see cref="Add"/> is called once per Y level, X
/// slice or Z face, in any order, and the accumulated cells are read from <see cref="Cells"/>. Two cells
/// may never land on the same world position, whichever <see cref="Add"/> call drew them - that is caught
/// here rather than left to whatever consumes <see cref="Cells"/> next.
/// </summary>
public sealed class CellGrid {
  private readonly GridPlane _plane;
  private readonly int _originA;
  private readonly int _originB;
  private readonly GridOptions _options;
  private readonly List<LayoutCell> _cells = new();
  private readonly HashSet<(int X, int Y, int Z)> _occupied = new();
  private readonly HashSet<char> _drawn = new();

  /// <summary>
  /// Starts an empty grid in <paramref name="plane"/>. <paramref name="originA"/> is the world value of
  /// the first column (X for <see cref="GridPlane.Horizontal"/> and <see cref="GridPlane.FaceZ"/>, Z for
  /// <see cref="GridPlane.SliceX"/>); <paramref name="originB"/> is the world value of the first row (Z
  /// for <see cref="GridPlane.Horizontal"/>, Y for the other two). <paramref name="options"/> defaults to
  /// a code-first layout's rules.
  /// </summary>
  public CellGrid(
    GridPlane plane,
    int originA,
    int originB,
    GridOptions? options = null
  ) {
    _plane = plane;
    _originA = originA;
    _originB = originB;
    _options = options ?? new GridOptions();
  }

  /// <summary>Every cell drawn so far, across every <see cref="Add"/> call, in drawing order.</summary>
  public IReadOnlyList<LayoutCell> Cells => _cells;

  /// <summary>True when <paramref name="symbol"/> was drawn by any <see cref="Add"/> call.</summary>
  public bool Drawn(char symbol) => _drawn.Contains(symbol);

  /// <summary>
  /// The position <see cref="GridOptions.Anchor"/> was drawn at, or null when no options were given an
  /// anchor glyph or that glyph was never drawn. The first occurrence wins; a second one is a duplicate
  /// cell position only if it lands on the same world cell, which <see cref="Add"/> already refuses.
  /// </summary>
  public (int X, int Y, int Z)? AnchorCell { get; private set; }

  /// <summary>
  /// Draws one Y level (<see cref="GridPlane.Horizontal"/>), X slice (<see cref="GridPlane.SliceX"/>) or
  /// Z face (<see cref="GridPlane.FaceZ"/>) at <paramref name="depth"/>. Blank lines at either end of
  /// <paramref name="grid"/> are trimmed; a blank line in the middle is an empty row. Within a row, a
  /// space or tab is a spacer that does not advance the column unless
  /// <see cref="GridOptions.SpaceAdvancesColumn"/> is set, in which case it advances like any other
  /// character; <see cref="GridOptions.Empty"/> always advances but draws nothing. Throws
  /// <see cref="System.InvalidOperationException"/> if a cell this call draws already sits at a
  /// position an earlier call drew.
  /// </summary>
  public CellGrid Add(int depth, string grid) {
    string[] rows = grid.Replace("\r", "").Split('\n');
    TrimBlankEnds(rows, out int first, out int last);

    int rowIndex = 0;
    for (int r = first; r <= last; r++) {
      int col = 0;
      foreach (char ch in rows[r]) {
        bool blank = ch is ' ' or '\t';
        if (blank && !_options.SpaceAdvancesColumn)
          continue; // a spacer between cells: neither drawn nor counted

        if (ch != _options.Empty) {
          (int x, int y, int z) = Resolve(depth, rowIndex, col);
          if (!_occupied.Add((x, y, z)))
            throw new System.InvalidOperationException(
              $"Layout grid draws two cells at ({x},{y},{z})."
            );
          _cells.Add(new LayoutCell(x, y, z, ch));
          _drawn.Add(ch);
          if (_options.Anchor == ch)
            AnchorCell ??= (x, y, z);
        }
        col++; // the empty glyph and every drawn glyph both advance the column
      }
      rowIndex++;
    }
    return this;
  }

  // Maps (depth, rowIndex, col) to a world cell for this grid's plane. Horizontal reads as a top-down
  // map (row = +Z, column = +X); the other two read as an elevation, top row highest (row = -Y from
  // originB, column = the plane's remaining horizontal axis).
  private (int X, int Y, int Z) Resolve(int depth, int rowIndex, int col) =>
    _plane switch {
      GridPlane.Horizontal => (_originA + col, depth, _originB + rowIndex),
      GridPlane.SliceX => (depth, _originB - rowIndex, _originA + col),
      _ => (_originA + col, _originB - rowIndex, depth), // FaceZ
    };

  // Indices of the first and last non-blank rows, so blank lines around a raw string literal do not
  // shift the depth axis. first > last when every row is blank.
  private static void TrimBlankEnds(string[] rows, out int first, out int last) {
    first = 0;
    last = rows.Length - 1;
    while (first <= last && IsBlank(rows[first]))
      first++;
    while (last >= first && IsBlank(rows[last]))
      last--;
  }

  private static bool IsBlank(string row) {
    foreach (char ch in row)
      if (ch is not (' ' or '\t'))
        return false;
    return true;
  }
}
