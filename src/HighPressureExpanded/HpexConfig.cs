using ExpandedLib.Registries.Config;
using Vintagestory.API.Common;

namespace HighPressureExpanded;

/// <summary>
/// JSON-serializable gameplay tunables for High Pressure Expanded (Lancashire boiler, Cornish engine),
/// registered as the <c>hpex</c> section of the shared <c>ModConfig/ex_values.json</c> and read through
/// <see cref="HpexValues"/> rather than directly. The property defaults apply when the file or a key is
/// missing; NaN, infinite and negative values are reset to their default on load. Only the per-variant
/// stats the HP leaves override live here - the boiler and engine FSM knobs both leaves inherit stay in
/// <c>LpexConfig</c>. Volumes are in litres, pressure in atm (volume / capacity).
/// </summary>
[ExConfigRegister("ex_values.json", "hpex", Manageable = true)]
public class HpexConfig : IExVersionedConfig {
  /// <summary>Mod version that last wrote this file; drives the <see cref="Migrations"/> resets.
  /// Managed by <see cref="ExConfigRegister{TConfig}"/> - do not set by hand.</summary>
  public string? ConfigVersion { get; set; }

  /// <summary>
  /// Version-driven default resets. On an upgrade across a listed version the named fields are forced
  /// back to their defaults, discarding saved tuning for those keys only. Field names are given with
  /// <c>nameof</c>; an entry with no <c>ResetFields</c> resets the whole config.
  /// </summary>
  public static readonly ExConfigMigration[] Migrations = [];

  #region Lancashire boiler
  // The variant stat table the Lancashire leaf overrides on lpex's BlockEntityBoiler. The shared FSM
  // knobs stay in LpexConfig.

  /// <summary>Total internal capacity (L) shared between water and steam.</summary>
  public float LancashireBoilerCapacity { get; set; } = 1200f;

  /// <summary>Minimum water (L) needed before the boiler will begin heating/boiling.</summary>
  public float LancashireBoilerMinBoilWater { get; set; } = 200f;

  /// <summary>Maximum water (L) the boiler will hold/boil - the rest of the capacity is reserved for steam.</summary>
  public float LancashireBoilerMaxBoilWater { get; set; } = 800f;

  /// <summary>Steam (L/s) produced while boiling at full tilt (consumes this divided by lpex's
  /// <c>SteamExpansionFactor</c> litres of water).</summary>
  public float LancashireBoilerSteamPerSecond { get; set; } = 48f;

  /// <summary>Steam pressure (atm) at which the Lancashire boiler chokes and stops pushing steam onto
  /// the outlet network. Set to the high band the Cornish engine needs.</summary>
  public float LancashireBoilerMaxOutputPressure { get; set; } = 12.0f;

  /// <summary>Block radius damaged when the Lancashire boiler explodes.</summary>
  public int LancashireBoilerExplosionRadius { get; set; } = 4;
  #endregion

  #region Cornish engine
  /// <summary>Inlet pressure (atm) at or above which the Cornish engine runs, at the low / normal /
  /// high control-rod settings. A higher setting raises the whole operating band: 5-8 atm at low,
  /// 6-8 at normal, 7-8 at high.</summary>
  public float CornishEngineEngagePressureLow { get; set; } = 5.0f;
  public float CornishEngineEngagePressureNormal { get; set; } = 6.0f;
  public float CornishEngineEngagePressureHigh { get; set; } = 7.0f;

  /// <summary>Inlet pressure (atm) above which the Cornish engine wears toward a break, at the
  /// low / normal / high control-rod settings.</summary>
  public float CornishEngineBreakPressureLow { get; set; } = 8.0f;
  public float CornishEngineBreakPressureNormal { get; set; } = 8.0f;
  public float CornishEngineBreakPressureHigh { get; set; } = 8.0f;

  /// <summary>Nominal power the Cornish engine delivers at the normal control-rod setting (display reference).</summary>
  public float CornishEngineMaxPower { get; set; } = 1.0f;

  /// <summary>Cornish engine steam draw (L/s) at the low / normal / high control-rod settings.</summary>
  public float CornishEngineSteamLow { get; set; } = 8f;
  public float CornishEngineSteamNormal { get; set; } = 16f;
  public float CornishEngineSteamHigh { get; set; } = 32f;

  /// <summary>Cornish engine power at the low / normal / high control-rod settings.</summary>
  public float CornishEnginePowerLow { get; set; } = 0.2f;
  public float CornishEnginePowerNormal { get; set; } = 0.4f;
  public float CornishEnginePowerHigh { get; set; } = 0.8f;

  /// <summary>Cornish engine condensed-water output (L/s) at the low / normal / high settings.</summary>
  public float CornishEngineWaterLow { get; set; } = 0.3f;
  public float CornishEngineWaterNormal { get; set; } = 0.6f;
  public float CornishEngineWaterHigh { get; set; } = 1.2f;

  /// <summary>Volume and pitch scaling applied to the Cornish engine's running sounds at the high
  /// control-rod setting. The low and normal settings stay at 1.</summary>
  public float CornishEngineOverclockVolume { get; set; } = 1.8f;
  public float CornishEngineOverclockPitch { get; set; } = 0.8f;
  #endregion

  #region Pipes
  /// <summary>
  /// Burst pressure (atm) of a rolled (hpex) Hadfield-steel pipe segment - the top pipe tier, and the
  /// only one that can carry the Cornish engine's output. The weakest segment limits a run, so one
  /// plated or cast segment spliced into an HP main drags the whole run down to its rating.
  /// </summary>
  public float RolledPipeBurstPressure { get; set; } = 12f;

  /// <summary>Throughput (L/s) of a plain rolled (hpex) pipe segment; the weakest segment caps a run.
  /// Sized to clear hpex's heavy blast (~160 L/s), which neither plated (50) nor cast (120) passes.
  /// Rated as throughput, not bore.</summary>
  public float RolledPipeThroughput { get; set; } = 250f;
  #endregion

  #region Construction
  /// <summary>Fraction (0..1) of an HP machine's construction materials recovered when it is mined
  /// intact - the right-click-construction salvage ratio. Registered per-domain into exlib's
  /// <c>ExRccSettings</c>, which keys on the broken block's <c>Code.Domain</c>, so hpex needs its own
  /// copy. Player-tunable; applied on the next break.</summary>
  [ExConfigRange(0, 1)]
  public float RccBrokenDropsRatio { get; set; } = 0.8f;
  #endregion

  #region Recipe balance
  /// <summary>Active HP-machine recipe cost level - <c>"normal"</c> or <c>"cheap"</c>. Toggled
  /// in-game by <c>/exmod recipes hpex &lt;level&gt;</c>; the per-recipe numbers live in the
  /// separate <c>hpex</c> recipe catalogue. Applied on the next world reload.</summary>
  public string RecipeLevel { get; set; } = "normal";
  #endregion
}
