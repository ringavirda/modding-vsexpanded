using System;
using System.Collections.Generic;

namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>
/// Which shape elements draw a puddling hearth's contents, and what a row can hold. Pure, and tested
/// against the shipped shape: every name here is a string literal naming a shape element, and
/// selective-element matching drops an unrecognised name without raising anything.
/// </summary>
public static class PuddlingHearthLayout {
  /// <summary>Pigs per hearth row. A pig is triangular in section, so two lie on the bed and the third
  /// nests in the groove between them, which is how the shape stacks them.</summary>
  public const int PigsPerRow = 3;

  /// <summary>The whole charge: three rows of three.</summary>
  public const int PigCapacity = 3 * PigsPerRow;

  // The Fettle cubes are not in positional order: Cube11 is the centre, Cube12 the right, Cube13 the left.
  // Blockbench numbers elements in creation order, and a re-export can renumber them. Mapped from the
  // shipped shape's absolute X: Cube13 spans -16..0, Cube11 0..16, Cube12 16..32.
  private static readonly Dictionary<HearthRows.Row, string> _fettle = new() {
    [HearthRows.Row.Left] = "Fettle/Cube13",
    [HearthRows.Row.Centre] = "Fettle/Cube11",
    [HearthRows.Row.Right] = "Fettle/Cube12",
  };

  /// <summary>The element that draws <paramref name="row"/>'s fettling: the oxide bed rammed over the
  /// bottom plate, which the process consumes as a reagent rather than as a lining.</summary>
  public static string FettleElement(HearthRows.Row row) => _fettle[row];

  /// <summary>
  /// The element drawing the <paramref name="index"/>-th pig (0-based) of <paramref name="row"/>. Pigs are
  /// numbered 1-9 across the bed left to right in threes; within a row the order is bed-left, bed-right,
  /// then the one nested on top, so filling in index order stacks correctly.
  /// </summary>
  /// <exception cref="ArgumentOutOfRangeException">
  /// <paramref name="index"/> is outside 0..<see cref="PigsPerRow"/>-1.
  /// </exception>
  public static string PigElement(HearthRows.Row row, int index) {
    if (index < 0 || index >= PigsPerRow)
      throw new ArgumentOutOfRangeException(nameof(index));
    return "Pigs/Pig" + ((int)row * PigsPerRow + index + 1);
  }

  /// <summary>The group drawing the melted charge: one surface lying across the whole bed, which is what
  /// a puddling furnace's bath is. Drawn in place of the pigs, never beside them.</summary>
  public const string BathElement = "Bath/*";

  /// <summary>
  /// Every element that should be drawn for the given contents, as the tesselator's <c>selectiveElements</c>
  /// paths. The structural groups are always present; only the charge varies.
  /// <para>
  /// Once the charge has melted down the pigs are gone and the bath stands in their place. The fettling
  /// stays drawn under it - it is a reagent the process consumes, not a lining, and it is still there
  /// until the bed is cleaned out.
  /// </para>
  /// </summary>
  public static string[] ElementsFor(
    IReadOnlyList<int> pigsPerRow,
    IReadOnlyList<bool> fettled,
    bool melted = false
  ) {
    var els = new List<string> { "Base/*", "BaseExtension/*", "Bed/*" };
    foreach (HearthRows.Row row in HearthRows.All) {
      if (fettled[(int)row])
        els.Add(FettleElement(row));
      if (melted)
        continue;
      for (int i = 0; i < pigsPerRow[(int)row]; i++)
        els.Add(PigElement(row, i));
    }
    if (melted)
      els.Add(BathElement);
    return [.. els];
  }
}
