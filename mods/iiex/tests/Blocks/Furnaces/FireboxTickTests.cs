using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Machines;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The firebox branch's production tick: a reverberatory hearth stood up complete and loaded with fuel
/// lights itself and runs a fire clock. <c>StructureRig.Raise</c> synthesises a stand-in for every empty
/// footprint cell, which is what lets the machine complete here and its <c>OnProductionTick</c> be driven -
/// so completion here says nothing about whether a player could build it. That is
/// <see cref="FurnaceFillerAccountingTests"/>'s subject, and only a definition-level check can see it.
/// <para>
/// The tick is invoked directly rather than through a registered listener: <c>TestWorld.Attach</c> only
/// links the API, and the listener is registered by <c>Initialize</c>, part of the core's placement
/// plumbing. <see cref="FireboxChargeTests"/> covers the branch's charge hooks by calling them directly.
/// </para>
/// </summary>
public class FireboxTickTests {
  #region Harness

  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>
  /// A heating hearth on its shipped layout, stood complete, with a full <see cref="BEBehaviorFirebox"/> bed
  /// in every cell the drawing marks <c>Firebox</c>. The beds are placed after completion, replacing the
  /// rig's stand-ins; the real firebox block code satisfies the same
  /// <see cref="FurnaceLayoutRig.FireboxGlyph"/>, so the structure stays complete.
  /// </summary>
  private static BlockEntityHeatingFurnace LoadedHearth(int unitsPerCell) {
    var hearth = new BlockEntityHeatingFurnace();
    StructureRig rig = Stand(
      hearth,
      BlockHeatingFurnaceCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-heatingcore-tier1",
      "north"
    );

    LoadFireboxes(rig, hearth, unitsPerCell);
    return hearth;
  }

  /// <summary>
  /// A puddling furnace on its shipped layout, its one firebox cell loaded full. Same construction as
  /// <see cref="LoadedHearth"/>; the two hearths are the same chassis one row apart.
  /// </summary>
  private static (
    BlockEntityPuddlingFurnace Furnace,
    StructureRig Rig
  ) LoadedPuddler() {
    var furnace = new BlockEntityPuddlingFurnace();
    StructureRig rig = Stand(
      furnace,
      BlockPuddlingFurnaceCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-puddlingcore-tier1",
      "north"
    );
    LoadFireboxes(rig, furnace, BEBehaviorFirebox.DefaultCellCapacity);
    return (furnace, rig);
  }

  /// <summary>
  /// Puts a real hearth in the furnace's bed cell, fettled and charged to capacity, replacing the rig's
  /// stand-in. Flanks before centre - a fettled centre blocks both flanks.
  /// </summary>
  private static BlockEntityPuddlingHearth ChargedBed(
    StructureRig rig,
    BlockEntityPuddlingFurnace furnace
  ) {
    BlockPos cell = rig.Cell(-2, 0, 0);
    var bed = new BlockEntityPuddlingHearth { Pos = cell.Copy() };
    rig.Occupy(
      cell,
      TestBlocks.Configure(
        new BlockPuddlingHearth(),
        "iiex:furnace-puddlinghearth-n",
        1,
        ("type", "puddlinghearth"),
        ("side", "north")
      ),
      bed
    );

    foreach (
      HearthRows.Row row in new[]
      {
        HearthRows.Row.Left,
        HearthRows.Row.Right,
        HearthRows.Row.Centre,
      }
    ) {
      bed.TryFettle(row);
      for (int i = 0; i < PuddlingHearthLayout.PigsPerRow; i++)
        bed.TryChargePig(row);
    }

    // The furnace resolved its parts when the structure completed, before this bed existed.
    ReflectionHelpers.Invoke(furnace, "ScanForOutlets");
    return bed;
  }

  /// <summary>Runs <paramref name="seconds"/> one-second production ticks through the furnace's own
  /// override, by reflection because <c>OnProductionTick</c> is <c>protected</c>.</summary>
  private static void Tick(BlockEntityFurnaceCore be, int seconds) {
    for (int i = 0; i < seconds; i++)
      be.GetBehavior<BEBehaviorProductionMachine>().DriveProductionTick(1f);
  }

