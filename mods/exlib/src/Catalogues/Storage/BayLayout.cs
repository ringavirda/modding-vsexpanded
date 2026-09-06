using System.Collections.Generic;

namespace ExpandedLib.Catalogues;

/// <summary>
/// One occupant of a bay row: the cell its run starts at and how many cells it spans. A run is contiguous
/// and never wraps, so a row is described by its runs alone.
/// </summary>
/// <param name="Start">First cell of the run, from 0 at the principal end.</param>
/// <param name="Length">Cells the run spans. At least one.</param>
public readonly record struct BayRun(int Start, int Length) {
  /// <summary>One past the last cell of the run.</summary>
  public int End => Start + Length;

  /// <summary>Whether <paramref name="cell"/> lies in this run.</summary>
  public bool Covers(int cell) => cell >= Start && cell < End;

  /// <summary>Whether this run and <paramref name="other"/> share a cell.</summary>
  public bool Overlaps(BayRun other) => Start < other.End && other.Start < End;
}

/// <summary>
/// A row of storage cells filled by occupants of declared length: three one-cell stacks, a two-cell stack
/// beside a one-cell one, or a single three-cell stack all fill a row of three. Length is the only axis -
/// nothing stacks upward and nothing sits side by side across the row.
/// <para>
/// Pure and world-free, so a rack's capacity is answerable before anything is tesselated. The row's own
/// length is the caller's: a machine reads it off its footprint rather than off a constant here, which is
/// what lets a longer rack be another blocktype instead of a code change.
/// </para>
/// </summary>
public static class BayLayout {
  /// <summary>
  /// Whether a run of <paramref name="length"/> starting at <paramref name="start"/> fits in a row of
  /// <paramref name="cells"/> without touching <paramref name="taken"/>.
  /// </summary>
  public static bool Fits(
    IReadOnlyList<BayRun> taken,
    int cells,
    int start,
    int length
  ) {
    if (length < 1 || start < 0 || start + length > cells)
      return false;

    var run = new BayRun(start, length);
    foreach (BayRun held in taken)
      if (held.Overlaps(run))
        return false;
    return true;
  }

  /// <summary>
  /// The lowest cell a run of <paramref name="length"/> can start at, or null when the row has no gap
  /// wide enough. Lowest-first rather than nearest-to-the-click: a row that packed toward whichever cell
  /// was clicked would leave holes a longer piece could not use.
  /// </summary>
  public static int? Fit(IReadOnlyList<BayRun> taken, int cells, int length) {
    for (int start = 0; start + length <= cells; start++)
      if (Fits(taken, cells, start, length))
        return start;
    return null;
  }

  /// <summary>
  /// Index into <paramref name="taken"/> of the run covering <paramref name="cell"/>, or null when that
  /// cell is empty. This is how a clicked cell selects what to take: any cell of a three-cell stack takes
  /// that stack, which is what makes every cell of the rack work it.
  /// </summary>
  public static int? IndexAt(IReadOnlyList<BayRun> taken, int cell) {
    for (int i = 0; i < taken.Count; i++)
      if (taken[i].Covers(cell))
        return i;
    return null;
  }

  /// <summary>Cells of a <paramref name="cells"/>-long row that no run covers.</summary>
  public static int FreeCells(IReadOnlyList<BayRun> taken, int cells) {
    int used = 0;
    foreach (BayRun run in taken)
      used += run.Length;
    return cells - used;
  }
}
