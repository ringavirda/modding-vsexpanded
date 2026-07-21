using ExpandedLib.Registries.Config;
using Vintagestory.API.Common;

namespace SteelmakingExpanded;

/// <summary>
/// JSON-serializable gameplay tunables for Steelmaking Expanded - the "magic
/// numbers" that balance the machines and the molten/gas systems. Loaded from
/// (and written to) <c>ModConfig/smex_values.json</c>; the property defaults below
/// are used when the file is missing or a key is absent (and any NaN/infinite/negative
/// value is reset to its default on load). Accessed through <see cref="SmexValues"/>, not directly.
/// </summary>
[ExConfigRegister(
  "ex_values.json",
  "smex",
  LegacyFileNames = new string[] { "smex_values.json", "smex.json" },
  Manageable = true
)]
public class SmexConfig : IExVersionedConfig
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
  public static readonly ExConfigMigration[] Migrations =
  [
    // 0.9.0: the bessemer blast draw (1 -> 8 L/s) and smoke-stack vent rate (4 -> 48 L/s)
    // were retuned during the gas-volume rebalance - push the new defaults to pre-0.9.0
    // configs (e.g. players coming from 0.8.6). FromVersion null so even unversioned
    // files are caught; 0.9.0+ already carry these values.
    new()
    {
      ToVersion = "0.9.0",
      ResetFields =
      [
        nameof(BessemerBlastPerSecond),
        nameof(SmokestackGasIntakeVolume),
      ],
    },
    // 0.9.2: the converter vessel now costs a single smithable large gear (8 rods)
    // instead of 4 rusty gears (12 rods), and the air blower now scales off absolute
    // engine power so its base output was retuned (16 -> 48 L/s) - push the rebalanced
    // defaults to existing configs.
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
    // dynamic carbon model (autothermal heat balance + over-blow + cold-scrap temp gate + slag +
    // split-pour), and the vessel was enlarged to ≥2 large billets. The retired
    // BessemerProcessDuration/BessemerProcessTemperature keys drop out of the POCO; force the capacity
    // and the new model tunables to their defaults so existing configs run the reworked machine.
    new()
    {
      ToVersion = "0.9.5",
      ResetFields =
      [
        nameof(BessemerConverterCapacity),
      ],
    },
  ];

  // The molten-metal system tunables (cooldown rates, canal flow, default capacities, canal-seal
  // clay costs) moved to the foundational iwex mod along with the molten subsystem itself - see
  // IronworkingExpanded.IwexConfig / IwexValues. The bessemer charge cooldown still reads the base
  // IwexValues.MoltenCooldownSpeed and scales it by BessemerCooldownCoefficient below.

  #region Hopper bell (blast-mix maker)
  /// <summary>Items the hopper magazine can buffer.</summary>
  public int HopperMaxMagazineCapacity { get; set; } = 48;

  /// <summary>Iron ore consumed per blast-mix batch.</summary>
  public int HopperIronOreRequired { get; set; } = 12;

  /// <summary>Coke (lumps) consumed per blast-mix batch. Halved from the old 3 crushed-coke when coke
  /// became a whole lump: one lump is worth two of the retired crushed pieces, so 2 lumps keep the
  /// blast-mix coke cost close to what it was (rounded up from 1.5, so it never got cheaper).</summary>
  public int HopperCokeRequired { get; set; } = 2;

  /// <summary>Lime consumed per blast-mix batch.</summary>
  public int HopperLimeRequired { get; set; } = 1;

  /// <summary>Blast-mix produced per batch.</summary>
  public int HopperBlastmixProduced { get; set; } = 16;

  /// <summary>Blast-mix dropped per output pulse.</summary>
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
  /// <see cref="PipesAndPowerExpanded.PpexValues.SteamEngineEfficiency"/>.</summary>
  public float AirBlowerOutputPerSecond { get; set; } = 48f;
  #endregion

  #region Player safety
  /// <summary>Minimum mold-content temperature (°C) that burns a bare-handed player carrying it.</summary>
  public float MoldBurnMinTemperature { get; set; } = 200f;
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

  /// <summary>Gas (L/s) the cowper stove draws each tick from each of its intakes - the furnace exhaust it soaks heat from, and the air it reheats into hot blast.</summary>
  public float CowperIntakeVolume { get; set; } = 24f;
  #endregion

  #region Bessemer converter
  /// <summary>Molten-metal capacity (units) of the converter vessel. Sized to hold ≥2 large billets
  /// (2×2400 u) so a full pig charge blows to two billets of steel and the line never stalls.</summary>
  public int BessemerConverterCapacity { get; set; } = 4800;

  /// <summary>Blast (L/s) the converter draws from its gas intake while blowing.</summary>
  public float BessemerBlastPerSecond { get; set; } = 8.0f;

  // --- Dynamic carbon model (the autothermal blow) -------------------------------------------------
  // The charge carries a per-unit carbon fraction; the air blow oxidises it, so carbon MONOTONICALLY
  // falls the longer you blow. The player picks the product by WHEN they stop: high carbon = still pig,
  // at the target = Bessemer steel, past it (over-blow) = soft ingot iron. Carbon is an ABSTRACTION for
  // total oxidisable content (real Bessemer heat is silicon-dominated); modelling it as "carbon burns →
  // heat, and reaching the target IS the steel" self-terminates the way the real flame drops at blow's
  // end. Acid process: no flux, self-forming siliceous slag (historically correct, not a shortcut).

  /// <summary>Carbon fraction of a fresh molten-pig charge (materials.md: pig ~4.0 % C).</summary>
  public float BessemerPigCarbonStart { get; set; } = 0.04f;

  /// <summary>Carbon fraction at which the charge becomes Bessemer steel (materials.md: ~0.2 % C).</summary>
  public float BessemerSteelCarbonTarget { get; set; } = 0.002f;

  /// <summary>Carbon fraction below which the blow has over-blown the heat to soft iron (~0 % C):
  /// the charge retypes to <c>game:ingot-iron</c> (ingot iron, the deliberate plain-iron route now that
  /// the blast furnace makes pig).</summary>
  public float BessemerOverblowCarbon { get; set; } = 0.0005f;

  /// <summary>Carbon fraction burned out per litre of blast that reaches the bath - the decarburisation
  /// rate that sets the blow length (a full ~4800 u charge at 8 L/s blows in ~5 min).</summary>
  public float BessemerCarbonPerBlastLitre { get; set; } = 0.000016f;

  // --- Autothermal heat balance (T_process = T_in − T_loss, shared exlib helper) --------------------

  /// <summary>Autothermal T_in floor (°C): the heat the molten pig arrives with, before the blow adds
  /// any. T_in = this + <see cref="BessemerHeatPerCarbonUnit"/> × (blast reaching the bath).</summary>
  public float BessemerAutothermalBase { get; set; } = 1250f;

  /// <summary>Autothermal heat (°C) the oxidising charge contributes to T_in at full blast - the
  /// carbon/silicon burn that makes the process need no external fuel. Scaled by how much blast actually
  /// reaches the bath. The post-blow peak is (base + this − radiation) ≈ 1800 °C at the shipped values;
  /// raise it for a hotter, longer liquid window.</summary>
  public float BessemerHeatPerCarbonUnit { get; set; } = 600f;

  /// <summary>Ceiling (°C) on autothermal T_in, so a retuned heat term can't run the bath arbitrarily hot.</summary>
  public float BessemerAutothermalCeiling { get; set; } = 2000f;

  /// <summary>Radiation/ambient heat loss (°C) off the open vessel mouth - the always-on term of T_loss.</summary>
  public float BessemerRadiationLoss { get; set; } = 50f;

  /// <summary>Process floor (°C): the blow only refines while T_process is at or above this (the steel
  /// liquidus - below it the bath would skull). Cold scrap that pushes T_process under this stalls the
  /// blow; heavier still and the bath freezes solid (the emergent scrap cap).</summary>
  public float BessemerRefineTemperature { get; set; } = 1500f;

  // --- Cold steel-scrap charge (the temperature gate) ----------------------------------------------
  // Optional cold steel bits (game:metalbit-steel, classified via the exlib Scrap role) are charged
  // alongside the pig; they add cold mass to T_loss, so MORE scrap ⇒ LOWER T_process. There is no
  // hardcoded scrap cap - past the ceiling the bath can't stay above the refine floor and the heat
  // stalls or freezes (conventions.md). The coefficient is tuned so ~15-20 % cold scrap on a full charge
  // is the practical ceiling (the real acid-Bessemer figure; ~30 % is too generous).

  /// <summary>Heat loss (°C) added to T_loss per unit of cold scrap in the vessel. At the shipped value a
  /// full 4800 u charge stalls the blow around ~850 u scrap (~15-18 %).</summary>
  public float BessemerColdScrapLossCoefficient { get; set; } = 0.35f;

  /// <summary>Molten units one cold steel bit adds when charged (a metal bit is 5 u, like the recovery drop).</summary>
  public int BessemerScrapUnitValue { get; set; } = 5;

  /// <summary>Fraction of cold-scrap mass that becomes steel (the rest is oxidation loss). Per 100 u
  /// scrap → ~97 u steel + ~3 u loss, negligible slag (materials.md scrap recovery).</summary>
  [ExConfigRange(0, 1)]
  public float BessemerScrapSteelYield { get; set; } = 0.97f;

  // --- Mass balance (R2: steel + slag ≤ input, never create matter) --------------------------------
  // Per 100 u pig → 90 u molten steel + 6 u molten slag (the same iwex:slag the furnaces make) + 4 u gas
  // (carbon burned off, gone - not a material). Slag accumulates into its own pool DURING the blow.

  /// <summary>Fraction of pig mass that becomes steel across the blow (materials.md).</summary>
  [ExConfigRange(0, 1)]
  public float BessemerSteelYield { get; set; } = 0.90f;

  /// <summary>Fraction of pig mass that becomes molten slag across the blow; the remainder (1 − steel −
  /// slag) is gas that leaves the bath.</summary>
  [ExConfigRange(0, 1)]
  public float BessemerSlagYield { get; set; } = 0.06f;

  /// <summary>Pour rate (units/second) draining the vessel through the output cell - tuned so a full
  /// 4800 u steel charge drains in ~110 s (slag, being far less, drains in seconds), leaving margin
  /// inside the ~3-4 min liquid window to tilt slag→steel and work the canal valves.</summary>
  public float BessemerPourRate { get; set; } = 44f;

  /// <summary>Minimum geared mechanical speed for the converter to count as powered.</summary>
  public float BessemerPowerSpeedThreshold { get; set; } = 0.1f;

  /// <summary>Multiplier on the converter charge's cooldown speed (vs the base molten-system rate,
  /// <c>IwexValues.MoltenCooldownSpeed</c>).
  /// Below 1 the bath holds its heat longer, giving the player more time to pour before it solidifies
  /// (0.5 ⇒ half the molten-system rate, i.e. cools twice as slowly).</summary>
  public float BessemerCooldownCoefficient { get; set; } = 0.5f;

  /// <summary>Fraction of the converter capacity below which a hardened (cooled) charge can be chiselled
  /// out of the vessel instead of breaking the whole structure. A residue at or above this is salvaged
  /// by breaking it.</summary>
  [ExConfigRange(0, 1)]
  public float BessemerChiselMaxFraction { get; set; } = 0.2f;

  /// <summary>Fraction (0..1) of the converter's construction materials recovered when its vessel is
  /// broken - the right-click-construction salvage ratio. Player-tunable; applied live on the next break.</summary>
  public float RccBrokenDropsRatio { get; set; } = 0.8f;
  #endregion

  #region Smoke stack
  /// <summary>Exhaust gas (L/s) the smoke stack vents from the network.</summary>
  public float SmokestackGasIntakeVolume { get; set; } = 48.0f;
  #endregion

  #region Tool molds
  // Availability of the mod's added casting molds. Disabling one removes its clay-forming recipe
  // and hides it from creative/the handbook on the next world load, and stops any already-placed
  // mold of that type from yielding a casting immediately. Toggled in-game by a server admin via
  // /exmod molds <plate|ingot|rod|all> <on|off>; persisted to smex_values.json.

  /// <summary>Whether the plate mold (casts metal plates) is available.</summary>
  public bool EnablePlateMold { get; set; } = true;

  /// <summary>Whether the double-ingot mold (casts 2 ingots) is available.</summary>
  public bool EnableIngotMold { get; set; } = true;

  /// <summary>Whether the quad-rod mold (casts 4 rods) is available.</summary>
  public bool EnableRodMold { get; set; } = true;
  #endregion

  #region Recipe balance
  /// <summary>Active steelmaking recipe cost level - <c>"normal"</c> or <c>"cheap"</c>. Toggled
  /// in-game by <c>/exmod steel &lt;level&gt;</c>; the per-recipe numbers live in the separate
  /// <c>smex_recipes.json</c> catalogue. Applied on the next world reload.</summary>
  public string RecipeLevel { get; set; } = "normal";
  #endregion
}
