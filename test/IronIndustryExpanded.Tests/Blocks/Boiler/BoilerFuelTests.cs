using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Boiler.BlockEntities;
using Xunit;
using BoilerState = IronIndustryExpanded.BlockStructures.Boiler.BlockEntityBoiler.BoilerState;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// <see cref="BlockEntityBoiler.FuelRateMultiplier"/>: how far the charged bed's own vanilla burn
/// temperature carries the vessel's rated output. A boiler is heating-surface limited, not flame
/// limited - every shipped fuel but lignite clears <see cref="IiexValues.BoilerFuelDesignTemp"/>, so the
/// multiplier reads 1 for most of the table and the fuels differentiate through burn duration instead
/// (<see cref="BoilerFireboxTests"/>). An empty or unlit bed must still read the rated multiplier, so an
/// unfuelled boiler's arithmetic never collapses to zero.
/// </summary>
public class BoilerFuelTests {
  /// <summary>A fired boiler with a bed charged to capacity with <paramref name="fuelCode"/>, at 0 atm
  /// gauge so <c>SteamTemperature()</c> reads exactly the boiling point.</summary>
  private static BlockEntityBoilerCornish BoilerWith(string fuelCode) =>
    new BoilerRig(fuelCode).Be;

  #region Multiplier follows burn temperature

  [Theory]
  [InlineData("game:ore-anthracite", 1.00f)]
  [InlineData("game:ore-bituminouscoal", 1.00f)]
  [InlineData("game:coke", 1.00f)]
  [InlineData("game:charcoal", 1.00f)]
  [InlineData("game:ore-lignite", 0.91f)]
  public void FuelMultiplierFollowsBurnTemperature(string code, float expected) {
    Assert.Equal(expected, BoilerWith(code).FuelRateMultiplier, 2);
  }

  [Fact]
  public void LigniteStillRunsOneWattEngine() {
    float rate =
      IiexValues.CornishBoilerSteamPerSecond
      * BoilerWith("game:ore-lignite").FuelRateMultiplier;
    Assert.True(
      rate > IiexValues.WattEngineSteamRate,
      $"lignite raises {rate} L/s against a Watt engine's {IiexValues.WattEngineSteamRate} L/s"
    );
  }

  #endregion

  #region An empty or unlit bed does not collapse the arithmetic

  [Fact]
  public void A_boiler_with_no_bed_reads_the_rated_multiplier() {
    // A bare entity, the way BoilerMathTests and BoilerSteamCycleTests stand one up: no Bed behavior is
    // hosted, so BedBurnTemperature has nothing to read.
    var be = new BlockEntityBoilerCornish();
    Assert.Equal(1f, be.FuelRateMultiplier);
  }

  [Fact]
  public void An_unlit_charged_bed_also_reads_the_rated_multiplier() {
    var rig = new BoilerRig().ExtinguishFire();
    Assert.Equal(1f, rig.Be.FuelRateMultiplier);
  }

  #endregion

  #region The choke-pressure timing survives the capacity rebalance

  [Fact]
  public void Unloaded_boiler_reaches_choke_pressure_in_the_predicted_seconds() {
    // 600 L of steam space (1600 L capacity, held at the 1000 L boil-water ceiling); the 5 atm choke
    // needs 5 x 600 = 3000 L of steam. Feedwater tops the vessel back up to the ceiling after every
    // tick, so the steam space stays fixed at 600 L and BoilStep alone (no bed, so the multiplier is the
    // rated 1) is what is timed. At 64 L/s that is 3000 / 64 ~= 46.9 s, so the 47th tick is the one that
    // crosses 5 atm - a floor a later capacity change must not silently move past "un-burstable" or
    // under a second.
    var be = new BlockEntityBoilerCornish();
    ReflectionHelpers.SetField(
      be,
      "_waterVolume",
      IiexValues.CornishBoilerMaxBoilWater
    );

    int ticks = 0;
    while (be.InternalPressure < IiexValues.CornishBoilerMaxOutputPressure) {
      ReflectionHelpers.Invoke(be, "BoilStep", 1f);
      ReflectionHelpers.SetField(
        be,
        "_waterVolume",
        IiexValues.CornishBoilerMaxBoilWater
      );
      ticks++;
      Assert.True(ticks <= 200, "the pressure never reached the choke ceiling");
    }

    Assert.Equal(47, ticks);
  }

  #endregion

  #region HeatProgress agrees with the fuel-adjusted heat-up gate

  [Fact]
  public void HeatProgress_does_not_read_complete_before_the_lignite_gate_does() {
    // Lignite's multiplier (~0.91) stretches the real Heating->Boiling gate past the raw rated heat-up
    // time. At exactly the raw time, a cool fire has not actually finished: the readout must not claim
    // complete, and a tick from here must not yet cross into Boiling either - HeatProgress and the gate
    // read the same EffectiveHeatUpSeconds, so they can only agree or both be wrong together.
    var rig = new BoilerRig("game:ore-lignite")
      .SetState(BoilerState.Heating)
      .SetWater(400f)
      .SetHeatingSeconds(IiexValues.BoilerHeatUpSeconds);

    Assert.True(
      rig.Be.HeatProgress < 1f,
      $"HeatProgress read {rig.Be.HeatProgress} at the raw heat-up time on a lignite bed, but the "
        + "fuel-adjusted gate needs more"
    );
    rig.Tick();
    Assert.Equal(BoilerState.Heating, rig.State);

    // Comfortably past the fuel-adjusted threshold (~198 s), both the readout and the gate agree it is
    // done.
    rig.SetHeatingSeconds(210f);
    Assert.Equal(1f, rig.Be.HeatProgress);
    rig.Tick();
    Assert.Equal(BoilerState.Boiling, rig.State);
  }

  #endregion
}
