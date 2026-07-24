using ExpandedLib.Testing;
using SteelmakingExpanded;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.Items;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The blast furnace's primary process driven end to end (handbook blast-furnace + hot-blast): a
/// charged, lit hearth fed hot blast climbs past iron's melting point, enters the Melting phase,
/// renders blast mix into molten pig iron, and taps it into a canal - and loses the melt if the blast
/// is cut. Exercises the gated firing/melting tick with its real peripherals via <see cref="BlastFurnaceRig"/>.
/// </summary>
public class BlastFurnaceScenarioTests
{
  #region The whole process, emergent

  // Every other test in this file jumps the multi-minute heat-up with the rig's Set* fast-forwards and
  // invokes OnProductionTick directly, so each asserts one transition in isolation. That is useful and
  // fast, but it can never catch the process failing to *start* - a furnace whose structure never
  // completes, whose tick is never registered, or which is never gated on. These two run the machine
  // the way the server does: the real structure standing, the furnace's own tick listener on the clock,
  // and nothing invoked by reflection.

  [Fact]
  public void A_built_and_blown_furnace_reaches_melting_on_its_own()
  {
    var rig = new BlastFurnaceRig(blastMix: 640).FeedBlast(950f);

    // It starts cold, idle, and untouched.
    Assert.True(rig.Furnace.StructureComplete, "the furnace should have completed its own structure");
    Assert.Equal(FurnaceState.Idle, rig.State);

    rig.RunLive(540); // 9 minutes of blown, charged operation

    Assert.Equal(FurnaceState.Melting, rig.State);
    Assert.True(
      rig.Temp > IwexValues.BfIronMeltingPoint,
      $"a blown furnace should settle above the melt line, was {rig.Temp} C"
    );
  }

  [Fact]
  public void The_headline_process_ends_with_pig_iron_in_the_canal()
  {
    // charge -> hot blast -> molten pig iron -> tapped into a canal, driven only by the clock.
    var rig = new BlastFurnaceRig(blastMix: 640)
      .WithIronTapAndCanal()
      .FeedBlast(950f);

    rig.RunLive(540);

    Assert.True(rig.CanalIron > 0, "the open tap should have poured metal into the canal");
    Assert.Contains("pigiron", rig.CanalMetalType!);
  }

  #endregion

  #region Heat + phase progression

  [Fact]
  public void Hot_blast_drives_the_furnace_past_irons_melting_point()
  {
    // A lit, firing furnace sat at where cold blast alone would hold it (1420 C, below iron's
    // 1482 C melt point).
    var rig = new BlastFurnaceRig()
      .FeedBlast(950f)
      .SetState(FurnaceState.Firing)
      .SetTemp(BlastFurnaceRig.ColdBlastCeiling);

    rig.Tick(30); // preheated blast lifts T_in well past the melt point

    Assert.True(
      rig.Temp > IwexValues.BfIronMeltingPoint,
      $"hot blast should drive the furnace past {IwexValues.BfIronMeltingPoint} C, was {rig.Temp}"
    );
  }

  [Fact]
  public void Sustained_heat_above_the_melt_point_transitions_to_melting()
  {
    var rig = new BlastFurnaceRig()
      .FeedBlast()
      .SetState(FurnaceState.Firing)
      .SetTemp(1600f)
      .SetSecondsAboveMelting(IwexValues.BfMeltStartDelay - 1f); // about to cross the soak time

    rig.Tick(1);

    Assert.Equal(FurnaceState.Melting, rig.State);
  }

  #endregion

  #region Melting → tapping

  [Fact]
  public void Melting_renders_blast_mix_into_molten_pig_iron()
  {
    var rig = new BlastFurnaceRig()
      .FeedBlast()
      .SetState(FurnaceState.Melting)
      .SetTemp(1600f)
      .SetMeltSeconds(IwexValues.BfMeltIntervalSec - 1f); // a melt cycle completes this tick

    rig.Tick(1);

    Assert.True(
      rig.MoltenIron > 0f,
      "a melt cycle should produce molten pig iron"
    );
  }

  [Fact]
  public void A_melting_furnace_taps_molten_pig_iron_into_the_canal()
  {
    var rig = new BlastFurnaceRig()
      .FeedBlast()
      .WithIronTapAndCanal()
      .SetState(FurnaceState.Melting)
      .SetTemp(1600f)
      .SetMoltenIron(100f);

    rig.Tick(1);

    Assert.True(
      rig.CanalIron > 0,
      "the open tap should pour metal into the canal start"
    );
    Assert.True(
      rig.MoltenIron < 100f,
      "the furnace should give up the tapped metal"
    );
    // The blast furnace makes PIG iron now, not plain iron: the metal that reached the canal is
    // iwex/game:ingot-pigiron, never ingot-iron (plain iron is a Bessemer over-blow product).
    string metal = rig.CanalMetalType!;
    Assert.Contains("pigiron", metal);
    Assert.DoesNotContain("ingot-iron", metal);
  }

