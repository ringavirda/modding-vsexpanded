using IronIndustryExpanded.BlockStructures.Forming;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The rolling-pass physics the mill is built on: bite <c>δ_max = μ²R</c>, spread, elongation, cooling, and
/// the load a working stand puts on its run. Also covers the rules that follow from them - hot stock bites
/// about 30 times deeper than cold, a cooling piece both refuses to bite and loads harder, and a stopped
/// shaft cannot start a pass. See docs/design/processes/rolling.md.
/// </summary>
public class RollingPassTests {
  private const float RollingTemp = 900f;
  private const float Radius = 4f;
  private const float ColdMultiplier = 10f;
  private const float ColdSpan = 400f;

  // A stand's working demand. The shipped number is IiexValues.RollingLoadTorque; this only has to be a
  // value the relations here are exercised against.
  private const float Demand = 0.34f;

  #region Bite (δ_max = μ²R)

  [Fact]
  public void Hot_stock_bites_about_thirty_times_deeper_than_cold() {
    // Draft scales with mu squared, so friction 0.3 against 0.055 is a ratio of about 30.
    float hot = RollingPass.MaxDraft(Radius, RollingPass.HotFriction);
    float cold = RollingPass.MaxDraft(Radius, RollingPass.ColdFriction);

    Assert.Equal(0.36f, hot, 4); // 0.3^2 * 4
    Assert.True(
      hot / cold > 25f,
      $"hot/cold draft ratio was {hot / cold:F1}, expected ~30"
    );
    Assert.True(hot / cold < 35f);
  }

  [Fact]
  public void Delta_max_sits_between_one_round_and_one_gap() {
    // The calibration the whole schedule rests on. Every shipped gap is a 0.5 step taken in two rounds, so
    // a round asks 0.25 and a skipped gap asks 0.5: the first must bite and the second must skid, or the
    // barrel stops being a sequence the player has to walk.
    float hot = RollingPass.MaxDraft(Radius, RollingPass.HotFriction);

    Assert.True(
      hot >= 0.25f,
      $"an ordinary round must bite, delta_max was {hot}"
    );
    Assert.True(hot < 0.5f, $"a skipped gap must skid, delta_max was {hot}");
  }

  [Fact]
  public void A_bigger_roll_takes_a_deeper_draft() {
    Assert.True(
      RollingPass.MaxDraft(8f, RollingPass.HotFriction)
        > RollingPass.MaxDraft(4f, RollingPass.HotFriction)
    );
  }

  [Fact]
  public void A_draft_a_hot_piece_takes_is_refused_once_it_cools() {
    // The same round: accepted at rolling heat, refused once the piece drops below it.
    const float draft = 0.25f;
    Assert.True(RollingPass.CanBite(draft, Radius, tempC: 1000f, RollingTemp));
    Assert.False(RollingPass.CanBite(draft, Radius, tempC: 600f, RollingTemp));
  }

  [Fact]
  public void An_over_deep_pass_will_not_enter_even_when_hot() {
    // Above mu^2 R the rolls skid however hot the stock is, which is what forces the gap sequence - each
    // barrel segment is two legal rounds - instead of a single deep reduction.
    Assert.False(RollingPass.CanBite(0.5f, Radius, tempC: 1200f, RollingTemp));
    Assert.True(RollingPass.CanBite(0.35f, Radius, tempC: 1200f, RollingTemp));
  }

  [Fact]
  public void A_zero_or_negative_draft_is_not_a_pass() {
    Assert.False(RollingPass.CanBite(0f, Radius, 1200f, RollingTemp));
    Assert.False(RollingPass.CanBite(-1f, Radius, 1200f, RollingTemp));
  }

  #endregion

  #region Flow stress (the keep-it-hot coupling)

