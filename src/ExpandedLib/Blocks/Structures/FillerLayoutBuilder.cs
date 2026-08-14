using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// Authors a mega-block <c>fillerOffsets</c> footprint from ASCII diagrams (grid rules in
/// <see cref="StructureFootprint.Layout"/> and <see cref="StructureLayout"/>). Draw it as one
/// <see cref="Layer"/> per Y level (floor plan, rows +Z / cols +X), one <see cref="Slice"/> per X level
/// (rows -Y down / cols +Z), or one <see cref="Face"/> per Z level (rows -Y down / cols +X); a builder
/// uses one kind only. By default <c>'#'</c> is a plain filler, <c>'+'</c> one other blocks may attach
/// to, <c>'.'</c> empty. The principal <c>(0,0,0)</c> may be marked <c>'O'</c> or <c>'0'</c>; it never
/// becomes a filler, and that glyph anywhere but the origin is a load-time error.
/// </summary>
public sealed class FillerLayoutBuilder {
  private int _originA;
  private int _originB;
  private readonly Dictionary<char, bool> _symbols = new() {
    ['#'] = false,
    ['+'] = true,
  };
  private readonly Dictionary<char, IReadOnlyList<FillerBehaviorSpec>> _hosted =
    new();
  private readonly Dictionary<char, IReadOnlyList<Cuboidf>> _boxes = new();
  private readonly List<(int Y, string Grid)> _layers = new();
  private readonly List<(int X, string Grid)> _slices = new();
  private readonly List<(int Z, string Grid)> _faces = new();

  /// <summary>Sets the top-left cell of every grid: <c>(xLeft, zTop)</c> for horizontal
  /// <see cref="Layer"/>s, <c>(zLeft, yTop)</c> for fixed-X <see cref="Slice"/>s, <c>(xLeft, yTop)</c>
  /// for fixed-Z <see cref="Face"/> elevations. Defaults to <c>(0, 0)</c>. A builder uses one kind of
  /// grid, since their Origin axes differ.</summary>
  public FillerLayoutBuilder Origin(int a, int b) {
    _originA = a;
    _originB = b;
    return this;
  }

  /// <summary>Registers a character as a plain filler cell (other blocks may not attach), overriding the
  /// default <c>'#'</c> mapping.</summary>
  public FillerLayoutBuilder Solid(char symbol) {
    _symbols[symbol] = false;
    return this;
  }

  /// <summary>Registers a character as a filler other blocks may attach to (the JSON <c>allowAttach: true</c>),
  /// overriding the default <c>'+'</c> mapping.</summary>
  public FillerLayoutBuilder Attach(char symbol) {
    _symbols[symbol] = true;
    return this;
  }

  /// <summary>
  /// Registers a character as an attach-allowing filler cell hosting <paramref name="behaviors"/> on the
  /// principal's behalf - the ports a mega-block exposes on a footprint cell, such as an MP filler port
  /// driven by an axle on that face, or a stateful molten cell.
  /// <code>
  ///   f.Host('M', FillerBehaviorSpec.Of&lt;BEBehaviorMPFillerPort&gt;("west"))
  ///    .Slice(0, """
  ///              M##
  ///              0##
  ///              """);
  /// </code>
  /// </summary>
  public FillerLayoutBuilder Host(
    char symbol,
    params FillerBehaviorSpec[] behaviors
  ) {
    _symbols[symbol] = true;
    _hosted[symbol] = behaviors;
    return this;
  }

  /// <summary>
  /// Registers a character as a filler that fills only the half of its cell against <paramref name="half"/>
  /// - a floor slab is <c>Slab('_', BlockFacing.DOWN)</c>, a slab against the north face
  /// <c>Slab('-', BlockFacing.NORTH)</c>. The cell keeps collision over that half and leaves the rest open,
  /// which is what lets a machine stand shorter than a whole cell without walling the space above it.
  /// <para>
  /// Attachment stays off, as it is for a plain filler: a partial cell is the machine's own volume, not a
  /// shelf. The box is authored in the north orientation and rotated with the rest of the footprint.
  /// </para>
  /// </summary>
  public FillerLayoutBuilder Slab(char symbol, BlockFacing half) {
    _symbols[symbol] = false;
    _boxes[symbol] = [FillerSlab.Half(half)];
    return this;
  }

  /// <summary>Adds one horizontal Y-level grid (a floor plan; rows run +Z, columns +X). Layers may be declared
  /// in any Y order.</summary>
  public FillerLayoutBuilder Layer(int y, string grid) {
    _layers.Add((y, grid));
    return this;
  }

  /// <summary>Adds one vertical X-level grid (a front elevation; rows run down in -Y from the top, columns
  /// run +Z). Suits a footprint that stacks in Y, such as an engine's beam column. Slices may be declared
  /// in any X order.</summary>
  public FillerLayoutBuilder Slice(int x, string grid) {
    _slices.Add((x, grid));
    return this;
  }

  /// <summary>Adds one vertical Z-level grid (a front elevation looking along -Z; rows run down in -Y
  /// from the top, columns run +X). Suits a thin-in-Z, north-facing structure such as a flywheel disc,
  /// whose face lies in the X-Y plane that neither <see cref="Layer"/> nor <see cref="Slice"/> draws
  /// in-plane. Faces may be declared in any Z order.</summary>
  public FillerLayoutBuilder Face(int z, string grid) {
    _faces.Add((z, grid));
    return this;
  }

  internal IReadOnlyList<FillerCellSpec> Build() {
    int kinds =
      (_layers.Count > 0 ? 1 : 0)
      + (_slices.Count > 0 ? 1 : 0)
      + (_faces.Count > 0 ? 1 : 0);
    if (kinds > 1)
      throw new InvalidOperationException(
        "Filler layout mixes Layer / Slice / Face grids; each uses a different Origin axis pair, so use one "
          + "kind in a single footprint."
      );

    var parsed =
      _faces.Count > 0
        ? StructureLayout.ParseFrontal(_originA, _originB, _faces)
      : _slices.Count > 0
        ? StructureLayout.ParseVertical(_originA, _originB, _slices)
      : StructureLayout.Parse(_originA, _originB, _layers);

    var cells = new List<FillerCellSpec>();
    foreach (LayoutCell cell in parsed) {
      if (cell.X == 0 && cell.Y == 0 && cell.Z == 0)
        continue; // the principal occupies the origin - never a filler, whatever glyph marks it
      if (cell.Symbol is 'O' or '0')
        throw new InvalidOperationException(
          $"Filler layout marks the principal ('{cell.Symbol}') at ({cell.X},{cell.Y},{cell.Z}), which is not "
            + "the origin (0,0,0) - check the grid's Origin offsets."
        );
      if (!_symbols.TryGetValue(cell.Symbol, out bool attach))
        throw new InvalidOperationException(
          $"Filler layout symbol '{cell.Symbol}' at ({cell.X},{cell.Y},{cell.Z}) is not registered "
            + "(use Solid/Attach/Slab/Host, or '#'/'+')."
        );
      cells.Add(
        new FillerCellSpec(
          cell.X,
          cell.Y,
          cell.Z,
          attach,
          _hosted.TryGetValue(cell.Symbol, out var hosted) ? hosted : null,
          _boxes.TryGetValue(cell.Symbol, out var boxes) ? boxes : null
        )
      );
    }
    StructureFootprint.Validate(cells);
    return cells;
  }
}
