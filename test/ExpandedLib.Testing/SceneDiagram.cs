using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Testing;

/// <summary>
/// Turns an ASCII layout into block placements. Each non-blank glyph is mapped by a legend to a placement
/// action; a layer is one horizontal (X/Z) plane at a given height, and several layers stack into a 3D
/// setup. A row's characters advance along +X, successive rows advance along +Z, and each
/// <see cref="Layer"/> sits at its own Y. The legend is supplied by the test, so the parser stays
/// mod-agnostic.
/// <code>
///   diagram.On('=', p =&gt; scene.Node(p, WePipe(), new BlockEntityPipe(), "pipe"))
///          .On('#', p =&gt; scene.Block(p, Rock))
///          .Layer("#====#");   // five-cell west-east pipe run between two rock caps
/// </code>
/// </summary>
public sealed class SceneDiagram {
  private readonly Dictionary<char, System.Action<BlockPos>> _legend = new();

  /// <summary>Maps a glyph to the action that places it at a resolved world position.</summary>
  public SceneDiagram On(char glyph, System.Action<BlockPos> place) {
    _legend[glyph] = place;
    return this;
  }

  /// <summary>
  /// Applies one horizontal layer at height <paramref name="y"/>, with the top-left glyph at
  /// (<paramref name="originX"/>, <paramref name="y"/>, <paramref name="originZ"/>). Blank spaces and
  /// unmapped glyphs are skipped, so labels and gaps cost nothing. Leading and trailing blank lines
  /// are trimmed.
  /// </summary>
  public SceneDiagram Layer(
    string ascii,
    int y = 0,
    int originX = 0,
    int originZ = 0
  ) {
    string[] rows = ascii.Replace("\r", "").Trim('\n').Split('\n');
    for (int row = 0; row < rows.Length; row++) {
      string line = rows[row];
      for (int col = 0; col < line.Length; col++)
        if (_legend.TryGetValue(line[col], out var place))
          place(new BlockPos(originX + col, y, originZ + row));
    }
    return this;
  }

  /// <summary>
  /// Stacks several horizontal layers along the Y axis in one call. <paramref name="layers"/>[0] sits at
  /// <paramref name="baseY"/>, [1] at <c>baseY + 1</c>, and so on (bottom to top); each entry is itself a
  /// multi-line X/Z plane in the same format as <see cref="Layer"/>. All layers share the
  /// (<paramref name="originX"/>, <paramref name="originZ"/>) origin so the columns line up vertically.
  /// </summary>
  public SceneDiagram Stack(
    int baseY,
    int originX,
    int originZ,
    params string[] layers
  ) {
    for (int i = 0; i < layers.Length; i++)
      Layer(layers[i], baseY + i, originX, originZ);
    return this;
  }

  /// <summary>Stacks layers from <paramref name="baseY"/> upward at origin (0,0); see the fuller overload.</summary>
  public SceneDiagram Stack(int baseY, params string[] layers) =>
    Stack(baseY, 0, 0, layers);
}