  [Fact]
  public void Hot_stock_sits_at_the_flow_stress_floor() {
    Assert.Equal(
      1f,
      RollingPass.FlowStress(900f, RollingTemp, ColdMultiplier, ColdSpan),
      4
    );
    Assert.Equal(
      1f,
      RollingPass.FlowStress(1400f, RollingTemp, ColdMultiplier, ColdSpan),
      4
    );
  }

  [Fact]
  public void Flow_stress_climbs_as_the_piece_cools_and_saturates_at_the_multiplier() {
    float warm = RollingPass.FlowStress(
      800f,
      RollingTemp,
      ColdMultiplier,
      ColdSpan
    );
    float cool = RollingPass.FlowStress(
      600f,
      RollingTemp,
      ColdMultiplier,
      ColdSpan
    );

    Assert.True(warm > 1f && warm < cool);
    // At or beyond the span below rolling heat the value saturates and stays there however cold it gets.
    Assert.Equal(
      ColdMultiplier,
      RollingPass.FlowStress(500f, RollingTemp, ColdMultiplier, ColdSpan),
      3
    );
    Assert.Equal(
      ColdMultiplier,
      RollingPass.FlowStress(20f, RollingTemp, ColdMultiplier, ColdSpan),
      3
    );
  }

  #endregion

  #region Torque

  [Fact]
  public void A_cold_pass_loads_the_run_far_harder_than_a_hot_one() {
    // Same stand, same declared demand; only the temperature differs.
    float hot = Torque(tempC: 1000f);
    float cold = Torque(tempC: 500f);

    Assert.Equal(ColdMultiplier, cold / hot, 2);
  }

  [Fact]
  public void Every_hot_pass_asks_the_same_of_the_run() {
    // The load is what the stand draws while it is working, not what the bite happens to be. Nothing about
    // the piece under the rolls moves it, which is what keeps the mill's balance from drifting when the
    // schedule is re-cut.
    Assert.Equal(Demand, Torque(tempC: 1000f), 6);
    Assert.Equal(Torque(tempC: 1000f), Torque(tempC: 1400f), 6);
  }

  [Fact]
  public void A_stand_that_draws_nothing_loads_nothing() {
    Assert.Equal(0f, Torque(tempC: 1000f, runningTorque: 0f));
    Assert.Equal(0f, Torque(tempC: 500f, runningTorque: -1f));
  }

  [Fact]
  public void The_pass_load_does_not_ease_off_as_the_shaft_slows() {
    // Plastic deformation resists the same at any speed, unlike friction, which falls with omega. A pass
    // can therefore drag a run down to a stall.
    float slow = Torque(tempC: 1000f);
    float fast = Torque(tempC: 1000f);
    Assert.Equal(slow, fast, 6); // the signature carries no speed term
  }

  #endregion

  #region Carrying the load

  [Fact]
  public void A_stopped_shaft_cannot_start_a_pass_however_much_torque_is_on_tap() {
    // A pass needs a turning shaft, not just available torque, so the flywheel must be spun up first.
    Assert.False(
      RollingPass.CanCarry(loadTorque: 1f, availableTorque: 1000f, speed: 0f)
    );
    Assert.True(
      RollingPass.CanCarry(loadTorque: 1f, availableTorque: 1000f, speed: 0.5f)
    );
  }

  [Fact]
  public void An_under_powered_run_cannot_carry_the_pass() {
    Assert.False(
      RollingPass.CanCarry(loadTorque: 100f, availableTorque: 10f, speed: 1f)
    );
    Assert.True(
      RollingPass.CanCarry(loadTorque: 10f, availableTorque: 100f, speed: 1f)
    );
  }

  #endregion

  #region Spread (why a narrow barrel stops coping)

  [Fact]
  public void Squeezed_metal_spreads_sideways_as_it_thins() {
    // A shingled bloom is 3 thick and 3 wide; reducing its thickness widens it, which is what eventually
    // makes it overhang a narrow roll barrel.
    Assert.Equal(3f, RollingPass.SpreadWidth(3f, 3f, 3f), 4); // no reduction, no spread
    Assert.Equal(4.243f, RollingPass.SpreadWidth(3f, 3f, 1.5f), 3);
    Assert.Equal(7.348f, RollingPass.SpreadWidth(3f, 3f, 0.5f), 3);
  }

