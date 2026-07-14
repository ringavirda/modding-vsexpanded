using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Definitions;

/// <summary>
/// Authors a <c>multiblockStructure</c> from ASCII layer diagrams instead of a coordinate array: a
/// <see cref="Legend"/> maps characters to block codes, and one <see cref="Layer"/> per Y level draws the
/// build as a top-down grid (see <see cref="StructureLayout"/> for the grid rules). The block numbers and the
/// offsets table are generated from the drawing, then validated through <see cref="MultiblockBuilder"/> (every
/// cell resolves to a legend entry; no cell is drawn twice). Because the game treats the offsets as an
/// unordered set of required blocks and the <c>w</c> numbers as a private index, the generated table is
/// behaviour-identical to any hand-written ordering of the same cells.
/// </summary>
public sealed class MultiblockLayoutBuilder
{
  private int _xLeft;
  private int _zTop;
  private readonly List<(char Symbol, string Code)> _legend = new();
  private readonly List<(int Y, string Grid)> _layers = new();

  /// <summary>Sets where the top-left of every layer grid sits: <paramref name="xLeft"/> is the X of the first
  /// column, <paramref name="zTop"/> the Z of the first row. Defaults to <c>(0, 0)</c>.</summary>
  public MultiblockLayoutBuilder Origin(int xLeft, int zTop)
  {
    _xLeft = xLeft;
    _zTop = zTop;
    return this;
  }

  /// <summary>Maps a grid character to a block code (a wildcard/selector, e.g.
  /// <c>Legend('#', "exlib:structurefiller")</c>). <c>'.'</c> is reserved for empty cells.</summary>
  public MultiblockLayoutBuilder Legend(char symbol, string code)
  {
    if (symbol is '.' or ' ')
      throw new ArgumentException(
        "'.' and space are reserved for empty cells and cannot be legend symbols."
      );
    _legend.Add((symbol, code));
    return this;
  }

  /// <summary>Adds one Y-level grid (see <see cref="StructureLayout"/> for the drawing rules). Layers may be
  /// declared in any Y order.</summary>
  public MultiblockLayoutBuilder Layer(int y, string grid)
  {
    _layers.Add((y, grid));
    return this;
  }

  internal JObject Build()
  {
    var mb = new MultiblockBuilder();
    var wOf = new Dictionary<char, int>();
    int w = 1;
    foreach ((char symbol, string code) in _legend)
    {
      if (!wOf.ContainsKey(symbol))
        wOf[symbol] = w++;
      mb.Number(code, wOf[symbol]);
    }

    foreach (LayoutCell cell in StructureLayout.Parse(_xLeft, _zTop, _layers))
    {
      if (!wOf.TryGetValue(cell.Symbol, out int cellW))
        throw new InvalidOperationException(
          $"Multiblock layout uses symbol '{cell.Symbol}' at ({cell.X},{cell.Y},{cell.Z}) with no Legend entry."
        );
      mb.At(cell.X, cell.Y, cell.Z, cellW);
    }
    return mb.Build();
  }
}
