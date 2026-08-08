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
/// <para>
/// <b>Every case here used to arrange the machine by assignment</b> - <c>SetState(Melting)</c>, a soak
/// counter, a melt-cycle counter - and none of them can any more. A shaft furnace recomputes what it is
/// doing from its charge every tick, so writing the label states a premise the next tick discards: the case
/// stays green while testing a furnace that was never in the state its own name claims.
/// <c>State</c> therefore has no setter at all (<c>FurnaceBranchGuards.NoFurnaceExposesASettableState</c>),
/// and arrangement here is <b>charging and blowing</b> - <see cref="BlastFurnaceRig.HeatSoak"/> runs the
/// real machine until it is melting and fails loudly if it never gets there.
/// </para>
/// </summary>
public class BlastFurnaceScenarioTests
{
  /// <summary>Long enough for a charged, blown furnace to warm its column through and reach the melt line,
  /// with slack. Waited for, never counted out: what takes the time is the charge warming through, and
  /// there is no closed form for it.</summary>
  private const int SoakSeconds = 900;

  #region The whole process, emergent

  // These run the machine the way the server does: the real structure standing, the furnace's own tick
  // listener on the clock, and nothing invoked by reflection. They are what catches the process failing to
  // *start* - a furnace whose structure never completes, whose tick is never registered, or which is never
  // gated on.

  [Fact]
  public void A_built_and_blown_furnace_reaches_melting_on_its_own()
  {
    var rig = new BlastFurnaceRig(blastMix: 0).FeedBlast(950f);

    // It starts cold, idle, and untouched.
    Assert.True(rig.Furnace.StructureComplete, "the furnace should have completed its own structure");
    Assert.Equal(FurnaceState.Idle, rig.State);

    Assert.True(
      rig.RunUntil(r => r.State == FurnaceState.Melting, SoakSeconds) > 0,
      "a charged, blown furnace should reach Melting on its own"
    );
    Assert.True(
      rig.Temp > IwexValues.BfIronMeltingPoint,
      $"a blown furnace should settle above the melt line, was {rig.Temp} C"
    );
  }

  [Fact]
  public void The_headline_process_ends_with_pig_iron_in_the_canal()
  {
    // charge -> hot blast -> molten pig iron -> tapped into a canal, driven only by the clock.
    var rig = new BlastFurnaceRig(blastMix: 0)
      .WithIronTapAndCanal()
      .FeedBlast(950f);

    Assert.True(
      rig.RunUntil(r => r.CanalIron > 0, SoakSeconds) > 0,
      "the open tap should have poured metal into the canal"
    );
    Assert.Contains("pigiron", rig.CanalMetalType!);
  }

  #endregion

  #region Heat + phase progression

  [Fact]
  public void Hot_blast_drives_the_furnace_past_irons_melting_point()
  {
    // No SetTemp any more, and none is needed: the shaft's temperature is the raceway flame, assigned
    // straight from the heat balance with no chase, so a preheated furnace is at its own T_process within a
    // tick of lighting. What used to be arranged (a furnace sat at the cold-blast ceiling) is now simply
    // what the cold rig below reads.
    var hot = new BlastFurnaceRig().FeedBlast(950f).RunLive(2);

    Assert.True(
      hot.Temp > IwexValues.BfIronMeltingPoint,
      $"hot blast should drive the furnace past {IwexValues.BfIronMeltingPoint} C, was {hot.Temp}"
    );

    // The control: the same furnace on ambient blast holds below the line, so the difference is the
    // preheat and nothing else.
    var cold = new BlastFurnaceRig().FeedBlast(20f).RunLive(2);
    Assert.Equal(0f, cold.Heat.PreheatGain, 1);
    Assert.True(
      cold.Temp < hot.Temp,
      $"an unpreheated furnace should run cooler; cold {cold.Temp} C vs hot {hot.Temp} C"
    );
  }

  /// <summary>
  /// <b>The flame being over the line is not enough, and this is the case that says so.</b>
  /// <para>
  /// The stored machine soaked for <c>FireboxMeltStartDelay</c> and then declared Melting on the furnace's own
  /// temperature alone. A shaft is not one hot space: the raceway reaches flame temperature within a tick
  /// of lighting while the burden above is still climbing, so "the furnace is hot enough" and "anything in
  /// it can melt" are different questions separated by minutes of counter-current warm-through. Reading
  /// only the machine put a furnace into <c>Melting</c> - HUD, sounds and all - while it rendered nothing.
  /// </para>
  /// </summary>
  [Fact]
  public void A_furnace_whose_flame_is_over_the_line_is_not_melting_until_its_CHARGE_is()
  {
    var rig = new BlastFurnaceRig(blastMix: 0).FeedBlast(950f).RunLive(2);

    // One tick in: the flame is already past iron's melt line...
    Assert.True(
      rig.Temp > IwexValues.BfIronMeltingPoint,
      $"the raceway should be over the line at once, was {rig.Temp} C"
    );
    // ...and the furnace is still only Firing, because the charge has not arrived there yet.
    Assert.Equal(FurnaceState.Firing, rig.State);
    Assert.Equal(0f, rig.MoltenIron, 3);

    // Given the time the column needs to warm through, it crosses - so this is a delay, not a refusal.
    rig.HeatSoak(SoakSeconds);
    Assert.Equal(FurnaceState.Melting, rig.State);
  }

