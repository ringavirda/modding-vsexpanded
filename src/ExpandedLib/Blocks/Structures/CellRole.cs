using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// What a multiblock layout cell is <b>for</b>, as opposed to what block may occupy it. A layout already
/// records the code per cell; a role records the purpose, so a machine can ask its own drawing "where are my
/// tuyeres?" instead of carrying a second, hand-written offset list beside the drawing it was copied from.
/// <para>
/// Roles attach to <b>glyphs</b>, not to codes, and the author uses a distinct glyph per role. That is
/// forced: a single code already serves several purposes in one layout - <c>game:air</c> is the vent shaft,
/// the flue column and the tap alcove - so "the air cells are the flue" is not a statement the DSL can make.
/// It is also the DSL's own existing idiom, which already writes <c>T</c> and <c>Y</c> for north- and
/// south-facing tuyeres rather than one glyph. Several glyphs may share one role (the two tuyere glyphs), and
/// several glyphs may share one code with different roles.
/// </para>
/// <para>
/// <b>And a glyph may carry several roles</b>, because a cell can genuinely be two things at once - the
/// shaft furnaces' crucible floor is burden column and metal pool together (<see cref="Chargeable"/> +
/// <see cref="Pool"/>). Nothing else can express that: a cell holds exactly one glyph, so two overlapping
/// roles cannot be split across two of them. Distinct roles accumulate rather than overwrite, so there is no
/// last-writer-wins for the drift to hide in.
/// </para>
/// <para>
/// The governing rule for what earns a role: <b>a role exists only where code asks the layout "where are my X
/// cells?"</b>. A block that finds its own core - a charge door, a hopper, a hearth, a filler, the core
/// itself - needs no role, because that lookup runs the other way (the part scans up to its anchor through
/// <see cref="BlockEntityMultiblockStructure.FindAnchorOwning{T}"/>). Deliberately absent for that reason:
/// <c>Core</c> (always <c>(0,0,0)</c> by the origin rule), <c>Filler</c> (a mechanism, not a semantic),
/// <c>ChargeDoor</c> / <c>Hopper</c> / <c>Hearth</c>, and <c>ShaftCentre</c> - a single geometric point
/// rather than a cell set, with no layout meaning at all.
/// </para>
/// <para>
/// <b>Arity is part of a role's meaning.</b> Most roles are cell <em>sets</em> - a shaft has a column of
/// chargeable cells, a furnace has two tuyeres - but a tap is one cell, and the hand-declared lists it
/// replaced (<c>MetalTapCell</c>, <c>SlagTapCell</c>) were a single <c>Vec3i</c> rather than an array. A
/// consumer of such a role reads a <em>point</em>, and reading a point out of a cell set with no build-time
/// promise behind it is an exception waiting for the first layout that draws the glyph twice.
/// <see cref="SingleCellAttribute"/> marks the roles that carry that promise;
/// <c>MultiblockLayoutBuilder</c> refuses a layout that breaks it. An unmarked role is a set of any size,
/// including one - the default is deliberately the loose end, because tightening a role later is a build
/// error the author sees, while loosening one silently invalidates a point-read already written against it.
/// </para>
/// <para>
/// <b><c>[SingleCell]</c> promises "at most one", never "exactly one" - so the consumer must be
/// nullable, not <c>.Single()</c>.</b> The guard runs over the cells a layout <em>draws</em>; a layout that
/// declines to declare the role at all draws none, which is legal and is frequently the truth. Both
/// reverberatory hearths mark neither tap, because a puddling furnace is cleaned rather than tapped - so
/// <c>BlockEntityFurnaceCore.MetalTapPos</c> is a <c>BlockPos?</c>. Writing <c>.Single()</c> there would have
/// turned a silent no-op into an exception thrown from <c>GetBlockInfo</c>, i.e. once per frame the player
/// looks at the block.
/// </para>
/// </summary>
public enum CellRole
{
  /// <summary>
  /// A cell of the burden column: where a charge pile may stand. This is the shaft furnace's volume, and
  /// its bounding box <b>is</b> the shaft box: <c>BlockEntityFurnaceCore.ShaftBox</c> takes the bounds of
  /// this role (or of <see cref="Firebox"/> on a hearth), which is what retired the last two hand-declared
  /// corners, <c>ShaftMin</c>/<c>ShaftMax</c>, in 2.6.5.
  /// <para>
  /// <b>The box is not the volume.</b> The cold blast furnace's box is 3x3x5 = 45 cells and its layout
  /// marks 38 of them, because the lowest level is mostly tuyere and brick. A consumer that wants the cells
  /// must read the role; only a consumer that genuinely wants a box (a <c>WalkBlocks</c> range, one column
  /// per <c>(x, z)</c>) may take the bounds.
  /// </para>
  /// <para>
  /// A role a drawing declines to mark has no cells and therefore <b>no bounding box</b>, and two corners
  /// cannot express that - so a bounds consumer is nullable for the same reason a <c>[SingleCell]</c> one is.
  /// </para>
  /// </summary>
  Chargeable,

