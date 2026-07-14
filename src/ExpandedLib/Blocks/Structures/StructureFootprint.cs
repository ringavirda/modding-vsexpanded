using System;
using System.Collections.Generic;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// One north-orientation footprint cell for a mega-block, authored in C#: the offset from the principal
/// and whether other blocks may attach to the filler placed there. This is the typed source the code-first
/// <c>fillerOffsets</c> attribute is serialized from (<see cref="ExpandedLib.Definitions.ExBlockDef.FillerOffsets"/>),
/// so a footprint can be <b>computed and validated</b> in C# instead of hand-typing a coordinate array.
/// </summary>
public readonly record struct FillerCellSpec(int X, int Y, int Z, bool AllowAttach = false);

/// <summary>
/// Computes mega-block footprints (the <c>fillerOffsets</c> tables) from a compact description instead of
/// listing every cell by hand. The generated cells are validated (<see cref="Validate"/>) at build time so
/// a duplicate or an origin-overlapping cell fails loudly at load rather than silently clobbering a filler -
/// the same class of mistake the block-number validation guards against, caught earlier.
/// </summary>
public static class StructureFootprint
{
  /// <summary>
  /// A rectangular floor footprint: <paramref name="depth"/> rows along +Z (<c>z = 0 .. depth-1</c>) and
  /// <c>2*halfWidth + 1</c> columns along X, emitted centre-out per row (<c>0, +1, -1, +2, -2, …</c>). The
  /// principal cell (the origin <c>0,0,0</c>) is skipped, and every flanking column (<c>x != 0</c>) opts
  /// into attachment. This reproduces the ore-bunker/ore-mixer style linear footprint.
  /// </summary>
  public static IReadOnlyList<FillerCellSpec> Rectangle(int halfWidth, int depth)
  {
    if (halfWidth < 0)
      throw new ArgumentOutOfRangeException(nameof(halfWidth));
    if (depth < 1)
      throw new ArgumentOutOfRangeException(nameof(depth));

    var cells = new List<FillerCellSpec>();
    for (int z = 0; z < depth; z++)
      foreach (int x in ColumnsCentreOut(halfWidth))
      {
        if (x == 0 && z == 0)
          continue; // the principal sits at the origin
        cells.Add(new FillerCellSpec(x, 0, z, AllowAttach: x != 0));
      }

    Validate(cells);
    return cells;
  }

  /// <summary>Column offsets from the centre out: <c>0, +1, -1, +2, -2, …</c> up to <paramref name="halfWidth"/>.</summary>
  private static IEnumerable<int> ColumnsCentreOut(int halfWidth)
  {
    yield return 0;
    for (int k = 1; k <= halfWidth; k++)
    {
      yield return k;
      yield return -k;
    }
  }

  /// <summary>
  /// Validates a footprint: no cell may sit at the principal origin (<c>0,0,0</c>) and no two cells may
  /// share a position. Throws <see cref="ArgumentException"/> on a violation so an authoring mistake is a
  /// load-time crash, not a silently broken structure.
  /// </summary>
  public static void Validate(IReadOnlyList<FillerCellSpec> cells)
  {
    var seen = new HashSet<(int, int, int)>();
    foreach (FillerCellSpec cell in cells)
    {
      if (cell.X == 0 && cell.Y == 0 && cell.Z == 0)
        throw new ArgumentException(
          "Filler footprint contains the principal origin (0,0,0); the principal already occupies it."
        );
      if (!seen.Add((cell.X, cell.Y, cell.Z)))
        throw new ArgumentException(
          $"Filler footprint has a duplicate cell at ({cell.X},{cell.Y},{cell.Z})."
        );
    }
  }
}
