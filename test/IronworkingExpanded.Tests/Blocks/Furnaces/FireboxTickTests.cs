using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The firebox branch's production tick: a reverberatory hearth stood up complete and loaded with fuel
/// lights itself and runs a fire clock. Both hearth layouts declare filler cells no part produces and so
/// cannot complete in game; <c>StructureRig.Raise</c> synthesises a stand-in for every empty footprint cell,
/// which is what lets the machine complete here and its <c>OnProductionTick</c> be driven.
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
      BlockHeatingFurnaceCore.Definitions("iwex").Single(),
      Anchor,
      "iwex:furnace-heatingcore-tier1",
      "north"
    );

    Item coke = new TestWorld().RegisterItem("game:coke");
    foreach (BlockPos cell in hearth.FireboxCells.ToList()) {
      var be = new BlockEntityFirebox { Pos = cell.Copy() };
      var bed = new BEBehaviorFirebox(be);
      if (unitsPerCell > 0)
        bed.TryAdd(new ItemStack(coke, unitsPerCell), unitsPerCell);
      ReflectionHelpers.SetField(
        be,
        "Behaviors",
        new List<BlockEntityBehavior> { bed }
      );
      rig.Occupy(
        cell,
        TestBlocks.Configure(
          new Block(),
          "iwex:furnace-firebox-tier1-n",
          900,
          ("side", "north")
        ),
        be
      );
    }

    return hearth;
  }

  /// <summary>Runs <paramref name="seconds"/> one-second production ticks through the furnace's own
  /// override, by reflection because <c>OnProductionTick</c> is <c>protected</c>.</summary>
  private static void Tick(BlockEntityFurnaceCore be, int seconds) {
    for (int i = 0; i < seconds; i++)
      ReflectionHelpers.Invoke(be, "OnProductionTick", 1f);
  }

  private static int Cadence(BlockEntityFurnaceCore be, string member) =>
    (int)System.Convert.ToDouble(ReflectionHelpers.GetProperty(be, member)!);

  private static float Temp(BlockEntityFurnaceCore be) =>
    (float)ReflectionHelpers.GetField(be, "_internalTemp")!;

  #endregion

  #region The branch's tick reaches ignition

  [Fact]
  public void A_loaded_hearth_lights_itself_on_its_own_production_tick() {
    BlockEntityHeatingFurnace hearth = LoadedHearth(
      BEBehaviorFirebox.CellCapacity
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
      BEBehaviorFirebox.CellCapacity - 1
    );

    Tick(hearth, 10);

    Assert.Equal(FurnaceState.Idle, hearth.State);
  }

  #endregion

  #region The fire cadence is live

  /// <summary>
  /// A firebox cannot hold the shaft's disruption floor, so a lit hearth snuffs itself before the fuel
  /// clock is reached. <c>ChargeCapacityUnits</c> is the firebox's own capacity, cells ×
  /// <see cref="BEBehaviorFirebox.CellCapacity"/>, while <c>DisruptionMixFloor</c> is the shaft's 144,
  /// inherited unchanged: a full hearth is both full enough to light and below the floor, so it fires,
  /// counts one disruption and goes out when the extinguish grace expires. <c>MaxFuelBurnTime</c> is
  /// unreachable while that holds. This describes the shipped behaviour; a fix has to rewrite the test.
  /// </summary>
  [Fact]
  public void A_full_firebox_is_below_the_shafts_disruption_floor_so_the_fuel_clock_is_never_reached() {
    BlockEntityHeatingFurnace hearth = LoadedHearth(
      BEBehaviorFirebox.CellCapacity
    );

    int canHold = hearth.FireboxCells.Count * BEBehaviorFirebox.CellCapacity;
    int floor = Cadence(hearth, "DisruptionMixFloor");
    int burn = Cadence(hearth, "MaxFuelBurnTime");

    Assert.True(
      burn > 1,
      $"MaxFuelBurnTime must be a real duration, got {burn}"
    );
    Assert.True(
      canHold < floor,
      $"B8's fifth cause is fixed: a full firebox now holds {canHold} against a floor of {floor}. "
        + "Rewrite this test as the two-sided fuel-clock assertion it was meant to be."
    );

    Tick(hearth, 1);
    Assert.Equal(FurnaceState.Firing, hearth.State);

    // It goes out on the disruption grace, long before the fuel clock could have run down.
    int seconds = 1;
    while (hearth.State != FurnaceState.Idle && seconds < burn) {
      Tick(hearth, 1);
      seconds++;
    }

    Assert.Equal(FurnaceState.Idle, hearth.State);
    Assert.True(
      seconds < burn,
      $"the hearth survived {seconds}s of a {burn}s fuel clock - the disruption no longer decides"
    );
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
      IwexValues.FireboxMaxFuelBurnTime,
      Cadence(hearth, "MaxFuelBurnTime")
    );
    Assert.Equal(
      IwexValues.FireboxMeltStartDelay,
      (float)ReflectionHelpers.GetProperty(hearth, "MeltStartDelay")!
    );
    Assert.Equal(
      IwexValues.FireboxMeltIntervalSec,
      (float)ReflectionHelpers.GetProperty(hearth, "MeltIntervalSec")!
    );

    Assert.True(IwexValues.FireboxMaxFuelBurnTime > 0);
    Assert.True(IwexValues.FireboxMeltStartDelay > 0);
    Assert.True(IwexValues.FireboxMeltIntervalSec > 0);
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
      BEBehaviorFirebox.CellCapacity
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
    Assert.Equal(seconds * IwexValues.FireboxHeatRatePerSecond, later - lit, 2);
  }

  [Fact]
  public void The_two_chase_rates_are_live_positive_values() {
    // The cooling half has no reachable behaviour on this branch (Extinguish resets to ambient), so it is
    // pinned here. A rate of 0 is the same defect as a deleted key: the furnace never moves.
    Assert.True(IwexValues.FireboxHeatRatePerSecond > 0);
    Assert.True(IwexValues.FireboxCoolRatePerSecond > 0);
  }

  #endregion
}