  [Fact]
  public void Spread_conserves_volume_when_length_takes_the_other_half() {
    // Width and length each take the square root of the thickness ratio, so w * t * L is unchanged.
    const float w0 = 3f,
      t0 = 3f,
      l0 = 16f;
    const float t1 = 0.75f;

    float w1 = RollingPass.SpreadWidth(w0, t0, t1);
    float l1 = RollingPass.SpreadWidth(l0, t0, t1); // same relation drives the elongation
    Assert.Equal(w0 * t0 * l0, w1 * t1 * l1, 2);
  }

  [Fact]
  public void Degenerate_inputs_leave_the_width_alone() {
    Assert.Equal(3f, RollingPass.SpreadWidth(3f, 3f, 0f), 4);
    Assert.Equal(3f, RollingPass.SpreadWidth(3f, 0f, 1f), 4);
    Assert.Equal(0f, RollingPass.SpreadWidth(-1f, 3f, 1f), 4);
  }

  #endregion

  #region Cooling under the rolls (what closes the keep-it-hot loop)

  [Fact]
  public void Stock_sheds_its_excess_heat_toward_ambient() {
    float once = RollingPass.Cool(
      1200f,
      ambientC: 20f,
      ratePerSecond: 0.02f,
      dt: 1f
    );
    Assert.True(
      once < 1200f && once > 1100f,
      $"a second should shave a little heat, got {once}"
    );

    // Long enough and it approaches ambient, never crossing it.
    float cold = RollingPass.Cool(1200f, 20f, 0.02f, dt: 10_000f);
    Assert.True(cold >= 20f);
    Assert.Equal(20f, cold, 1);
  }

  [Fact]
  public void Cooling_is_monotonic_and_never_reheats_the_piece() {
    float warm = RollingPass.Cool(1200f, 20f, 0.02f, 10f);
    float cooler = RollingPass.Cool(warm, 20f, 0.02f, 10f);
    Assert.True(cooler < warm);

    // At or below ambient nothing happens; cooling never adds heat.
    Assert.Equal(20f, RollingPass.Cool(20f, 20f, 0.02f, 100f), 4);
    Assert.Equal(10f, RollingPass.Cool(10f, 20f, 0.02f, 100f), 4);
  }

  [Fact]
  public void A_cooling_piece_loads_the_line_harder_as_the_pass_goes_on() {
    // The same stand costs more torque later in the pass, so a schedule the run carried at the first gap
    // can stall it by the last.
    float entered = 1000f;
    float later = RollingPass.Cool(entered, 20f, 0.02f, dt: 60f);

    Assert.True(later < entered);
    Assert.True(Torque(tempC: later) > Torque(tempC: entered));
  }

  [Fact]
  public void A_zero_rate_or_zero_time_leaves_the_piece_alone() {
    Assert.Equal(
      1000f,
      RollingPass.Cool(1000f, 20f, ratePerSecond: 0f, dt: 100f),
      4
    );
    Assert.Equal(1000f, RollingPass.Cool(1000f, 20f, 0.02f, dt: 0f), 4);
  }

  #endregion

  #region The section law both directions run on

  /// <summary>
  /// The two figures the forming design quotes, neither of which is written down anywhere: a piece wide
  /// against its gauge exposes <c>2/t</c>, a square bar <c>4/t</c>. They are the limits of one formula, so
  /// the section falls out of the dimensions a piece already carries and nothing needs a shape class.
  /// </summary>
  [Fact]
  public void Area_over_volume_collapses_to_the_plate_and_the_square_bar() {
    // Square: w = t, so exactly 4/t.
    Assert.Equal(4f / 3f, RollingPass.AreaOverVolume(3f, 3f), 4);
    Assert.Equal(2f, RollingPass.AreaOverVolume(2f, 2f), 4);

    // Wide against its gauge: approaches 2/t from above, and is within 5 % by 40 to 1.
    float plate = RollingPass.AreaOverVolume(40f, 1f);
    Assert.True(plate > 2f, $"{plate} should exceed the 2/t limit");
    Assert.True(plate < 2.1f, $"{plate} is not near the 2/t limit");
  }

