using ExpandedLib.Config;
using Vintagestory.API.Common;

namespace SteelIndustryExpanded;

/// <summary>
/// JSON-serializable gameplay tunables for Steel Industry Expanded: the balance numbers for its
/// machines, the high-pressure steam leaves and the molten/gas systems. Held in the <c>siex</c>
/// section of <c>ModConfig/ex_values.json</c>. The property defaults apply when the file or a key is
/// missing, and NaN, infinite or negative values are reset to their defaults on load. Accessed
/// through <see cref="SiexValues"/>, not directly.
/// <para>
/// <c>LegacySectionIds</c> carries the tuning of the two mods this one merges. A section is keyed by
/// mod id, so without it every value a player had tuned under <c>smex</c> would revert to its coded
/// default on the first load - silently, since a missing section is indistinguishable from a fresh
/// install. smex reached players at 0.9.8; hpex never shipped, and is listed for the developer
/// configs that do carry it.
/// </para>
/// </summary>
[ExConfigRegister(
  "ex_values.json",
  "siex",
  LegacyFileNames = new string[] { "smex_values.json", "smex.json" },
  LegacySectionIds = new string[] { "smex", "hpex" },
  Manageable = true
)]
public class SiexConfig : IExVersionedConfig {
  /// <summary>Mod version that last wrote this file; drives the <see cref="Migrations"/> resets.
  /// Managed by <see cref="ExConfigRegister{TConfig}"/> - do not set by hand.</summary>
  public string? ConfigVersion { get; set; }

  /// <summary>
  /// Version-driven default resets. Upgrading across one of these versions forces the listed fields
  /// back to their defaults, discarding saved tuning for those keys only. Add an entry per release
  /// whose rebalanced defaults must reach existing configs; use <c>nameof</c> for the field names.
  /// An entry with no <c>ResetFields</c> resets the whole config.
  /// <para>
  /// The pre-merge rows below still fire despite siex starting at 0.9.9: a section carried over by
  /// <c>LegacySectionIds</c> brings its own <c>ConfigVersion</c>, so a config last written by smex
  /// 0.9.3 crosses the 0.9.5 row on its first siex load. They are dead only for a fresh install.
  /// </para>
  /// </summary>
  public static readonly ExConfigMigration[] Migrations =
  [
    // 0.9.0: the gas-volume rebalance retuned the bessemer blast draw (1 -> 8 L/s) and the
    // smoke-stack vent rate (4 -> 48 L/s). No FromVersion, so unversioned files are caught too.
    new()
    {
      ToVersion = "0.9.0",
      ResetFields =
      [
        nameof(BessemerBlastPerSecond),
        nameof(SmokestackGasIntakeVolume),
      ],
    },
    // 0.9.2: the converter vessel costs one smithable large gear (8 rods) instead of 4 rusty gears
    // (12 rods), and the air blower scales off absolute engine power, retuning its base output
    // (16 -> 48 L/s).
    new()
    {
      ToVersion = "0.9.2",
      ResetFields =
      [
        nameof(BessemerRequiredGears),
        nameof(BessemerRequiredRods),
        nameof(AirBlowerOutputPerSecond),
      ],
    },
    // 0.9.5: the Bessemer converter's fixed blow-time/hold-temperature refine was replaced with the
    // dynamic carbon model (autothermal heat balance, over-blow, cold-scrap temp gate, slag,
    // split-pour), and the vessel was enlarged to at least 2 large billets. The retired
    // BessemerProcessDuration/BessemerProcessTemperature keys drop out of the POCO; the capacity is
    // reset so existing configs run the reworked machine.
    new()
    {
      ToVersion = "0.9.5",
      ResetFields = [nameof(BessemerConverterCapacity)],
    },
  ];

  // The molten-metal system tunables (cooldown rates, canal flow, default capacities, canal-seal clay
  // costs) live in IronIndustryExpanded.IiexConfig / IiexValues alongside the molten subsystem itself.

  #region Hopper feed (reinforced tank + bell drip)
  // The reinforced hopper is a plain charge tank; iiex's burdenmaker makes the burden. Its buffer is
  // small next to the tall hopper's 128 because it is meant to be fed by the skip hoist.
  /// <summary>Burden units the reinforced hopper tank holds.</summary>
  public int HopperReinforcedCapacity { get; set; } = 48;

