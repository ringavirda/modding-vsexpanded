using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces;

/// <summary>
/// Which row of a reverberatory hearth a click lands on, and whether the player can reach it.
/// <para>
/// Both hearths are worked <b>through a door</b> rather than from above, so the three rows across the
/// bed are not equally reachable: you reach over the near rows to load the far one, and a loaded
/// <b>centre</b> row is in the way of both flanks. That is the whole access rule, and it is the reason
/// the layouts put slab shoulders around the door - they open the opening up far enough that all three
/// cells can be reached at all.
/// </para>
/// </summary>
public static class HearthRows
{
  /// <summary>The three rows across a hearth bed, left to right as the player faces the door.</summary>
  public enum Row
  {
    Left = 0,
    Centre = 1,
    Right = 2,
  }

  /// <summary>Every row, in the order a UI or a drop should walk them.</summary>
  public static readonly Row[] All = [Row.Left, Row.Centre, Row.Right];

  /// <summary>
  /// The shape element group that draws <paramref name="row"/>'s contents.
  /// <para>
  /// <b>The names are not in positional order and never were.</b> The three <c>Items{n}</c> groups in
  /// the authored shapes have identical children, so the <em>group pivot</em> decides where each one
  /// draws: <c>Items1</c> sits at x0 (left), <c>Items3</c> at x16 (<b>centre</b>) and <c>Items2</c> at
  /// x32 (right). Reading "Items2 = middle" off the name puts every loaded piece one cell out, silently -
  /// selective-element matching drops an unknown name without an exception, and a wrongly-*known* name
  /// just draws in the wrong place. Pinned by a test against the shipped shape.
  /// </para>
  /// </summary>
  public static string ElementGroup(Row row) =>
    row switch
    {
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
      : local.X switch
      {
        -1 => Row.Left,
        0 => Row.Centre,
        1 => Row.Right,
        _ => null,
      };

  /// <summary>
  /// Whether <paramref name="row"/> can be worked right now. A loaded <b>centre</b> row blocks both
  /// flanks - you cannot reach past a hot charge - so the flanks must be loaded first and drawn last.
  /// The centre itself is always reachable.
  /// <para>
  /// This is a real ordering constraint rather than flavour: it means a full hearth is unloaded
  /// centre-first, which is exactly the order a puddler drew balls, and it makes the middle of the bed
  /// the fast lane for a single piece.
  /// </para>
  /// </summary>
  public static bool CanReach(Row row, bool centreLoaded) =>
    row == Row.Centre || !centreLoaded;

  /// <summary>
  /// The rows that are reachable given the centre's state - the flanks vanish from the list the moment
  /// the centre is loaded.
  /// </summary>
  public static IEnumerable<Row> Reachable(bool centreLoaded)
  {
    foreach (Row row in All)
      if (CanReach(row, centreLoaded))
        yield return row;
  }
}
