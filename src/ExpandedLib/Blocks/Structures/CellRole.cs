using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// What a multiblock layout cell is for, as opposed to what block may occupy it: the layout records the
/// code per cell, a role records the purpose, so a machine can ask its own drawing where its tuyeres are.
/// Roles attach to glyphs rather than codes, since one code serves several purposes in one layout
/// (<c>game:air</c> is vent shaft, flue column and tap alcove); a cell holds exactly one glyph, a glyph
/// may carry several roles, and distinct roles accumulate rather than overwrite. A role exists only where
/// code asks the layout which cells are its X cells - a block that finds its own core looks up the other
/// way, through <see cref="BlockEntityMultiblockStructure.FindAnchorOwning{T}"/>. An unmarked role is a
/// set of any size; see <see cref="SingleCellAttribute"/> and docs/design/mechanics/multiblock.md.
/// </summary>
public enum CellRole {
  /// <summary>
  /// A cell of the burden column, where a charge pile may stand: the shaft furnace's volume.
  /// <c>BlockEntityFurnaceCore.ShaftBox</c> takes the bounds of this role, or of <see cref="Firebox"/> on
  /// a hearth. The box is not the volume - a 3x3x5 layout has a 45-cell box but may mark only 38 cells -
  /// so a consumer wanting cells reads the role. A role the drawing does not mark has no cells and hence
  /// no bounding box, so a bounds consumer must be nullable.
  /// </summary>
  Chargeable,

  /// <summary>
  /// A cell of a fuel bed, such as the reverberatory hearth's firebox. Distinct from
  /// <see cref="Chargeable"/>: a shaft's glyph is <c>*:@(air|coalpile|furnace-chargepile)</c>, a
  /// firebox's <c>@(air|coalpile)</c>. A layout carries one or the other, never both.
  /// </summary>
  Firebox,

  /// <summary>A blast intake cell: where a tuyere stands and where air enters the furnace.</summary>
  Tuyere,

  /// <summary>A cell where combustion gas leaves the structure into a pipe or a stove.</summary>
  GasOutlet,

  /// <summary>The cell the metal tap occupies, where liquid iron leaves the hearth. One cell.</summary>
  [SingleCell]
  MetalTap,

  /// <summary>
  /// The cell the slag tap occupies, where slag is skimmed off above the metal tap. One cell.
  /// </summary>
  [SingleCell]
  SlagTap,

  /// <summary>
  /// A cell holding a liquid pool: the hearth bath, and where metal freezes when it is not tapped. The
  /// course below a shaft furnace's burden, and disjoint from <see cref="Chargeable"/> - a pool cell
  /// stands a live bath, which is not somewhere charge may rest. The one drawing that still marks a cell
  /// both is siex's hot blast furnace, deferred to its remake; see BlockBlastFurnaceCoreHot.
  /// </summary>
  Pool,

  /// <summary>
  /// A cell of the stack column, the draught path out of the structure. Cannot be inferred from the code,
  /// since a flue cell is air like every other empty cell in the drawing. The player-built chimney starts
  /// at the highest flue cell.
  /// </summary>
  Flue,

  /// <summary>
  /// A cell that throttles the flue - the crucible furnace's flue-base bypass, the puddling furnace's
  /// chimney cap. A set rather than a point: one layout may carry a damper at both ends of the stack.
  /// </summary>
  Damper,
}

/// <summary>
/// Marks a <see cref="CellRole"/> a layout may give at most one cell, so a consumer may read it as a
/// point. Enforced by <c>MultiblockLayoutBuilder.Build()</c>, which counts the cells actually drawn, so
/// both one glyph drawn twice and two glyphs sharing the role fail. Unmarked means a set of any size.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class SingleCellAttribute : Attribute { }

/// <summary>Facts about <see cref="CellRole"/> that both the layout builder and its consumers read.</summary>
public static class CellRoles {
  // Resolved once at static init rather than per Build(): the enum is closed at compile time, so
  // reflecting per layout would only put an unchanging answer on the load path.
  private static readonly HashSet<CellRole> SingleCellRoles =
  [
    .. Enum.GetValues<CellRole>()
      .Where(r =>
        typeof(CellRole)
          .GetField(r.ToString())
          ?.GetCustomAttribute<SingleCellAttribute>() != null
      ),
  ];

  /// <summary>
  /// Whether <paramref name="role"/> is declared <see cref="SingleCellAttribute">single-cell</see>: a
  /// layout that declares it declares exactly one cell for it, so a consumer may read the answer as a
  /// point. A layout may still declare none, so the consumer handles empty.
  /// </summary>
  public static bool IsSingleCell(CellRole role) =>
    SingleCellRoles.Contains(role);
}
