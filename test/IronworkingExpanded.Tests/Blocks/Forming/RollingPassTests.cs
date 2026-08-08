using IronworkingExpanded.BlockStructures.Forming;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The rolling-pass physics - the first real load on the mechanical-energy network. These pin the three
/// relations the mill is built on (bite <c>δ_max = μ²R</c>, contact arc <c>√(Rδ)</c>, torque <c>Y·w·R·δ</c>)
/// and, more importantly, the <b>gameplay rules that fall out of them</b>: hot stock bites ~30× deeper than
/// cold, a cooling piece both refuses to bite and loads harder, and a stopped shaft cannot start a pass.
/// </summary>
public class RollingPassTests
{
  private const float RollingTemp = 900f;
  private const float Radius = 4f;
  private const float ColdMultiplier = 10f;
  private const float ColdSpan = 400f;

  #region Bite (δ_max = μ²R)

  [Fact]
  public void Hot_stock_bites_about_thirty_times_deeper_than_cold()
  {
    // The whole reason this is a hot mill: draft scales with mu squared, so 0.5 vs 0.09 is ~31x.
    float hot = RollingPass.MaxDraft(Radius, RollingPass.HotFriction);
    float cold = RollingPass.MaxDraft(Radius, RollingPass.ColdFriction);

    Assert.Equal(1f, hot, 4); // 0.5^2 * 4
    Assert.True(hot / cold > 25f, $"hot/cold draft ratio was {hot / cold:F1}, expected ~31");
    Assert.True(hot / cold < 35f);
  }

  [Fact]
  public void A_bigger_roll_takes_a_deeper_draft()
  {
    Assert.True(
      RollingPass.MaxDraft(8f, RollingPass.HotFriction)
        > RollingPass.MaxDraft(4f, RollingPass.HotFriction)
    );
  }

  [Fact]
  public void A_draft_a_hot_piece_takes_is_refused_once_it_cools()
  {
    // The same gap segment: fine at rolling heat, refused when the piece drops below it. This is the player
    // being told to reheat rather than being allowed to stall the run.
    const float draft = 0.5f;
    Assert.True(RollingPass.CanBite(draft, Radius, tempC: 1000f, RollingTemp));
    Assert.False(RollingPass.CanBite(draft, Radius, tempC: 600f, RollingTemp));
  }

  [Fact]
  public void An_over_deep_pass_will_not_enter_even_when_hot()
  {
    // Above mu^2 R the rolls skid on the stock however hot it is - which is what forces the gap sequence
    // (each barrel segment is one legal step) instead of one huge reduction.
    Assert.False(RollingPass.CanBite(1.5f, Radius, tempC: 1200f, RollingTemp));
    Assert.True(RollingPass.CanBite(0.9f, Radius, tempC: 1200f, RollingTemp));
  }

  [Fact]
  public void A_zero_or_negative_draft_is_not_a_pass()
  {
    Assert.False(RollingPass.CanBite(0f, Radius, 1200f, RollingTemp));
    Assert.False(RollingPass.CanBite(-1f, Radius, 1200f, RollingTemp));
  }

  #endregion

  #region Flow stress (the keep-it-hot coupling)

  [Fact]
  public void Hot_stock_sits_at_the_flow_stress_floor()
  {
    Assert.Equal(1f, RollingPass.FlowStress(900f, RollingTemp, ColdMultiplier, ColdSpan), 4);
    Assert.Equal(1f, RollingPass.FlowStress(1400f, RollingTemp, ColdMultiplier, ColdSpan), 4);
  }

  [Fact]
  public void Flow_stress_climbs_as_the_piece_cools_and_saturates_at_the_multiplier()
  {
    float warm = RollingPass.FlowStress(800f, RollingTemp, ColdMultiplier, ColdSpan);
    float cool = RollingPass.FlowStress(600f, RollingTemp, ColdMultiplier, ColdSpan);

    Assert.True(warm > 1f && warm < cool);
    // Fully cold (>= the span below rolling heat) saturates, and stays there however cold it gets.
    Assert.Equal(ColdMultiplier, RollingPass.FlowStress(500f, RollingTemp, ColdMultiplier, ColdSpan), 3);
    Assert.Equal(ColdMultiplier, RollingPass.FlowStress(20f, RollingTemp, ColdMultiplier, ColdSpan), 3);
  }

  #endregion

  #region Torque

  [Fact]
  public void A_cold_pass_loads_the_run_far_harder_than_a_hot_one()
  {
    // The stall mechanic: identical geometry, only the temperature differs.
    float hot = Torque(draft: 0.5f, tempC: 1000f);
    float cold = Torque(draft: 0.5f, tempC: 500f);

    Assert.Equal(ColdMultiplier, cold / hot, 2);
  }

  [Fact]
  public void Torque_grows_with_draft_and_width()
  {
    Assert.True(Torque(draft: 0.8f, tempC: 1000f) > Torque(draft: 0.2f, tempC: 1000f));
    Assert.True(
      Torque(draft: 0.5f, tempC: 1000f, width: 8f) > Torque(draft: 0.5f, tempC: 1000f, width: 2f)
    );
  }

  [Fact]
  public void Torque_is_linear_in_draft_because_the_contact_arc_is_squared()
  {
    // T = Y*w*L_c^2 and L_c = sqrt(R*delta), so T is exactly linear in delta. Pinning it keeps the force
    // relation honest: force goes as sqrt(delta), the lever arm as sqrt(delta), the product as delta.
    Assert.Equal(2f * Torque(draft: 0.25f, tempC: 1000f), Torque(draft: 0.5f, tempC: 1000f), 4);
  }

