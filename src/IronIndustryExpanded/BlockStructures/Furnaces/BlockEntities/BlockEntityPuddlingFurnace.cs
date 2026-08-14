using System.Text;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// Reverberatory puddling furnace. Inherits the fire, heat-balance and extinguish path of
/// <see cref="BlockEntityFireboxFurnace"/> unchanged; the charge box the base walks is the firebox cell
/// beside the hearth, so fuel burns clear of the work. Puddled iron leaves as pasty balls through the
/// door, so the furnace has no molten pool and no tap and inherits the defaults of
/// <see cref="BlockEntityFurnaceCore"/>'s molten-product members.
/// <para>
/// Incomplete: the structural shell only. The puddling cycle and the tap-cinder yield on cleaning are not
/// built, and the layout has five filler cells no part produces, so the structure cannot complete.
/// See docs/design/machines/puddling-furnace.md.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityPuddlingFurnace : BlockEntityFireboxFurnace {
  #region Reverberatory geometry

  // No shaft bounds are declared here: the base derives the one-cell firebox from the layout cell marked
  // CellRole.Firebox.

  /// <summary>The hearth's own cell, where the work sits, away from the fire.</summary>
  protected override Vec3i ShaftCentre => new(-2, 0, 0);

  // No tap cells either. The layout marks neither the MetalTap nor the SlagTap role, so MetalTapPos and
  // SlagTapPos are null on this furnace: a reverberatory hearth is raked out, not tapped.

  #endregion

  #region Tunables

  // Placeholder: iron's melting point on a furnace that works pig in the pasty state, well under 1482 C.
  // With no tuyeres the natural-draught ceiling puts T_process near 1392 C, so this threshold is
  // unreachable here. It is revised with the draught model (NaturalDraughtFor(courses, damper)).
  // See docs/design/machines/puddling-furnace.md.
  protected override float MeltingPoint => IiexValues.BfIronMeltingPoint;

  #endregion

  #region Firebox charge

  /// <summary>
  /// Hook for the puddling cycle - melt down, rabble, ball up, draw out - called by the core on the melt
  /// cadence. Not implemented: the furnace does not yet resolve its own hearth and there is no pool for a
  /// cycle to fill. See docs/design/machines/puddling-furnace.md.
  /// </summary>
  protected override void SmeltCycle(object chargeHandle, float dt) { }

  #endregion

  #region HUD

  // One firebox cell means one pile, so there is no partially-lit state to report: the shaft branch's
  // two-branch line needs at least two piles in disagreement.
  protected override void AppendReadyInfo(StringBuilder sb) =>
    sb.AppendLine(Lang.Get(IiexLang.BfInfoReady));

  #endregion
}
