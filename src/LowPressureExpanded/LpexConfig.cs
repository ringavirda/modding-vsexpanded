using ExpandedLib.Registries.Config;
using Vintagestory.API.Common;

namespace LowPressureExpanded;

/// <summary>
/// JSON-serializable gameplay tunables for Low Pressure Expanded, read from and written to the
/// <c>lpex</c> section of <c>ModConfig/ex_values.json</c>. The property defaults apply when the file
/// or a key is missing; NaN, infinite and negative values are reset to their default on load.
/// Accessed through <see cref="LpexValues"/>, not directly.
/// <para>
/// Gas and liquid volumes are in litres, matching vanilla liquid containers. Pressure is a
/// dimensionless volume/capacity ratio expressed in atm.
/// </para>
/// </summary>
[ExConfigRegister(
  "ex_values.json",
  "lpex",
  LegacyFileNames = new string[] { "lpex_values.json", "lpex.json" },
  Manageable = true
)]
public class LpexConfig : IExVersionedConfig {
  /// <summary>Mod version that last wrote this file; drives the <see cref="Migrations"/> resets.
  /// Managed by <see cref="ExConfigRegister{TConfig}"/> - do not set by hand.</summary>
  public string? ConfigVersion { get; set; }

  /// <summary>
  /// Version-driven default resets. Upgrading across one of these versions forces the listed fields
  /// back to their defaults and leaves every other saved value untouched; an entry with no
  /// <c>ResetFields</c> resets the whole config. Field names are given with <c>nameof</c>. One entry
  /// per release that rebalances values existing configs must pick up.
  /// </summary>
  public static readonly ExConfigMigration[] Migrations =
  [
    // 0.6.0: the engine fluid pump scales off absolute engine power; base throughput retuned
    // from 5 to 16.67 L/s.
    new() { ToVersion = "0.6.0", ResetFields = [nameof(PumpWaterPerSecond)] },
  ];

  #region Pipes
  // The base pipe block, the "pipe" network registration and the vanilla-chimney draw rate live in
  // iwex, read through IwexValues; the generic pipe-network constants (LitresPerPipe, leak rates)
  // live in ExlibValues. This region holds only lpex content: the cast pipe tier and the
  // water/steam phase point.

  /// <summary>Burst pressure (atm) of a plain cast (lpex) pipe segment - the weakest pipe limits a run.
  /// The plated (iwex) and rolled (hpex) tiers register their own ratings.</summary>
  public float CastPipeBurstPressure { get; set; } = 5.0f;

  /// <summary>Throughput (L/s) of a plain cast (lpex) pipe segment - the weakest segment caps a run.
  /// Pipes are rated by throughput, not bore. 120 passes the flows the plated tier's 50 refuses,
  /// such as a converter blast main or a cowper hot-blast run. First-pass calibration; see
  /// <c>IwexValues.PlatedPipeThroughput</c>.</summary>
  public float CastPipeThroughput { get; set; } = 120f;

  /// <summary>Temperature (°C) at which water boils into steam / steam condenses into water.</summary>
  public float BoilingPoint { get; set; } = 100f;
  #endregion

  #region Steam
  /// <summary>Litres of steam produced by boiling one litre of water, and the ratio steam condenses back at.</summary>
  [ExConfigRange(1, 100_000)] // divisor when steam condenses - must stay positive
  public float SteamExpansionFactor { get; set; } = 16f;

  /// <summary>Exponent of the saturated-steam temperature curve: steam temperature (°C) =
  /// <see cref="BoilingPoint"/> × (gaugePressure + 1)^exponent (absolute pressure in atm). 0.25
  /// tracks the water saturation curve: 100°C at 0 atm gauge, ~150°C at 4 atm, ~186°C at 11 atm.</summary>
  public float SteamSaturationExponent { get; set; } = 0.25f;
  #endregion

  #region Boiler (shared FSM values - every boiler variant, LP and HP)
  // Each variant's own stat table (capacity, boil-water window, steam rate, choke pressure,
  // explosion radius) lives with the variant: the Cornish boiler's below, the Lancashire boiler's
  // in hpex's config.

  /// <summary>Max output-network pressure (atm) a boiler can vent exhaust into; above it the fire goes out.</summary>
  public float ExhaustMaxOutputPressure { get; set; } = 0.8f;