  #endregion

  #region Losing the blast

  [Fact]
  public void Cutting_the_blast_lets_the_melt_fall_back_to_firing()
  {
    // Melting just below the melt point with the blast cut: with no air arriving the combustion
    // term falls to natural draught and the furnace's process temperature collapses to ~970 C, so
    // it cools out of Melting and reverts to Firing once it's been cold long enough.
    var rig = new BlastFurnaceRig()
      .SetState(FurnaceState.Melting)
      .SetTemp(IwexValues.BfIronMeltingPoint - 2f)
      .CutBlast();
    ReflectionHelpers.SetField(rig.Furnace, "_belowMeltingSeconds", 29f);

    rig.Tick(1);

    Assert.Equal(FurnaceState.Firing, rig.State);
  }

  #endregion

  #region Cold vs hot blast, emergent

  // The two claims the design docs make about the furnaces, which nothing used to test: a cold blast
  // furnace runs a high-coke burden (docs/design/iwex.md) and a hot blast furnace is what lets a
  // low-coke burden clear the melt line at all (docs/design/smex.md). Neither is a branch in the
  // code - both fall out of the heat balance, which is why they can be asserted on one furnace class
  // by changing only what is charged into it and whether the blast is preheated.

  private static readonly BurdenMix HighCoke = new(65f, 5f, 30f);
  private static readonly BurdenMix LowCoke = new(85f, 5f, 10f);

  [Fact]
  public void A_high_coke_burden_melts_on_cold_blast()
  {
    var rig = new BlastFurnaceRig(burden: HighCoke)
      .FeedBlast(20f) // pressurised, but straight off the blower - no cowper, no preheat
      .SetState(FurnaceState.Firing);

    rig.Tick(1);

    Assert.Equal(0f, rig.Heat.PreheatGain, 1); // cold blast really is cold
    Assert.True(
      rig.Heat.TProcess > IwexValues.BfIronMeltingPoint,
      $"a high-coke burden should melt on cold blast; settled at {rig.Heat.TProcess} C"
    );
  }

  [Fact]
  public void A_low_coke_burden_needs_hot_blast()
  {
    var cold = new BlastFurnaceRig(burden: LowCoke)
      .FeedBlast(20f)
      .SetState(FurnaceState.Firing);
    cold.Tick(1);

    var hot = new BlastFurnaceRig(burden: LowCoke)
      .FeedBlast(950f) // the same furnace, now with a charged cowper on the line
      .SetState(FurnaceState.Firing);
    hot.Tick(1);

    Assert.True(
      cold.Heat.TProcess < IwexValues.BfIronMeltingPoint,
      $"a low-coke burden should stall on cold blast; settled at {cold.Heat.TProcess} C"
    );
    Assert.True(
      hot.Heat.TProcess > IwexValues.BfIronMeltingPoint,
      $"the same burden should melt once the blast is preheated; settled at {hot.Heat.TProcess} C"
    );
  }

  [Fact]
  public void An_under_pressure_blast_line_runs_the_furnace_as_natural_draught()
  {
    // Blast pressure is the tier gate (docs/design/smex.md): under what the burden demands the line
    // stops counting as blast at all - preheated or not - and the furnace falls back to what its own
    // stack can pull. No branch does this; it comes out of the air factor. The rig charges unstamped
    // blast mix, which reads as the standard grade, so the demand is the reference pressure.
    var blown = new BlastFurnaceRig()
      .FeedBlast(950f, pressure: IwexValues.BfBlastPressureAtReference * 2f)
      .SetState(FurnaceState.Firing);
    var starved = new BlastFurnaceRig()
      .FeedBlast(950f, pressure: IwexValues.BfBlastPressureAtReference / 2f)
      .SetState(FurnaceState.Firing);
    blown.Tick(1);
    starved.Tick(1);

    Assert.Equal(1f, blown.Heat.AirFactor, 3);
    Assert.Equal(IwexValues.BfNaturalDraughtFactor, starved.Heat.AirFactor, 3);
    Assert.False(starved.Heat.BlastSupplied);
    Assert.True(
      starved.Heat.TProcess < blown.Heat.TProcess,
      $"an under-pressure line should run cooler; {starved.Heat.TProcess} C vs {blown.Heat.TProcess} C"
    );
  }

