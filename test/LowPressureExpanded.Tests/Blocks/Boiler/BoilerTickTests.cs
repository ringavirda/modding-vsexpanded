using ExpandedLib.Testing;
using Xunit;
using BoilerState = LowPressureExpanded.BlockStructures.Boiler.BlockEntityBoiler.BoilerState;

namespace LowPressureExpanded.Tests;

/// <summary>
/// The boiler's full production tick: the construction- and structure-gated state machine, driven
/// through <see cref="BoilerRig"/>, which fakes the finished construction, the structure, and a burning
/// firebox.
/// </summary>
public class BoilerTickTests {
  [Fact]
  public void Rig_reports_a_constructed_operational_boiler() {
    var rig = new BoilerRig();
    Assert.True(rig.Be.IsConstructed);
    Assert.True(rig.Be.IsOperational);
  }

  [Fact]
  public void Idle_starts_heating_when_fired_with_enough_water() {
    var rig = new BoilerRig().SetState(BoilerState.Idle).SetWater(200f);

    rig.Tick();

    Assert.Equal(BoilerState.Heating, rig.State);
  }

  [Fact]
  public void Idle_stays_idle_without_enough_water() {
    // The Cornish boiler needs 150 L; 100 L is below the floor.
    var rig = new BoilerRig().SetState(BoilerState.Idle).SetWater(100f);

    rig.Tick();

    Assert.Equal(BoilerState.Idle, rig.State);
  }

  [Fact]
  public void Idle_stays_idle_when_the_fire_is_out() {
    var rig = new BoilerRig()
      .SetState(BoilerState.Idle)
      .SetWater(200f)
      .ExtinguishFire();

    rig.Tick();

    Assert.Equal(BoilerState.Idle, rig.State);
  }

  [Fact]
  public void Heating_reaches_boiling_once_the_heat_up_time_elapses() {
    var rig = new BoilerRig()
      .SetState(BoilerState.Heating)
      .SetWater(200f)
      .SetHeatingSeconds(LpexValues.BoilerHeatUpSeconds - 1f);

    rig.Tick(); // crosses the heat-up threshold this tick

    Assert.Equal(BoilerState.Boiling, rig.State);
  }

  [Fact]
  public void Boiling_converts_water_into_steam() {
    var rig = new BoilerRig()
      .SetState(BoilerState.Boiling)
      .SetWater(200f)
      .SetSteam(0f);

    rig.Tick();

    Assert.True(rig.SteamVolume > 0f, "boiling should generate steam");
    // BoilStep consumes SteamPerSecond/expansion litres of water per second (32/16 = 2 L for the
    // Cornish). The steam pool is not asserted exactly: with no pipe on the outlet, the open neck
    // leaks some of it back out the same tick.
    float expectedWaterUse =
      LpexValues.CornishBoilerSteamPerSecond / LpexValues.SteamExpansionFactor;
    Assert.Equal(200f - expectedWaterUse, rig.WaterVolume, 2);
  }

  [Fact]
  public void A_running_boiler_shuts_down_after_the_grace_period_when_the_fire_dies() {
    var rig = new BoilerRig()
      .SetState(BoilerState.Boiling)
      .SetWater(200f)
      .SetShutdownSeconds(LpexValues.BoilerShutdownDelaySeconds)
      .ExtinguishFire();

    rig.Tick(); // pushes the shutdown timer past the grace period

    Assert.Equal(BoilerState.Idle, rig.State);
  }

  [Fact]
  public void Heating_aborts_back_to_idle_after_grace_when_water_runs_out() {
    var rig = new BoilerRig()
      .SetState(BoilerState.Heating)
      .SetWater(0f) // below the boil floor
      .SetShutdownSeconds(LpexValues.BoilerShutdownDelaySeconds);

    rig.Tick();

    Assert.Equal(BoilerState.Idle, rig.State);
  }

  // Shutdown leaves steam in the vessel (it condenses back to water in Idle), so a re-fire runs against
  // a steam-laden vessel. That residual steam must not latch the boiler out of operation: re-lighting
  // with enough water must reach Heating again.
  [Fact]
  public void A_boiler_re_fires_after_a_shutdown_despite_leftover_steam() {
    // Run to Boiling, snuff the fire, and tick past the grace so it shuts down to Idle - with steam
    // still in the vessel.
    var rig = new BoilerRig()
      .SetState(BoilerState.Boiling)
      .SetWater(300f)
      .SetSteam(200f)
      .SetShutdownSeconds(LpexValues.BoilerShutdownDelaySeconds)
      .ExtinguishFire();
    rig.Tick();
    Assert.Equal(BoilerState.Idle, rig.State);
    Assert.True(
      rig.SteamVolume > 0f,
      "precondition: leftover steam remains in the vessel after shutdown"
    );

    // Re-light and top the water back up: the second heat must reach Heating again - the residual
    // steam must not block the re-fire.
    rig.RelightFire().SetWater(300f).Tick();

    Assert.Equal(BoilerState.Heating, rig.State);
  }
}
