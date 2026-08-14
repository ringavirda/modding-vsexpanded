using ExpandedLib.Testing;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.Items;
using SteelIndustryExpanded;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The blast furnace's primary process end to end (handbook blast-furnace + hot-blast): a charged, lit
/// hearth on hot blast climbs past iron's melting point, enters Melting, renders blast mix into molten
/// pig iron and taps it into a canal, and falls back out of Melting when the blast is cut. Drives the
/// gated firing/melting tick with its real peripherals via <see cref="BlastFurnaceRig"/>.
/// <para>
/// A shaft furnace recomputes its state from the charge every tick, so <c>State</c> has no setter
/// (<c>FurnaceBranchGuards.NoFurnaceExposesASettableState</c>) and cases arrange by charging and
/// blowing. <see cref="BlastFurnaceRig.HeatSoak"/> runs the real machine until it melts and fails if
/// it never does.
/// </para>
/// </summary>
public class BlastFurnaceScenarioTests {
  /// <summary>Seconds allowed for a charged, blown furnace to warm its column through and reach the
  /// melt line, with slack. Cases wait on a condition rather than counting out the interval: the
  /// warm-through time has no closed form.</summary>
  private const int SoakSeconds = 900;

  #region The whole process, emergent

  // These run the machine the way the server does: the real structure standing, the furnace's own tick
  // listener on the clock, nothing invoked by reflection. They catch a process that never starts - a
  // structure that never completes, a tick that is never registered, a gate that never opens.

  [Fact]
  public void A_built_and_blown_furnace_reaches_melting_on_its_own() {
    var rig = new BlastFurnaceRig(blastMix: 0).FeedBlast(950f);

    // Starts cold and idle.
    Assert.True(
      rig.Furnace.StructureComplete,
      "the furnace should have completed its own structure"
    );
    Assert.Equal(FurnaceState.Idle, rig.State);

    Assert.True(
      rig.RunUntil(r => r.State == FurnaceState.Melting, SoakSeconds) > 0,
      "a charged, blown furnace should reach Melting on its own"
    );
    Assert.True(
      rig.Temp > IiexValues.BfIronMeltingPoint,
      $"a blown furnace should settle above the melt line, was {rig.Temp} C"
    );
  }