  private static int Cadence(BlockEntityFurnaceCore be, string member) =>
    (int)System.Convert.ToDouble(ReflectionHelpers.GetProperty(be, member)!);

  private static float Temp(BlockEntityFurnaceCore be) =>
    (float)ReflectionHelpers.GetField(be, "_internalTemp")!;

  /// <summary>The fuel beds standing in a hearth's firebox cells, read the way the branch reads them.</summary>
  private static IEnumerable<BEBehaviorFirebox> BedsOf(
    BlockEntityFireboxFurnace hearth
  ) =>
    hearth
      .FireboxCells.Select(cell =>
        hearth
          .Api.World.BlockAccessor.GetBlockEntity(cell)
          ?.GetBehavior<BEBehaviorFirebox>()
      )
      .Where(bed => bed is not null)
      .Select(bed => bed!);

  #endregion

  #region The branch's tick reaches ignition

  [Fact]
  public void A_loaded_hearth_lights_itself_on_its_own_production_tick() {
    BlockEntityHeatingFurnace hearth = LoadedHearth(
      BEBehaviorFirebox.DefaultCellCapacity
    );

    // The premise: swapping the stand-ins for real fireboxes must not break completion, or the tick
    // returns on its first line and the assertions below are vacuous.
    Assert.True(hearth.StructureComplete);
    Assert.NotEmpty(hearth.FireboxCells);
    Assert.Equal(FurnaceState.Idle, hearth.State);

    Tick(hearth, 1);

    // The tick itself reaches the branch's CollectCharge -> ReadChargeMix -> TryIgniteCharge chain.
    Assert.Equal(FurnaceState.Firing, hearth.State);
  }

  [Fact]
  public void An_empty_hearth_stays_idle_however_long_it_is_ticked() {
    // The control: a hearth that lit unconditionally, the shape a broken ChargeCapacityUnits takes,
    // would satisfy the case above.
    BlockEntityHeatingFurnace hearth = LoadedHearth(0);

    Tick(hearth, 60);

    Assert.Equal(FurnaceState.Idle, hearth.State);
  }

  [Fact]
  public void A_half_loaded_hearth_stays_idle() {
    // One unit short of full. TryIgniteCharge demands every bed IsFull, so the threshold is a real
    // comparison rather than "any fuel at all".
    BlockEntityHeatingFurnace hearth = LoadedHearth(
      BEBehaviorFirebox.DefaultCellCapacity - 1
    );

    Tick(hearth, 10);

    Assert.Equal(FurnaceState.Idle, hearth.State);
  }

  #endregion

  #region The fire cadence is live

  /// <summary>
  /// A full hearth stays lit. While the branch carried the shaft's flat floor of 144 it could not: a full
  /// firebox holds cells x <see cref="BEBehaviorFirebox.DefaultCellCapacity"/>, at most 24 u, so every lit tick
  /// counted a disruption and the fire went out the moment the extinguish grace expired.
  /// </summary>
  [Fact]
  public void A_full_firebox_outlasts_the_extinguish_grace() {
    BlockEntityHeatingFurnace hearth = LoadedHearth(
      BEBehaviorFirebox.DefaultCellCapacity
    );
    int grace = Cadence(hearth, "ExtinguishThresholdDefault");
    Assert.True(grace > 0, $"the grace must be a real duration, got {grace}");

    Tick(hearth, 1);
    Assert.Equal(FurnaceState.Firing, hearth.State);

    Tick(hearth, grace * 3);

    Assert.NotEqual(FurnaceState.Idle, hearth.State);
  }

  /// <summary>
  /// And it goes on to melt, which no reverberatory hearth in this mod has ever done: the reheat furnace
  /// works stock at <c>RollingTempC</c>, well inside what a firebox reaches, and only the inherited floor
  /// stood between it and its melt phase.
  /// </summary>
  [Fact]
  public void A_lit_hearth_crosses_into_its_melt_phase() {
    BlockEntityHeatingFurnace hearth = LoadedHearth(
      BEBehaviorFirebox.DefaultCellCapacity
    );

    Tick(hearth, (int)IiexValues.FireboxMeltStartDelay + 2);

    Assert.Equal(FurnaceState.Melting, hearth.State);
  }

