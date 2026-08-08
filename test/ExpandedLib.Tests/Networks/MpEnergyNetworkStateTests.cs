using ExpandedLib.Networks;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The mechanical-energy shaft math (docs/design/mp-energy-network.md §2): one spinning shaft integrated as
/// torque on inertia (<c>ω += (τ_drive − τ_load − τ_fric)/I · dt</c>), with <c>E = ½Iω²</c> for the readout.
/// Pure static helpers, pinned without a world - the same way the pipe pressure helpers are. The point of the
/// model is that the flywheel is <b>inertia, not a battery</b>, so the anti-accumulation cases below are the
/// load-bearing ones.
/// </summary>
public class MpEnergyNetworkStateTests
{
  private const float MaxSpeed = 2f;
  private const float Wide = 1000f; // a ceiling high enough not to clamp, when a test isn't about the ceiling

  #region Capacity / speed relationship

  [Fact]
  public void Capacity_is_the_energy_of_a_flywheel_at_max_speed()
  {
    // E_cap = ½·I·ω_max²; a full reservoir is exactly a flywheel spinning at the burst speed.
    Assert.Equal(
      0.5f * 10f * MaxSpeed * MaxSpeed,
      MpEnergyNetworkState.CapacityFor(10f, MaxSpeed),
      3
    );
  }

  [Fact]
  public void Speed_and_energy_round_trip_through_the_flywheel_law()
  {
    // ω = √(2E/I) is the inverse of E = ½Iω².
    float e = MpEnergyNetworkState.EnergyAtSpeed(8f, 1.5f);
    Assert.Equal(1.5f, MpEnergyNetworkState.DeriveSpeed(e, 8f), 3);
  }

  [Fact]
  public void No_inertia_means_no_speed()
  {
    // Nothing to store energy in -> nothing spins, even with energy on the books.
    Assert.Equal(0f, MpEnergyNetworkState.DeriveSpeed(100f, 0f));
  }

  #endregion

  #region Shaft dynamics

  [Fact]
  public void Drive_torque_spins_the_shaft_up_and_stores_energy()
  {
    var s = new MpEnergyNetworkState { Inertia = 10f };
    // dω = τ/I·dt = 5/10·1 = 0.5; E = ½·10·0.5².
    MpEnergyNetworkState.Step(s, 1f, driveTorque: 5f, loadTorque: 0f, frictionCoeff: 0f, idleTorque: 0f, MaxSpeed);

    Assert.Equal(0.5f, s.Speed, 3);
    Assert.Equal(MpEnergyNetworkState.EnergyAtSpeed(10f, 0.5f), s.StoredEnergy, 3);
  }

  [Fact]
  public void A_charged_shaft_coasts_down_under_friction_with_no_drive()
  {
    var s = new MpEnergyNetworkState { Inertia = 10f, Speed = 1f };
    MpEnergyNetworkState.Step(s, 1f, driveTorque: 0f, loadTorque: 0f, frictionCoeff: 0.5f, idleTorque: 0f, MaxSpeed);

    Assert.True(s.Speed < 1f, "an undriven spinning shaft should wind down"); // 1 - 0.5·1/10
  }

  [Fact]
  public void Speed_clamps_to_the_burst_ceiling()
  {
    var s = new MpEnergyNetworkState { Inertia = 1f };
    // A huge torque would overshoot in one step; the clamp is the governor holding ω ≤ ω_max.
    MpEnergyNetworkState.Step(s, 1f, driveTorque: 1000f, loadTorque: 0f, frictionCoeff: 0f, idleTorque: 0f, MaxSpeed);

    Assert.Equal(MaxSpeed, s.Speed, 3);
  }

  [Fact]
  public void Steady_state_holds_speed_when_drive_balances_load_and_friction()
  {
    var s = new MpEnergyNetworkState { Inertia = 10f, Speed = 2f };
    // τ_fric = 0.5·2 = 1; with load 1, a drive of 2 exactly balances → dω = 0.
    MpEnergyNetworkState.Step(s, 1f, driveTorque: 2f, loadTorque: 1f, frictionCoeff: 0.5f, idleTorque: 0f, Wide);

    Assert.Equal(2f, s.Speed, 3);
  }

  #endregion

  #region The flywheel is inertia, not a battery

  [Fact]
  public void A_drive_below_the_resistance_floor_never_starts_the_shaft()
  {
    // The whole point: a drive that can't beat load + idle at rest cannot spin the flywheel up, so there is
    // nothing to accumulate toward a pulse. ω stays 0, energy stays 0 - no battery trickle-charge.
    var s = new MpEnergyNetworkState { Inertia = 10f };
    MpEnergyNetworkState.Step(s, 1f, driveTorque: 0.4f, loadTorque: 0f, frictionCoeff: 0f, idleTorque: 0.5f, Wide);

    Assert.Equal(0f, s.Speed);
    Assert.Equal(0f, s.StoredEnergy);
  }

  [Fact]
  public void An_over_load_drags_the_shaft_to_a_hard_stall()
  {
    // A load the drive can't cover winds ω down; a big enough one stalls it outright (op jams mid-cut).
    var s = new MpEnergyNetworkState { Inertia = 1f, Speed = 2f };
    MpEnergyNetworkState.Step(s, 1f, driveTorque: 0f, loadTorque: 5f, frictionCoeff: 0f, idleTorque: 0f, Wide);

    Assert.Equal(0f, s.Speed);
    Assert.Equal(0f, s.StoredEnergy); // stalled = no stored energy
  }

