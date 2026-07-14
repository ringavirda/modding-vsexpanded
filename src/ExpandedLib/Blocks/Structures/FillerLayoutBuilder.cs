using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// Authors a mega-block <c>fillerOffsets</c> footprint from ASCII diagrams (see
/// <see cref="StructureFootprint.Layout"/> and <see cref="StructureLayout"/> for the grid rules). Draw it either
/// way: one <see cref="Layer"/> per Y level (a floor plan, rows +Z / cols +X) for a footprint that spreads across
/// the ground, or one <see cref="Slice"/> per X level (a front elevation, rows -Y down / cols +Z) for one that
/// stacks up in Y. A cell's character says whether it is a plain filler or one other blocks may attach to: by
/// default <c>'#'</c> is plain and <c>'+'</c> attach-allowing; <c>'.'</c> is empty. The principal
/// <c>(0,0,0)</c> is marked <c>'O'</c> (or <c>'0'</c>) for readability - it is skipped, so it never becomes
/// a filler; drawing the principal glyph anywhere but the origin is a load-time error (a misplaced grid).
/// </summary>
public sealed class FillerLayoutBuilder
{
  private int _originA;
  private int _originB;
  private readonly Dictionary<char, bool> _symbols = new() { ['#'] = false, ['+'] = true };
  private readonly List<(int Y, string Grid)> _layers = new();
  private readonly List<(int X, string Grid)> _slices = new();

  /// <summary>Sets the top-left cell of every grid. For horizontal <see cref="Layer"/>s the pair is
  /// <c>(xLeft, zTop)</c> - the X of the first column and the Z of the first row. For vertical
  /// <see cref="Slice"/>s it is <c>(zLeft, yTop)</c> - the Z of the first column and the Y of the top row.
  /// Defaults to <c>(0, 0)</c>. A builder uses layers OR slices, not both.</summary>
  public FillerLayoutBuilder Origin(int a, int b)
  {
    _originA = a;
    _originB = b;
    return this;
  }

  /// <summary>Registers a character as a plain filler cell (other blocks may not attach). Overrides the
  /// default <c>'#'</c> mapping when a different glyph reads better.</summary>
  public FillerLayoutBuilder Solid(char symbol)
  {
    _symbols[symbol] = false;
    return this;
  }

  /// <summary>Registers a character as a filler other blocks may attach to (the JSON <c>allowAttach: true</c>).
  /// Overrides the default <c>'+'</c> mapping.</summary>
  public FillerLayoutBuilder Attach(char symbol)
  {
    _symbols[symbol] = true;
    return this;
  }

  /// <summary>Adds one horizontal Y-level grid (a floor plan; rows run +Z, columns +X). Layers may be
  /// declared in any Y order.</summary>
  public FillerLayoutBuilder Layer(int y, string grid)
  {
    _layers.Add((y, grid));
    return this;
  }

  /// <summary>Adds one vertical X-level grid (a front elevation; rows run down in -Y from the top, columns
  /// run +Z). For a footprint that stacks in Y - an engine's beam column, a piston tower - this reads far more
  /// naturally than a stack of one-row horizontal layers. Slices may be declared in any X order.</summary>
  public FillerLayoutBuilder Slice(int x, string grid)
  {
    _slices.Add((x, grid));
    return this;
  }

  internal IReadOnlyList<FillerCellSpec> Build()
  {
    if (_layers.Count > 0 && _slices.Count > 0)
      throw new InvalidOperationException(
        "Filler layout mixes horizontal Layer and vertical Slice grids; the Origin axes differ, so use one "
          + "or the other in a single footprint."
      );

    var parsed =
      _slices.Count > 0
        ? StructureLayout.ParseVertical(_originA, _originB, _slices)
        : StructureLayout.Parse(_originA, _originB, _layers);

    var cells = new List<FillerCellSpec>();
    foreach (LayoutCell cell in parsed)
    {
      if (cell.X == 0 && cell.Y == 0 && cell.Z == 0)
        continue; // the principal occupies the origin - never a filler (whatever glyph marks it)
      if (cell.Symbol is 'O' or '0')
        throw new InvalidOperationException(
          $"Filler layout marks the principal ('{cell.Symbol}') at ({cell.X},{cell.Y},{cell.Z}), which is not "
            + "the origin (0,0,0) - check the grid's Origin offsets."
        );
      if (!_symbols.TryGetValue(cell.Symbol, out bool attach))
        throw new InvalidOperationException(
          $"Filler layout symbol '{cell.Symbol}' at ({cell.X},{cell.Y},{cell.Z}) is not registered "
            + "(use Solid/Attach, or '#'/'+')."
        );
      cells.Add(new FillerCellSpec(cell.X, cell.Y, cell.Z, attach));
    }
    StructureFootprint.Validate(cells);
    return cells;
  }
}