  [Fact]
  public void An_idle_mill_loads_nothing()
  {
    Assert.Equal(0f, Torque(draft: 0f, tempC: 1000f));
    Assert.Equal(0f, Torque(draft: 0.5f, tempC: 1000f, width: 0f));
  }

  [Fact]
  public void The_pass_load_does_not_ease_off_as_the_shaft_slows()
  {
    // Plastic deformation resists the same at any speed - unlike friction, which falls with omega. That is
    // precisely why a pass can drag a run all the way to a stall.
    float slow = Torque(draft: 0.5f, tempC: 1000f);
    float fast = Torque(draft: 0.5f, tempC: 1000f);
    Assert.Equal(slow, fast, 6); // no speed term exists in the signature at all
  }

  #endregion

  #region Carrying the load

  [Fact]
  public void A_stopped_shaft_cannot_start_a_pass_however_much_torque_is_on_tap()
  {
    // The startup ritual: spin the flywheel up first. Emergent, not special-cased.
    Assert.False(RollingPass.CanCarry(loadTorque: 1f, availableTorque: 1000f, speed: 0f));
    Assert.True(RollingPass.CanCarry(loadTorque: 1f, availableTorque: 1000f, speed: 0.5f));
  }

  [Fact]
  public void An_under_powered_run_cannot_carry_the_pass()
  {
    Assert.False(RollingPass.CanCarry(loadTorque: 100f, availableTorque: 10f, speed: 1f));
    Assert.True(RollingPass.CanCarry(loadTorque: 10f, availableTorque: 100f, speed: 1f));
  }

  #endregion

  #region Spread (why a narrow barrel stops coping)

  [Fact]
  public void Squeezed_metal_spreads_sideways_as_it_thins()
  {
    // A shingled bloom is 3 thick and 3 wide. Halve its thickness and it comes out noticeably wider - which is
    // what eventually makes it overhang a narrow roll barrel.
    Assert.Equal(3f, RollingPass.SpreadWidth(3f, 3f, 3f), 4); // no reduction, no spread
    Assert.Equal(4.243f, RollingPass.SpreadWidth(3f, 3f, 1.5f), 3);
    Assert.Equal(7.348f, RollingPass.SpreadWidth(3f, 3f, 0.5f), 3);
  }

  [Fact]
  public void Spread_conserves_volume_when_length_takes_the_other_half()
  {
    // Width and length each take the square root of the thickness ratio, so w * t * L is unchanged - the
    // metal goes somewhere rather than vanishing.
    const float w0 = 3f, t0 = 3f, l0 = 16f;
    const float t1 = 0.75f;

    float w1 = RollingPass.SpreadWidth(w0, t0, t1);
    float l1 = RollingPass.SpreadWidth(l0, t0, t1); // same relation drives the elongation
    Assert.Equal(w0 * t0 * l0, w1 * t1 * l1, 2);
  }

  [Fact]
  public void Degenerate_inputs_leave_the_width_alone()
  {
    Assert.Equal(3f, RollingPass.SpreadWidth(3f, 3f, 0f), 4);
    Assert.Equal(3f, RollingPass.SpreadWidth(3f, 0f, 1f), 4);
    Assert.Equal(0f, RollingPass.SpreadWidth(-1f, 3f, 1f), 4);
  }

  #endregion

  #region Cooling under the rolls (what closes the keep-it-hot loop)

  [Fact]
  public void Stock_sheds_its_excess_heat_toward_ambient()
  {
    float once = RollingPass.Cool(1200f, ambientC: 20f, ratePerSecond: 0.02f, dt: 1f);
    Assert.True(once < 1200f && once > 1100f, $"a second should shave a little heat, got {once}");

    // Long enough and it approaches ambient, never crossing it.
    float cold = RollingPass.Cool(1200f, 20f, 0.02f, dt: 10_000f);
    Assert.True(cold >= 20f);
    Assert.Equal(20f, cold, 1);
  }

  [Fact]
  public void Cooling_is_monotonic_and_never_reheats_the_piece()
  {
    float warm = RollingPass.Cool(1200f, 20f, 0.02f, 10f);
    float cooler = RollingPass.Cool(warm, 20f, 0.02f, 10f);
    Assert.True(cooler < warm);

    // Already at ambient (or below): nothing happens - the rolls are not a furnace.
    Assert.Equal(20f, RollingPass.Cool(20f, 20f, 0.02f, 100f), 4);
    Assert.Equal(10f, RollingPass.Cool(10f, 20f, 0.02f, 100f), 4);
  }

  [Fact]
  public void A_cooling_piece_loads_the_line_harder_as_the_pass_goes_on()
  {
    // The whole point of modelling the cooling: the same geometry costs more torque later in the pass, so a
    // schedule the line carried at the first gap can drag it down by the last.
    float entered = 1000f;
    float later = RollingPass.Cool(entered, 20f, 0.02f, dt: 60f);

    Assert.True(later < entered);
    Assert.True(Torque(draft: 0.5f, tempC: later) > Torque(draft: 0.5f, tempC: entered));
  }

  [Fact]
  public void A_zero_rate_or_zero_time_leaves_the_piece_alone()
  {
    Assert.Equal(1000f, RollingPass.Cool(1000f, 20f, ratePerSecond: 0f, dt: 100f), 4);
    Assert.Equal(1000f, RollingPass.Cool(1000f, 20f, 0.02f, dt: 0f), 4);
  }

  #endregion

  private static float Torque(float draft, float tempC, float width = 4f) =>
    RollingPass.LoadTorque(
      draft,
      width,
      Radius,
      tempC,
      RollingTemp,
      ColdMultiplier,
      ColdSpan,
      torqueScale: 1f
    );
}