  #endregion

  #region Melting → tapping

  [Fact]
  public void Melting_renders_blast_mix_into_molten_pig_iron()
  {
    var rig = new BlastFurnaceRig(blastMix: 0).FeedBlast();

    // Waited on the product, not on the label. Entering Melting and having melted something are separate
    // moments now: the state turns the instant a hot enough band reaches a raceway, and the render happens
    // on the carbon burnt after that.
    Assert.True(
      rig.RunUntil(r => r.MoltenIron > 0f, SoakSeconds) > 0,
      "a melting furnace should render molten pig iron"
    );
    Assert.Equal(FurnaceState.Melting, rig.State);
  }

  [Fact]
  public void A_melting_furnace_taps_molten_pig_iron_into_the_canal()
  {
    var rig = new BlastFurnaceRig(blastMix: 0)
      .FeedBlast()
      .WithIronTapAndCanal();

    Assert.True(
      rig.RunUntil(r => r.MoltenIron > 0f, SoakSeconds) > 0,
      "there should be a pool to tap"
    );
    rig.RunLive(1);

    Assert.True(
      rig.CanalIron > 0,
      "the open tap should pour metal into the canal start"
    );
    // The blast furnace makes pig iron now, not plain iron: the metal that reached the canal is
    // iwex/game:ingot-pigiron, never ingot-iron (plain iron is a Bessemer over-blow product).
    string metal = rig.CanalMetalType!;
    Assert.Contains("pigiron", metal);
    Assert.DoesNotContain("ingot-iron", metal);
  }

  #endregion

  #region Losing the blast

  /// <summary>
  /// <b>A stopped blower throttles the fire; it does not snuff it.</b> With no air arriving the
  /// combustion term falls to natural draught and the process temperature collapses to ~970 °C, so the
  /// furnace drops out of Melting - and goes on burning its carbon at half rate, which is a worse outcome
  /// for the player than going out and the correct one.
  /// <c>docs/design/layered-charge.md</c> § <i>What sets the rate: the blast</i>.
  /// </summary>
  [Fact]
  public void Cutting_the_blast_lets_the_melt_fall_back_to_firing()
  {
    var rig = new BlastFurnaceRig(blastMix: 0).FeedBlast().HeatSoak(SoakSeconds);

    rig.CutBlast();
    Assert.True(
      rig.RunUntil(r => r.State != FurnaceState.Melting, 120) > 0,
      "an unblown furnace should fall out of Melting"
    );
    Assert.Equal(FurnaceState.Firing, rig.State); // fell back, did not go out

    // The carbon is banked here, not before the cut: the burn is whole units carried over ticks
    // (`_burnCarry`), so on natural draught a column spends one unit every ~50 s and a couple of ticks
    // buys nothing to measure. What is being asserted is that it is still burning at all.
    int coke = rig.CokeUnits;
    rig.RunLive(200);

    Assert.NotEqual(FurnaceState.Idle, rig.State);
    Assert.True(
      rig.CokeUnits < coke,
      $"and it is still burning its carbon; {rig.CokeUnits} vs {coke}"
    );
  }

  #endregion

  #region Cold vs hot blast, emergent

  // The two claims the design docs make about the furnaces, which nothing used to test: a cold blast
  // furnace runs a high-coke burden (docs/design/iwex.md) and a hot blast furnace is what lets a
  // low-coke burden clear the melt line at all (docs/design/smex.md). Neither is a branch in the
  // code - both fall out of the heat balance, which is why they can be asserted on one furnace class
  // by changing only what is charged into it and whether the blast is preheated.
  //
  // Each rig is run two seconds rather than being told it is Firing: a charged, blown shaft catches on
  // its first tick, and the heat balance is only computed by a furnace that is actually alight.

  private static readonly BurdenMix HighCoke = new(65f, 5f, 30f);
  private static readonly BurdenMix LowCoke = new(85f, 5f, 10f);