  /// <summary>
  /// A cell of a fuel bed - the reverberatory hearth's firebox. Deliberately <b>not</b> the same as
  /// <see cref="Chargeable"/>: a shaft's glyph is <c>*:@(air|coalpile|furnace-chargepile)</c> and a firebox's is
  /// <c>@(air|coalpile)</c>, which is exactly the distinction the shaft/firebox furnace split is built on.
  /// A layout is one or the other, never both - see <c>MultiblockLayoutBuilder.Role</c>.
  /// </summary>
  Firebox,

  /// <summary>A blast intake cell: where a tuyere stands and where air enters the furnace.</summary>
  Tuyere,

  /// <summary>A cell where combustion gas leaves the structure into a pipe or a stove.</summary>
  GasOutlet,

  /// <summary>
  /// The cell the metal tap occupies - where liquid iron leaves the hearth. <b>One cell:</b> a hearth is
  /// drained at a single point, and the <c>MetalTapCell</c> this replaces is a single <c>Vec3i</c>.
  /// </summary>
  [SingleCell]
  MetalTap,

  /// <summary>
  /// The cell the slag tap occupies - where slag is skimmed off, above the metal tap. <b>One cell</b>, for
  /// the same reason as <see cref="MetalTap"/>.
  /// </summary>
  [SingleCell]
  SlagTap,

  /// <summary>
  /// A cell holding a liquid pool: the hearth bath, and where metal freezes when it is not tapped. Replaced
  /// the hand-declared <c>SolidifyCells</c>.
  /// <para>
  /// Today a crucible cell is <b>both</b> pool and burden - the shaft furnaces' lowest chargeable level is
  /// exactly their pool floor - so those cells carry this role <em>and</em> <see cref="Chargeable"/>. That
  /// overlap is the reason a glyph may carry several roles at all; see the remark on
  /// <see cref="CellRole"/>. The layered-charge layout change makes the lowest level pool-only, at which
  /// point the two sets become disjoint and the glyph drops <see cref="Chargeable"/> - a one-line edit to
  /// the drawing rather than to any consumer, which is the point of declaring both today.
  /// </para>
  /// </summary>
  Pool,

  /// <summary>
  /// A cell of the stack column - the draught path out of the structure. Cannot be inferred from the code:
  /// a flue cell is air, and so is every other empty cell in the drawing. The player-built chimney starts at
  /// the highest flue cell, so that needs no role of its own.
  /// </summary>
  Flue,

  /// <summary>
  /// A cell that throttles the flue. One mechanic at both ends of the stack: the crucible furnace's
  /// flue-base bypass and the puddling furnace's chimney cap. A <b>set</b>, deliberately, despite reading
  /// like a point: "both ends of the stack" is one layout with two dampers, and this role has no consumer
  /// yet to tell us otherwise.
  /// </summary>
  Damper,
}

/// <summary>
/// Marks a <see cref="CellRole"/> that a layout may give <b>exactly one</b> cell, so a consumer reading it
/// as a point rather than a set has a build-time promise behind that reading rather than a hope. Enforced by
/// <c>MultiblockLayoutBuilder.Build()</c>, which counts the cells actually drawn - so both "one glyph drawn
/// twice" and "two glyphs sharing the role" fail, which a check on the role table alone would miss.
/// <para>
/// Unmarked is the default and means a set of any size. See the arity remark on <see cref="CellRole"/> for
/// why the loose end is the default.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class SingleCellAttribute : Attribute { }

/// <summary>Facts about <see cref="CellRole"/> that both the layout builder and its consumers read.</summary>
public static class CellRoles
{
  // Resolved once at static init rather than per Build(): the enum is closed at compile time, so the answer
  // cannot change while the process runs, and reflecting per layout would put it on the load path.
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
  /// Whether <paramref name="role"/> is declared <see cref="SingleCellAttribute">single-cell</see>, in which
  /// case a layout that declares it at all declares exactly one cell for it and a consumer may
  /// <c>Single()</c> the answer.
  /// </summary>
  public static bool IsSingleCell(CellRole role) =>
    SingleCellRoles.Contains(role);
}
