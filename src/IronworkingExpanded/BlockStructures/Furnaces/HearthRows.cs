using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces;

/// <summary>
/// Which row of a reverberatory hearth a click lands on, and whether that row can be reached. Both
/// hearths are worked through a door rather than from above, so the three rows across the bed are not
/// equally reachable: a loaded centre row is in the way of both flanks. The layouts put slab shoulders
/// around the door to widen the mouth far enough that all three cells are reachable at all.
/// </summary>
public static class HearthRows {
  /// <summary>The three rows across a hearth bed, left to right as the player faces the door.</summary>
  public enum Row {
    Left = 0,
    Centre = 1,
    Right = 2,
  }

  /// <summary>Every row, in the order a UI or a drop should walk them.</summary>
  public static readonly Row[] All = [Row.Left, Row.Centre, Row.Right];

  /// <summary>
  /// The shape element group that draws <paramref name="row"/>'s contents. The names are not in
  /// positional order: the three <c>Items{n}</c> groups have identical children and the group pivot
  /// decides where each one draws, so <c>Items1</c> sits at x0 (left), <c>Items3</c> at x16 (centre) and
  /// <c>Items2</c> at x32 (right). A wrong mapping draws every loaded piece one cell out, with no
  /// exception raised.
  /// </summary>
  public static string ElementGroup(Row row) =>
    row switch {
      Row.Left => "Items1",
      Row.Centre => "Items3",
      Row.Right => "Items2",
      _ => throw new ArgumentOutOfRangeException(nameof(row)),
    };

  /// <summary>
  /// The row a hearth cell is: the bed runs along the structure's local X, so the offset from the
  /// principal picks the row directly. Null for a cell that is not part of the bed.
  /// </summary>
  public static Row? FromLocalOffset(Vec3i local) =>
    local.Y != 0
      ? null
      : local.X switch {
        -1 => Row.Left,
        0 => Row.Centre,
        1 => Row.Right,
        _ => null,
      };

  /// <summary>
  /// Whether <paramref name="row"/> can be worked right now. A loaded centre row blocks both flanks, so
  /// the flanks must be loaded first and drawn last, and a full hearth is unloaded centre-first. The
  /// centre itself is always reachable.
  /// </summary>
  public static bool CanReach(Row row, bool centreLoaded) =>
    row == Row.Centre || !centreLoaded;

  /// <summary>
  /// The rows reachable given the centre's state: the flanks drop out of the list once the centre is
  /// loaded.
  /// </summary>
  public static IEnumerable<Row> Reachable(bool centreLoaded) {
    foreach (Row row in All)
      if (CanReach(row, centreLoaded))
        yield return row;
  }
}
