using System;

namespace ExpandedLib.Networks;

/// <summary>
/// Live state of one mechanical-energy run, modelled as a single spinning shaft: a drive applies torque,
/// machines and friction resist it, and the net spins a lumped inertia up or down
/// (<c>I·dω/dt = τ_drive − τ_load − τ_fric</c>), with stored energy <c>E = ½Iω²</c>. A drive that cannot
/// out-torque the load plus standing friction never spins the run up at all.
/// <para>
/// The simulation is in the static helpers here and needs no world; <see cref="MpEnergyNetwork"/> gathers
/// the per-tick torques and inertia off the nodes and calls <see cref="Step"/>. Quantities are SI
/// (ω rad/s, I kg·m², τ N·m, E J, P W); display conversion is <c>ExMeasure</c>'s.
/// See docs/design/mechanics/mp-energy.md.
/// </para>
/// </summary>
public class MpEnergyNetworkState {
  /// <summary>Shaft speed <c>ω</c> in rad/s, integrated from the net torque. Drives the flywheel spin
  /// animation, the charge readout and any speed gates.</summary>
  public float Speed { get; set; }

  /// <summary>Lumped rotational inertia <c>I</c> in kg·m²: the sum over every flywheel and transmission
  /// buffer. With <c>maxSpeed</c> it sets the capacity, the spin-up time and how hard the run resists a
  /// torque change.</summary>
  public float Inertia { get; set; }

  /// <summary>Stored mechanical energy <c>E = ½Iω²</c> in joules. Recomputed from <see cref="Speed"/> and
  /// <see cref="Inertia"/> each step; kept as a field for the block-info charge readout.</summary>
  public float StoredEnergy { get; set; }

  /// <summary>Power fed into the run this tick, <c>P = τ_drive·ω</c> in watts. Display only.</summary>
  public float SupplyPower { get; set; }

  /// <summary>Power drawn from the run this tick, <c>P = τ_load·ω</c> in watts. Display only.</summary>
  public float DemandPower { get; set; }

  /// <summary>Direction of rotation. <see cref="Speed"/> is unsigned because the torque balance uses
  /// magnitudes only; machines whose geometry depends on direction read this instead (see
  /// <see cref="IMpEnergyDirection"/>).</summary>
  public bool Reversed { get; set; }

  /// <summary>Reservoir capacity <c>E_cap = ½·I·ω_max²</c> in joules for the given inertia and burst speed.
  /// Storage contributes inertia and capacity is derived from it, so a full reservoir is a flywheel spinning
  /// at <paramref name="maxSpeed"/>.</summary>
  public static float CapacityFor(float inertia, float maxSpeed) =>
    0.5f * inertia * maxSpeed * maxSpeed;

  /// <summary>Shaft speed for a reservoir holding <paramref name="storedEnergy"/>: <c>ω = √(2E/I)</c>. Zero
  /// when there is no inertia.</summary>
  public static float DeriveSpeed(float storedEnergy, float inertia) =>
    inertia > 0f ? MathF.Sqrt(2f * MathF.Max(0f, storedEnergy) / inertia) : 0f;

  /// <summary>Energy held at a given speed: <c>E = ½Iω²</c>, the inverse of
  /// <see cref="DeriveSpeed"/>.</summary>
  public static float EnergyAtSpeed(float inertia, float speed) =>
    0.5f * inertia * speed * speed;

  /// <summary>
  /// One integration step of the shaft dynamics: <c>ω += (τ_drive − τ_load − τ_fric)/I · dt</c>, clamped to
  /// <c>[0, maxSpeed]</c>. Friction is windage plus a standing-resistance floor,
  /// <c>τ_fric = b·ω + τ_idle</c>. Refreshes <see cref="Speed"/>, <see cref="StoredEnergy"/> and the display
  /// powers. Stall, coast-down and buffering all follow from the sign of the net torque; there are no
  /// special cases.
  /// </summary>
  public static void Step(
    MpEnergyNetworkState s,
    float dt,
    float driveTorque,
    float loadTorque,
    float frictionCoeff,
    float idleTorque,
    float maxSpeed
  ) {
    // The idle floor is a resistance and only ever opposes rotation. With the ω ≥ 0 clamp, a stopped shaft
    // the drive cannot start stays stopped.
    float frictionTorque = frictionCoeff * s.Speed + MathF.Max(0f, idleTorque);
    float netTorque = driveTorque - loadTorque - frictionTorque;
    float dOmega = s.Inertia > 0f ? netTorque / s.Inertia * dt : 0f;

    s.Speed = Math.Clamp(s.Speed + dOmega, 0f, maxSpeed);
    s.StoredEnergy = EnergyAtSpeed(s.Inertia, s.Speed);
    s.SupplyPower = driveTorque * s.Speed;
    s.DemandPower = loadTorque * s.Speed;
  }

  /// <summary>
  /// Couples two separate runs across a rigid gear of reduction <paramref name="ratio"/> (at least 1): the
  /// north run is held at <c>ω_south / ratio</c>, so south to north slows and gains torque. Each run keeps
  /// its own reservoir; both are projected onto the gear constraint, conserving total kinetic energy times
  /// <paramref name="retention"/> (per-tick gear-mesh loss, 1 = lossless), with the north inertia reflected
  /// to the south side by <c>1/ratio²</c>. Clamping <c>ω_south</c> to <paramref name="maxSpeed"/> sheds the
  /// surplus and covers <c>ω_north</c> as well. A no-op when either side has no inertia. Speed, not
  /// <see cref="StoredEnergy"/>, is read as the source of truth.
  /// </summary>
  public static void CoupleRatio(
    MpEnergyNetworkState south,
    MpEnergyNetworkState north,
    float ratio,
    float maxSpeed,
    float retention = 1f
  ) {
    if (ratio <= 0f || south.Inertia <= 0f || north.Inertia <= 0f)
      return;

    float total =
      (
        EnergyAtSpeed(south.Inertia, south.Speed)
        + EnergyAtSpeed(north.Inertia, north.Speed)
      ) * Math.Clamp(retention, 0f, 1f);
    float combined = south.Inertia + north.Inertia / (ratio * ratio);
    float omegaSouth = MathF.Min(MathF.Sqrt(2f * total / combined), maxSpeed);
    float omegaNorth = omegaSouth / ratio;

    south.Speed = omegaSouth;
    north.Speed = omegaNorth;
    south.StoredEnergy = EnergyAtSpeed(south.Inertia, omegaSouth);
    north.StoredEnergy = EnergyAtSpeed(north.Inertia, omegaNorth);
  }
}
