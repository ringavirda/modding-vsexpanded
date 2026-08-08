using System;
using System.Collections.Generic;

namespace IronworkingExpanded.BlockStructures.Furnaces;

/// <summary>
/// Which shape elements draw a puddling hearth's contents, and what a row can hold.
/// <para>
/// Pure, and tested against the shipped shape, because every name here is a <b>string literal aimed at
/// art</b>: selective-element matching drops a name it does not recognise without raising anything, so a
/// typo or a re-export is an invisible hole in the hearth rather than a crash. Worse, a name that exists
/// but belongs to the wrong cell draws the charge one row over and looks like a modelling slip.
/// </para>
/// </summary>
public static class PuddlingHearthLayout
{
  /// <summary>Pigs per hearth row. Three is not arbitrary - a pig is triangular in section, so two lie on
  /// the bed and the third nests in the groove between them, which is how the shape stacks them.</summary>
  public const int PigsPerRow = 3;

  /// <summary>The whole charge: three rows of three. The design's "9 pigs" is this.</summary>
  public const int PigCapacity = 3 * PigsPerRow;

  // Note: the Fettle cubes are not in positional order - Cube11 is the centre, Cube12 the right, Cube13 the
  // left. Blockbench numbers elements in creation order, not layout order, and re-exports re-roll it.
  // Verified against the shipped shape's absolute X: Cube13 spans -16..0, Cube11 0..16, Cube12 16..32.
  private static readonly Dictionary<HearthRows.Row, string> _fettle = new()
  {
    [HearthRows.Row.Left] = "Fettle/Cube13",
    [HearthRows.Row.Centre] = "Fettle/Cube11",
    [HearthRows.Row.Right] = "Fettle/Cube12",
  };

  /// <summary>The element that draws <paramref name="row"/>'s fettling - the oxide bed rammed over the
  /// bottom plate, which is the reagent the whole process runs on, not a lining.</summary>
  public static string FettleElement(HearthRows.Row row) => _fettle[row];

  /// <summary>
  /// The element drawing the <paramref name="index"/>-th pig (0-based) of <paramref name="row"/>. Pigs are
  /// numbered 1-9 across the bed left-to-right in threes, and <b>within</b> a row the order is
  /// bed-left, bed-right, then the one nested on top - so filling in index order stacks correctly.
  /// </summary>
  public static string PigElement(HearthRows.Row row, int index)
  {
    if (index < 0 || index >= PigsPerRow)
      throw new ArgumentOutOfRangeException(nameof(index));
    return "Pigs/Pig" + ((int)row * PigsPerRow + index + 1);
  }

  /// <summary>
  /// Every element that should be drawn for the given contents, as <see cref="Vintagestory.API.Common.Shape.SelectiveElements"/>
  /// paths. The structural groups are always present; only the charge varies.
  /// </summary>
  public static string[] ElementsFor(IReadOnlyList<int> pigsPerRow, IReadOnlyList<bool> fettled)
  {
    var els = new List<string> { "Base/*", "BaseExtension/*", "Bed/*" };
    foreach (HearthRows.Row row in HearthRows.All)
    {
      if (fettled[(int)row])
        els.Add(FettleElement(row));
      for (int i = 0; i < pigsPerRow[(int)row]; i++)
        els.Add(PigElement(row, i));
    }
    return [.. els];
  }
}
