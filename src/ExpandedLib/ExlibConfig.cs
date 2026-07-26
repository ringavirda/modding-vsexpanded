using ExpandedLib.Registries.Config;

namespace ExpandedLib;

/// <summary>
/// JSON-serializable gameplay tunables owned by the shared library (<c>exlib</c>). Loaded from (and
/// written to) <c>ModConfig/exlib_values.json</c>; the property defaults below apply when the file is
/// missing or a key is absent (and any NaN/infinite/negative value is reset to its default on load).
/// Accessed through the generated <c>ExlibValues</c> accessor, not directly.
/// <para>
/// These are the constants the framework's own code reads - chiefly the block-network layer
/// (<see cref="Blocks.Networks.BlockNetwork"/> subclasses live here now, so their tunables do too).
/// Content-specific numbers stay in each mod's own config (<c>lpex_values.json</c> etc.). All
/// gas/liquid volumes are in <b>litres</b>; molten flow is in canal <b>units</b>.
/// </para>
/// </summary>
[ExConfigRegister(
  "ex_values.json",
  "exlib",
  LegacyFileNames = new string[] { "exlib_values.json" },
  Manageable = true
)]
public class ExlibConfig : IExVersionedConfig
{
  /// <summary>Mod version that last wrote this file. Managed by the config store - do not set by hand.</summary>
  public string? ConfigVersion { get; set; }

  #region Pipe network
  /// <summary>Litres a single pipe holds at 1 atm (both the gas and water pools). A run's capacity is
  /// this times its node count.</summary>
  [ExConfigRange(1, 1_000_000)] // pipe capacity divides pressure - must stay positive
  public float LitresPerPipe { get; set; } = 30f;

  /// <summary>Gas (L/s) bled per open-ended pipe connector (leak) - only the volume above the
  /// network's 1 atm capacity is vented, so a leaking run can never build pressure.</summary>
  public float GasLeakRate { get; set; } = 8.0f;

  /// <summary>Liquid (L/s) drained from the network per open-ended pipe connector (leak).</summary>
  public float LiquidLeakRate { get; set; } = 10.0f;

  /// <summary>Water (L) lost to natural evaporation per in-game day (pipe water pool and the boiler
  /// pool that reuses this rate). 100 L over 2 days = 50 L/day.</summary>
  public float EvaporationLitresPerDay { get; set; } = 50f;

  /// <summary>Seconds a pipe run may sit at its weakest pipe's burst pressure (nowhere to vent)
  /// before a pipe lets go - mirrors the boiler over-pressure grace.</summary>
  public float PipeOverpressureSeconds { get; set; } = 30f;
  #endregion

  #region Molten network
  /// <summary>Max metal (units) flowing across one canal connection per second.</summary>
  public int MoltenFlowRate { get; set; } = 50;

  /// <summary>Minimum metal (units) that must move across a canal connection for any flow that tick
  /// (stops sub-unit dribbles).</summary>
  public int MoltenMinFlowAmount { get; set; } = 10;

  /// <summary>Default VS time-based cooldown speed stamped on a molten carrier stack when a caller
  /// gives none. Each mod may still pass its own rate (e.g. a per-container coefficient).</summary>
  public float MoltenCooldownDefault { get; set; } = 24f;

  /// <summary>Fraction of the melting point above which a metal stack counts as liquid (flows).</summary>
  public float MetalLiquidThreshold { get; set; } = 0.8f;

  /// <summary>Fraction of the melting point below which a metal stack counts as fully hardened
  /// (chisellable).</summary>
  public float MetalHardenedThreshold { get; set; } = 0.3f;

  /// <summary>Below this temperature (°C) hot metal emits no incandescent block light.</summary>
  public float MetalGlowMinTemp { get; set; } = 500f;

  /// <summary>Item code recovered when a molten metal's solid drop cannot be resolved (the shared
  /// recovery fallback, historically <c>iwex:slag</c>). A metal may override it per-entry in its
  /// <c>MetalDef</c>. Harmless when the item is absent - the chisel/break drop guards a null resolve.</summary>
  public string MetalRecoveryFallback { get; set; } = "iwex:slag";
  #endregion

  #region Mechanical-energy network
  // The cast-iron energy MP network (docs/design/mp-energy-network.md) models each run as one spinning shaft:
  // torque on a lumped inertia, dω/dt = (τ_drive − τ_load − τ_fric)/I, with E = 1/2 I ω^2 the stored energy.
  // These are the framework torque/speed constants the MpEnergyNetwork reads; per-flywheel inertia (its
  // capacity and spin-up) is content and lives in each mod's config (iwex's FlywheelInertia*). SI units.

  /// <summary>Windage/bearing friction coefficient <c>b</c> (N·m per rad/s) - the speed-proportional drain, so
  /// an unpowered run winds down. Sets how long a charged flywheel coasts before it needs re-driving.</summary>
  public float MpFrictionCoeff { get; set; } = 0.05f;

  /// <summary>Standing-resistance torque floor <c>τ_idle</c> (N·m) that any drive must beat just to keep the
  /// shaft turning. This (with the load) is the threshold below which a drive never spins the flywheel up -
  /// the "not a battery" gate: no torque over the floor, no accumulation.</summary>
  [ExConfigRange(0, 1000)]
  public float MpIdleTorque { get; set; } = 0.5f;

  /// <summary>Burst shaft speed <c>ω_max</c> (rad/s): reservoir capacity is <c>1/2 I ω_max^2</c>, so this caps
  /// how much energy any inertia can hold and where the governor eases the drive to 0. The weakest flywheel on
  /// a run sets the real ceiling.</summary>
  [ExConfigRange(0.1, 1000)] // capacity scales with its square - must stay positive
  public float MpMaxSpeed { get; set; } = 2f;

  /// <summary>Gear-mesh loss for a transmission coupling, as a fraction of the coupled energy lost <b>per
  /// second</b> (dt-scaled at the coupling tick). A small drain on power crossing a gear train, so chaining
  /// transmissions costs a little; 0 = a lossless (idealised) mesh.</summary>
  [ExConfigRange(0, 1)]
  public float MpGearMeshLoss { get; set; } = 0.02f;
  #endregion
}