  /// <summary>Burden the bell hopper's magazine can buffer below the reinforced tank.</summary>
  public int HopperMaxMagazineCapacity { get; set; } = 48;

  /// <summary>Burden dropped into the shaft per output pulse.</summary>
  public int HopperDropAmount { get; set; } = 4;
  #endregion

  #region Bessemer converter
  /// <summary>Seconds the pour/fill lever must be held before the converter commits the action.</summary>
  public float BessemerPourHoldSeconds { get; set; } = 1f;

  /// <summary>Large gears consumed to spawn the converter vessel.</summary>
  public int BessemerRequiredGears { get; set; } = 1;

  /// <summary>Iron/steel rods consumed to spawn the converter vessel.</summary>
  public int BessemerRequiredRods { get; set; } = 8;
  #endregion

  #region Air blower / blast
  /// <summary>Pressure (atm) at or above which air in a pipe network counts as "blast".</summary>
  public float BlastPressureThreshold { get; set; } = 2.5f;

  /// <summary>Air (L/s) the blower injects per unit of engine power (Cornish 0.2/0.4/0.8 →
  /// 9.6/19.2/38.4 L/s, Watt 0.3 → 14.4 L/s); output pressure tracks the engine's inlet steam ×
  /// <see cref="IronIndustryExpanded.IiexValues.SteamEngineEfficiency"/>.</summary>
  public float AirBlowerOutputPerSecond { get; set; } = 48f;
  #endregion

  #region Cowper stove
  /// <summary>Cap (°C) on the cowper stove's internal regenerator temperature.</summary>
  public float CowperMaxTemperature { get; set; } = 1240f;

  /// <summary>Per-second heat-soak rate when an anthracite coal pile burns below the stove.</summary>
  public float CowperHeatingSpeedAnthracite { get; set; } = 0.0064f;

  /// <summary>Per-second heat-soak rate when a non-anthracite coal pile burns below the stove.</summary>
  public float CowperHeatingSpeedOtherCoal { get; set; } = 0.0048f;

  /// <summary>Per-second heat-soak rate with no coal pile below the stove.</summary>
  public float CowperHeatingSpeedDefault { get; set; } = 0.0012f;

  /// <summary>Per-second rate the soaked-up exhaust gives its heat to the regenerator.</summary>
  public float CowperCoolingSpeedExhaust { get; set; } = 0.3f;

  /// <summary>Per-second rate the regenerator loses heat into the air it reheats into hot blast.</summary>
  public float CowperCoolingSpeedAir { get; set; } = 0.0012f;

  /// <summary>Fraction of the incoming exhaust temperature the stove's outlet exhaust keeps after the
  /// regenerator has soaked up its heat (the rest went into the brick core).</summary>
  public float CowperExhaustAttenuation { get; set; } = 0.4f;

  /// <summary>Gas (L/s) drawn each tick from each intake: the furnace exhaust the stove soaks heat
  /// from, and the air it reheats into hot blast.</summary>
  public float CowperIntakeVolume { get; set; } = 24f;
  #endregion

  #region Bessemer converter
  /// <summary>Molten-metal capacity (units) of the converter vessel. Sized for at least two large
  /// billets (2×2400 u), so a full pig charge blows to two billets of steel.</summary>
  public int BessemerConverterCapacity { get; set; } = 4800;

  /// <summary>Blast (L/s) the converter draws from its gas intake while blowing.</summary>
  public float BessemerBlastPerSecond { get; set; } = 8.0f;

  // --- Dynamic carbon model (the autothermal blow) -------------------------------------------------
  // The charge carries a per-unit carbon fraction and the blow oxidises it, so carbon falls
  // monotonically while blowing and the product depends on when the blow stops: high carbon is still
  // pig, the target is Bessemer steel, past it (over-blow) is soft ingot iron. Carbon stands in for
  // total oxidisable content, since real Bessemer heat is silicon-dominated. Acid process: no flux,
  // self-forming siliceous slag. See docs/design/machines/bessemer.md.