  /// <summary>Seconds a boiler may sit over its choke pressure (still firing, nowhere to vent) before it explodes.</summary>
  public float BoilerOverpressureSeconds { get; set; } = 30f;

  /// <summary>Seconds of heating up (water present and coal lit) before the boiler starts boiling.</summary>
  public float BoilerHeatUpSeconds { get; set; } = 180f;

  /// <summary>Grace seconds the boiler keeps running after the fire dies (or water leaves range) before it shuts down.</summary>
  public float BoilerShutdownDelaySeconds { get; set; } = 10f;

  /// <summary>Internal steam (L/s) that condenses back to water once the boiler has shut down.</summary>
  public float BoilerShutdownCondenseRate { get; set; } = 200f;

  /// <summary>Fraction of the vessel capacity the automatic pump intake fills to. Manual pouring is
  /// not capped by it and can still reach the boil-water ceiling.</summary>
  [ExConfigRange(0, 1)]
  public float BoilerWaterIntakeFillFraction { get; set; } = 0.5f;

  /// <summary>Maximum rate (L/s) the boiler draws water from its feed network through the automatic intake.</summary>
  public float BoilerWaterIntakeRate { get; set; } = 10f;

  /// <summary>Exhaust (L/s) a burning boiler vents into its exhaust network - fixed for every boiler variant.</summary>
  public float BoilerExhaustPerSecond { get; set; } = 16f;

  /// <summary>Seconds a boiler may sit choked (fire lit but its exhaust outlet backed up to the vent-pressure cap) before its fuel pile is snuffed out.</summary>
  public float BoilerChokeExtinguishSeconds { get; set; } = 10f;

  /// <summary>A boiler burst shatters every block in its radius below this resistance (pipes, ports,
  /// coal piles, soft terrain). Keep under 45 so other boilers/engines and their resistance-45
  /// fillers survive.</summary>
  public float BoilerBlastResistanceThreshold { get; set; } = 20f;

  /// <summary>Fraction (0..1) of the boiler's construction materials scattered as salvage when it
  /// bursts; lower than <see cref="RccBrokenDropsRatio"/>, the ratio for mining it intact.</summary>
  [ExConfigRange(0, 1)]
  public float BoilerExplosionDropRatio { get; set; } = 0.4f;

  /// <summary>Fraction (0..1) of an engine/boiler's construction materials recovered when it is mined
  /// intact - the right-click-construction salvage ratio. Player-tunable; applied live on the next break.</summary>
  [ExConfigRange(0, 1)]
  public float RccBrokenDropsRatio { get; set; } = 0.8f;

  /// <summary>Internal steam (L/s) an open lid vents to atmosphere.</summary>
  public float BoilerLidVentRate { get; set; } = 200f;

  /// <summary>Internal steam (L/s) bled to atmosphere when the steam outlet has no pipe attached;
  /// an unattached outlet vents instead of pressurising.</summary>
  public float BoilerSteamLeakRate { get; set; } = 16f;

  /// <summary>Rendered water-surface height (block units) while the boiler holds some water
  /// but is below its operating threshold - kept below the flue tubes.</summary>
  [ExConfigRange(0, 1)]
  public float BoilerWaterSurfaceLowLevel { get; set; } = 0.2f;

  /// <summary>Rendered water-surface height (block units) once the boiler holds enough
  /// water to operate - raised above the flue tubes. Kept just under a full block to
  /// avoid z-fighting with the cell boundary.</summary>
  [ExConfigRange(0, 1)]
  public float BoilerWaterSurfaceHighLevel { get; set; } = 0.99f;

  /// <summary>Extra steam (L) flashed per litre of admitted water per atm of feed-water pressure
  /// above 1 atm, so pumped water raises a boiling boiler's steam pressure toward a burst.</summary>
  public float WaterPressureSteamBoost { get; set; } = 1f;
  #endregion

  #region Cornish boiler
  /// <summary>Total internal capacity (L) of the Cornish boiler.</summary>
  public float CornishBoilerCapacity { get; set; } = 800f;

  /// <summary>Minimum water (L) the Cornish boiler needs to begin heating/boiling.</summary>
  public float CornishBoilerMinBoilWater { get; set; } = 150f;

  /// <summary>Maximum water (L) the Cornish boiler will hold/boil.</summary>
  public float CornishBoilerMaxBoilWater { get; set; } = 500f;

