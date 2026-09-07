using ExpandedLib.Structures;

namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>
/// The cell roles a furnace multiblock layout may mark, read back through
/// <see cref="ExpandedLib.Structures.BlockEntityMultiblockStructure.CellsWithRole"/>. exlib's
/// <see cref="CellRole"/> is an open string key; these are the nine keys iiex (and siex, which shares
/// this class) declares for its furnace family. Key spelling is pinned to what earlier releases already
/// emit into <c>attributes.multiblockRoles</c> - changing a key here changes shipped JSON.
/// </summary>
public static class FurnaceCellRoles {
  /// <summary>
  /// A cell of the burden column, where a charge pile may stand: the shaft furnace's volume.
  /// <c>BlockEntityFurnaceCore.ShaftBox</c> takes the bounds of this role, or of <see cref="Firebox"/> on
  /// a hearth. The box is not the volume - a 3x3x5 layout has a 45-cell box but may mark only 38 cells -
  /// so a consumer wanting cells reads the role. A role the drawing does not mark has no cells and hence
  /// no bounding box, so a bounds consumer must be nullable.
  /// </summary>
  public static readonly CellRole Chargeable = CellRole.Of("Chargeable");

  /// <summary>
  /// A cell of a fuel bed, such as the reverberatory hearth's firebox. Distinct from
  /// <see cref="Chargeable"/>: a shaft's glyph is <c>*:@(air|coalpile|furnace-chargepile)</c>, a
  /// firebox's <c>@(air|coalpile)</c>. A layout carries one or the other, never both - iiex and siex
  /// enforce that themselves; exlib's layout builder no longer knows the two roles are exclusive.
  /// </summary>
  public static readonly CellRole Firebox = CellRole.Of("Firebox");

  /// <summary>A blast intake cell: where a tuyere stands and where air enters the furnace.</summary>
  public static readonly CellRole Tuyere = CellRole.Of("Tuyere");

  /// <summary>A cell where combustion gas leaves the structure into a pipe or a stove.</summary>
  public static readonly CellRole GasOutlet = CellRole.Of("GasOutlet");

  /// <summary>The cell the metal tap occupies, where liquid iron leaves the hearth. One cell.</summary>
  public static readonly CellRole MetalTap = CellRole.Of(
    "MetalTap",
    single: true
  );

  /// <summary>
  /// The cell the slag tap occupies, where slag is skimmed off above the metal tap. One cell.
  /// </summary>
  public static readonly CellRole SlagTap = CellRole.Of(
    "SlagTap",
    single: true
  );

  /// <summary>
  /// A cell holding a liquid pool: the hearth bath, and where metal freezes when it is not tapped. The
  /// course below a shaft furnace's burden, and disjoint from <see cref="Chargeable"/> - a pool cell
  /// stands a live bath, which is not somewhere charge may rest. The one drawing that still marks a cell
  /// both is siex's hot blast furnace, deferred to its remake; see BlockBlastFurnaceCoreHot.
  /// </summary>
  public static readonly CellRole Pool = CellRole.Of("Pool");

  /// <summary>
  /// A cell of the stack column, the draught path out of the structure. Cannot be inferred from the code,
  /// since a flue cell is air like every other empty cell in the drawing. The player-built chimney starts
  /// at the highest flue cell.
  /// </summary>
  public static readonly CellRole Flue = CellRole.Of("Flue");

  /// <summary>
  /// A cell that throttles the flue - the crucible furnace's flue-base bypass, the puddling furnace's
  /// chimney cap. A set rather than a point: one layout may carry a damper at both ends of the stack.
  /// </summary>
  public static readonly CellRole Damper = CellRole.Of("Damper");
}
