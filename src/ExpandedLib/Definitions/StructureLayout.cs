using System.Collections.Generic;

namespace ExpandedLib.Definitions;

/// <summary>One parsed cell of a structure layout: its offset from the principal and the legend symbol.</summary>
public readonly record struct LayoutCell(int X, int Y, int Z, char Symbol);

/// <summary>
/// Parses the ASCII layer diagrams the multiblock and filler DSLs are authored with. A structure is a
/// stack of 2D grids, one per Y level, whose rows run along +Z and characters along +X, so the source
/// reads as a top-down map and the coordinate table is generated from it.
/// <para>
/// The top-left non-space character sits at <c>(xLeft, y, zTop)</c>. <c>'.'</c> is an empty cell
/// (advances X, emits nothing), spaces and tabs are separators, blank lines at either end are trimmed,
/// and a blank line in the middle is an empty Z row.
/// </para>
/// </summary>
public static class StructureLayout {
  /// <summary>Parses the layers into their non-empty cells (each carrying its legend symbol for the caller to
  /// resolve). <paramref name="layers"/> is a list of <c>(y, grid)</c>; grids may be in any Y order.</summary>
  public static List<LayoutCell> Parse(
    int xLeft,
    int zTop,
    IReadOnlyList<(int Y, string Grid)> layers
  ) {
    var cells = new List<LayoutCell>();
    foreach ((int y, string grid) in layers) {
      string[] rows = grid.Replace("\r", "").Split('\n');
      TrimBlankEnds(rows, out int first, out int last);

      int z = zTop;
      for (int r = first; r <= last; r++) {
        int x = xLeft;
        foreach (char ch in rows[r]) {
          if (ch is ' ' or '\t')
            continue; // spacer between cells
          if (ch != '.')
            cells.Add(new LayoutCell(x, y, z, ch));
          x++; // '.' and real cells both advance the column
        }
        z++;
      }
    }
    return cells;
  }

  /// <summary>
  /// Parses vertical slices: one 2D grid per X level drawn as an elevation, for a structure whose cells
  /// stack in Y rather than spreading across a floor. The top row is the highest Y
  /// (<paramref name="yTop"/>, decreasing down the rows) and each column runs along +Z from
  /// <paramref name="zLeft"/>; the slice's X is fixed. Character rules are those of <see cref="Parse"/>.
  /// </summary>
  public static List<LayoutCell> ParseVertical(
    int zLeft,
    int yTop,
    IReadOnlyList<(int X, string Grid)> slices
  ) {
    var cells = new List<LayoutCell>();
    foreach ((int x, string grid) in slices) {
      string[] rows = grid.Replace("\r", "").Split('\n');
      TrimBlankEnds(rows, out int first, out int last);

      int y = yTop;
      for (int r = first; r <= last; r++) {
        int z = zLeft;
        foreach (char ch in rows[r]) {
          if (ch is ' ' or '\t')
            continue; // spacer between cells
          if (ch != '.')
            cells.Add(new LayoutCell(x, y, z, ch));
          z++; // '.' and real cells both advance the column (+Z)
        }
        y--; // each row down the grid is one step lower in Y
      }
    }
    return cells;
  }

  /// <summary>
  /// Parses frontal slices: one 2D grid per Z level drawn looking along -Z, for a structure whose face
  /// lies in the X-Y plane and is thin in Z. The top row is the highest Y (<paramref name="yTop"/>,
  /// decreasing down the rows) and each column runs along +X from <paramref name="xLeft"/>; the slice's
  /// Z is fixed. Character rules are those of <see cref="Parse"/>.
  /// </summary>
  public static List<LayoutCell> ParseFrontal(
    int xLeft,
    int yTop,
    IReadOnlyList<(int Z, string Grid)> faces
  ) {
    var cells = new List<LayoutCell>();
    foreach ((int z, string grid) in faces) {
      string[] rows = grid.Replace("\r", "").Split('\n');
      TrimBlankEnds(rows, out int first, out int last);

      int y = yTop;
      for (int r = first; r <= last; r++) {
        int x = xLeft;
        foreach (char ch in rows[r]) {
          if (ch is ' ' or '\t')
            continue; // spacer between cells
          if (ch != '.')
            cells.Add(new LayoutCell(x, y, z, ch));
          x++; // '.' and real cells both advance the column (+X)
        }
        y--; // each row down the grid is one step lower in Y
      }
    }
    return cells;
  }

  // Indices of the first and last non-blank rows, so blank lines around a raw string literal do not shift
  // Z. Returns first > last when every row is blank.
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
