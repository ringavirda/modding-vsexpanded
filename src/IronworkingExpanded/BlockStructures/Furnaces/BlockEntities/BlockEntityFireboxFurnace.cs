using System;
using System.Collections.Generic;
using IronworkingExpanded.Items;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The <b>firebox furnace</b>: a <see cref="BlockEntityFurnaceCore"/> whose fire is a firebox <i>beside</i>
/// the work rather than a column of burden over it. A reverberatory hearth, in other words - the flame is
/// drawn across the work by the chimney's own draught, so the fuel never touches what it is heating.
/// <para>
/// The two facts that follow from that are what this class carries. <b>Plain fuel, not burden</b>: nothing
/// in a firebox is being reduced, so the ore/flux proportions a shaft furnace lives on have no meaning
/// here. <b>Natural draught</b>: there are no tuyeres and no blower, so <see cref="RequiresBlast"/> is
/// false and no air supply can starve the fire.
/// </para>
/// <para>
/// <b>The name is the invariant.</b> Being on this branch rather than
/// <see cref="BlockEntityShaftFurnace"/> is what keeps <see cref="ShaftHoldsLayeredCharge"/> off, so a
/// hearth can never inherit a shaft's charge column over its firebox - the type carries the invariant
/// rather than a warning comment. See <c>docs/design/conventions.md</c> § "the furnace axes
/// become a class tree".
/// </para>
/// <para>
/// The three members that carry that invariant - <see cref="ShaftHoldsLayeredCharge"/>,
/// <see cref="ChargeCapacityUnits"/> and the charge walk - are <b>sealed</b>. Each one of
/// them was, at some point, a shaft's answer sitting on a hearth; sealing is what turns "a leaf undid it"
/// from a test failure in one assembly into a compile error in every mod.
/// </para>
/// </summary>
public abstract class BlockEntityFireboxFurnace : BlockEntityFurnaceCore
{
  #region Draught

  // Natural draft: nothing to blow, so nothing can starve it.
  protected override bool RequiresBlast => false;

  // A hearth's drawing carries no tuyere glyph and no pipe-outlet glyph, so CellRole.Tuyere and
  // CellRole.GasOutlet answer empty of their own accord - the same way the branch's ChargeableCells
  // does. An absence stated twice is an absence that can disagree with itself.

  // No tuyeres, so neither of these is ever read: TuyereDrawFor scales a per-tuyere rate the tick only
  // ever applies inside the (empty) tuyere loop, and RequiredBlastPressureFor gates a draw that never
  // happens. 0f rather than the shaft's numbers because a hearth with no tuyere does not draw 14 L/s and
  // does not demand 2 atm - the shaft's values here would be a lie that reads true through a public API.
  protected override float TuyereIntakeVolume => 0f;
  protected override float BlastPressureThreshold => 0f;

  #endregion

  #region Firebox charge

  /// <summary>
  /// A firebox is never a charge column, so it never holds layered charge. <b>Sealed</b>: a leaf writing
  /// <c>=> true</c> here is exactly how a hearth comes to inherit a shaft's charge model, and this branch
  /// exists to make that a compile error rather than a test failure in one assembly.
  /// </summary>
  protected sealed override bool ShaftHoldsLayeredCharge => false;

  // A firebox refuses nothing, and `ReadChargeMix` below states it: a fuel bed is only ever making
  // flame, and has no answer to "which burden is this reducing".

  /// <summary>
  /// The branch's charge walk: every <see cref="BEBehaviorFirebox"/> bed in the cells this hearth's own
  /// drawing marks <see cref="ExpandedLib.Blocks.Structures.CellRole.Firebox"/>.
  /// <para>
  /// <b>The beds are read directly, never vanilla piles out of a bounding box.</b> That keeps out four
  /// defects at once: a fuel cell that could be satisfied by <em>air</em> (an <c>@(air|coalpile)</c>
  /// legend), a per-cell ceiling nobody chose (<c>BlockEntityCoalPile.MaxStackSize</c>), vanilla's pile
  /// machinery and its Harmony side-table, and a firebox with no fuel identity in the world.
  /// </para>
  /// <para>
  /// Walks the role's cells, not the bounding box: <see cref="BlockEntityFurnaceCore.ShaftBox"/> is a
  /// box round them and would sweep the brick between two beds. The box survives only for
  /// <see cref="FireboxCellCount"/>, which wants a count rather than the cells.
  /// </para>
  /// </summary>
  protected override object CollectCharge()
  {
    var beds = new List<(BlockPos pos, BEBehaviorFirebox bed)>();
    foreach (BlockPos cell in FireboxCells)
      if (
        Api.World.BlockAccessor.GetBlockEntity(cell)?.GetBehavior<BEBehaviorFirebox>()
        is { } bed
      )
        beds.Add((cell, bed));
    return beds;
  }

  /// <summary>The handle <see cref="CollectCharge"/> hands out, named once so the three consumers cannot
  /// come to disagree about what they are casting to.</summary>
  private static List<(BlockPos pos, BEBehaviorFirebox bed)> BedsOf(
    object chargeHandle
  ) => (List<(BlockPos pos, BEBehaviorFirebox bed)>)chargeHandle;

