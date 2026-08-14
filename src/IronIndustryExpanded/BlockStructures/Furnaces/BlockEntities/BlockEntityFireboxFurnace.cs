using System;
using System.Collections.Generic;
using IronIndustryExpanded.Items;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// A furnace whose fire is a firebox beside the work rather than a column of burden over it - a
/// reverberatory hearth, where the chimney draught draws the flame across the work. It burns plain fuel
/// rather than a burden, and runs on natural draught, so <see cref="RequiresBlast"/> is false and no air
/// supply can starve it.
/// </summary>
/// <remarks>
/// <see cref="ShaftHoldsLayeredCharge"/>, <see cref="ChargeCapacityUnits"/> and the charge walk are
/// sealed here, so a hearth cannot inherit a shaft's charge column over its firebox. See
/// <c>docs/design/conventions.md</c> § "the furnace axes become a class tree".
/// </remarks>
public abstract class BlockEntityFireboxFurnace : BlockEntityFurnaceCore {
  #region Draught

  // Natural draught: nothing to blow, so nothing can starve it.
  protected override bool RequiresBlast => false;

  // A hearth's drawing marks no tuyere and no gas outlet, so CellRole.Tuyere and CellRole.GasOutlet
  // answer empty on their own.

  // Neither value is ever read, because the tick only applies them inside the (empty) tuyere loop. 0f
  // rather than the shaft's numbers, which through a public API would report a draw and a pressure demand
  // this furnace does not have.
  protected override float TuyereIntakeVolume => 0f;
  protected override float BlastPressureThreshold => 0f;

  #endregion

  #region Firebox charge

  /// <summary>
  /// A firebox is never a charge column, so it never holds layered charge. Sealed so a leaf cannot opt a
  /// hearth into the shaft's charge model.
  /// </summary>
  protected sealed override bool ShaftHoldsLayeredCharge => false;

  // A firebox refuses nothing: a fuel bed only makes flame and has no burden to recognise.

  /// <summary>
  /// The branch's charge walk: every <see cref="BEBehaviorFirebox"/> bed in the cells this hearth's own
  /// drawing marks <see cref="ExpandedLib.Blocks.Structures.CellRole.Firebox"/>.
  /// </summary>
  /// <remarks>
  /// Walks the role's cells, not the bounding box: <see cref="BlockEntityFurnaceCore.ShaftBox"/> is a box
  /// round them and would sweep the brick between two beds. The box is used only by
  /// <see cref="FireboxCellCount"/>, which wants a count rather than the cells.
  /// </remarks>
  protected override object CollectCharge() {
    var beds = new List<(BlockPos pos, BEBehaviorFirebox bed)>();
    foreach (BlockPos cell in FireboxCells)
      if (
        Api
          .World.BlockAccessor.GetBlockEntity(cell)
          ?.GetBehavior<BEBehaviorFirebox>() is { } bed
      )
        beds.Add((cell, bed));
    return beds;
  }

  /// <summary>The handle type <see cref="CollectCharge"/> hands out, named once so its consumers cast to
  /// the same thing.</summary>
  private static List<(BlockPos pos, BEBehaviorFirebox bed)> BedsOf(
    object chargeHandle
  ) => (List<(BlockPos pos, BEBehaviorFirebox bed)>)chargeHandle;

  /// <summary>
  /// Reads the firebox. A reverberatory firebox burns plain fuel rather than a burden, so the mix reports
  /// as pure fuel - which is also what the heat balance wants. The count needs no filtering: a bed only
  /// holds fuel it already accepted.
  /// </summary>
  protected override int ReadChargeMix(
    object chargeHandle,
    out bool isFull,
    out BurdenMix mix,
    out int rejectedCount
  ) {
    int count = 0;
    foreach (var (_, bed) in BedsOf(chargeHandle))
      count += bed.Units;

    isFull = count >= ChargeCapacityUnits;
    mix = new BurdenMix(0f, 0f, count);
    // Nothing is refused: a firebox takes any fuel. A rejection reported here would block the furnace's
    // own conversion.
    rejectedCount = 0;
    return count;
  }

  /// <summary>
  /// A firebox is lit when every bed of it is full: <see cref="ChargeCapacityUnits"/> is one cell's six
  /// courses times the cell count, so a bed at capacity fires a hearth of any size. An empty bed list
  /// refuses, which keeps a drawing that marks no firebox inert - its capacity is 0 and every count would
  /// otherwise pass.
  /// </summary>
  protected override bool TryIgniteCharge(object chargeHandle) {
    var beds = BedsOf(chargeHandle);
    if (beds.Count == 0)
      return false;
    foreach (var (_, bed) in beds)
      if (!bed.IsFull)
        return false;
    return true;
  }

  /// <summary>
  /// Puts the bed out by burning most of it off, keeping <c>BfBurnoutFuelRetainedBottom</c> of what was in
  /// it as salvage. No height interpolation, unlike the shaft's burn-out: a firebox is one course of cells
  /// all at the same level and equally in the fire, so only the bottom fraction applies.
  /// </summary>
  protected override void BurnOutCharge() {
    foreach (BlockPos cell in FireboxCells) {
      if (
        Api
          .World.BlockAccessor.GetBlockEntity(cell)
          ?.GetBehavior<BEBehaviorFirebox>()
        is not { Units: > 0 } bed
      )
        continue;
      int keep = (int)(bed.Units * IiexValues.BfBurnoutFuelRetainedBottom);
      bed.Consume(bed.Units - keep);
    }
  }

  #endregion

  #region Tunables

  // Fire cadence is shared across the furnace machine rather than set per hearth; only the temperatures
  // differ.
  protected override int MaxFuelBurnTime => IiexValues.FireboxMaxFuelBurnTime;
  protected override float MeltStartDelay => IiexValues.FireboxMeltStartDelay;
  protected override float MeltIntervalSec => IiexValues.FireboxMeltIntervalSec;

  /// <summary>
  /// Cells in the firebox this machine declares - the ceiling on what it can hold, at
  /// <see cref="BEBehaviorFirebox.CellCapacity"/> units each (6 courses x 2 units, this mod's own figure
  /// rather than vanilla's pile stack size). Derived from the drawing's
  /// <see cref="ExpandedLib.Blocks.Structures.CellRole.Firebox"/> marks
  /// (<see cref="BlockEntityFurnaceCore.ShaftBox"/>) so it cannot drift from the cells a player can load.
  /// </summary>
  /// <remarks>
  /// A hearth whose drawing marks no firebox counts 0 cells, giving a <see cref="ChargeCapacityUnits"/> of
  /// 0 that is never reached: <see cref="TryIgniteCharge"/> refuses an empty bed list.
  /// </remarks>
  protected int FireboxCellCount =>
    ShaftBox is not { } box
      ? 0
      : (Math.Abs(box.max.X - box.min.X) + 1)
        * (Math.Abs(box.max.Y - box.min.Y) + 1)
        * (Math.Abs(box.max.Z - box.min.Z) + 1);

  /// <summary>
  /// A firebox fires when its own cells are loaded, so capacity is geometry rather than a constant.
  /// Sealed, because a hand-picked total sized for one machine can be inherited by a much smaller one and
  /// put the threshold out of physical reach.
  /// </summary>
  protected sealed override int ChargeCapacityUnits =>
    FireboxCellCount * IiexValues.FireboxMixPerCell;

  #endregion
}
