using System;

namespace ExpandedLib.Networks;

/// <summary>
/// Live state of a mechanical-<b>energy</b> run, modelled as <b>one spinning shaft</b> — torque on inertia,
/// not an energy bucket. A drive applies torque, machines and friction resist it, and the net spins a lumped
/// inertia up or down (<c>I·dω/dt = τ_drive − τ_load − τ_fric</c>); the stored energy is simply
/// <c>E = ½Iω²</c>, kept for the charge readout. This is why the flywheel is <b>inertia, not a battery</b>: a
/// drive that can't out-torque the loaded resistance never spins it up, so you can never trickle-charge your
/// way to a pulse. See <c>docs/design/mp-energy-network.md</c> §2.
/// <para>
/// The simulation lives in the pure static helpers here (unit-testable without a world, like
/// <see cref="PipeNetworkState"/>'s pressure helpers); <see cref="MpEnergyNetwork"/> only gathers the
/// per-tick drive/load torque and inertia off the nodes and calls <see cref="Step"/>. All quantities are SI
/// (ω rad/s, I kg·m², τ N·m, E J, P W); display conversion (RPM, kW/hp, charge %) is in <c>ExMeasure</c>.
/// </para>
/// </summary>
public class MpEnergyNetworkState
{
  /// <summary>Shaft speed <c>ω</c> in <b>rad/s</b>, integrated from the net torque. The shared observable:
  /// drives the flywheel spin animation, the charge readout, and any speed gates.</summary>
  public float Speed { get; set; }

  /// <summary>Lumped rotational inertia <c>I</c> in <b>kg·m²</b> — Σ of every flywheel's and transmission
  /// buffer's inertia. Sets the capacity (with <c>maxSpeed</c>), the spin-up time, and how hard the run
  /// resists a torque change (the buffering).</summary>
  public float Inertia { get; set; }

  /// <summary>Stored mechanical energy <c>E = ½Iω²</c> in <b>joules</b>. Derived from <see cref="Speed"/> and
  /// <see cref="Inertia"/> each step; kept as a field for the block-info charge readout.</summary>
  public float StoredEnergy { get; set; }

  /// <summary>Power fed into the run this tick, <c>P = τ_drive·ω</c> in <b>watts</b> — for display.</summary>
  public float SupplyPower { get; set; }

  /// <summary>Power drawn from the run this tick, <c>P = τ_load·ω</c> in <b>watts</b> — for display.</summary>
  public float DemandPower { get; set; }

  /// <summary>Which way the run turns. <see cref="Speed"/> stays unsigned because the torque balance is all
  /// magnitudes; direction rides alongside for the machines whose geometry depends on it (see
  /// <see cref="IMpEnergyDirection"/>).</summary>
  public bool Reversed { get; set; }

  /// <summary>Reservoir capacity <c>E_cap = ½·I·ω_max²</c> in joules for the given inertia and burst speed.
  /// Storage contributes <em>inertia</em>; capacity is derived, so a full reservoir is exactly a flywheel
  /// spinning at <c>maxSpeed</c> (the large wheel's larger <c>I</c> is why it stores ~15× the normal one).</summary>
  public static float CapacityFor(float inertia, float maxSpeed) =>
    0.5f * inertia * maxSpeed * maxSpeed;

  /// <summary>Shaft speed for a reservoir holding <paramref name="storedEnergy"/>: <c>ω = √(2E/I)</c>. Zero
  /// when there is no inertia (nothing can store energy, so nothing spins).</summary>
  public static float DeriveSpeed(float storedEnergy, float inertia) =>
    inertia > 0f ? MathF.Sqrt(2f * MathF.Max(0f, storedEnergy) / inertia) : 0f;

  /// <summary>The energy a reservoir holds at a given speed: <c>E = ½Iω²</c> (the inverse of
  /// <see cref="DeriveSpeed"/>).</summary>
  public static float EnergyAtSpeed(float inertia, float speed) =>
    0.5f * inertia * speed * speed;

