using ExpandedLib.Config;
using System.ComponentModel;

namespace ExpandedLib;

/// <summary>
/// JSON-serializable gameplay tunables owned by the shared library (<c>exlib</c>), read by the framework's
/// own code and chiefly by the block-network layer. Loaded from and written to the <c>exlib</c> section of
/// <c>ModConfig/ex_values.json</c>; the property defaults below apply when the file is missing or a key is
/// absent, and any NaN, infinite or negative value is reset to its default on load. Accessed through the
/// generated <c>ExlibValues</c> accessor, not directly. Content-specific numbers stay in each mod's own
/// config. Gas and liquid volumes are in litres; molten flow is in canal units.
/// </summary>
[ExConfigRegister(
  "ex_values.json",
  "exlib",
  LegacyFileNames = new string[] { "exlib_values.json" },
  Manageable = true
)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ExlibConfig : IExVersionedConfig {
  /// <summary>Mod version that last wrote this file. Managed by the config store - do not set by hand.</summary>
  public string? ConfigVersion { get; set; }

  /// <summary>
  /// Version-driven default resets. A tunable's coded default only reaches an existing install through
  /// one of these: the file already on disk wins otherwise, so correcting a default without a row here
  /// fixes nothing for anyone who has run the mod.
  /// </summary>
  // Empty: the one existing row (0.7.3, MetalRecoveryFallback) went with the field when the slag
  // recovery fallback moved to IiexConfig - it is iiex's own knowledge now, not exlib's, and the field
  // no longer exists here for ResetFields to name.
  public static readonly ExConfigMigration[] Migrations = [];

  #region World
  /// <summary>World ambient reference temperature (°C) used by machine heat models: the temperature cold
  /// feeds enter at and the floor idle machines cool toward. Pipe runs cool toward their own
  /// <see cref="PipeAmbientTemperature"/>.</summary>
  public float AmbientTemperature { get; set; } = 20f;
  #endregion

  #region Pipe network
  /// <summary>Litres a single pipe holds at 1 atm (both the gas and water pools). A run's capacity is
  /// this times its node count.</summary>
  [ExConfigRange(1, 1_000_000)] // pipe capacity divides pressure - must stay positive
  public float LitresPerPipe { get; set; } = 30f;

  /// <summary>Gas (L/s) bled per open-ended pipe connector. Only the volume above the network's 1 atm
  /// capacity is vented, so a leaking run can never build pressure.</summary>
  public float GasLeakRate { get; set; } = 8.0f;

  /// <summary>Liquid (L/s) drained from the network per open-ended pipe connector.</summary>
  public float LiquidLeakRate { get; set; } = 10.0f;

  /// <summary>Water (L) lost to natural evaporation per in-game day (pipe water pool and the boiler
  /// pool that reuses this rate). 100 L over 2 days = 50 L/day.</summary>
  public float EvaporationLitresPerDay { get; set; } = 50f;

  /// <summary>Seconds a pipe run may sit at its weakest pipe's burst pressure with nowhere to vent before
  /// a pipe bursts. Mirrors the boiler over-pressure grace.</summary>
  public float PipeOverpressureSeconds { get; set; } = 30f;

  /// <summary>
  /// Degrees C per second a pipe run's gas sheds toward <see cref="PipeAmbientTemperature"/>. Applies
  /// whenever the run holds gas above ambient, not only when the line is idle. Run length affects cooling
  /// through the volume-weighted blend rather than through this rate: a longer main holds more gas, so an
  /// injected second of hot gas is a smaller fraction of the total. See
  /// <c>docs/design/mechanics/gas-system.md</c>.
  /// </summary>
  public float PipeGasCoolPerSecond { get; set; } = 2.0f;

  /// <summary>Temperature (°C) a pipe run's gas cools toward. Cooling never takes a run below it.</summary>
  public float PipeAmbientTemperature { get; set; } = 20f;
  #endregion

  #region Molten network
  /// <summary>Max metal (units) flowing across one canal connection per second.</summary>
  public int MoltenFlowRate { get; set; } = 50;

  /// <summary>Minimum metal (units) that must move across a canal connection for any flow that tick
  /// (stops sub-unit dribbles).</summary>
  public int MoltenMinFlowAmount { get; set; } = 10;

  /// <summary>Default time-based cooldown speed stamped on a molten carrier stack when a caller gives none.
  /// A caller may still pass its own rate, such as a per-container coefficient.</summary>
  public float MoltenCooldownDefault { get; set; } = 24f;

  /// <summary>Fraction of the melting point above which a metal stack counts as liquid (flows).</summary>
  public float MetalLiquidThreshold { get; set; } = 0.8f;

  /// <summary>Fraction of the melting point below which a metal stack counts as fully hardened
  /// (chisellable).</summary>
  public float MetalHardenedThreshold { get; set; } = 0.3f;

  /// <summary>Below this temperature (°C) hot metal emits no incandescent block light.</summary>
  public float MetalGlowMinTemp { get; set; } = 500f;

  // The recovery-item fallback (a metal's solid drop failing to resolve) moved to IiexConfig -
  // "what to drop instead" is content knowledge, not a framework default; see
  // MetalRegistry.DefaultRecoveryFallback (Industry) and IiexConfig.MetalRecoveryFallback.
  #endregion

  #region Mechanical-energy network
  // The MP energy network models each run as one spinning shaft: torque on a lumped inertia,
  // dω/dt = (τ_drive - τ_load - τ_fric)/I, with stored energy E = 1/2 I ω^2. SI units. These are the
  // framework torque/speed constants MpEnergyNetwork reads; per-flywheel inertia is content and lives in
  // each mod's config. See docs/design/mechanics/mp-energy.md.

  /// <summary>Windage and bearing friction coefficient <c>b</c> (N·m per rad/s): the speed-proportional
  /// drain that winds an unpowered run down, and so how long a charged flywheel coasts.</summary>
  public float MpFrictionCoeff { get; set; } = 0.05f;

  /// <summary>Standing-resistance torque floor <c>τ_idle</c> (N·m) any drive must beat to keep the shaft
  /// turning. With the load it is the threshold below which a drive never spins the flywheel up: no torque
  /// over the floor, no accumulation.</summary>
  [ExConfigRange(0, 1000)]
  public float MpIdleTorque { get; set; } = 0.5f;

  /// <summary>Burst shaft speed <c>ω_max</c> (rad/s). Reservoir capacity is <c>1/2 I ω_max^2</c>, so this
  /// caps how much energy any inertia can hold and marks where the governor eases the drive to 0. The weakest
  /// flywheel on a run sets the effective ceiling.</summary>
  [ExConfigRange(0.1, 1000)] // capacity scales with its square - must stay positive
  public float MpMaxSpeed { get; set; } = 2f;

  /// <summary>Gear-mesh loss for a transmission coupling, as a fraction of the coupled energy lost per
  /// second (dt-scaled at the coupling tick), so chaining transmissions costs a little. 0 is a lossless
  /// mesh.</summary>
  [ExConfigRange(0, 1)]
  public float MpGearMeshLoss { get; set; } = 0.02f;
  #endregion

  #region Diagnostics
  /// <summary>
  /// Whether <c>ExpandedLibModSystem.AssetsFinalize</c> runs <c>ExpandedLib.Checks.ExlibChecks.All</c>
  /// after the catalogues load and logs the results - the content guards a JSON-only mod otherwise
  /// only gets by opening the xUnit harness. Also available on demand with <c>/exmod verify</c>
  /// regardless of this setting. Off saves the one-time scan on a very large modpack's world load.
  /// </summary>
  public bool RunChecksOnLoad { get; set; } = true;
  #endregion
}