  /// <summary>Steam (L/s) the Cornish boiler produces while boiling at full tilt.</summary>
  public float CornishBoilerSteamPerSecond { get; set; } = 32f;

  /// <summary>Steam pressure (atm) the Cornish boiler chokes at - above the Watt engine's 4 atm
  /// break, so a pressure valve between boiler and engine is mandatory.</summary>
  public float CornishBoilerMaxOutputPressure { get; set; } = 5.0f;

  public int CornishBoilerExplosionRadius { get; set; } = 3;
  #endregion

  #region Engines + sub-machines
  /// <summary>Inlet pressure (atm) at/above which the Watt engine runs.</summary>
  public float WattEngineEngagePressure { get; set; } = 2.0f;

  /// <summary>Inlet pressure (atm) above which the Watt engine wears toward a break.</summary>
  public float WattEngineBreakPressure { get; set; } = 4.0f;

  /// <summary>Power a Watt engine delivers while running.</summary>
  public float WattEngineMaxPower { get; set; } = 0.3f;

  /// <summary>Steam (L/s) a Watt engine consumes while running.</summary>
  public float WattEngineSteamRate { get; set; } = 30f;

  /// <summary>Hot condensed water (L/s) a Watt engine spits out its outlet while running.</summary>
  public float WattEngineWaterRate { get; set; } = 1f;

  // The Cornish engine's three-band control-rod table (engage/break pressures, steam, power, water
  // and the overclock sound scalars) lives in hpex's config, with the block.

  /// <summary>Steam-engine efficiency: an engine sets its sub-machine's output pressure
  /// (pump water, air blower) to its inlet steam pressure times this fraction.</summary>
  [ExConfigRange(0, 1)]
  public float SteamEngineEfficiency { get; set; } = 0.75f;

  /// <summary>Seconds an engine may run above its band before it breaks and needs repairing.</summary>
  public float EngineOverPressureSeconds { get; set; } = 60f;

  /// <summary>Nominal MP-network speed a generator holds while its load is within the engine's
  /// rated capacity; lighter loads cannot push past it, heavier loads drag it below.</summary>
  public float MpRatedSpeed { get; set; } = 1.0f;

  /// <summary>MP load an engine's generator holds at <see cref="MpRatedSpeed"/> per unit of engine
  /// power; a Watt at full power (0.3) gives ~0.5, four helve hammers. Load past the rated amount
  /// slows the network (speed = budget / load); past double it the engine stalls and stops.</summary>
  public float MpLoadPerEnginePower { get; set; } = 0.875f;

  /// <summary>Water (L/s) the engine fluid pump moves per unit of mechanical power (Watt 0.3 → 5 L/s,
  /// Cornish 0.2/0.4/0.8 → 3.3/6.7/13.3 L/s).</summary>
  public float PumpWaterPerSecond { get; set; } = 16.67f;

  /// <summary>Water (L/s) the manual (hand-cranked) fluid pump transfers from its intake line to its
  /// output line at a fixed 1 atm. Slower than the engine pump; feeds a boiler at startup.</summary>
  public float ManualPumpWaterPerSecond { get; set; } = 2f;

  /// <summary>A fluid intake only draws water when the whole cube of this depth directly below it is water.</summary>
  public int FluidIntakeWaterDepth { get; set; } = 3;

  /// <summary>An intake is disabled if another intake sits within this many blocks (Euclidean).</summary>
  public float FluidIntakeExclusionRange { get; set; } = 6f;
  #endregion

  #region Steam condenser
  /// <summary>Steam (L/s) a condenser pulls from its (north) steam line and condenses.</summary>
  public float CondenserSteamPerSecond { get; set; } = 30f;

  /// <summary>Water (L/s) the condenser passes through its W↔E water line (the through-flow cap).</summary>
  public float CondenserWaterThroughput { get; set; } = 50f;
  #endregion

  #region Recipe balance
  /// <summary>Active steam-machine recipe cost level - <c>"normal"</c> or <c>"cheap"</c>. Toggled
  /// in-game by <c>/exmod steam &lt;level&gt;</c>; the per-recipe numbers live in the separate
  /// <c>lpex_recipes.json</c> catalogue. Applied on the next world reload.</summary>
  public string RecipeLevel { get; set; } = "normal";
  #endregion
}