  /// <summary>
  /// The puddling furnace crosses into its melt phase - the first time in the mod's history it can. Three
  /// things had to land together: it stays lit (B8's fifth cause), it works at 1400 C rather than iron's
  /// 1482 (B8's surviving half), and its four courses of chimney carry it there. The damper reads open
  /// because no cap block entity stands in this scene, which is the case the player sets up by hand.
  /// </summary>
  [Fact]
  public void A_puddling_furnace_crosses_into_its_melt_phase() {
    var (furnace, _) = LoadedPuddler();

    Assert.True(furnace.StructureComplete);
    Assert.Single(furnace.FireboxCells);

    Tick(furnace, 1);
    Assert.Equal(FurnaceState.Firing, furnace.State);

    // It lights at IgnitionTemp and climbs at a fixed rate, then has to hold above the process
    // temperature for the melt-start soak before the phase turns over.
    Tick(
      furnace,
      ClimbToProcess(furnace) + (int)IiexValues.FireboxMeltStartDelay + 2
    );

    Assert.Equal(FurnaceState.Melting, furnace.State);
  }

  /// <summary>Seconds a freshly lit hearth needs to climb from its ignition temperature to its process
  /// temperature, at the branch's fixed heat rate.</summary>
  private static int ClimbToProcess(BlockEntityFurnaceCore be) =>
    (int)
      System.Math.Ceiling(
        (Cadence(be, "MeltingPoint") - Cadence(be, "IgnitionTemp"))
          / IiexValues.FireboxHeatRatePerSecond
      );

  /// <summary>
  /// The whole first half of a heat, on the clock: a fettled and charged bed in a lit furnace loses its
  /// pigs to a bath. This is what U6's gate calls melt-down, and it is driven entirely from the core's
  /// <c>SmeltCycle</c> - the hearth registers no listener of its own, so the melt rides the bounded
  /// away-catch-up instead of teleporting on reload.
  /// </summary>
  [Fact]
  public void A_lit_puddling_furnace_melts_its_charge_down_to_a_bath() {
    var (furnace, rig) = LoadedPuddler();
    BlockEntityPuddlingHearth bed = ChargedBed(rig, furnace);

    Assert.Equal(PuddlingHearthLayout.PigCapacity, bed.PigCount);

    // Light it, carry it to the process temperature, hold there through the melt-start soak, then run
    // enough melt cycles for a full charge to go down.
    Tick(
      furnace,
      1
        + ClimbToProcess(furnace)
        + (int)IiexValues.FireboxMeltStartDelay
        + (int)(
          IiexValues.FireboxMeltIntervalSec
          / IiexValues.PuddlingMeltFractionPerCycle
        )
        + 10
    );

    Assert.Equal(FurnaceState.Melting, furnace.State);
    Assert.True(bed.HasBath, $"the bed is {bed.MeltProgress:P0} melted down");
    Assert.Equal(0, bed.PigCount);
    Assert.Equal(
      PuddlingHearthLayout.PigCapacity * ItemPig.PigUnits,
      bed.BathUnits
    );
  }

  /// <summary>
  /// A furnace whose bed was broken out mid-heat must not throw on the production tick. It reads as no
  /// hearth and does nothing, which is the honest answer.
  /// </summary>
  [Fact]
  public void A_furnace_with_no_hearth_ticks_without_throwing() {
    var (furnace, _) = LoadedPuddler();

    Assert.Null(furnace.Hearth);
    Tick(
      furnace,
      1 + ClimbToProcess(furnace) + (int)IiexValues.FireboxMeltStartDelay + 5
    );

    Assert.Equal(FurnaceState.Melting, furnace.State);
  }