  [Fact]
  public void A_high_coke_burden_melts_on_cold_blast()
  {
    var rig = new BlastFurnaceRig(burden: HighCoke)
      .FeedBlast(20f) // pressurised, but straight off the blower - no cowper, no preheat
      .RunLive(2);

    Assert.Equal(0f, rig.Heat.PreheatGain, 1); // cold blast really is cold
    Assert.True(
      rig.Heat.TProcess > IwexValues.BfIronMeltingPoint,
      $"a high-coke burden should melt on cold blast; settled at {rig.Heat.TProcess} C"
    );
  }

  [Fact]
  public void A_low_coke_burden_needs_hot_blast()
  {
    var cold = new BlastFurnaceRig(burden: LowCoke).FeedBlast(20f).RunLive(2);
    // the same furnace, now with a charged cowper on the line
    var hot = new BlastFurnaceRig(burden: LowCoke).FeedBlast(950f).RunLive(2);

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
      .RunLive(2);
    var starved = new BlastFurnaceRig()
      .FeedBlast(950f, pressure: IwexValues.BfBlastPressureAtReference / 2f)
      .RunLive(2);

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
    var full = new BlastFurnaceRig(blastMix: 0).FeedBlast(20f).RunLive(2);
    var thin = new BlastFurnaceRig(blastMix: 160).FeedBlast(20f).RunLive(2);

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
    // It needs no state at all - MeltSpeedFactor is a pure function of the furnace's own temperature -
    // which is why the SetState(Melting) that used to arrange it was decoration even before the label
    // became a read.
    var rig = new BlastFurnaceRig();

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
  // network (decrementing it), an idle one leaves the main alone, and "not enough air" is an air-limited
  // combustion that cools T_in and slows the burn.

  [Fact]
  public void An_idle_furnace_draws_no_air_but_a_firing_one_does()
  {
    // Consumption is gated on being lit. An idle furnace primed with a full blast main sits on it and
    // draws nothing - it must not bleed a shared main dry while cold.
    //
    // It is kept idle by having no carbon in it, not by being thinly charged. The quantity threshold
    // stopped gating ignition when the state became derived: a shaft holding six units of coke per column
    // lights exactly as a full one does, so `blastMix: 100` - which is what this used to rely on - now
    // catches on its first tick and the case would have been asserting the opposite of its own name.
    var idle = new BlastFurnaceRig(blastMix: -1).ChargeWithoutCoke().PrimeBlast();
    float primed = idle.TuyereVolume;
    Assert.True(primed > 0f, "the blast main should be primed with air");

    idle.Tick(5); // five idle ticks (PrimeBlast does not arm the per-tick re-feed)

    Assert.Equal(FurnaceState.Idle, idle.State); // no carbon -> never lights
    Assert.Equal(primed, idle.TuyereVolume, 1); // ...and drew none of the air

    // The same furnace, charged so it lights, pulls air out of the tuyeres.
    var firing = new BlastFurnaceRig().FeedBlast().RunLive(2);
    Assert.NotEqual(FurnaceState.Idle, firing.State);
    firing.PrimeBlast();
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
      .RunLive(2);
    var starved = new BlastFurnaceRig()
      .FeedBlast(20f, pressure: IwexValues.BfBlastPressureAtReference / 2f)
      .RunLive(2);

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
    // The "with blast, melts" control for the starvation case below: on sustained full blast the furnace
    // crosses into Melting and stays there.
    var rig = new BlastFurnaceRig(blastMix: 0).FeedBlast().HeatSoak(SoakSeconds);
    Assert.False(rig.AirStarved);

    bool everStarved = false;
    bool everLeftMelting = false;
    rig.RunUntil(
      _ => false,
      120,
      r =>
      {
        everStarved |= r.AirStarved;
        everLeftMelting |= r.State != FurnaceState.Melting;
      }
    );

    Assert.False(everLeftMelting, "sustained blast should keep it melting");
    Assert.False(everStarved, "and it should never read as starved");
  }

  /// <summary>
  /// <b>This case once asserted the opposite, and the inversion is deliberate.</b> It was
  /// <c>Air_starvation_extinguishes_the_furnace_after_the_grace_window</c>: a sub-pressure blast counted as
  /// a disruption, ran up <c>_extinguishSeconds</c>, and snuffed the fire.
  /// <para>
  /// There is no extinguish countdown on this branch, and there should not be. Air is the reagent, so what
  /// a failed blower costs is the <b>rate</b>: the furnace slides to natural draught, burns cooler and
  /// slower, and renders nothing - it does not die. What genuinely ends a campaign is running out of
  /// carbon, and a starved furnace takes twice as long to get there.
  /// </para>
  /// <para>
  /// The <b>choke</b> is the failure mode that still kills instantly, and it is the physical opposite:
  /// a sealed furnace with nowhere to vent. Do not merge the two back together.
  /// </para>
  /// </summary>
  [Fact]
  public void A_starved_blast_line_throttles_the_furnace_rather_than_extinguishing_it()
  {
    var rig = new BlastFurnaceRig(blastMix: 0)
      .FeedBlast(pressure: IwexValues.BfBlastPressureAtReference / 2f)
      .RunLive(2);

    Assert.Equal(FurnaceState.Firing, rig.State);
    Assert.True(rig.AirStarved, "a sub-blast furnace should read as air-starved");

    int coke = rig.CokeUnits;
    bool everWentOut = false;
    rig.RunUntil(_ => false, 300, r => everWentOut |= r.State == FurnaceState.Idle);

    Assert.False(
      everWentOut,
      "a starved furnace slides to natural draught - it does not go out"
    );
    Assert.True(rig.AirStarved);
    Assert.True(
      rig.CokeUnits < coke,
      $"and it is still burning, only slower; {rig.CokeUnits} vs {coke}"
    );
    Assert.True(
      rig.Temp < IwexValues.BfIronMeltingPoint,
      $"natural draught should sit below the melt line; was {rig.Temp} C"
    );
  }

  #endregion

  #region Burden family gate

  // The cupola's remelt burden charged into a blast furnace: the shaft still lights and burns (it is
  // real fuel), but the family gate refuses to render it into molten iron. Ore burden in the same rig
  // converts normally (Melting_renders_blast_mix_into_molten_pig_iron above), so this is the wrong-family
  // half of "right family melts, wrong family burns but never converts".

  private static readonly BurdenMix Remelt = new(60f, 5f, 35f);

  [Fact]
  public void A_blast_furnace_will_not_convert_a_remelt_burden_charge()
  {
    var rig = new BlastFurnaceRig(
      blastMix: 0,
      burden: Remelt,
      chargeCode: "remeltburden"
    ).FeedBlast(950f);

    // Run it well past the point the same scene with ore burden is melting and producing.
    rig.RunLive(SoakSeconds / 2);

    Assert.Equal(0f, rig.MoltenIron, 3); // hot and "ready", but the wrong family never converts
  }

  /// <summary>
  /// Family-blind fullness: the furnace does not silently refuse to light a shaft packed with the wrong
  /// burden - it lights and burns it out. Only the conversion is gated.
  /// <para>
  /// And the <b>label stays honest with it</b>: a furnace that will render nothing is not melting, it is
  /// burning, so it reads <c>Firing</c> however hot it gets. That used to fall out of the soak transition
  /// refusing to fire; on the derived branch <c>DeriveState</c> carries the same clause deliberately,
  /// because otherwise the HUD would show "Melting" over a furnace producing nothing until the player dug
  /// the offending pile out.
  /// </para>
  /// </summary>
  [Fact]
  public void A_wrong_family_shaft_still_reads_full_so_it_lights_and_burns()
  {
    var rig = new BlastFurnaceRig(
      blastMix: 0,
      burden: Remelt,
      chargeCode: "remeltburden"
    ).FeedBlast(950f);

    int coke = rig.CokeUnits;
    bool everMelted = false;
    rig.RunUntil(_ => false, SoakSeconds / 2, r => everMelted |= r.State == FurnaceState.Melting);

    Assert.Equal(FurnaceState.Firing, rig.State); // lit, burning, never melting
    Assert.False(everMelted, "a wrong-family shaft must never read as Melting");
    Assert.True(
      rig.CokeUnits < coke,
      $"but it really is burning the charge out; {rig.CokeUnits} vs {coke}"
    );
  }

  #endregion

  #region Live config

  /// <summary>
  /// Regression (player-reported): an <c>/exmod config</c> change used to take effect only after a relog,
  /// because the furnace cached its tunables once at load. The production tick re-reads them, so an admin
  /// change applies on the next tick without a reload.
  /// <para>
  /// It used to watch <c>FireboxMeltStartDelay</c>, which the shaft branch seals to <b>0</b> - the
  /// counter-current warm-through is itself the soak, so there is no delay left to shorten. The melting point is
  /// the same fact about the same cache and is one the shaft genuinely reads every tick.
  /// </para>
  /// </summary>
  [Fact]
  public void A_live_config_change_applies_without_a_reload()
  {
    float original = IwexValues.BfIronMeltingPoint;
    try
    {
      var rig = new BlastFurnaceRig().FeedBlast().RunLive(2);

      // Admin retunes the melt line mid-session.
      IwexValues.Edit(c => c.BfIronMeltingPoint = 1234f);
      rig.Tick(1);

      Assert.Equal(
        1234f,
        (float)ReflectionHelpers.GetField(rig.Furnace, "_ironMeltingPoint")!,
        3
      );
    }
    finally
    {
      IwexValues.Edit(c => c.BfIronMeltingPoint = original);
    }
  }

  #endregion
}
