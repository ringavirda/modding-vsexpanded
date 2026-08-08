using ExpandedLib.Registries.Config;
using Vintagestory.API.Common;

namespace HighPressureExpanded;

/// <summary>
/// JSON-serializable gameplay tunables for High Pressure Expanded - the two high-pressure leaves
/// (Lancashire boiler, Cornish engine). Registered as the <c>hpex</c> section of the shared
/// <c>ModConfig/ex_values.json</c>; the property defaults below apply when the file is missing or a
/// key is absent (and any NaN/infinite/negative value is reset to its default on load). Accessed
/// through <see cref="HpexValues"/>, not directly.
/// <para>
/// Everything the two leaves inherit - the boiler FSM (heat-up, choke, shutdown, exhaust, water
/// intake, lid vent, explosion blast/drop rules) and the engine FSM (efficiency, over-pressure
/// seconds, MP rating, pump throughput) - is owned by the <c>lpex</c> bases and stays in
/// <c>LpexConfig</c>. What lives here is only the per-variant stat table the HP leaves override.
/// </para>
/// <para>
/// All gas/liquid volumes are in <b>litres</b> (matching vanilla liquid containers). Pressure is
/// a dimensionless ratio (volume / capacity), expressed in atm.
/// </para>
/// </summary>
[ExConfigRegister("ex_values.json", "hpex", Manageable = true)]
public class HpexConfig : IExVersionedConfig
{
  /// <summary>Mod version that last wrote this file; drives the <see cref="Migrations"/> resets.
  /// Managed by <see cref="ExConfigRegister{TConfig}"/> - do not set by hand.</summary>
  public string? ConfigVersion { get; set; }

  /// <summary>
  /// Version-driven default resets. When a player upgrades across one of these versions the listed
  /// values are forced back to the defaults above, discarding their saved tuning for just those keys
  /// (everything else is preserved). Add an entry per release that rebalances values you want pushed
  /// out to existing configs; use <c>nameof</c> for the field names. An entry with no
  /// <c>ResetFields</c> resets the whole config.
  /// </summary>
  public static readonly ExConfigMigration[] Migrations = [];

  #region Lancashire boiler
  // The variant stat table the Lancashire leaf overrides on lpex's BlockEntityBoiler. These carried
  // the un-prefixed `Boiler*` names while the boiler lived in lpex (they were that config's "base"
  // block, with the Cornish variant prefixed); prefixed here to match `CornishBoiler*` now that each
  // variant owns its own section. The shared FSM knobs stay in LpexConfig.

  /// <summary>Total internal capacity (L) shared between water and steam.</summary>
  public float LancashireBoilerCapacity { get; set; } = 1200f;

  /// <summary>Minimum water (L) needed before the boiler will begin heating/boiling.</summary>
  public float LancashireBoilerMinBoilWater { get; set; } = 200f;

  /// <summary>Maximum water (L) the boiler will hold/boil - the rest of the capacity is reserved for steam.</summary>
  public float LancashireBoilerMaxBoilWater { get; set; } = 800f;

  /// <summary>Steam (L/s) produced while boiling at full tilt (consumes this divided by lpex's
  /// <c>SteamExpansionFactor</c> litres of water).</summary>
  public float LancashireBoilerSteamPerSecond { get; set; } = 48f;

  /// <summary>Steam pressure (atm) the Lancashire boiler chokes at - it stops pushing steam above
  /// this on the outlet network. The high band the Cornish engine needs.</summary>
  public float LancashireBoilerMaxOutputPressure { get; set; } = 12.0f;

  /// <summary>Block radius damaged when the Lancashire boiler explodes.</summary>
  public int LancashireBoilerExplosionRadius { get; set; } = 4;
  #endregion

  #region Cornish engine
  /// <summary>Inlet pressure (atm) at/above which the Cornish engine runs, at the low / normal /
  /// high control-rod settings. The throttle raises the whole operating band: low works on a
  /// gentle 5-8 atm, normal on 6-8 atm, high demands a hot 7-8 atm.</summary>
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

  /// <summary>When the Cornish engine is overclocked (high throttle) its running sounds are
  /// scaled by these - louder strokes/hum and a lower, more violent gear growl. Normal and low
  /// settings are left at 1 (unchanged).</summary>
  public float CornishEngineOverclockVolume { get; set; } = 1.8f;
  public float CornishEngineOverclockPitch { get; set; } = 0.8f;
  #endregion

  #region Pipes
  /// <summary>
  /// Burst pressure (atm) of a rolled (hpex) Hadfield-steel pipe segment - the top pipe tier, and the
  /// only one that can carry the Cornish engine's output. The weakest pipe limits a run, so one plated
  /// or cast segment spliced into an HP main drags the whole run down to its rating.
  /// <para>
  /// The rating must be registered: without one the rolled tier has no strength advantage over cast
  /// and the HP pipe is cosmetic.
  /// </para>
  /// </summary>
  public float RolledPipeBurstPressure { get; set; } = 12f;

  /// <summary>Throughput (L/s) of a plain rolled (hpex) pipe segment - the weakest segment caps a run.
  /// First-pass calibration: 250 clears hpex's own planned heavy blast (~160 L/s),
  /// which neither plated (50) nor cast (120) will pass - so the top tier has a service only it can
  /// carry. Throughput, not bore.</summary>
  public float RolledPipeThroughput { get; set; } = 250f;
  #endregion

  #region Construction
  /// <summary>Fraction (0..1) of an HP machine's construction materials recovered when it is mined
  /// intact - the right-click-construction salvage ratio. Registered per-domain into exlib's
  /// <c>ExRccSettings</c>, so hpex needs its own copy (the lookup keys on the broken block's
  /// <c>Code.Domain</c>). Player-tunable; applied live on the next break.</summary>
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
