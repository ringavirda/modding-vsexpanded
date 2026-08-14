using System.Text;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The heating (reheat) furnace: a <see cref="BlockEntityFireboxFurnace"/> that melts nothing. Stock is
/// laid on the hearth, the firebox flame is drawn over it, and it comes back out hot enough to roll. It has
/// no molten pool, no tap and no product, and overrides none of the molten-product members, inheriting the
/// defaults of <see cref="BlockEntityFurnaceCore"/>. Natural draught, no tuyeres and plain fuel come from
/// the branch; this type sets where its firebox and hearth sit and how hot it runs.
/// <para>
/// Structural shell only: the multiblock stands, lights, burns its firebox and holds heat. The hearth rows
/// of typed stock, the row access rule, the heat into the pieces and the roasting mode are not built. See
/// <c>docs/design/iiex.md</c> and <c>conventions.md</c> § metal recovery.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityHeatingFurnace : BlockEntityFireboxFurnace {
  #region Reverberatory geometry

  // No `ShaftMin`/`ShaftMax` override: the layout marks both `c` cells (col 1, rows 1-2 of layer 1, i.e.
  // `(-5, 1, 0)` and `(-5, 1, 1)` after the Origin(-6,-1) shift) as `CellRole.Firebox`, and the base
  // derives the same two-cell box from it. Two fuel cells along one side, not a column under the work.

  /// <summary>The hearth cell, where the work sits, clear of the fire.</summary>
  protected override Vec3i ShaftCentre => new(-2, 0, 1);

  #endregion

  #region Firebox charge

  /// <summary>
  /// The melt cycle for a furnace that melts nothing, so it does nothing. Soaking heat into the stock on
  /// the hearth belongs here once the hearth rows exist; until then a lit heating furnace holds its
  /// temperature. Reading and igniting the firebox is <see cref="BlockEntityFireboxFurnace"/>'s.
  /// </summary>
  protected override void SmeltCycle(object chargeHandle, float dt) { }

  #endregion

  #region Tunables

  // A reheat temperature, not a melting point: it brings wrought stock back above the rolling floor, well
  // under the 1482 C where the metal would start to melt.
  protected override float MeltingPoint => IiexValues.RollingTempC;

  #endregion

  #region HUD

  protected override void AppendReadyInfo(StringBuilder sb) =>
    sb.AppendLine(Lang.Get("iiex:heatingfurnace-ready"));

  #endregion
}