  [Fact]
  public void A_heavy_flywheel_barely_dents_under_a_load_spike()
  {
    // Same load pulse on a light shaft vs a heavy one: the heavy inertia is what buffers the bite.
    var light = new MpEnergyNetworkState { Inertia = 1f, Speed = 5f };
    var heavy = new MpEnergyNetworkState { Inertia = 100f, Speed = 5f };

    MpEnergyNetworkState.Step(light, 0.1f, 0f, loadTorque: 10f, 0f, 0f, Wide);
    MpEnergyNetworkState.Step(heavy, 0.1f, 0f, loadTorque: 10f, 0f, 0f, Wide);

    Assert.True(
      5f - heavy.Speed < 5f - light.Speed,
      $"heavy dω {5f - heavy.Speed} should be far smaller than light dω {5f - light.Speed}"
    );
  }

  #endregion

  #region Gear coupling (the transmission)

  private static float TotalKE(MpEnergyNetworkState a, MpEnergyNetworkState b) =>
    MpEnergyNetworkState.EnergyAtSpeed(a.Inertia, a.Speed)
    + MpEnergyNetworkState.EnergyAtSpeed(b.Inertia, b.Speed);

  [Fact]
  public void Coupling_holds_the_reduction_constraint()
  {
    // Drive from the south (small gear); the north (big gear) is held at half the speed - a x2 reduction.
    var south = new MpEnergyNetworkState { Inertia = 10f, Speed = 2f };
    var north = new MpEnergyNetworkState { Inertia = 10f, Speed = 0f };

    MpEnergyNetworkState.CoupleRatio(south, north, ratio: 2f, Wide);

    Assert.Equal(south.Speed / 2f, north.Speed, 4);
    Assert.True(north.Speed < south.Speed, "S→N is a reduction, so the north side turns slower");
  }

  [Fact]
  public void Lossless_coupling_conserves_total_energy()
  {
    var south = new MpEnergyNetworkState { Inertia = 10f, Speed = 2f };
    var north = new MpEnergyNetworkState { Inertia = 5f, Speed = 0.3f };
    float before = TotalKE(south, north);

    MpEnergyNetworkState.CoupleRatio(south, north, ratio: 4f, Wide, retention: 1f);

    Assert.Equal(before, TotalKE(south, north), 3);
    Assert.Equal(south.Speed / 4f, north.Speed, 4);
  }

  [Fact]
  public void A_consistent_pair_is_left_untouched()
  {
    // Already on the constraint (ω_north = ω_south / r): the projection is a no-op, so a spun-up train is stable.
    var south = new MpEnergyNetworkState { Inertia = 10f, Speed = 2f };
    var north = new MpEnergyNetworkState { Inertia = 10f, Speed = 1f };

    MpEnergyNetworkState.CoupleRatio(south, north, ratio: 2f, Wide);

    Assert.Equal(2f, south.Speed, 4);
    Assert.Equal(1f, north.Speed, 4);
  }

  [Fact]
  public void Driving_from_the_north_speeds_the_south_up()
  {
    // Reverse power flow: the north (big gear) drives, so the south (small gear) ends up spinning faster.
    var south = new MpEnergyNetworkState { Inertia = 10f, Speed = 0f };
    var north = new MpEnergyNetworkState { Inertia = 10f, Speed = 1f };

    MpEnergyNetworkState.CoupleRatio(south, north, ratio: 2f, Wide);

    Assert.True(south.Speed > north.Speed, "N→S speeds up");
    Assert.Equal(south.Speed / 2f, north.Speed, 4);
  }

  [Fact]
  public void Mesh_loss_drains_a_little_energy()
  {
    var south = new MpEnergyNetworkState { Inertia = 10f, Speed = 2f };
    var north = new MpEnergyNetworkState { Inertia = 10f, Speed = 0f };
    float before = TotalKE(south, north);

    MpEnergyNetworkState.CoupleRatio(south, north, ratio: 2f, Wide, retention: 0.9f);

    Assert.Equal(before * 0.9f, TotalKE(south, north), 3);
  }

  [Fact]
  public void Coupling_a_side_with_no_inertia_is_a_no_op()
  {
    // No shaft (no inertia) on one side: nothing to couple to, so the live side is left alone.
    var south = new MpEnergyNetworkState { Inertia = 0f, Speed = 0f };
    var north = new MpEnergyNetworkState { Inertia = 10f, Speed = 1f };

    MpEnergyNetworkState.CoupleRatio(south, north, ratio: 2f, Wide);

    Assert.Equal(1f, north.Speed, 4);
    Assert.Equal(0f, south.Speed, 4);
  }

  [Fact]
  public void Over_energised_coupling_clamps_the_south_and_keeps_the_ratio()
  {
    // Far more energy than the ceiling can hold: the south clamps to ω_max and the north follows at ω_max / r,
    // so the constraint survives the governor (ratio ≥ 1 means the north can never be the one to clamp).
    var south = new MpEnergyNetworkState { Inertia = 10f, Speed = 10f };
    var north = new MpEnergyNetworkState { Inertia = 10f, Speed = 0f };

    MpEnergyNetworkState.CoupleRatio(south, north, ratio: 2f, MaxSpeed);

    Assert.Equal(MaxSpeed, south.Speed, 4);
    Assert.Equal(MaxSpeed / 2f, north.Speed, 4);
  }

  #endregion
}
