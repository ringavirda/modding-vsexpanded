using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Definitions;

/// <summary>
/// Typed builder for a block's <c>multiblockStructure</c> attribute - the <c>blockNumbers</c> map plus the
/// <c>offsets</c> table (the largest hand-typed coordinate array in the mega-blocks, up to ~90 cells). It
/// turns that table into named block numbers, a <see cref="Fill"/> helper for regular sub-volumes, and
/// <b>build-time validation</b> that every offset references a declared block number and no cell position is
/// duplicated - directly retiring the block-number class of bugs (offsets that point at a <c>w</c> with no
/// <c>blockNumbers</c> entry, or two offsets on the same cell). The emitted JSON is byte-identical to the
/// hand-written form so the vanilla <c>MultiblockStructure</c> deserializer reads it unchanged.
/// </summary>
public sealed class MultiblockBuilder
{
  private readonly JObject _blockNumbers = new();
  private readonly HashSet<int> _numbers = new();
  private readonly JArray _offsets = new();
  private readonly HashSet<(int, int, int)> _positions = new();

  /// <summary>Declares a <c>blockNumbers</c> entry: the block <paramref name="code"/> (a wildcard/selector,
  /// e.g. <c>"smex:converterbessemer*"</c>) mapped to the number <paramref name="w"/> that offsets
  /// reference. Numbers should be unique per structure.</summary>
  public MultiblockBuilder Number(string code, int w)
  {
    _blockNumbers[code] = w;
    _numbers.Add(w);
    return this;
  }

  /// <summary>Adds one offset cell at <paramref name="x"/>,<paramref name="y"/>,<paramref name="z"/>
  /// requiring block number <paramref name="w"/>.</summary>
  public MultiblockBuilder At(int x, int y, int z, int w)
  {
    if (!_positions.Add((x, y, z)))
      throw new ArgumentException(
        $"Multiblock structure has a duplicate offset at ({x},{y},{z})."
      );
    _offsets.Add(
      new JObject
      {
        ["x"] = x,
        ["y"] = y,
        ["z"] = z,
        ["w"] = w,
      }
    );
    return this;
  }

  /// <summary>
  /// Fills a cuboid volume with offsets all requiring block number <paramref name="w"/>, emitted in the
  /// canonical z-outer, y-mid, x-inner order (matching the hand-written tables). Ranges are inclusive. This
  /// is the "computed" half of the data-table win for the regular structure body; use <see cref="At"/> for
  /// the bespoke cells around it.
  /// </summary>
  public MultiblockBuilder Fill(
    int x1,
    int y1,
    int z1,
    int x2,
    int y2,
    int z2,
    int w
  )
  {
    // An inverted range would iterate zero times and silently omit the whole sub-volume - fail loudly
    // instead, consistent with the builder's other build-time guards.
    if (x2 < x1 || y2 < y1 || z2 < z1)
      throw new ArgumentException(
        $"Multiblock Fill has an inverted range: ({x1},{y1},{z1})..({x2},{y2},{z2})."
      );

    for (int z = z1; z <= z2; z++)
      for (int y = y1; y <= y2; y++)
        for (int x = x1; x <= x2; x++)
          At(x, y, z, w);
    return this;
  }

  /// <summary>
  /// Builds the <c>{ blockNumbers, offsets }</c> object, validating that every offset's <c>w</c> resolves to
  /// a declared block number. Throws <see cref="InvalidOperationException"/> naming the first offset that
  /// references an undeclared number - a load-time failure instead of a structure that never completes.
  /// </summary>
  internal JObject Build()
  {
    foreach (JToken offset in _offsets)
    {
      int w = (int)offset["w"]!;
      if (!_numbers.Contains(w))
        throw new InvalidOperationException(
          $"Multiblock offset at ({offset["x"]},{offset["y"]},{offset["z"]}) requires block number {w}, "
            + "which has no blockNumbers entry."
        );
    }
    return new JObject { ["blockNumbers"] = _blockNumbers, ["offsets"] = _offsets };
  }
}
