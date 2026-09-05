using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
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

  /// <summary>
  /// The stack over the fire, counted off the drawing's own <see cref="CellRole.Flue"/> marks rather than
  /// walked up the world. A reverberatory furnace's chimney is fixed at the height its layout declares,
  /// which is also why the cap is part of the drawing: there is nothing above it for a walk to find.
  /// </summary>
  protected override int StackCourses =>
    LocalCellsWithRole(CellRole.Flue).Count;

  /// <summary>
  /// The damper the stack is regulated by, at the cell directly over the highest flue course. Derived
  /// rather than declared, so a drawing that raises its chimney moves the cap with it. Null on a hearth
  /// whose drawing carries no cap, which reads as an unregulated open stack.
  /// </summary>
  protected BlockEntityPuddlingChimneyCap? Cap { get; private set; }

  /// <summary>The work door, whose opening spills the stack's pull into the room.</summary>
  protected BlockEntityChargeDoor? Door { get; private set; }

  /// <summary>
  /// The bed the work stands on, at <c>ShaftCentre</c>. Typed as the shared part base because the two
  /// reverberatory hearths hold different things - pigs and a bath here, stock on the reheat furnace - and
  /// each leaf reads it as its own kind. Null until the structure completes, and null again the moment a
  /// player breaks the bed out mid-heat, which every consumer has to tolerate.
  /// </summary>
  protected BlockEntityFurnacePart? HearthPart { get; private set; }

  protected override bool DamperOpen => Cap?.IsOpen ?? true;

  protected override bool Venting => Door?.IsVenting ?? false;

  /// <summary>
  /// Resolves the damper and the door alongside the outlets, so they are refreshed on exactly the
  /// schedule the taps are - on completion, on load and on the two lit-tick recovery paths.
  /// </summary>
  protected override void ScanForOutlets() {
    base.ScanForOutlets();

    IReadOnlyList<BlockPos> flue = CellsWithRole(CellRole.Flue);
    BlockPos? capPos =
      flue.Count == 0
        ? null
        : flue.Aggregate((a, b) => b.Y > a.Y ? b : a).UpCopy();

    Cap = capPos is null
      ? null
      : Api?.World.BlockAccessor.GetBlockEntity(capPos)
        as BlockEntityPuddlingChimneyCap;
    Door = DoorCell is not { } local
      ? null
      : Api?.World.BlockAccessor.GetBlockEntity(GlobalOf(local))
        as BlockEntityChargeDoor;
    // ShaftCentrePos is GlobalOf(ShaftCentre), so the rotation is already applied; hand-rotating here
    // would turn the bed twice and find brick at three of the four facings.
    HearthPart =
      Api?.World.BlockAccessor.GetBlockEntity(ShaftCentrePos)
      as BlockEntityFurnacePart;
  }

  /// <summary>
  /// Where this hearth's work door stands, in structure-local coordinates. Hand-declared like
  /// <c>ShaftCentre</c>, no role marking a door; both reverberatory drawings agree on it, being the same
  /// chassis one row apart, and a leaf whose drawing does not overrides. Rotation is free -
  /// <c>GlobalOf</c> applies the structure's own angle.
  /// </summary>
  protected virtual Vec3i? DoorCell => new(-2, 1, 1);

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
  /// Whether this machine's beds take <paramref name="stack"/> at all: hot enough to be firebox fuel at
  /// all, and not a coal too low-rank to carry a metallurgical heat. A firebox burns any fuel, so the
  /// branch answers with the bed's own test plus that one exclusion; a retort is pickier about what it
  /// will bake and overrides.
  /// </summary>
  /// <remarks>
  /// The rank exclusion is declared here rather than on <see cref="BEBehaviorFirebox"/>, whose
  /// <c>IsFuel</c> is static and knows only the stack: what a cell accepts is the owning machine's rule,
  /// not the cell's - a boiler burns what a reverberatory hearth refuses. The deposit path asks through
  /// <c>BlockEntityFirebox.Accepts</c>.
  /// </remarks>
  public virtual bool AcceptsFireboxFuel(ItemStack? stack) =>
    BEBehaviorFirebox.IsFuel(stack)
    && !IsLowRank(stack?.Collectible?.Code?.Path);

  /// <summary>Low-rank, high-moisture, high-ash coal, which will not carry a metallurgical heat however
  /// well it raises steam.</summary>
  private static bool IsLowRank(string? path) =>
    path != null && path.Contains("lignite", StringComparison.Ordinal);

  /// <summary>
  /// Tells <paramref name="player"/> why this machine turned down a stack that burns hot enough to be
  /// firebox fuel. Paired with <see cref="AcceptsFireboxFuel"/>: the machine that narrows the rule is the
  /// only one that knows the reason, so a leaf overriding one overrides both or the player is given the
  /// branch's answer for a rule it does not follow.
  /// </summary>
  public virtual void RefuseFireboxFuel(IServerPlayer? player) =>
    player?.SendIngameError("iiex-firebox-refused");

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
  /// Cells the drawing marks <see cref="ExpandedLib.Blocks.Structures.CellRole.Firebox"/> - the ceiling on
  /// what this machine can hold, at <see cref="BEBehaviorFirebox.DefaultCellCapacity"/> units each.
  /// </summary>
  /// <remarks>
  /// The marked cells, never the <see cref="BlockEntityFurnaceCore.ShaftBox"/> around them: the two agree
  /// only while a firebox is one solid cuboid, which the coke oven's two chambers either side of a shared
  /// wall are not, and a box would price in the brick between them. Since
  /// <see cref="MinChargeToIgnite"/> is sealed to this count, that oven could never have been lit. Read
  /// local rather than world cells, so the count answers on an unplaced machine. A drawing marking no
  /// firebox counts 0, which <see cref="TryIgniteCharge"/> refuses as an empty bed list.
  /// </remarks>
  protected int FireboxCellCount =>
    LocalCellsWithRole(ExpandedLib.Blocks.Structures.CellRole.Firebox).Count;

  /// <summary>
  /// A firebox fires when its own cells are loaded, so capacity is geometry rather than a constant.
  /// Sealed, because a hand-picked total sized for one machine can be inherited by a much smaller one and
  /// put the threshold out of physical reach.
  /// </summary>
  protected sealed override int ChargeCapacityUnits =>
    FireboxCellCount * IiexValues.FireboxMixPerCell;

  /// <summary>
  /// A share of the bed rather than the shaft's flat 144, which is four times what the largest firebox in
  /// the mod can hold: a hearth carrying it is at once full enough to light and under the floor, so it
  /// fires and goes out on the extinguish grace with the fuel clock never reached.
  /// </summary>
  /// <remarks>
  /// Firebox-only. The shaft branch derives its state per column and skips the disruption block entirely
  /// (<c>DerivesState</c>), so this override changes nothing there and blesses nothing about the shaft's
  /// own floor. Sealed for the same reason the capacity above is: a leaf picking a hand-sized number is
  /// how the unreachable floor was born. <c>FurnaceBranchGuards.NoFireboxCarriesAFloorAboveItsOwnCapacity</c>
  /// covers any branch that arrives unsealed.
  /// </remarks>
  protected sealed override int DisruptionMixFloor =>
    (int)(ChargeCapacityUnits * IiexValues.FireboxDisruptionFloorFraction);

  /// <summary>A bed charge or a few pots, not a descending column of cold ore.</summary>
  protected override float ChargeLossFull => IiexValues.FireboxChargeLossFull;

  /// <summary>
  /// The bridge between the fire and the work. Left virtual: a firebox machine whose work stands in the
  /// fuel bed rather than across a bridge - the crucible furnace, whose pots sit in the coke - pays none
  /// of it and overrides this to zero.
  /// </summary>
  protected override float TransferLoss => IiexValues.ReverberatoryTransferLoss;

  #endregion

  #region HUD

  /// <summary>
  /// The two lines a naturally-aspirated furnace needs and a blown one does not: what its stack is
  /// pulling, and what is taking heat out on the way to the work. Without the first, a damper shut or a
  /// door left open reads as the furnace being mysteriously cold; without the second the transfer loss
  /// shows up in the total with nothing accounting for it.
  /// </summary>
  protected override void AppendHeatExtras(StringBuilder sb) {
    sb.AppendLine(
      Lang.Get(
        IiexLang.BfInfoDraught,
        (int)
          Math.Round(
            StackDraught.NaturalDraughtFor(StackCourses, DamperOpen, Venting)
              * 100f
          ),
        StackCourses
      )
    );
    if (!DamperOpen)
      sb.AppendLine(Lang.Get(IiexLang.BfInfoDraughtDamped));
    if (Venting)
      sb.AppendLine(Lang.Get(IiexLang.BfInfoDraughtVenting));

    if (TransferLoss > 0f)
      sb.AppendLine(
        Lang.Get(
          IiexLang.BfInfoTransferloss,
          ExMeasure.Temperature(TransferLoss)
        )
      );
  }

  #endregion
}
