using ExpandedLib.Machines;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkPipe;
using IronIndustryExpanded.BlockStructures.Engine.BlockEntities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The Watt engine's power tick, driven through <see cref="EngineFixture"/> and
/// <see cref="Scene"/>: the engine engages only inside its pressure band and only with a sub-machine
/// demanding power, draws steam, makes power, wears toward a burst above the band, and recovers on
/// repair.
/// </summary>
public class EngineTickTests {
  private static (Scene scene, EngineFixture eng) NewEngine() {
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var eng = new EngineFixture(scene, new BlockPos(0, 8, 0));
    scene.Build();
    return (scene, eng);
  }

  [Fact]
  public void Idle_below_the_engage_pressure() {
    var (scene, eng) = NewEngine();
    eng.SetInletPressure(1f); // Watt engages at 2 atm
    scene.Step();

    Assert.Equal(0f, eng.Engine.AvailablePower, 4);
    Assert.False(eng.Engine.IsRunning);
  }

  [Fact]
  public void Engages_and_makes_power_in_band() {
    var (scene, eng) = NewEngine();
    eng.SetInletPressure(3f); // inside the 2-4 atm band
    float before = eng.InletVolume;
    scene.Step();

    Assert.True(
      eng.Engine.AvailablePower > 0f,
      "engine should make power in band"
    );
    Assert.True(eng.Engine.IsRunning);
    Assert.Equal(3f, eng.Engine.InletPressure, 1);
    Assert.True(
      eng.InletVolume < before,
      "running should draw steam from the inlet"
    );
  }

  [Fact]
  public void Bursts_after_sustained_over_pressure_then_stops() {
    var (scene, eng) = NewEngine();

    // Pre-load the over-pressure timer to just under the break threshold, then run one tick above
    // the break pressure so it crosses - avoids stepping the full EngineOverPressureSeconds.
    var primed = new GraceTimer();
    primed.Update(
      true,
      IiexValues.EngineOverPressureSeconds - 0.5f,
      float.MaxValue
    );
    ReflectionHelpers.SetField(eng.Engine, "_overPressure", primed);

    eng.SetInletPressure(4.5f); // above the 4 atm break pressure
    scene.Step();

    Assert.True(
      eng.Engine.IsBroken,
      "sustained over-pressure should burst the engine"
    );
    Assert.Equal(0f, eng.Engine.AvailablePower, 4);
    Assert.False(eng.Engine.IsRunning);
  }

  [Fact]
  public void A_broken_engine_stays_inert_until_repaired() {
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

  // The burst path is a sticky IsBroken latch plus an over-pressure accumulator. Repair() must clear
  // both, otherwise a repaired engine re-breaks on the first over-pressure tick.
  [Fact]
  public void Repairing_resets_the_over_pressure_timer_so_a_single_tick_does_not_re_break() {
    var (scene, eng) = NewEngine();

    // Break it: prime the timer to just under the threshold so one over-pressure tick trips it.
    var primed = new GraceTimer();
    primed.Update(
      true,
      IiexValues.EngineOverPressureSeconds - 0.5f,
      float.MaxValue
    );
    ReflectionHelpers.SetField(eng.Engine, "_overPressure", primed);
    eng.SetInletPressure(4.5f); // above the 4 atm break pressure
    scene.Step();
    Assert.True(
      eng.Engine.IsBroken,
      "precondition: sustained over-pressure broke it"
    );

    // Repair, then run one more over-pressure tick. With the accumulator reset the engine must take
    // the full grace again rather than breaking on this single tick.
    eng.Engine.Repair();
    eng.SetInletPressure(4.5f);
    scene.Step();

    Assert.False(
      eng.Engine.IsBroken,
      "a single over-pressure tick must not re-break a just-repaired engine"
    );
    Assert.True(
      eng.Engine.OverPressureRemaining > 1f,
      $"the over-pressure grace should have reset on repair, was {eng.Engine.OverPressureRemaining}"
    );
  }

  [Fact]
  public void Power_state_round_trips_through_the_tree() {
    var (scene, eng) = NewEngine();
    ReflectionHelpers.SetField(eng.Engine, "_running", true);
    ReflectionHelpers.SetField(
      eng.Engine,
      "<AvailablePower>k__BackingField",
      0.25f
    );
    ReflectionHelpers.SetField(eng.Engine, "<IsBroken>k__BackingField", true);

    var tree = new TreeAttribute();
    eng.Engine.ToTreeAttributes(tree);

    var restored = new BlockEntityEngineWatt {
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
