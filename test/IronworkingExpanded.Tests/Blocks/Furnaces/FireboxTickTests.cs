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
/// The firebox branch's <b>production tick</b> — that a reverberatory hearth, stood up complete and loaded
/// with fuel, actually lights itself and runs a fire clock.
/// <para>
/// <b>Why this file exists, and why now.</b> <see cref="FireboxChargeTests"/> covers the branch's charge
/// hooks by calling them directly, because when it was written neither hearth could complete a structure to
/// walk. That is still true <em>in game</em> — both layouts declare filler cells no part produces —
/// but it is <b>not</b> true in the harness: <c>StructureRig.Raise</c> synthesises a stand-in for every
/// empty footprint cell, so the machine completes and its own <c>OnProductionTick</c> can be driven. Nothing
/// had ever driven it on this branch.
/// </para>
/// <para>
/// <b>This is a prerequisite of retiring the shaft cadence keys, not a nicety.</b> The <c>Bf*</c>
/// cadence keys are slated for deletion, and <see cref="BlockEntityFireboxFurnace"/> reads three of them —
/// <c>MaxFuelBurnTime</c> / <c>MeltStartDelay</c> / <c>MeltIntervalSec</c>. With no test driving the tick,
/// deleting them leaves both hearths permanently <see cref="FurnaceState.Idle"/> and the whole suite green:
/// every existing firebox test asserts on hooks that are reached <em>before</em> the cadence is consulted.
/// A deletion that fails soft is this plan's signature failure mode, and the required shape of guard is a
/// <b>positive existence</b> assertion — "it lights, and the clock runs out" — never the absence of an error.
/// </para>
/// <para>
/// The tick is invoked directly rather than through a registered listener. <c>TestWorld.Attach</c> only
/// links the API; the listener is registered by <c>Initialize</c>, which belongs to the furnace core's
/// placement plumbing and is <see cref="ColdBlastFurnaceScenes"/>' subject, not this branch's. Invoking the
/// override is what pins the seam the cadence cutover breaks.
/// </para>
/// </summary>
public class FireboxTickTests
{
  #region Harness

  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>
  /// A heating hearth on its <b>shipped</b> layout, stood complete, with a real full <see cref="BEBehaviorFirebox"/>
  /// bed in every cell the drawing marks <c>Firebox</c>.
  /// <para>
  /// The beds are placed <em>after</em> completion, replacing the rig's stand-ins. The real firebox block
  /// code satisfies the same <see cref="FurnaceLayoutRig.FireboxGlyph"/> the stand-in did, so the structure
  /// stays complete — which the caller asserts rather than assumes.
  /// </para>
  /// </summary>
  private static BlockEntityHeatingFurnace LoadedHearth(int unitsPerCell)
  {
    var hearth = new BlockEntityHeatingFurnace();
    StructureRig rig = Stand(
      hearth,
      BlockHeatingFurnaceCore.Definitions("iwex").Single(),
      Anchor,
      "iwex:furnace-heatingcore-tier1",
      "north"
    );

    Item coke = new TestWorld().RegisterItem("game:coke");
    foreach (BlockPos cell in hearth.FireboxCells.ToList())
    {
      var be = new BlockEntityFirebox { Pos = cell.Copy() };
      var bed = new BEBehaviorFirebox(be);
      if (unitsPerCell > 0)
        bed.TryAdd(new ItemStack(coke, unitsPerCell), unitsPerCell);
      ReflectionHelpers.SetField(be, "Behaviors", new List<BlockEntityBehavior> { bed });
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
  /// override. <c>OnProductionTick</c> is <c>protected</c>.</summary>
  private static void Tick(BlockEntityFurnaceCore be, int seconds)
  {
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
  public void A_loaded_hearth_lights_itself_on_its_own_production_tick()
  {
    BlockEntityHeatingFurnace hearth = LoadedHearth(BEBehaviorFirebox.CellCapacity);

    // The premise, asserted rather than assumed: swapping the stand-ins for real fireboxes must not have
    // broken completion, or the tick returns on its first line and everything below is vacuous.
    Assert.True(hearth.StructureComplete);
    Assert.NotEmpty(hearth.FireboxCells);
    Assert.Equal(FurnaceState.Idle, hearth.State);

    Tick(hearth, 1);

    // The whole point: the firebox branch's CollectCharge -> ReadChargeMix -> TryIgniteCharge chain is
    // reached by the tick, not just by a test calling the hooks in order.
    Assert.Equal(FurnaceState.Firing, hearth.State);
  }

  [Fact]
  public void An_empty_hearth_stays_idle_however_long_it_is_ticked()
  {
    // The control. Without it, a hearth that lit unconditionally - the exact shape a broken
    // ChargeCapacityUnits would take - would satisfy the test above.
    BlockEntityHeatingFurnace hearth = LoadedHearth(0);

    Tick(hearth, 60);

    Assert.Equal(FurnaceState.Idle, hearth.State);
  }

  [Fact]
  public void A_half_loaded_hearth_stays_idle()
  {
    // One unit short of full. TryIgniteCharge demands every bed IsFull, so this must not light - and it
    // pins that the threshold is a real comparison rather than "any fuel at all".
    BlockEntityHeatingFurnace hearth = LoadedHearth(BEBehaviorFirebox.CellCapacity - 1);

    Tick(hearth, 10);

    Assert.Equal(FurnaceState.Idle, hearth.State);
  }

  #endregion

  #region The fire cadence is live

  /// <summary>
  /// <b>B8's fifth cause, stated as arithmetic and then as behaviour: a firebox cannot hold a shaft's
  /// disruption floor, so a lit hearth snuffs itself and the fuel clock is never reached.</b>
  /// <para>
  /// <c>ChargeCapacityUnits</c> is the firebox's own capacity — cells × <see cref="BEBehaviorFirebox.CellCapacity"/> —
  /// while <c>DisruptionMixFloor</c> is the shaft's <b>144</b>, inherited unchanged. A full hearth is
  /// therefore simultaneously "full enough to light" and "too empty to stay lit": it fires, immediately
  /// counts one disruption, and goes out when the extinguish grace expires. That is why
  /// <c>MaxFuelBurnTime</c> — the thing this file most wants to pin — is unreachable today.
  /// </para>
  /// <para>
  /// Written as a <b>failing-by-design description of the shipped defect</b> rather than skipped: the
  /// fix will have to change this test, which is exactly the notice a guard should give.
  /// Until then <see cref="A_loaded_hearth_lights_itself_on_its_own_production_tick"/> is what stands
  /// between the cadence-key deletion and a permanently-Idle pair of hearths.
  /// </para>
  /// </summary>
  [Fact]
  public void A_full_firebox_is_below_the_shafts_disruption_floor_so_the_fuel_clock_is_never_reached()
  {
    BlockEntityHeatingFurnace hearth = LoadedHearth(BEBehaviorFirebox.CellCapacity);

    int canHold = hearth.FireboxCells.Count * BEBehaviorFirebox.CellCapacity;
    int floor = Cadence(hearth, "DisruptionMixFloor");
    int burn = Cadence(hearth, "MaxFuelBurnTime");

    Assert.True(burn > 1, $"MaxFuelBurnTime must be a real duration, got {burn}");
    Assert.True(
      canHold < floor,
      $"B8's fifth cause is fixed: a full firebox now holds {canHold} against a floor of {floor}. "
        + "Rewrite this test as the two-sided fuel-clock assertion it was meant to be."
    );

    Tick(hearth, 1);
    Assert.Equal(FurnaceState.Firing, hearth.State);

    // It goes out on the disruption grace, long before the fuel clock could have run down.
    int seconds = 1;
    while (hearth.State != FurnaceState.Idle && seconds < burn)
    {
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
  /// they read. Deliberately redundant with the clock test above: that one proves
  /// <c>MaxFuelBurnTime</c> is consumed, while the melt pair are only reachable on a hearth hot enough to
  /// cross its process temperature - which nothing this file can stage today.
  /// </summary>
  [Fact]
  public void The_branch_declares_all_three_cadence_values_and_they_are_the_shipped_ones()
  {
    BlockEntityHeatingFurnace hearth = LoadedHearth(0);

    Assert.Equal(IwexValues.FireboxMaxFuelBurnTime, Cadence(hearth, "MaxFuelBurnTime"));
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
  /// <b>The shaft cutover's other prerequisite, and it is the same trap one member over.</b> The
  /// first-order chase - <c>_internalTemp</c> creeping toward <c>HeatBalance.TProcess</c> at
  /// <c>FireboxHeatRatePerSecond</c> - lives on the <b>shared</b> core tick, and the shaft branch sheds it
  /// because a shaft's thermal inertia is carried on its charge segments instead.
  /// <see cref="BlockEntityFireboxFurnace"/> overrides no tick at all, so deleting the chase rather than
  /// pushing it down makes every reverberatory hearth, the reheat furnace and the later hearth machines
  /// reach their ceiling <b>in one tick</b>.
  /// <para>
  /// That failure is invisible without this test. Preheat, soak and blow-in all lose their beat and
  /// nothing goes red - the end state is identical, only the journey to it disappears - and
  /// <i>"buy temperature by building the chimney taller"</i> becomes a step function with no observable
  /// transient - the very thing the transient exists to provide.
  /// </para>
  /// <para>
  /// Only the heating half is staged. <c>Extinguish()</c> resets straight to ambient, so a hearth never
  /// takes the cooling branch on any path a test can reach; <c>FireboxCoolRatePerSecond</c> is pinned by
  /// <see cref="The_two_chase_rates_are_live_positive_values"/> rather than by behaviour.
  /// </para>
  /// </summary>
  [Fact]
  public void A_lit_hearth_climbs_toward_its_target_over_seconds_rather_than_arriving_in_one_tick()
  {
    BlockEntityHeatingFurnace hearth = LoadedHearth(BEBehaviorFirebox.CellCapacity);

    Tick(hearth, 1);
    Assert.Equal(FurnaceState.Firing, hearth.State);
    float lit = Temp(hearth);

    const int seconds = 5;
    Tick(hearth, seconds);
    float later = Temp(hearth);

    // Still Firing, so the climb below is the chase and not some other path resetting the temperature.
    Assert.Equal(FurnaceState.Firing, hearth.State);
    // A jump-to-target implementation makes these two equal - that is the whole assertion. That it
    // rises by exactly the shipped rate is what pins the key as live rather than merely present.
    Assert.True(later > lit, $"the hearth sat at {lit} °C for {seconds}s instead of climbing");
    Assert.Equal(seconds * IwexValues.FireboxHeatRatePerSecond, later - lit, 2);
  }

  [Fact]
  public void The_two_chase_rates_are_live_positive_values()
  {
    // The cooling half has no reachable behaviour on this branch (Extinguish resets to ambient), so it is
    // pinned here. A rate of 0 is the same defect as a deleted key: the furnace never moves.
    Assert.True(IwexValues.FireboxHeatRatePerSecond > 0);
    Assert.True(IwexValues.FireboxCoolRatePerSecond > 0);
  }

  #endregion
}
