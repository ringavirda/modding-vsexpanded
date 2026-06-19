using ExpandedLib.Registries.Config;
using Vintagestory.API.Common;

namespace SteelmakingExpanded;

/// <summary>
/// JSON-serializable gameplay tunables for Steelmaking Expanded - the "magic
/// numbers" that balance the machines and the molten/gas systems. Loaded from
/// (and written to) <c>ModConfig/smex.json</c>; the property defaults below are
/// used when the file is missing or a key is absent. Accessed through
/// <see cref="SmexValues"/>, not directly.
/// </summary>
[ExConfigRegister("smex.json", "smex")]
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
  ];

  #region Molten system
  /// <summary>Temperature cooldown speed applied to molten-metal stacks held by the molten system (canal cells, taps, barrels, molds, the bessemer charge).</summary>
  public float MoltenCooldownSpeed { get; set; } = 24f;

  /// <summary>Max metal (units) flowing across one canal connection per second; balance against <see cref="MoltenCooldownSpeed"/>.</summary>
  public int MoltenFlowRate { get; set; } = 50;

  /// <summary>Minimum metal (units) that must move across a canal connection for any flow that tick (stops sub-unit dribbles).</summary>
  public int MoltenMinFlowAmount { get; set; } = 10;

  /// <summary>Default per-canal-block capacity (units) when a block sets no <c>maxUnits</c> attribute.</summary>
  public int CanalDefaultUnitCapacity { get; set; } = 50;

  /// <summary>Default canal-tap network drain speed (units/s) when no <c>drainSpeed</c> attribute is set.</summary>
  public float CanalDefaultDrainSpeed { get; set; } = 20f;

  /// <summary>Default large-mold capacity (units) when the mold sets no <c>requiredUnits</c> attribute.</summary>
  public int MoldDefaultUnits { get; set; } = 100;

  /// <summary>Default molten-barrel capacity (units) when no <c>maxUnits</c> attribute is set.</summary>
  public int BarrelDefaultMaxUnits { get; set; } = 800;

  /// <summary>Fire-clay consumed to seal a straight canal into a separator.</summary>
  public int CanalSealClayCost { get; set; } = 4;

  /// <summary>Fire-clay refunded when breaking a canal seal.</summary>
  public int CanalUnsealClayRefund { get; set; } = 2;
  #endregion

  #region Blastmix
  /// <summary>Blast-mix units that must be loaded into the hearth before the furnace can fire.</summary>
  public int BlastMixRequiredToFire { get; set; } = 320;

  /// <summary>Burn time (seconds) granted by a blast-mix charge burning in a coal pile.</summary>
  public int BlastmixBurnTime { get; set; } = 300;
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

  #region Blast furnace
  /// <summary>Maximum hearth temperature (°C) without a hot-blast boost.</summary>
  public float BfNaturalMaxTemp { get; set; } = 1420f;

  /// <summary>Maximum hearth temperature (°C) when fed hot blast above the boost threshold.</summary>
  public float BfBoostedMaxTemp { get; set; } = 1740f;

  /// <summary>Hot-blast temperature (°C) at or above which the furnace reaches its boosted max temp.</summary>
  public float BfBlastBoostThreshold { get; set; } = 800f;

  /// <summary>Temperature (°C) the hearth must reach (and hold) to start melting iron.</summary>
  public float BfIronMeltingPoint { get; set; } = 1482f;

  /// <summary>Maximum molten iron (units) the furnace can hold before stalling.</summary>
  public float BfMaxMoltenIron { get; set; } = 2400f;

  /// <summary>Maximum molten slag (units) the furnace can hold before stalling.</summary>
  public float BfMaxMoltenSlag { get; set; } = 600f;

  /// <summary>Seconds a fired furnace burns before it extinguishes.</summary>
  public int BfMaxFuelBurnTime { get; set; } = 1200;

  /// <summary>Seconds above the melting point before the furnace transitions to the melting phase.</summary>
  public float BfMeltStartDelay { get; set; } = 300f;

  /// <summary>Seconds between melt cycles while melting.</summary>
  public float BfMeltIntervalSec { get; set; } = 10f;

  /// <summary>Molten iron (units) produced per melt cycle.</summary>
  public float BfIronPerMeltCycle { get; set; } = 60f;

  /// <summary>Molten slag (units) produced per melt cycle.</summary>
  public float BfSlagPerMeltCycle { get; set; } = 10f;

  /// <summary>Blast-mix consumed per melt cycle.</summary>
  public int BfBlastMixPerMeltCycle { get; set; } = 16;

  /// <summary>Air/blast (L/s) the blast furnace draws through each tuyere.</summary>
  public float TuyereIntakeVolume { get; set; } = 12f;
  #endregion

  #region Bessemer converter
  /// <summary>Molten-metal capacity (units) of the converter vessel.</summary>
  public int BessemerConverterCapacity { get; set; } = 1200;

  /// <summary>Blast (L/s) the converter draws from its gas intake while refining.</summary>
  public float BessemerBlastPerSecond { get; set; } = 8.0f;

  /// <summary>Seconds of blast a charge needs to refine iron into steel.</summary>
  public float BessemerProcessDuration { get; set; } = 300f;

  /// <summary>Temperature (°C) the blast holds the charge at while refining.</summary>
  public float BessemerProcessTemperature { get; set; } = 1800f;

  /// <summary>Minimum geared mechanical speed for the converter to count as powered.</summary>
  public float BessemerPowerSpeedThreshold { get; set; } = 0.1f;
  #endregion

  #region Hopper bell (blast-mix maker)
  /// <summary>Items the hopper magazine can buffer.</summary>
  public int HopperMaxMagazineCapacity { get; set; } = 48;

  /// <summary>Iron ore consumed per blast-mix batch.</summary>
  public int HopperIronOreRequired { get; set; } = 12;

  /// <summary>Coke consumed per blast-mix batch.</summary>
  public int HopperCokeRequired { get; set; } = 3;

  /// <summary>Lime consumed per blast-mix batch.</summary>
  public int HopperLimeRequired { get; set; } = 1;

  /// <summary>Blast-mix produced per batch.</summary>
  public int HopperBlastmixProduced { get; set; } = 16;

  /// <summary>Blast-mix dropped per output pulse.</summary>
  public int HopperDropAmount { get; set; } = 4;
  #endregion

  #region Smoke stack
  /// <summary>Exhaust gas (L/s) the smoke stack vents from the network.</summary>
  public float SmokestackGasIntakeVolume { get; set; } = 48.0f;
  #endregion
}
