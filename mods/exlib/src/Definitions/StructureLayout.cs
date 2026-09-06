using System.Collections.Generic;
using ExpandedLib.Structures;

namespace ExpandedLib.Definitions;

/// <summary>One parsed cell of a structure layout: its offset from the principal and the legend symbol.</summary>
public readonly record struct LayoutCell(int X, int Y, int Z, char Symbol);

/// <summary>
/// Parses the ASCII layer diagrams the multiblock and filler DSLs are authored with, over the shared
/// <see cref="CellGrid"/> core. A structure is a stack of 2D grids, one per Y level, whose rows run along
/// +Z and characters along +X, so the source reads as a top-down map and the coordinate table is
/// generated from it.
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
    var grid = new CellGrid(GridPlane.Horizontal, xLeft, zTop);
    foreach ((int y, string cells) in layers)
      grid.Add(y, cells);
    return new List<LayoutCell>(grid.Cells);
  }
}
