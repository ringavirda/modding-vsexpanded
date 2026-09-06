using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Structures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Testing;

/// <summary>
/// Turns an ASCII layout into block placements, over the shared <see cref="CellGrid"/> core: each
/// non-blank glyph is mapped by a legend to a placement action, drawn as a horizontal plane where a
/// space is a gap between cells rather than a spacer (the opposite of a code-first layout's rule, since
/// a scene is meant to read at a glance). <see cref="SceneDiagram"/> is the public surface this backs.
/// </summary>
public sealed class SceneGrid {
  private readonly SymbolLegend<Action<BlockPos>> _legend =
    new(DuplicatePolicy.Replace);

  /// <summary>Maps a glyph to the action that places it at a resolved world position.</summary>
  public SceneGrid On(char glyph, Action<BlockPos> place) {
    _legend.Map(glyph, place);
    return this;
  }

  /// <summary>
  /// Applies one horizontal layer at height <paramref name="y"/>, with the top-left glyph at
  /// (<paramref name="originX"/>, <paramref name="y"/>, <paramref name="originZ"/>). A blank glyph
  /// (unmapped, or a gap between cells) is skipped, so labels and gaps cost nothing. Leading and
  /// trailing blank lines are trimmed.
  /// </summary>
  public SceneGrid Layer(string ascii, int y = 0, int originX = 0, int originZ = 0) {
    // A fresh grid per call: each Layer may declare its own origin, unlike a code-first layout's one
    // Origin for the whole drawing, so the grids cannot be accumulated across calls.
    var grid = new CellGrid(
      GridPlane.Horizontal,
      originX,
      originZ,
      new GridOptions(SpaceAdvancesColumn: true)
    );
    grid.Add(y, ascii);
    foreach (LayoutCell cell in grid.Cells)
      if (_legend.TryGet(cell.Symbol, out Action<BlockPos>? place))
        place(new BlockPos(cell.X, cell.Y, cell.Z));
    return this;
  }

  /// <summary>
  /// Stacks several horizontal layers along the Y axis in one call. <paramref name="layers"/>[0] sits at
  /// <paramref name="baseY"/>, [1] at <c>baseY + 1</c>, and so on (bottom to top); each entry is itself a
  /// multi-line X/Z plane in the same format as <see cref="Layer"/>. All layers share the
  /// (<paramref name="originX"/>, <paramref name="originZ"/>) origin so the columns line up vertically.
  /// </summary>
  public SceneGrid Stack(int baseY, int originX, int originZ, params string[] layers) {
    for (int i = 0; i < layers.Length; i++)
      Layer(layers[i], baseY + i, originX, originZ);
    return this;
  }

  /// <summary>Stacks layers from <paramref name="baseY"/> upward at origin (0,0); see the fuller overload.</summary>
  public SceneGrid Stack(int baseY, params string[] layers) => Stack(baseY, 0, 0, layers);
}
