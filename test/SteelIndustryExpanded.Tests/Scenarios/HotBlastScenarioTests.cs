using ExpandedLib;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockNetworkPipe;
using IronIndustryExpanded.Tests;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Whole-process hot-blast scenarios (handbook hot-blast article): a furnace spills exhaust into a gas
/// main every tick and the smoke stack vents the surplus the cowper stoves cannot swallow, keeping the
/// main from backing up and choking the furnace. The stack is a built <see cref="SmokeStackRig"/>, the
/// 72-cell chimney raised from its shipped layout and completed by its own monitor tick.
/// </summary>
public class HotBlastScenarioTests {
  /// <summary>One tick of the furnace spilling <paramref name="exhaust"/> litres into the main.</summary>
  private static void Furnace(
    TestWorld world,
    PipeNetwork net,
    float exhaust
  ) =>
    net.TryProduceGas(
      exhaust,
      700f,
      "Exhaust",
      world.Accessor,
      maxOutputPressure: 20f
    );

  #region Smoke stack safety valve

  // Furnace exhaust per tick: under one stack intake (48 L), so a working stack keeps ahead of it,
  // but 12 ticks of it (≈460 L) would swamp the 180 L main if nothing vents.
  private const float FurnacePerTick = 38f;

  [Fact]
  public void The_smoke_stack_vents_furnace_exhaust_so_the_main_does_not_choke() {
    // A long sealed exhaust main fed by a furnace each tick, with a built stack venting it.
    var rig = new SmokeStackRig(mainLength: 6);

    for (int i = 0; i < 12; i++)
      rig.SpillExhaust(FurnacePerTick).Tick();

    // The stack swallows more than the furnace spills, so the main stays well below a choking
    // over-pressure (a couple intakes' worth of slack at most).
    Assert.True(rig.LastVented > 0f, "the stack should be venting exhaust");
    Assert.True(
      rig.MainVolume <= 2f * SiexValues.SmokestackGasIntakeVolume,
      $"a vented main should stay near empty, was {rig.MainVolume} L"
    );
  }

  [Fact]
  public void Without_a_stack_the_exhaust_main_backs_up_and_chokes() {
    // The same main and the same furnace, but the stack never ticks - nothing draws the exhaust off.
    var rig = new SmokeStackRig(mainLength: 6);
    PipeNetwork net = rig.Main;

    for (int i = 0; i < 12; i++)
      Furnace(rig.World, net, FurnacePerTick); // furnace spills, but nothing vents

    // With no sink the exhaust accumulates well past the main's 1 atm capacity, the backed-up
    // condition that chokes the furnace.
    float maxVolume = net.Nodes.Count * ExlibValues.LitresPerPipe;
    Assert.True(
      net.State!.Volume > maxVolume,
      $"the unvented main should back up over-pressure, was {net.State!.Volume} of {maxVolume} L"
    );
  }

  #endregion

  #region Cowper stove regenerator cycle

  [Fact]
  public void A_charged_cowper_stove_blows_cool_air_back_out_as_hot_blast() {
    var rig = new CowperRig();

    // Charge: 1200 C furnace exhaust soaks heat into the brick core. Transfer is gradual, so the
    // charging session has to run long.
    for (int i = 0; i < 200; i++)
      rig.ChargeFromExhaust(exhaustTemp: 1200f);
    float charged = rig.CoreTemperature;
    Assert.True(
      charged > 100f,
      $"the core should charge from exhaust, was {charged} C"
    );

    // Discharge: cool 20 C air routed through the charged stove leaves hot, and the core gives up heat.
    rig.DischargeAir(airTemp: 20f);

    Assert.Equal("Air", rig.HotBlastMedium);
    Assert.True(
      rig.HotBlastVolume > 0f,
      "hot blast should be produced at the outlet"
    );
    Assert.True(
      rig.HotBlastTemperature > 100f,
      $"the air should leave far hotter than it entered, was {rig.HotBlastTemperature} C"
    );
    Assert.True(
      rig.CoreTemperature < charged,
      "discharging should cool the core"
    );
  }

  [Fact]
  public void A_cold_cowper_stove_cannot_make_hot_blast() {
    var rig = new CowperRig();

    // Never charged: discharging cool air through a cold core warms nothing.
    rig.DischargeAir(airTemp: 20f);

    Assert.True(
      rig.HotBlastTemperature <= 20f,
      $"a cold stove should not heat the blast, was {rig.HotBlastTemperature} C"
    );
  }

  // The two-phase cycle is discharge, close the air valve, recharge from exhaust. Discharging leaves
  // blast air stranded in the passthrough (a pressurised run holds well over a pipe's worth), and the
  // mix guard must not read that stranded air as air actively flowing.
  [Fact]
  public void Recharging_after_a_discharge_is_not_blocked_by_air_left_in_the_passthrough() {
    var rig = new CowperRig();

    // One discharge leaves air stranded in the passthrough.
    rig.DischargeAir(airTemp: 20f, litres: 90f);
    Assert.True(
      rig.CoreTemperature <= 25f,
      "precondition: the core is still cold after a cold discharge"
    );

    // Air valve now closed (no fresh air); recharge from exhaust. The stranded air must not block it.
    for (int i = 0; i < 200; i++)
      rig.ChargeFromExhaust(exhaustTemp: 1200f);

    Assert.True(
      rig.CoreTemperature > 100f,
      $"a recharge must clear the stranded air and heat the core, was {rig.CoreTemperature} C"
    );
  }

  // With both valves open the mix guard must still fire: the stove cannot soak exhaust into the core
  // while air streams through the passthrough, so it must not charge.
  [Fact]
  public void Exhaust_with_air_actively_flowing_is_still_treated_as_mixing() {
    var rig = new CowperRig();
    float before = rig.CoreTemperature;

    // Re-supply air every tick (open air valve) alongside the exhaust.
    for (int i = 0; i < 50; i++)
      rig.MixAirAndExhaust(exhaustTemp: 1200f);

    Assert.True(
      rig.CoreTemperature - before < 5f,
      $"the core must not charge while air is actively mixing, rose to {rig.CoreTemperature} C"
    );
  }

  #endregion
}
