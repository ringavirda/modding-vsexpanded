namespace ExpandedLib.Industry.MechanicalPower;

/// <summary>
/// A node that drives a mechanical-energy run, such as an engine generator or the flywheel's bridge to
/// the vanilla MP network. The network sums every driver's torque each tick and integrates it against
/// the load and friction.
/// </summary>
public interface IMpEnergyProducer {
  /// <summary>Drive torque (N·m) applied at the run's current shaft speed <paramref name="speed"/>
  /// (rad/s). Implementations follow a torque-speed curve: high torque at rest so a load can be
  /// started, easing toward 0 near the rated speed. A curve rather than flat power is what lets an
  /// under-powered drive stall a load instead of eventually reaching it.</summary>
  float DriveTorque(float speed);
}

/// <summary>
/// A node that stores energy: a flywheel, or the small inherent inertia of a cast-iron shaft or gear.
/// It contributes <see cref="Inertia"/>, from which the run's capacity follows as
/// <c>E_cap = ½Iω_max²</c>, and which buffers the run against torque transients.
/// </summary>
public interface IMpEnergyStorage {
  /// <summary>Rotational inertia (kg·m²) this node adds to the run. Sets its capacity, its spin-up
  /// time, and how hard it resists a change in shaft speed.</summary>
  float Inertia { get; }
}

/// <summary>
/// A node that loads the run: a heavy machine such as a rolling pass, hammer or crusher. While working
/// it imposes a resisting torque, and its operation advances only while the drive and flywheel
/// out-torque it. If the load drags the shaft to a stop the operation jams where it stands; no energy
/// accrues over time, so an under-powered run stalls.
/// </summary>
public interface IMpEnergyConsumer {
  /// <summary>Resisting torque (N·m) imposed at the run's current shaft speed <paramref name="speed"/>
  /// (rad/s), or 0 when idle. May scale with machine state, for example a cold rolling pass whose
  /// higher flow stress raises the torque enough to stall the mill.</summary>
  float LoadTorque(float speed);
}

/// <summary>
/// A node that knows which way the run turns. Shaft speed is unsigned because the torque balance works
/// in magnitudes, so direction rides alongside it as a flag set by whatever drives the run. It serves
/// machines whose geometry depends on the direction rather than the rate, such as a rolling mill whose
/// feed side follows the rolls.
/// </summary>
public interface IMpEnergyDirection {
  /// <summary>True when the run turns in reverse.</summary>
  bool IsReversed { get; }
}