  /// <summary>Carbon fraction of a fresh molten-pig charge (materials.md: pig ~4.0 % C).</summary>
  public float BessemerPigCarbonStart { get; set; } = 0.04f;

  /// <summary>Carbon fraction at which the charge becomes Bessemer steel (materials.md: ~0.2 % C).</summary>
  public float BessemerSteelCarbonTarget { get; set; } = 0.002f;

  /// <summary>Carbon fraction below which the blow has over-blown the heat to soft iron (~0 % C):
  /// the charge retypes to <c>game:ingot-iron</c>, the plain-iron route.</summary>
  public float BessemerOverblowCarbon { get; set; } = 0.0005f;

  /// <summary>Carbon fraction burned out per litre of blast that reaches the bath - the decarburisation
  /// rate that sets the blow length (a full ~4800 u charge at 8 L/s blows in ~5 min).</summary>
  public float BessemerCarbonPerBlastLitre { get; set; } = 0.000016f;

  // --- Autothermal heat balance (T_process = T_in − T_loss, shared exlib helper) --------------------

  /// <summary>Autothermal T_in floor (°C): the heat the molten pig arrives with, before the blow adds
  /// any. T_in = this + <see cref="BessemerHeatPerCarbonUnit"/> × (blast reaching the bath).</summary>
  public float BessemerAutothermalBase { get; set; } = 1250f;

  /// <summary>Autothermal heat (°C) the oxidising charge contributes to T_in at full blast - the
  /// carbon/silicon burn that makes the process need no external fuel. Scaled by how much blast
  /// actually reaches the bath. The post-blow peak is (base + this − radiation), about 1800 °C at the
  /// shipped values.</summary>
  public float BessemerHeatPerCarbonUnit { get; set; } = 600f;

  /// <summary>Ceiling (°C) on autothermal T_in, so a retuned heat term can't run the bath arbitrarily hot.</summary>
  public float BessemerAutothermalCeiling { get; set; } = 2000f;

  /// <summary>Radiation/ambient heat loss (°C) off the open vessel mouth - the always-on term of T_loss.</summary>
  public float BessemerRadiationLoss { get; set; } = 50f;

  /// <summary>Process floor (°C), the steel liquidus: the blow refines only while T_process is at or
  /// above this. Cold scrap that pushes T_process below it stalls the blow; more scrap still freezes
  /// the bath.</summary>
  public float BessemerRefineTemperature { get; set; } = 1500f;

  // --- Cold steel-scrap charge (the temperature gate) ----------------------------------------------
  // Optional cold steel bits (game:metalbit-steel, classified via the exlib Scrap role) are charged
  // alongside the pig and add cold mass to T_loss, so more scrap means a lower T_process. There is no
  // hardcoded scrap cap: past the ceiling the bath cannot hold the refine floor and the heat stalls or
  // freezes (conventions.md). The coefficient is tuned so 15-20 % cold scrap on a full charge is the
  // practical ceiling, the acid-Bessemer figure.

  /// <summary>Heat loss (°C) added to T_loss per unit of cold scrap in the vessel. At the shipped value a
  /// full 4800 u charge stalls the blow around ~850 u scrap (~15-18 %).</summary>
  public float BessemerColdScrapLossCoefficient { get; set; } = 0.35f;

  /// <summary>Molten units one cold steel bit adds when charged (a metal bit is 5 u, like the recovery drop).</summary>
  public int BessemerScrapUnitValue { get; set; } = 5;

  /// <summary>Fraction of cold-scrap mass that becomes steel (the rest is oxidation loss). Per 100 u
  /// scrap → ~97 u steel + ~3 u loss, negligible slag (materials.md scrap recovery).</summary>
  [ExConfigRange(0, 1)]
  public float BessemerScrapSteelYield { get; set; } = 0.97f;

  // --- Mass balance (R2: steel + slag ≤ input) -----------------------------------------------------
  // Per 100 u pig → 90 u molten steel + 6 u molten slag (the same iiex:slag the furnaces make) + 4 u
  // gas from the burned-off carbon. Slag accumulates into its own pool during the blow.

  /// <summary>Fraction of pig mass that becomes steel across the blow (materials.md).</summary>
  [ExConfigRange(0, 1)]
  public float BessemerSteelYield { get; set; } = 0.90f;