  /// <summary>
  /// Reads the firebox. A reverberatory firebox burns <b>plain fuel</b>, never a burden - the ore/flux
  /// proportions the blast furnace lives on have no meaning here, because nothing in the fire is being
  /// reduced. So the mix reports as pure fuel, which is also the honest input to the heat balance: a
  /// firebox is all coke and nothing else.
  /// <para>
  /// The count needs no filtering: a bed only ever holds fuel it already accepted, so there is nothing
  /// left to filter and no way for an item-identity check to zero the count.
  /// </para>
  /// </summary>
  protected override int ReadChargeMix(
    object chargeHandle,
    out bool isFull,
    out BurdenMix mix,
    out int rejectedCount
  )
  {
    int count = 0;
    foreach (var (_, bed) in BedsOf(chargeHandle))
      count += bed.Units;

    isFull = count >= ChargeCapacityUnits;
    mix = new BurdenMix(0f, 0f, count);
    // Nothing is refused: a firebox takes any fuel, because it is only ever making flame. This zero is
    // the whole of the branch's answer - a firebox that ever reported a rejection would block its own
    // conversion and print a stall it cannot explain.
    rejectedCount = 0;
    return count;
  }

  /// <summary>
  /// A firebox is lit when every bed of it is <b>full</b>.
  /// <para>
  /// Asking each pile block whether it is burning would make ignition a property of
  /// blocks the furnace does not own and cannot set. With the bed a first-class part, "lit" is the
  /// furnace's own arithmetic: <see cref="ChargeCapacityUnits"/> is one cell's full six courses times the
  /// cell count, so a bed at capacity on a hearth of any size is what fires it.
  /// </para>
  /// <para>
  /// An empty bed list refuses, which is what keeps a drawing that marks <b>no</b> firebox inert rather
  /// than dangerous: <see cref="ChargeCapacityUnits"/> is 0 there and every count would otherwise pass.
  /// </para>
  /// </summary>
  protected override bool TryIgniteCharge(object chargeHandle)
  {
    var beds = BedsOf(chargeHandle);
    if (beds.Count == 0)
      return false;
    foreach (var (_, bed) in beds)
      if (!bed.IsFull)
        return false;
    return true;
  }

  /// <summary>
  /// Puts the bed out by burning most of it off, keeping <c>BfBurnoutFuelRetainedBottom</c> of what was
  /// in it as salvage - the same bargain the shaft's burn-out strikes, and for the same reason: a furnace
  /// going out is a setback, not a total loss of the charge.
  /// <para>
  /// <b>No height interpolation, unlike the shaft's.</b> A shaft's column is metres tall and the blast
  /// only ever reached the bottom of it, so what survives depends on how high it sat. A firebox is one
  /// course of cells all at the same level and all equally in the fire, so the bottom fraction is the only
  /// honest one to apply.
  /// </para>
  /// </summary>
  protected override void BurnOutCharge()
  {
    foreach (BlockPos cell in FireboxCells)
    {
      if (
        Api.World.BlockAccessor.GetBlockEntity(cell)?.GetBehavior<BEBehaviorFirebox>()
        is not { Units: > 0 } bed
      )
        continue;
      int keep = (int)(bed.Units * IwexValues.BfBurnoutFuelRetainedBottom);
      bed.Consume(bed.Units - keep);
    }
  }

  #endregion

  #region Tunables

  // Fire cadence is the shared furnace machine's, not a per-hearth fact: a firebox burns its fuel and
  // runs its cycle on the same clock the shaft branch does. Only the temperatures differ.
  protected override int MaxFuelBurnTime => IwexValues.FireboxMaxFuelBurnTime;
  protected override float MeltStartDelay => IwexValues.FireboxMeltStartDelay;
  protected override float MeltIntervalSec => IwexValues.FireboxMeltIntervalSec;

  /// <summary>
  /// Cells in the firebox this machine declares - the physical ceiling on what it can hold, at
  /// <see cref="BEBehaviorFirebox.CellCapacity"/> units each.
  /// <para>
  /// The ceiling is the firebox's own layer arithmetic (6 courses x 2 units) - a number this mod chose,
  /// not one read off vanilla's pile stack size.
  /// </para>
  /// <para>
  /// Derived from the geometry rather than hand-set, because a hand-set count is free to drift away from
  /// the firebox the machine actually has - and a threshold drifting out of physical reach is precisely
  /// the defect this guards against. The geometry itself comes off the drawing's
  /// <see cref="ExpandedLib.Blocks.Structures.CellRole.Firebox"/> marks
  /// (<see cref="BlockEntityFurnaceCore.ShaftBox"/>), so there is nothing between the count and the
  /// cells a player can actually load.
  /// </para>
  /// <para>
  /// A hearth whose drawing marks no firebox counts <b>0</b> cells, not 1. That is the honest answer -
  /// there is nowhere to put fuel - and it is inert rather than dangerous: the resulting
  /// <see cref="ChargeCapacityUnits"/> of 0 is never reached, because <see cref="TryIgniteCharge"/> refuses
  /// an empty pile list and a box-less furnace collects none.
  /// </para>
  /// </summary>
  protected int FireboxCellCount =>
    ShaftBox is not { } box
      ? 0
      : (Math.Abs(box.max.X - box.min.X) + 1)
        * (Math.Abs(box.max.Y - box.min.Y) + 1)
        * (Math.Abs(box.max.Z - box.min.Z) + 1);

  /// <summary>
  /// A firebox fires when its own cells are loaded, not against a shaft's stack count. A fixed constant
  /// here can be sized for one machine and inherited by another ten times smaller, with nothing in the
  /// type system or the suite to notice it has drifted out of physical reach.
  /// <para>
  /// <b>Sealed</b>: a leaf re-introducing a hand-picked constant is how that defect is born.
  /// </para>
  /// </summary>
  protected sealed override int ChargeCapacityUnits =>
    FireboxCellCount * IwexValues.FireboxMixPerCell;

  #endregion
}
