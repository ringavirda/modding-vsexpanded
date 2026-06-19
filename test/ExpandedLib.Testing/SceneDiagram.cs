using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Testing;

/// <summary>
/// Turns an ASCII layout into block placements, so a multi-network integration setup reads like the
/// thing it models instead of a wall of <c>Place(new BlockPos(...))</c> calls. Each non-blank glyph is
/// mapped by a <em>legend</em> to a placement action; a layer is one horizontal (X/Z) plane at a given
/// height, and several layers stack a 3D setup.
/// <para>
/// Coordinate convention: a row's characters advance along +X (columns), successive rows advance along
/// +Z, and each <see cref="Layer"/> sits at its own Y. So
/// <code>
///   diagram.On('=', p =&gt; scene.Node(p, WePipe(), new BlockEntityPipe(), "pipe"))
///          .On('#', p =&gt; scene.Block(p, Rock))
///          .Layer("#====#");
/// </code>
/// lays a five-cell west-east pipe run between two rock caps along the X axis.
/// </para>
/// The legend is supplied by the test (it is mod-specific - which glyph means which oriented pipe,
/// canal, machine, or cap), keeping this parser itself mod-agnostic.
/// </summary>
public sealed class SceneDiagram
{
  private readonly Dictionary<char, System.Action<BlockPos>> _legend = new();

  /// <summary>Maps a glyph to the action that places it at a resolved world position.</summary>
  public SceneDiagram On(char glyph, System.Action<BlockPos> place)
  {
    _legend[glyph] = place;
    return this;
  }

  /// <summary>
  /// Applies one horizontal layer of the diagram at height <paramref name="y"/>, with the top-left
  /// glyph at (<paramref name="originX"/>, <paramref name="y"/>, <paramref name="originZ"/>). Blank
  /// spaces and unmapped glyphs are skipped, so labels/gaps are free. Leading/trailing blank lines
  /// (e.g. from a verbatim string) are trimmed.
  /// </summary>
  public SceneDiagram Layer(
    string ascii,
    int y = 0,
    int originX = 0,
    int originZ = 0
  )
  {
    string[] rows = ascii.Replace("\r", "").Trim('\n').Split('\n');
    for (int row = 0; row < rows.Length; row++)
    {
      string line = rows[row];
      for (int col = 0; col < line.Length; col++)
        if (_legend.TryGetValue(line[col], out var place))
          place(new BlockPos(originX + col, y, originZ + row));
    }
    return this;
  }
}