  /// <summary>
  /// One integration step of the shaft dynamics: <c>ω += (τ_drive − τ_load − τ_fric)/I · dt</c>, clamped to
  /// <c>[0, maxSpeed]</c>. Friction is windage plus a standing-resistance floor: <c>τ_fric = b·ω + τ_idle</c>.
  /// Refreshes <see cref="Speed"/>, <see cref="StoredEnergy"/>, and the display powers.
  /// <para>
  /// Everything the design wants is emergent from the sign of the net torque, no special cases: a drive that
  /// can't beat <c>τ_load + τ_idle</c> at rest never leaves <c>ω = 0</c> (no battery accumulation); sustained
  /// under-torque winds ω to a hard stall; a cut drive lets a charged wheel coast down; and a large <c>I</c>
  /// (a flywheel) barely moves under a brief load spike or between engine strokes (the buffering).
  /// </para>
  /// </summary>
  public static void Step(
    MpEnergyNetworkState s,
    float dt,
    float driveTorque,
    float loadTorque,
    float frictionCoeff,
    float idleTorque,
    float maxSpeed
  )
  {
    // The idle floor is a resistance, so it only ever opposes rotation - never a source. With the ω ≥ 0
    // clamp, a stopped shaft the drive can't start simply stays stopped.
    float frictionTorque = frictionCoeff * s.Speed + MathF.Max(0f, idleTorque);
    float netTorque = driveTorque - loadTorque - frictionTorque;
    float dOmega = s.Inertia > 0f ? netTorque / s.Inertia * dt : 0f;

    s.Speed = Math.Clamp(s.Speed + dOmega, 0f, maxSpeed);
    s.StoredEnergy = EnergyAtSpeed(s.Inertia, s.Speed);
    s.SupplyPower = driveTorque * s.Speed;
    s.DemandPower = loadTorque * s.Speed;
  }

  /// <summary>
  /// Couples two <em>separate</em> runs across a rigid gear of reduction <paramref name="ratio"/>: the north
  /// (output) run is held at <c>ω_south / ratio</c> — a bigger gear on the north shaft, so S→N slows and gains
  /// torque while N→S speeds up. The runs keep their own reservoirs; this projects them onto the gear
  /// constraint <c>ω_north = ω_south / ratio</c> while conserving total kinetic energy (times
  /// <paramref name="retention"/>, a per-tick gear-mesh loss — 1 = lossless). Reflecting the north inertia to
  /// the south side by <c>1/ratio²</c> gives the combined inertia, and the shared speed follows from
  /// <c>½·I_comb·ω_south² = E_total</c>. Energy is conserved exactly when nothing clamps (the test invariant);
  /// clamping <c>ω_south</c> to <paramref name="maxSpeed"/> sheds the surplus (the over-speed governor). Because
  /// <paramref name="ratio"/> ≥ 1, <c>ω_north ≤ ω_south</c>, so the one clamp keeps the constraint. A no-op when
  /// either side has no inertia. Reads speed as the source of truth, so it is robust to a stale
  /// <see cref="StoredEnergy"/>.
  /// </summary>
  public static void CoupleRatio(
    MpEnergyNetworkState south,
    MpEnergyNetworkState north,
    float ratio,
    float maxSpeed,
    float retention = 1f
  )
  {
    if (ratio <= 0f || south.Inertia <= 0f || north.Inertia <= 0f)
      return;

    float total =
      (EnergyAtSpeed(south.Inertia, south.Speed)
        + EnergyAtSpeed(north.Inertia, north.Speed))
      * Math.Clamp(retention, 0f, 1f);
    float combined = south.Inertia + north.Inertia / (ratio * ratio);
    float omegaSouth = MathF.Min(MathF.Sqrt(2f * total / combined), maxSpeed);
    float omegaNorth = omegaSouth / ratio;

    south.Speed = omegaSouth;
    north.Speed = omegaNorth;
    south.StoredEnergy = EnergyAtSpeed(south.Inertia, omegaSouth);
    north.StoredEnergy = EnergyAtSpeed(north.Inertia, omegaNorth);
  }
}
