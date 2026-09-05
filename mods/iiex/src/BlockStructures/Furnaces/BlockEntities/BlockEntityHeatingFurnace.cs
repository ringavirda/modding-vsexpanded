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
/// The multiblock stands, lights, burns its firebox, holds heat and soaks the bed. Not built: the roasting
/// mode, and the crosswise seating that would let a long piece lie across the rows. See
/// <c>docs/design/machines/reheat-furnace.md</c>.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityHeatingFurnace : BlockEntityFireboxFurnace {
  #region Reverberatory geometry

  // No `ShaftMin`/`ShaftMax` override: the layout marks both `F` cells (col 1, rows 1-2 of layer 1, i.e.
  // `(-5, 1, -1)` and `(-5, 1, 0)` after the Origin(-6,-2) shift) as `CellRole.Firebox`, and the base
  // derives the same two-cell box from it. Two fuel cells along one side, not a column under the work.

  /// <summary>
  /// The hearth cell, where the work sits, clear of the fire: layer 0's `H`, at column 4 of row 2 against
  /// this drawing's `Origin(-6, -2)`. The same cell the puddling furnace's bed stands in - the two are the
  /// same chassis, and only the depth of the bed behind the anchor differs - but the origins differ by a
  /// row, so the offset must be read off each drawing rather than copied between them.
  /// </summary>
  protected override Vec3i ShaftCentre => new(-2, 0, 0);

  #endregion

  #region Parts

  /// <summary>
  /// The bed the work lies on. Resolved by the branch on the same schedule as the taps - at completion, on
  /// load and on both lit-tick recovery paths - so a bed broken and replaced mid-heat is picked up again
  /// without a reload.
  /// </summary>
  public BlockEntityHeatingHearth? Hearth =>
    HearthPart as BlockEntityHeatingHearth;

  #endregion

  #region Firebox charge

  /// <summary>
  /// The melt cycle of a furnace that melts nothing: it soaks the bed instead. The core already calls this
  /// only once the chamber is at or above <see cref="MeltingPoint"/>, which on this furnace is the rolling
  /// heat itself - so "hot enough to melt" and "hot enough to reheat" are the same test and the reheat
  /// needs no gate of its own.
  /// </summary>
  /// <remarks>
  /// Hosted here rather than on a listener of the hearth's own for the away-catch-up: <paramref name="dt"/>
  /// is the interval this call stands for, so the soak integrates the same whether the cycle ran on the
  /// clock or is being caught up after a reload.
  /// </remarks>
  protected override void SmeltCycle(object chargeHandle, float dt) {
    Hearth?.SoakTick(InternalTemperature, dt);
  }

  #endregion

  #region Tunables

  // A reheat temperature, not a melting point: it brings wrought stock back above the rolling floor, well
  // under the 1482 C where the metal would start to melt.
  protected override float MeltingPoint => IiexValues.RollingTempC;

  #endregion

  #region HUD

  protected override void AppendReadyInfo(StringBuilder sb) =>
    sb.AppendLine(Lang.Get(IiexLang.HeatingfurnaceReady));

  /// <summary>
  /// What the bed is carrying, which the hearth's own block info cannot say: it does not know whether the
  /// furnace is lit or how near rolling heat the pieces on it are.
  /// </summary>
  protected override void AppendHeatExtras(StringBuilder sb) {
    base.AppendHeatExtras(sb);
    if (Hearth is not { } bed)
      sb.AppendLine(Lang.Get(IiexLang.HeatingNohearth));
    else if (bed.LoadedRows > 0)
      sb.AppendLine(
        Lang.Get(IiexLang.HeatingSoaking, bed.LoadedRows, bed.RowsAtRollingHeat)
      );
  }

  #endregion
}