  [Fact]
  public void The_headline_process_ends_with_pig_iron_in_the_canal() {
    // Charge -> hot blast -> molten pig iron -> tapped into a canal, driven only by the clock.
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
  public void Hot_blast_drives_the_furnace_past_irons_melting_point() {
    // The shaft's temperature is the raceway flame, assigned straight from the heat balance with no
    // chase, so a preheated furnace sits at its own T_process within a tick of lighting.
    var hot = new BlastFurnaceRig().FeedBlast(950f).RunLive(2);

    Assert.True(
      hot.Temp > IiexValues.BfIronMeltingPoint,
      $"hot blast should drive the furnace past {IiexValues.BfIronMeltingPoint} C, was {hot.Temp}"
    );

    // Control: the same furnace on ambient blast holds below the line, so the difference is the preheat.
    var cold = new BlastFurnaceRig().FeedBlast(20f).RunLive(2);
    Assert.Equal(0f, cold.Heat.PreheatGain, 1);
    Assert.True(
      cold.Temp < hot.Temp,
      $"an unpreheated furnace should run cooler; cold {cold.Temp} C vs hot {hot.Temp} C"
    );
  }

  /// <summary>
  /// Flame temperature over the melt line is not sufficient for Melting. A shaft is not one hot space:
  /// the raceway reaches flame temperature within a tick of lighting while the burden above is still
  /// climbing, so the state also waits on the charge, minutes of counter-current warm-through later.
  /// </summary>
  [Fact]
  public void A_furnace_whose_flame_is_over_the_line_is_not_melting_until_its_CHARGE_is() {
    var rig = new BlastFurnaceRig(blastMix: 0).FeedBlast(950f).RunLive(2);

    // One tick in, the flame is already past iron's melt line.
    Assert.True(
      rig.Temp > IiexValues.BfIronMeltingPoint,
      $"the raceway should be over the line at once, was {rig.Temp} C"
    );
    // The furnace is still Firing: the charge has not reached the melt line.
    Assert.Equal(FurnaceState.Firing, rig.State);
    Assert.Equal(0f, rig.MoltenIron, 3);

    // Given the time the column needs to warm through it crosses, so this is a delay, not a refusal.
    rig.HeatSoak(SoakSeconds);
    Assert.Equal(FurnaceState.Melting, rig.State);
  }

  #endregion

  #region Melting → tapping

  [Fact]
  public void Melting_renders_blast_mix_into_molten_pig_iron() {
    var rig = new BlastFurnaceRig(blastMix: 0).FeedBlast();

    // Waits on the product, not the state. The two are separate moments: the state turns as soon as a
    // hot enough band reaches a raceway, while the render happens on the carbon burnt after that.
    Assert.True(
      rig.RunUntil(r => r.MoltenIron > 0f, SoakSeconds) > 0,
      "a melting furnace should render molten pig iron"
    );
    Assert.Equal(FurnaceState.Melting, rig.State);
  }

  [Fact]
  public void A_melting_furnace_taps_molten_pig_iron_into_the_canal() {
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
    // The blast furnace makes pig iron, not plain iron: the metal reaching the canal is
    // iiex/game:ingot-pigiron, never ingot-iron (plain iron is a Bessemer over-blow product).
    string metal = rig.CanalMetalType!;
    Assert.Contains("pigiron", metal);
    Assert.DoesNotContain("ingot-iron", metal);
  }

  #endregion

  #region Losing the blast

  /// <summary>
  /// A stopped blower throttles the fire without snuffing it. With no air arriving the combustion term
  /// falls to natural draught and the process temperature collapses to about 970 C, so the furnace drops
  /// out of Melting and goes on burning its carbon at half rate.
  /// See docs/design/layered-charge.md.
  /// </summary>
  [Fact]
  public void Cutting_the_blast_lets_the_melt_fall_back_to_firing() {
    var rig = new BlastFurnaceRig(blastMix: 0)
      .FeedBlast()
      .HeatSoak(SoakSeconds);

    rig.CutBlast();
    Assert.True(
      rig.RunUntil(r => r.State != FurnaceState.Melting, 120) > 0,
      "an unblown furnace should fall out of Melting"
    );
    Assert.Equal(FurnaceState.Firing, rig.State); // fell back, did not go out

    // Coke is sampled after the cut, not before: the burn is whole units carried over ticks
    // (`_burnCarry`), so on natural draught a column spends one unit every ~50 s and a couple of ticks
    // leaves nothing to measure. The assertion is only that it is still burning.
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

  // A cold blast furnace runs a high-coke burden (docs/design/machines/blast-furnace-cold.md); hot blast
  // is what lets a low-coke burden clear the melt line (docs/design/machines/blast-furnace-hot.md).
  // Neither is a branch in the code - both fall out of the heat balance, so one furnace class covers
  // both by changing only what is charged and whether the blast is preheated.
  //
  // Each rig runs two seconds first because the heat balance is only computed by a lit furnace, and a
  // charged, blown shaft catches on its first tick.

  private static readonly BurdenMix HighCoke = new(65f, 5f, 30f);
  private static readonly BurdenMix LowCoke = new(85f, 5f, 10f);

  [Fact]
  public void A_high_coke_burden_melts_on_cold_blast() {
    var rig = new BlastFurnaceRig(burden: HighCoke)
      .FeedBlast(20f) // pressurised, but straight off the blower - no cowper, no preheat
      .RunLive(2);

    Assert.Equal(0f, rig.Heat.PreheatGain, 1); // no preheat on a cold blast
    Assert.True(
      rig.Heat.TProcess > IiexValues.BfIronMeltingPoint,
      $"a high-coke burden should melt on cold blast; settled at {rig.Heat.TProcess} C"
    );
  }

  [Fact]
  public void A_low_coke_burden_needs_hot_blast() {
    var cold = new BlastFurnaceRig(burden: LowCoke).FeedBlast(20f).RunLive(2);
    // The same furnace with a charged cowper on the line.
    var hot = new BlastFurnaceRig(burden: LowCoke).FeedBlast(950f).RunLive(2);

    Assert.True(
      cold.Heat.TProcess < IiexValues.BfIronMeltingPoint,
      $"a low-coke burden should stall on cold blast; settled at {cold.Heat.TProcess} C"
    );
    Assert.True(
      hot.Heat.TProcess > IiexValues.BfIronMeltingPoint,
      $"the same burden should melt once the blast is preheated; settled at {hot.Heat.TProcess} C"
    );
  }

  [Fact]
  public void An_under_pressure_blast_line_runs_the_furnace_as_natural_draught() {
    // Blast pressure is the tier gate: below what the burden demands the line stops counting as blast,
    // preheated or not, and the furnace falls back to what its own stack can pull. This comes out of
    // the air factor rather than a branch (docs/design/mechanics/heat-balance.md). The rig charges
    // unstamped blast mix, which reads as the standard grade, so the demand is the reference pressure.
    var blown = new BlastFurnaceRig()
      .FeedBlast(950f, pressure: IiexValues.BfBlastPressureAtReference * 2f)
      .RunLive(2);
    var starved = new BlastFurnaceRig()
      .FeedBlast(950f, pressure: IiexValues.BfBlastPressureAtReference / 2f)
      .RunLive(2);

    Assert.Equal(1f, blown.Heat.AirFactor, 3);
    Assert.Equal(IiexValues.BfNaturalDraughtFactor, starved.Heat.AirFactor, 3);
    Assert.False(starved.Heat.BlastSupplied);
    Assert.True(
      starved.Heat.TProcess < blown.Heat.TProcess,
      $"an under-pressure line should run cooler; {starved.Heat.TProcess} C vs {blown.Heat.TProcess} C"
    );
  }

  [Fact]
  public void A_brim_full_hearth_runs_cooler_than_a_thin_charge() {
    // Cold charge mass is a heat sink, so topping a furnace up costs temperature. The block info
    // reports the loss because the trade is counter-intuitive.
    var full = new BlastFurnaceRig(blastMix: 0).FeedBlast(20f).RunLive(2);
    var thin = new BlastFurnaceRig(blastMix: 160).FeedBlast(20f).RunLive(2);

    Assert.True(
      thin.Heat.TProcess > full.Heat.TProcess,
      $"a thin charge should run hotter; thin {thin.Heat.TProcess} C vs full {full.Heat.TProcess} C"
    );
    Assert.True(full.Heat.ChargeLoss > thin.Heat.ChargeLoss);
  }

  [Fact]
  public void Melt_rate_scales_with_how_far_past_the_melt_line_the_furnace_runs() {
    // MeltSpeedFactor is a pure function of the furnace's own temperature and reads no state: one
    // yield constant and two temperatures give the documented cold ~30 u/s and hot ~45 u/s.
    var rig = new BlastFurnaceRig();

    rig.SetTemp(IiexValues.BfIronMeltingPoint);
    float atLine = rig.MeltSpeed;
    rig.SetTemp(
      IiexValues.BfIronMeltingPoint + IiexValues.BfMeltMarginReference
    );
    float wellPast = rig.MeltSpeed;

    Assert.Equal(1f, atLine, 2); // no margin, nominal rate
    Assert.Equal(1f + IiexValues.BfMeltMarginGain, wellPast, 2);
  }

  #endregion

  #region Air consumption + starvation

  // A lit furnace draws blast out of the tuyere network and decrements it; an idle one leaves the main
  // alone. Insufficient air is an air-limited combustion that lowers T_in and slows the burn.

  [Fact]
  public void An_idle_furnace_draws_no_air_but_a_firing_one_does() {
    // Consumption is gated on being lit: an idle furnace primed with a full blast main draws nothing,
    // so it cannot bleed a shared main dry while cold.
    //
    // The furnace is kept idle by having no carbon in it, not by being thinly charged. Quantity does not
    // gate ignition on the derived branch - a shaft holding six units of coke per column lights exactly
    // as a full one does.
    var idle = new BlastFurnaceRig(blastMix: -1)
      .ChargeWithoutCoke()
      .PrimeBlast();
    float primed = idle.TuyereVolume;
    Assert.True(primed > 0f, "the blast main should be primed with air");

    idle.Tick(5); // five idle ticks (PrimeBlast does not arm the per-tick re-feed)

    Assert.Equal(FurnaceState.Idle, idle.State); // no carbon -> never lights
    Assert.Equal(primed, idle.TuyereVolume, 1); // and drew none of the air

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
  public void Air_limited_combustion_lowers_the_heat_input() {
    // T_in = coke x air flow, so an under-pressure line makes less heat than a full-pressure one,
    // everything else equal. Both blasts are cold (20 C), so the only difference is the air factor.
    var blown = new BlastFurnaceRig()
      .FeedBlast(20f, pressure: IiexValues.BfBlastPressureAtReference * 2f)
      .RunLive(2);
    var starved = new BlastFurnaceRig()
      .FeedBlast(20f, pressure: IiexValues.BfBlastPressureAtReference / 2f)
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
  public void A_furnace_on_good_blast_reaches_melting_and_never_air_starves() {
    // Control for the starvation case below: on sustained full blast the furnace crosses into Melting
    // and stays there.
    var rig = new BlastFurnaceRig(blastMix: 0)
      .FeedBlast()
      .HeatSoak(SoakSeconds);
    Assert.False(rig.AirStarved);

    bool everStarved = false;
    bool everLeftMelting = false;
    rig.RunUntil(
      _ => false,
      120,
      r => {
        everStarved |= r.AirStarved;
        everLeftMelting |= r.State != FurnaceState.Melting;
      }
    );

    Assert.False(everLeftMelting, "sustained blast should keep it melting");
    Assert.False(everStarved, "and it should never read as starved");
  }

  /// <summary>
  /// A sub-pressure blast line costs rate, not the fire. There is no extinguish countdown on this
  /// branch: the furnace slides to natural draught, burns cooler and slower and renders nothing, but
  /// stays lit. What ends a campaign is running out of carbon, which a starved furnace reaches at half
  /// the rate.
  /// <para>
  /// The choke, a sealed furnace with nowhere to vent, is a separate failure mode that does kill the
  /// fire at once. The two must not be merged.
  /// </para>
  /// </summary>
  [Fact]
  public void A_starved_blast_line_throttles_the_furnace_rather_than_extinguishing_it() {
    var rig = new BlastFurnaceRig(blastMix: 0)
      .FeedBlast(pressure: IiexValues.BfBlastPressureAtReference / 2f)
      .RunLive(2);

    Assert.Equal(FurnaceState.Firing, rig.State);
    Assert.True(
      rig.AirStarved,
      "a sub-blast furnace should read as air-starved"
    );

    int coke = rig.CokeUnits;
    bool everWentOut = false;
    rig.RunUntil(
      _ => false,
      300,
      r => everWentOut |= r.State == FurnaceState.Idle
    );

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
      rig.Temp < IiexValues.BfIronMeltingPoint,
      $"natural draught should sit below the melt line; was {rig.Temp} C"
    );
  }

  #endregion

  #region Burden family gate

  // The cupola's remelt burden charged into a blast furnace: the shaft still lights and burns it as
  // fuel, but the family gate refuses to render it into molten iron. Ore burden in the same rig
  // converts normally (Melting_renders_blast_mix_into_molten_pig_iron above).

  private static readonly BurdenMix Remelt = new(60f, 5f, 35f);

  [Fact]
  public void A_blast_furnace_will_not_convert_a_remelt_burden_charge() {
    var rig = new BlastFurnaceRig(
      blastMix: 0,
      burden: Remelt,
      chargeCode: "remeltburden"
    ).FeedBlast(950f);

    // Run it well past the point the same scene with ore burden is melting and producing.
    rig.RunLive(SoakSeconds / 2);

    Assert.Equal(0f, rig.MoltenIron, 3); // hot and ready, but the wrong family never converts
  }

  /// <summary>
  /// Fullness is family-blind: a shaft packed with the wrong burden lights and burns out, and only the
  /// conversion is gated. <c>DeriveState</c> carries the same gate, so such a furnace reads
  /// <c>Firing</c> however hot it gets rather than showing Melting while producing nothing.
  /// </summary>
  [Fact]
  public void A_wrong_family_shaft_still_reads_full_so_it_lights_and_burns() {
    var rig = new BlastFurnaceRig(
      blastMix: 0,
      burden: Remelt,
      chargeCode: "remeltburden"
    ).FeedBlast(950f);

    int coke = rig.CokeUnits;
    bool everMelted = false;
    rig.RunUntil(
      _ => false,
      SoakSeconds / 2,
      r => everMelted |= r.State == FurnaceState.Melting
    );

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
  /// The production tick re-reads the furnace's cached tunables, so an <c>/exmod config</c> change
  /// applies on the next tick without a reload. Watches the melting point because the shaft branch
  /// reads it every tick; <c>FireboxMeltStartDelay</c> is sealed to 0 there.
  /// </summary>
  [Fact]
  public void A_live_config_change_applies_without_a_reload() {
    float original = IiexValues.BfIronMeltingPoint;
    try {
      var rig = new BlastFurnaceRig().FeedBlast().RunLive(2);

      // Admin retunes the melt line mid-session.
      IiexValues.Edit(c => c.BfIronMeltingPoint = 1234f);
      rig.Tick(1);

      Assert.Equal(
        1234f,
        (float)ReflectionHelpers.GetField(rig.Furnace, "_ironMeltingPoint")!,
        3
      );
    } finally {
      IiexValues.Edit(c => c.BfIronMeltingPoint = original);
    }
  }

  #endregion
}