  [Fact]
  public void A_brim_full_hearth_runs_cooler_than_a_thin_charge()
  {
    // Cold charge mass is a real heat sink, so topping a furnace up costs temperature. Counter-
    // intuitive enough that the block info has to show it, but it is the historically correct trade.
    var full = new BlastFurnaceRig(blastMix: 640)
      .FeedBlast(20f)
      .SetState(FurnaceState.Firing);
    var thin = new BlastFurnaceRig(blastMix: 160)
      .FeedBlast(20f)
      .SetState(FurnaceState.Firing);
    full.Tick(1);
    thin.Tick(1);

    Assert.True(
      thin.Heat.TProcess > full.Heat.TProcess,
      $"a thin charge should run hotter; thin {thin.Heat.TProcess} C vs full {full.Heat.TProcess} C"
    );
    Assert.True(full.Heat.ChargeLoss > thin.Heat.ChargeLoss);
  }

  [Fact]
  public void Melt_rate_scales_with_how_far_past_the_melt_line_the_furnace_runs()
  {
    // Where the docs' "cold ~30 u/s, hot ~45 u/s" comes from: one yield constant, two temperatures.
    var rig = new BlastFurnaceRig().FeedBlast().SetState(FurnaceState.Melting);

    rig.SetTemp(IwexValues.BfIronMeltingPoint);
    float atLine = rig.MeltSpeed;
    rig.SetTemp(
      IwexValues.BfIronMeltingPoint + IwexValues.BfMeltMarginReference
    );
    float wellPast = rig.MeltSpeed;

    Assert.Equal(1f, atLine, 2); // no margin, nominal rate
    Assert.Equal(1f + IwexValues.BfMeltMarginGain, wellPast, 2);
  }

  #endregion

  #region Air consumption + starvation

  // The furnace burns air, it does not merely sense it: a lit furnace draws blast out of the tuyere
  // network (decrementing it), an idle one leaves the main alone, "not enough air" is an air-limited
  // combustion that cools T_in, and a fire held below the blast floor starves and - sustained past the
  // extinguish grace - dies through the same disruption machinery as any other stall.

  [Fact]
  public void An_idle_furnace_draws_no_air_but_a_firing_one_does()
  {
    // Consumption is gated on being lit. A thin-charged (so it will not auto-ignite), idle furnace
    // primed with a full blast main sits on it and draws nothing - it must not bleed a shared main dry
    // while cold.
    var idle = new BlastFurnaceRig(blastMix: 100).PrimeBlast();
    float primed = idle.TuyereVolume;
    Assert.True(primed > 0f, "the blast main should be primed with air");

    idle.Tick(5); // five idle ticks (PrimeBlast does not arm the per-tick re-feed)

    Assert.Equal(FurnaceState.Idle, idle.State); // thin charge -> never lights
    Assert.Equal(primed, idle.TuyereVolume, 1); // ...and drew none of the air

    // The same furnace, lit, pulls air out of the tuyeres.
    var firing = new BlastFurnaceRig(blastMix: 100)
      .SetState(FurnaceState.Firing)
      .PrimeBlast();
    float before = firing.TuyereVolume;

    firing.Tick(1);

    Assert.True(
      firing.TuyereVolume < before,
      $"a firing furnace should draw air from the tuyeres; {firing.TuyereVolume} vs {before}"
    );
  }

  [Fact]
  public void Air_limited_combustion_lowers_the_heat_input()
  {
    // "Not enough air" is an air-limited burn: T_in = coke x air flow, so an under-pressure line (little
    // air arriving) makes less heat than a full-pressure one, everything else equal. Both blasts are
    // cold (20 C) so no preheat muddies the comparison - the whole difference is the air factor.
    var blown = new BlastFurnaceRig()
      .FeedBlast(20f, pressure: IwexValues.BfBlastPressureAtReference * 2f)
      .SetState(FurnaceState.Firing);
    var starved = new BlastFurnaceRig()
      .FeedBlast(20f, pressure: IwexValues.BfBlastPressureAtReference / 2f)
      .SetState(FurnaceState.Firing);

    blown.Tick(1);
    starved.Tick(1);

    Assert.True(
      starved.Heat.AirFactor < blown.Heat.AirFactor,
      $"less air arriving should lower the air factor; {starved.Heat.AirFactor} vs {blown.Heat.AirFactor}"
    );
    Assert.True(
      starved.Heat.TIn < blown.Heat.TIn,
      $"air-limited combustion should lower T_in; starved {starved.Heat.TIn} vs blown {blown.Heat.TIn}"
    );
  }

