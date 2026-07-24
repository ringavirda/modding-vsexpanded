using ExpandedLib.Networks;
using ExpandedLib.Testing;
using SteelmakingExpanded;
using Vintagestory.API.MathTools;
using Xunit;

namespace HighPressureExpanded.Tests;

/// <summary>
/// Whole-process scenarios for the high-pressure Cornish engine driving each of its sub-machines: an
/// MP generator producing a mechanical-power budget, and smex's air blower pressurising air past the
/// blast threshold. Each lays the machines + their pipe lines into one <see cref="Scene"/> and advances
/// them together, asserting the emergent result rather than any single component.
/// <para>
/// Split out of lpex's <c>SteamPlantScenarioTests</c> when the Cornish engine moved to hpex; the Watt
/// engine's water-pump halves stayed there.
/// </para>
/// </summary>
public class HpSteamPlantScenarioTests
{
  #region MP production (boiler→engine→MP generator)

  [Fact]
  public void Steam_in_band_makes_the_mp_generator_deliver_a_power_budget()
  {
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var plant = new MPGeneratorPlant(scene, new BlockPos(0, 8, 0));
    scene.Build();

    plant.RunWithSteam(7f, 2); // above the Cornish engine's normal-throttle engage pressure

    Assert.True(plant.Engine.IsRunning, "the cornish engine should engage");
    Assert.True(
      plant.MpPowerBudget > 0f,
      "the MP generator should deliver a power budget"
    );
  }

  [Fact]
  public void Without_steam_the_mp_generator_delivers_no_power()
  {
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var plant = new MPGeneratorPlant(scene, new BlockPos(0, 8, 0));
    scene.Build();

    scene.Step(2); // no steam

    Assert.False(plant.Engine.IsRunning);
    Assert.Equal(0f, plant.MpPowerBudget, 4);
  }

  #endregion

  #region Blast production (boiler→engine→air blower→blast)

  [Fact]
  public void Steam_drives_the_air_blower_to_pressurise_air_into_blast()
  {
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var plant = new AirBlowerPlant(scene, new BlockPos(0, 8, 0));
    scene.Build();

    plant.RunWithSteam(7f, 4); // above the Cornish engine's engage pressure

    Assert.True(plant.Engine.IsRunning, "the cornish engine should engage");
    Assert.Equal("Air", plant.BlastMedium); // the blower pushes Air onto its left network
    Assert.True(
      plant.BlastPressure >= SmexValues.BlastPressureThreshold,
      $"the air should be pressurised past the blast threshold, was {plant.BlastPressure} atm"
    );
  }

  [Fact]
  public void Without_steam_the_air_blower_produces_no_blast()
  {
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var plant = new AirBlowerPlant(scene, new BlockPos(0, 8, 0));
    scene.Build();

    scene.Step(4); // no steam at the engine

    Assert.False(plant.Engine.IsRunning);
    Assert.Equal(0f, plant.BlastPressure, 3);
  }

  #endregion
}
