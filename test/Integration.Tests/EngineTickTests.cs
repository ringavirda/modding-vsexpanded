using ExpandedLib.Testing;
using ExpandedLib.Blocks.Machines;
using PipesAndPowerExpanded.BlockNetworkPipe;
using PipesAndPowerExpanded.BlockStructures.Engine.BlockEntities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace PipesAndPowerExpanded.Tests;

/// <summary>
/// The steam engine's power tick - the gated, multi-component subsystem (engine + sub-machine +
/// steam inlet) that had no coverage. Driven through <see cref="EngineFixture"/> + <see cref="Scene"/>:
/// the engine engages only inside its pressure band with a demanding sub-machine, draws steam, makes
/// power, wears toward a burst above the band, and recovers on repair.
/// </summary>
public class EngineTickTests
{
  private static (Scene scene, EngineFixture eng) NewEngine()
  {
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var eng = new EngineFixture(scene, new BlockPos(0, 8, 0));
    scene.Build();
    return (scene, eng);
  }

  [Fact]
  public void Idle_below_the_engage_pressure()
  {
    var (scene, eng) = NewEngine();
    eng.SetInletPressure(1f); // Watt engages at 2 atm
    scene.Step();

    Assert.Equal(0f, eng.Engine.AvailablePower, 4);
    Assert.False(eng.Engine.IsRunning);
  }

  [Fact]
  public void Engages_and_makes_power_in_band()
  {
    var (scene, eng) = NewEngine();
    eng.SetInletPressure(3f); // inside the 2-4 atm band
    float before = eng.InletVolume;
    scene.Step();

    Assert.True(eng.Engine.AvailablePower > 0f, "engine should make power in band");
    Assert.True(eng.Engine.IsRunning);
    Assert.Equal(3f, eng.Engine.InletPressure, 1);
    Assert.True(eng.InletVolume < before, "running should draw steam from the inlet");
  }

  [Fact]
  public void Bursts_after_sustained_over_pressure_then_stops()
  {
    var (scene, eng) = NewEngine();

    // Pre-load the over-pressure timer to just under the break threshold, then run one tick above
    // the break pressure so it crosses - avoids stepping the full EngineOverPressureSeconds.
    var primed = new GraceTimer();
    primed.Update(true, PpexValues.EngineOverPressureSeconds - 0.5f, float.MaxValue);
    ReflectionHelpers.SetField(eng.Engine, "_overPressure", primed);

    eng.SetInletPressure(4.5f); // above the 4 atm break pressure
    scene.Step();

    Assert.True(eng.Engine.IsBroken, "sustained over-pressure should burst the engine");
    Assert.Equal(0f, eng.Engine.AvailablePower, 4);
    Assert.False(eng.Engine.IsRunning);
  }

  [Fact]
  public void A_broken_engine_stays_inert_until_repaired()
  {
    var (scene, eng) = NewEngine();
    ReflectionHelpers.SetField(eng.Engine, "<IsBroken>k__BackingField", true);

    eng.SetInletPressure(3f);
    scene.Step();
    Assert.Equal(0f, eng.Engine.AvailablePower, 4); // inert despite good steam

    eng.Engine.Repair();
    Assert.False(eng.Engine.IsBroken);
    scene.Step();
    Assert.True(eng.Engine.AvailablePower > 0f, "a repaired engine runs again");
  }

  [Fact]
  public void Power_state_round_trips_through_the_tree()
  {
    var (scene, eng) = NewEngine();
    ReflectionHelpers.SetField(eng.Engine, "_running", true);
    ReflectionHelpers.SetField(eng.Engine, "<AvailablePower>k__BackingField", 0.25f);
    ReflectionHelpers.SetField(eng.Engine, "<IsBroken>k__BackingField", true);

    var tree = new TreeAttribute();
    eng.Engine.ToTreeAttributes(tree);

    var restored = new BlockEntityEngineWatt
    {
      Pos = eng.Engine.Pos.Copy(),
      Block = eng.Block,
    };
    restored.Api = eng.Engine.Api;
    restored.FromTreeAttributes(tree, eng.Engine.Api.World);

    Assert.True(restored.IsBroken);
    Assert.Equal(0.25f, restored.AvailablePower, 3);
    Assert.True(restored.IsRunning);
  }
}