  /// <summary>
  /// The floor still bites, from the other side: fuel drawn back out of a lit box below its share of the
  /// bed counts a disruption, and the fire goes out on the grace. A firebox lights full and burns on a
  /// clock rather than by consumption, so emptying it by hand is the only way under the floor.
  /// </summary>
  [Fact]
  public void A_lit_firebox_raked_below_its_floor_goes_out_on_the_grace() {
    BlockEntityHeatingFurnace hearth = LoadedHearth(
      BEBehaviorFirebox.DefaultCellCapacity
    );
    int grace = Cadence(hearth, "ExtinguishThresholdDefault");
    int floor = Cadence(hearth, "DisruptionMixFloor");

    Tick(hearth, 1);
    Assert.Equal(FurnaceState.Firing, hearth.State);

    foreach (BEBehaviorFirebox bed in BedsOf(hearth))
      bed.Consume(bed.Units - (floor / hearth.FireboxCells.Count) + 1);

    Tick(hearth, grace - 1);
    Assert.Equal(FurnaceState.Firing, hearth.State);

    Tick(hearth, 2);
    Assert.Equal(FurnaceState.Idle, hearth.State);
  }

  /// <summary>
  /// The three cadence members the branch declares, pinned as live positive values against the config keys
  /// they read. Redundant with the clock test above for <c>MaxFuelBurnTime</c>; the melt pair are only
  /// reachable on a hearth hot enough to cross its process temperature, which nothing here can stage.
  /// </summary>
  [Fact]
  public void The_branch_declares_all_three_cadence_values_and_they_are_the_shipped_ones() {
    BlockEntityHeatingFurnace hearth = LoadedHearth(0);

    Assert.Equal(
      IiexValues.FireboxMaxFuelBurnTime,
      Cadence(hearth, "MaxFuelBurnTime")
    );
    Assert.Equal(
      IiexValues.FireboxMeltStartDelay,
      (float)ReflectionHelpers.GetProperty(hearth, "MeltStartDelay")!
    );
    Assert.Equal(
      IiexValues.FireboxMeltIntervalSec,
      (float)ReflectionHelpers.GetProperty(hearth, "MeltIntervalSec")!
    );

    Assert.True(IiexValues.FireboxMaxFuelBurnTime > 0);
    Assert.True(IiexValues.FireboxMeltStartDelay > 0);
    Assert.True(IiexValues.FireboxMeltIntervalSec > 0);
  }

  #endregion

  #region The temperature transient is live

  /// <summary>
  /// The first-order chase - <c>_internalTemp</c> creeping toward <c>HeatBalance.TProcess</c> at
  /// <c>FireboxHeatRatePerSecond</c> - lives on the shared core tick, which the shaft branch sheds because
  /// a shaft carries its thermal inertia on the charge segments. <see cref="BlockEntityFireboxFurnace"/>
  /// overrides no tick, so removing the chase would make every hearth reach its ceiling in one tick: same
  /// end state, no observable transient.
  /// <para>
  /// Only the heating half is staged. <c>Extinguish()</c> resets straight to ambient, so no reachable path
  /// takes the cooling branch; <c>FireboxCoolRatePerSecond</c> is pinned by
  /// <see cref="The_two_chase_rates_are_live_positive_values"/> instead.
  /// </para>
  /// </summary>
  [Fact]
  public void A_lit_hearth_climbs_toward_its_target_over_seconds_rather_than_arriving_in_one_tick() {
    BlockEntityHeatingFurnace hearth = LoadedHearth(
      BEBehaviorFirebox.DefaultCellCapacity
    );

    Tick(hearth, 1);
    Assert.Equal(FurnaceState.Firing, hearth.State);
    float lit = Temp(hearth);

    const int seconds = 5;
    Tick(hearth, seconds);
    float later = Temp(hearth);

    // Still Firing, so the climb below is the chase and not some other path resetting the temperature.
    Assert.Equal(FurnaceState.Firing, hearth.State);
    // A jump-to-target implementation makes these two equal; rising by exactly the shipped rate is what
    // pins the config key as live rather than merely present.
    Assert.True(
      later > lit,
      $"the hearth sat at {lit} °C for {seconds}s instead of climbing"
    );
    Assert.Equal(seconds * IiexValues.FireboxHeatRatePerSecond, later - lit, 2);
  }

  [Fact]
  public void The_two_chase_rates_are_live_positive_values() {
    // The cooling half has no reachable behaviour on this branch (Extinguish resets to ambient), so it is
    // pinned here. A rate of 0 is the same defect as a deleted key: the furnace never moves.
    Assert.True(IiexValues.FireboxHeatRatePerSecond > 0);
    Assert.True(IiexValues.FireboxCoolRatePerSecond > 0);
  }

  #endregion
}
