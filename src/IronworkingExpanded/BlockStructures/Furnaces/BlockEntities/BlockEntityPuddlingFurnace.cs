using System.Text;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.Items;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The puddling furnace: a <see cref="BlockEntityFireboxFurnace"/> run as a <b>reverberatory</b> hearth.
/// It inherits the fire / heat-balance / extinguish path unchanged and supplies the facts that make it
/// this hearth rather than the reheat one:
/// <list type="bullet">
/// <item><b>The fuel never touches the work.</b> The "shaft" the base walks for charge is redefined as the
/// firebox off to the side, so the coke burns there and the flame is drawn across the hearth. That single
/// redirection is what turns a shaft furnace into a reverberatory one.</item>
/// <item><b>Nothing is poured.</b> Puddled iron leaves as pasty balls through the door, so this furnace
/// has no molten pool, no metal tap and no product to freeze - it overrides none of the core's
/// molten-product members and inherits their truthful defaults, exactly as the reheat furnace does
/// (see <see cref="BlockEntityFurnaceCore"/>).</item>
/// </list>
/// <para>
/// <b>Incomplete.</b> This is the structural shell: the multiblock stands and holds heat. The puddling
/// <em>cycle</em> - fettle the bed, charge up to nine pigs, melt down, rabble, ball up, draw out - is not
/// built yet, and neither is the tap-cinder yield on cleaning. It is also not reachable: the layout has
/// five filler cells no part produces, so the structure can never complete
/// (<c>docs/design/machines/puddling-furnace.md</c>, Open #1).
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityPuddlingFurnace : BlockEntityFireboxFurnace
{
  #region Reverberatory geometry

  // No `ShaftMin`/`ShaftMax` override here any more, and nothing replaced it. It pointed at two
  // `private static readonly Vec3i` firebox corners, both `(-5, 1, 0)` - the single `c` cell of the
  // drawing, at (col 1, row 1) of layer 1, which after the Origin(-6,-1) shift is exactly that. The
  // drawing marks that cell `CellRole.Firebox`, so the base now derives the same one-cell box from it and
  // the redirection that separates fuel from work costs no C# at all. It was never a shaft: a firebox is
  // one cell of fuel beside the hearth, not a column under the charge.

  /// <summary>The hearth's own cell - where the work sits, deliberately away from the fire.</summary>
  protected override Vec3i ShaftCentre => new(-2, 0, 0);

  // No `SlagTapCell` override here any more, and nothing replaced it. It read `(-3,1,1)`, described
  // itself as "where a slag tap stands at the hearth's low corner (layout `T`)", and the drawing has never
  // had a `T` glyph: that cell is one of the `i` fire-brick slab shoulders round the doorway. The inherited
  // `MetalTapCell = (2,1,0)` was worse - column 8 of an 8-wide grid, off the structure entirely. Both are
  // now the layout's answer, and the layout marks neither role, so both `MetalTapPos` and `SlagTapPos` are
  // null on this furnace. That is the truth: puddled iron leaves as pasty balls through the door and the
  // cinder is raked out, so a reverberatory hearth is cleaned, not tapped. (puddling-furnace.md Open #7.)

  #endregion

  #region Tunables

  // Wrong, and knowingly left: iron's melting point, on a furnace that never melts iron. Puddling
  // works pig in the pasty state, well under 1482 C, and with no tuyeres the natural-draught ceiling
  // puts T_process at ~1392 C - so this furnace could not reach its own melt line even if it lit. This
  // is B8's second cause, and it is entangled with the draught model the damper and doors are meant to
  // feed (NaturalDraughtFor(courses, damper)); it moves with that work, not with the charge fixes.
  // See docs/design/machines/puddling-furnace.md § Gotchas and Open #2.
  protected override float MeltingPoint => IwexValues.BfIronMeltingPoint;

  #endregion

  #region Firebox charge

  /// <summary>
  /// The puddling cycle - melt down, rabble, ball up, draw out - hangs here; the core calls it on the
  /// melt cadence. Nothing yet: the hearth rows exist as blocks but the furnace does not resolve its own
  /// hearth, and there is no pool for a cycle to fill (puddling-furnace.md Open #5, #6).
  /// </summary>
  protected override void SmeltCycle(object chargeHandle, float dt) { }

  #endregion

  #region HUD

  // One firebox cell means one pile, so this hearth has no "partially lit" state to report - the shaft
  // branch's two-branch line needs at least two piles in disagreement before it can say anything else.
  protected override void AppendReadyInfo(StringBuilder sb) =>
    sb.AppendLine(Lang.Get(IwexLang.BfInfoReady));

  #endregion
}