  /// <summary>Fraction of pig mass that becomes molten slag across the blow; the remainder (1 − steel −
  /// slag) is gas that leaves the bath.</summary>
  [ExConfigRange(0, 1)]
  public float BessemerSlagYield { get; set; } = 0.06f;

  /// <summary>Pour rate (units/second) draining the vessel through the output cell. A full 4800 u
  /// steel charge drains in ~110 s, leaving margin inside the ~3-4 min liquid window to tilt
  /// slag→steel and work the canal valves.</summary>
  public float BessemerPourRate { get; set; } = 44f;

  /// <summary>Minimum geared mechanical speed for the converter to count as powered.</summary>
  public float BessemerPowerSpeedThreshold { get; set; } = 0.1f;

  /// <summary>Multiplier on the converter charge's cooldown speed, against the base molten-system rate
  /// <c>IiexValues.MoltenCooldownSpeed</c>. Below 1 the bath holds its heat longer (0.5 is half the
  /// molten-system rate, so it cools twice as slowly).</summary>
  public float BessemerCooldownCoefficient { get; set; } = 0.5f;

  /// <summary>Fraction of the converter capacity below which a hardened (cooled) charge can be chiselled
  /// out of the vessel instead of breaking the whole structure. A residue at or above this is salvaged
  /// by breaking it.</summary>
  [ExConfigRange(0, 1)]
  public float BessemerChiselMaxFraction { get; set; } = 0.2f;

  /// <summary>Fraction (0..1) of a right-click-constructed machine's materials recovered when it is
  /// broken intact - the converter vessel, the Lancashire boiler and the Cornish engine. Registered
  /// per-domain into exlib's <c>ExRccSettings</c>, which keys on the broken block's
  /// <c>Code.Domain</c>, so one domain means one ratio. Applied live on the next break.</summary>
  [ExConfigRange(0, 1)]
  public float RccBrokenDropsRatio { get; set; } = 0.8f;
  #endregion

  #region Smoke stack
  /// <summary>Exhaust gas (L/s) the smoke stack vents from the network.</summary>
  public float SmokestackGasIntakeVolume { get; set; } = 48.0f;
  #endregion

  #region Lancashire boiler
  // The variant stat table the Lancashire leaf overrides on iiex's BlockEntityBoiler. The shared FSM
  // knobs stay in IiexConfig.

  /// <summary>Total internal capacity (L) shared between water and steam.</summary>
  public float LancashireBoilerCapacity { get; set; } = 1200f;

  /// <summary>Minimum water (L) needed before the boiler will begin heating/boiling.</summary>
  public float LancashireBoilerMinBoilWater { get; set; } = 200f;

  /// <summary>Maximum water (L) the boiler will hold/boil - the rest of the capacity is reserved for steam.</summary>
  public float LancashireBoilerMaxBoilWater { get; set; } = 800f;

  /// <summary>Steam (L/s) produced while boiling at full tilt (consumes this divided by iiex's
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
  /// Burst pressure (atm) of a rolled Hadfield-steel pipe segment - the top pipe tier, and the only
  /// one that can carry the Cornish engine's output. The weakest segment limits a run, so one plated
  /// or cast segment spliced into an HP main drags the whole run down to its rating.
  /// </summary>
  public float RolledPipeBurstPressure { get; set; } = 12f;

  /// <summary>Throughput (L/s) of a plain rolled pipe segment; the weakest segment caps a run. Sized
  /// to clear the heavy hot blast (~160 L/s), which neither plated (50) nor cast (120) passes. Rated
  /// as throughput, not bore.</summary>
  public float RolledPipeThroughput { get; set; } = 250f;
  #endregion

  #region Recipe balance
  /// <summary>Active recipe cost level - <c>"normal"</c> or <c>"cheap"</c> - for everything this mod
  /// ships, the steel line and the high-pressure machines alike. Toggled in-game by
  /// <c>/exmod recipes siex &lt;level&gt;</c>; the per-recipe numbers live in the separate
  /// <see cref="SiexRecipeConfig"/> catalogue. Applied on the next world reload.</summary>
  public string RecipeLevel { get; set; } = "normal";
  #endregion
}
