namespace ExpandedLib.Networks;

/// <summary>
/// A node that <b>drives</b> a mechanical-energy run — an engine generator, or the flywheel's vanilla-MP
/// bridge reading torque off the vanilla network. It applies a torque that spins the run's inertia up; the
/// network sums every driver's torque each tick and integrates it against the load and friction.
/// </summary>
public interface IMpEnergyProducer
{
  /// <summary>The drive torque (N·m) this node applies at the run's current shaft speed
  /// <paramref name="speed"/> (rad/s). A real drive follows a torque–speed curve: high torque at rest so it
  /// can start a load, easing toward 0 near its rated/governed speed. Returning a curve (not a flat power) is
  /// what lets an under-powered drive <b>stall</b> a load rather than slowly buffer its way past it.</summary>
  float DriveTorque(float speed);
}

/// <summary>
/// A node that <b>stores</b> energy — a flywheel (the signature block) or the small inherent inertia of a
/// cast-iron shaft/gear. It contributes <see cref="Inertia"/>; capacity is derived from inertia and the burst
/// speed (<c>E_cap = ½Iω_max²</c>), and the inertia is also what buffers the run against torque transients.
/// </summary>
public interface IMpEnergyStorage
{
  /// <summary>Rotational inertia (kg·m²) this node adds to the run — sets its capacity, its spin-up time, and
  /// how hard it resists a change in shaft speed (the buffering).</summary>
  float Inertia { get; }
}

/// <summary>
/// A node that <b>loads</b> the run — a heavy machine (rolling pass, hammer blow, crusher). While it is
/// working it imposes a resisting torque; the shaft can only turn (and the op only advance) while the drive +
/// flywheel out-torque it. If the load drags the shaft to a stop the op jams mid-cut — no energy is granted
/// "over time", so an under-powered run simply stalls.
/// </summary>
public interface IMpEnergyConsumer
{
  /// <summary>The resisting torque (N·m) this machine imposes at the run's current shaft speed
  /// <paramref name="speed"/> (rad/s), or 0 when idle. May scale with state (a cold rolling pass has higher
  /// flow stress → more torque → it drags the shaft down and stalls the mill — the "keep it hot" coupling).</summary>
  float LoadTorque(float speed);
}

/// <summary>
/// A node that knows which <b>way</b> the run turns. Speed itself is deliberately unsigned - the whole torque
/// balance is magnitudes, and making omega signed would complicate every relation for the sake of one machine -
/// so direction rides alongside it as a flag, set by whatever drives the run.
/// <para>
/// It exists for machines whose <em>geometry</em> depends on rotation rather than just its rate. The rolling
/// mill is the case in point: its feed side follows the rolls, so reversing the drive swaps which deck you feed
/// and which one the piece lands on - which is exactly why a reversing mill was worth building historically.
/// </para>
/// </summary>
public interface IMpEnergyDirection
{
  /// <summary>True when the run turns in reverse.</summary>
  bool IsReversed { get; }
}