  [Fact]
  public void A_furnace_on_good_blast_reaches_melting_and_never_air_starves()
  {
    // The "with blast, melts" control for the starvation test below: on sustained full blast the furnace
    // crosses into Melting and stays there well past the window a starved furnace would die in.
    var rig = new BlastFurnaceRig()
      .FeedBlast()
      .SetState(FurnaceState.Firing)
      .SetTemp(1600f)
      .SetSecondsAboveMelting(IwexValues.BfMeltStartDelay - 1f);

    rig.Tick(1); // crosses the soak line
    Assert.Equal(FurnaceState.Melting, rig.State);
    Assert.False(rig.AirStarved);

    int window = (int)
      ReflectionHelpers.GetProperty(rig.Furnace, "ExtinguishThresholdDefault")!;
    rig.Tick(window + 5); // sustained blast keeps it melting, never starving

    Assert.Equal(FurnaceState.Melting, rig.State);
    Assert.False(rig.AirStarved);
  }

  [Fact]
  public void Air_starvation_extinguishes_the_furnace_after_the_grace_window()
  {
    // A lit furnace whose blast never reaches pressure (a dead or too-weak blower): the air factor floors
    // it at natural draught, and the starvation disruption counts up on the shared _extinguishSeconds
    // timer. One tick of this is the existing under-pressure test (which only checks it runs cooler);
    // here the grace elapses and it goes out through the same extinguish path as any other stall.
    var rig = new BlastFurnaceRig()
      .FeedBlast(pressure: IwexValues.BfBlastPressureAtReference / 2f)
      .SetState(FurnaceState.Firing);
    int window = (int)
      ReflectionHelpers.GetProperty(rig.Furnace, "ExtinguishThresholdDefault")!;

    // Just short of the grace it is starved but hanging on...
    rig.Tick(window - 1);
    Assert.Equal(FurnaceState.Firing, rig.State);
    Assert.True(
      rig.AirStarved,
      "a sub-blast furnace should read as air-starved"
    );

    // ...one more starved tick tips it over the extinguish threshold.
    rig.Tick(1);
    Assert.Equal(FurnaceState.Idle, rig.State);
  }

  #endregion

  #region Burden family gate

  // The cupola's remelt burden charged into a blast furnace: the shaft still lights and burns (it is
  // real fuel), but the family gate refuses to render it into molten iron. Ore burden in the same rig
  // converts normally (Melting_renders_blast_mix_into_molten_iron above), so this is the wrong-family
  // half of "right family melts, wrong family burns but never converts".

  [Fact]
  public void A_blast_furnace_will_not_convert_a_remelt_burden_charge()
  {
    var rig = new BlastFurnaceRig(
      burden: new BurdenMix(60f, 5f, 35f),
      chargeCode: "remeltburden"
    )
      .FeedBlast()
      .SetState(FurnaceState.Melting)
      .SetTemp(1600f)
      .SetMeltSeconds(IwexValues.BfMeltIntervalSec - 1f); // a melt cycle WOULD complete this tick

    rig.Tick(1);

    Assert.Equal(0f, rig.MoltenIron, 3); // hot and "ready", but the wrong family never converts
  }

  [Fact]
  public void A_wrong_family_shaft_still_reads_full_so_it_lights_and_burns()
  {
    // Family-blind fullness: the furnace does not silently refuse to light a shaft packed with the
    // wrong burden - it lights and burns it out. Only the conversion is gated.
    var rig = new BlastFurnaceRig(
      burden: new BurdenMix(60f, 5f, 35f),
      chargeCode: "remeltburden"
    )
      .FeedBlast()
      .SetState(FurnaceState.Firing)
      .SetTemp(1600f)
      .SetSecondsAboveMelting(IwexValues.BfMeltStartDelay - 1f); // would cross into Melting this tick

    rig.Tick(1);

    // The soak completes but the transition is blocked: it stays Firing (burning), never Melting.
    Assert.Equal(FurnaceState.Firing, rig.State);
  }

  #endregion

  #region Live config

  // Regression (player-reported): /exmod config smex bfmeltstartdelay 30 used to take effect only
  // after a relog, because the furnace cached its tunables once at load. The production tick now
  // re-reads them, so an admin change applies on the next tick without a reload.
  [Fact]
  public void A_live_config_change_to_the_melt_delay_applies_without_a_reload()
  {
    float original = IwexValues.BfMeltStartDelay;
    try
    {
      var rig = new BlastFurnaceRig()
        .FeedBlast()
        .SetState(FurnaceState.Firing);

      // Admin shortens the soak time mid-session.
      IwexValues.Edit(c => c.BfMeltStartDelay = 30f);
      rig.Tick(1);

      Assert.Equal(
        30f,
        (float)ReflectionHelpers.GetField(rig.Furnace, "_meltStartDelay")!,
        3
      );
    }
    finally
    {
      IwexValues.Edit(c => c.BfMeltStartDelay = original);
    }
  }

  #endregion
}