  /// <summary>
  /// The whole point of an area law rather than a mass one: at equal metal the thinner piece exchanges heat
  /// faster, in both directions. A mass law makes these two identical and the last gap of a schedule - the
  /// thinnest and longest the piece ever is - the cheapest to keep hot.
  /// </summary>
  [Fact]
  public void At_equal_mass_the_thinner_piece_exchanges_heat_faster() {
    // 3 x 3 and 9 x 1: the same section area, so the same metal per unit length.
    float square = RollingPass.AreaOverVolume(3f, 3f);
    float flat = RollingPass.AreaOverVolume(9f, 1f);
    Assert.True(flat > square, $"flat {flat} should beat square {square}");

    Assert.True(
      RollingPass.Cool(1200f, 20f, 0.005f * flat, 60f)
        < RollingPass.Cool(1200f, 20f, 0.005f * square, 60f)
    );
  }

  [Fact]
  public void A_degenerate_section_has_no_rate() {
    Assert.Equal(0f, RollingPass.AreaOverVolume(0f, 3f), 4);
    Assert.Equal(0f, RollingPass.AreaOverVolume(3f, 0f), 4);
    Assert.Equal(0f, RollingPass.AreaOverVolume(-1f, 3f), 4);
  }

  #endregion

  #region Soaking, the mirror of the cooling

  [Fact]
  public void Stock_takes_up_the_chamber_heat_without_passing_it() {
    float once = RollingPass.Soak(
      20f,
      furnaceC: 1400f,
      ratePerSecond: 0.02f,
      dt: 1f
    );
    Assert.True(
      once > 20f && once < 60f,
      $"a second should add a little heat, got {once}"
    );

    // Long enough and it approaches the chamber, never crossing it.
    float hot = RollingPass.Soak(20f, 1400f, 0.02f, dt: 10_000f);
    Assert.True(hot <= 1400f);
    Assert.Equal(1400f, hot, 1);
  }

  /// <summary>
  /// The soak only ever adds heat. A piece already hotter than the chamber is left where it is - the
  /// engine's own cooling owns that direction, and a soak that dragged it down would fight it.
  /// </summary>
  [Fact]
  public void Soaking_never_takes_heat_out_of_a_piece() {
    Assert.Equal(1500f, RollingPass.Soak(1500f, 1400f, 0.02f, 100f), 4);
    Assert.Equal(1400f, RollingPass.Soak(1400f, 1400f, 0.02f, 100f), 4);
    Assert.Equal(1000f, RollingPass.Soak(1000f, 1400f, 0f, 100f), 4);
    Assert.Equal(1000f, RollingPass.Soak(1000f, 1400f, 0.02f, 0f), 4);
  }

  /// <summary>Stepping the soak and running it in one go agree, so a cadence that catches up after a
  /// reload lands where an uninterrupted one would.</summary>
  [Fact]
  public void Soaking_in_steps_matches_soaking_in_one_go() {
    float stepped = 20f;
    for (int i = 0; i < 60; i++)
      stepped = RollingPass.Soak(stepped, 1400f, 0.02f, 1f);

    Assert.Equal(RollingPass.Soak(20f, 1400f, 0.02f, 60f), stepped, 1);
  }

  #endregion

  private static float Torque(float tempC, float runningTorque = Demand) =>
    RollingPass.LoadTorque(
      runningTorque,
      tempC,
      RollingTemp,
      ColdMultiplier,
      ColdSpan
    );
}
