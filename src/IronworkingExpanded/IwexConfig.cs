using System.Collections.Generic;
using ExpandedLib.Registries.Config;
using Vintagestory.API.Common;

namespace IronworkingExpanded;

/// <summary>
/// JSON-serializable gameplay tunables for Ironworking Expanded (iwex) - the "magic numbers" that
/// balance the molten-metal network (canals, taps, barrels, mold pedestals) that this foundational
/// mod owns. Loaded from (and written to) <c>ModConfig/iwex_values.json</c>; the property defaults
/// below are used when the file is missing or a key is absent (and any NaN/infinite/negative value
/// is reset to its default on load). Accessed through <see cref="IwexValues"/>, not directly.
/// </summary>
[ExConfigRegister(
  "ex_values.json",
  "iwex",
  LegacyFileNames = new string[] { "iwex_values.json" },
  Manageable = true
)]
public class IwexConfig : IExVersionedConfig
{
  /// <summary>Mod version that last wrote this file; drives the <see cref="Migrations"/> resets.
  /// Managed by <see cref="ExConfigRegister{TConfig}"/> - do not set by hand.</summary>
  public string? ConfigVersion { get; set; }

  /// <summary>Version-driven default resets (none yet).</summary>
  public static readonly ExConfigMigration[] Migrations = [];

  #region Molten system
  /// <summary>Temperature cooldown speed applied to molten-metal stacks held by the molten system (canal cells, taps, barrels, molds, the bessemer charge).</summary>
  public float MoltenCooldownSpeed { get; set; } = 24f;

  /// <summary>Multiplier on <see cref="MoltenCooldownSpeed"/> for metal stored in a standalone molten
  /// barrel. Below 1 the barrel holds its heat longer; 1 = the base molten rate. Applied live to metal already in the barrel.</summary>
  public float BarrelCooldownCoefficient { get; set; } = 1f;

  /// <summary>Multiplier on <see cref="MoltenCooldownSpeed"/> for metal cast in a mold parked under a
  /// canal tap. Below 1 the cast holds its heat longer; 1 = the base molten rate. Applied live.</summary>
  public float TapMoldCooldownCoefficient { get; set; } = 1f;

  /// <summary>Multiplier on <see cref="MoltenCooldownSpeed"/> for metal cast in a mold on a pedestal.
  /// Below 1 the cast holds its heat longer; 1 = the base molten rate. Applied live.</summary>
  public float MoldPedestalCooldownCoefficient { get; set; } = 1f;

  // MoltenFlowRate and MoltenMinFlowAmount moved to exlib's config (ExlibValues) now that the
  // MoltenNetwork class lives in exlib - the per-connection flow driver is framework code. The
  // cooldown values above stay here: they're read by iwex's MoltenMetal/canal cells.

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

  #region Air blower / blast
  /// <summary>Pressure (atm) at or above which air in a pipe network counts as "blast".</summary>
  public float BlastPressureThreshold { get; set; } = 2.5f;
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

  #region Ore bunker
  /// <summary>Maximum burden (units) a finished ore bunker can hold across all grades.</summary>
  public int BunkerMaxBurden { get; set; } = 1152;
  #endregion

  #region Ore mixer
  /// <summary>Maximum raw input units (iron + flux + coke combined) the mixer holds in one batch.</summary>
  public int MixerMaxRaw { get; set; } = 512;

  /// <summary>
  /// Seconds to mix a <b>full</b> batch when the rotor turns at (or below) <see cref="MixerMinSpeed"/>.
  /// Mixing time scales linearly with how full the mixer is, so a part-full batch mixes proportionally
  /// faster.
  /// </summary>
  public float MixerFullMixSecondsSlow { get; set; } = 30f;

  /// <summary>Seconds to mix a <b>full</b> batch when the rotor turns at (or above) <see cref="MixerMaxSpeed"/>.</summary>
  public float MixerFullMixSecondsFast { get; set; } = 10f;

  /// <summary>Axle speed at/below which mixing runs at its slowest (<see cref="MixerFullMixSecondsSlow"/>).</summary>
  public float MixerMinSpeed { get; set; } = 0.5f;

  /// <summary>Axle speed at/above which mixing runs at its fastest (<see cref="MixerFullMixSecondsFast"/>).</summary>
  public float MixerMaxSpeed { get; set; } = 1.5f;

  /// <summary>
  /// Fuel (carbon) value of one charcoal added to the mixer, relative to one coke (= 1.0). Charcoal is
  /// a poorer reductant than coke, so it counts for less: at the default 0.5 it takes two charcoal to
  /// match one coke - the pre-19th-century charcoal-burden trade-off.
  /// </summary>
  public float MixerCharcoalFuelValue { get; set; } = 0.5f;

  /// <summary>Burden units the mixer drains into the container below per second while the lids are open.</summary>
  public float MixerDrainPerSecond { get; set; } = 8f;
  #endregion

  #region Burden grades
  /// <summary>
  /// Named burden grades, matched by composition band. The ore mixer reports the first profile whose
  /// iron/flux/fuel fraction bands all contain the current mix (so order them most-specific first);
  /// any mix outside every band is "off-spec" and can be reloaded into an empty mixer to adjust.
  /// Extend or retune freely in <c>iwex_values.json</c> - this is the single source of grade truth,
  /// shared by the burden tooltip and the mixer block info. Fractions are 0..1 of the total mix.
  /// </summary>
  public List<BurdenProfile> BurdenProfiles { get; set; } =
    [
      // All valid grades need a flux floor for proper slagging; the fuel fraction sets the grade.
      new()
      {
        Key = "lowcoke",
        MinFlux = 0.05f,
        MaxFuel = 0.15f,
      },
      new()
      {
        Key = "standard",
        MinFlux = 0.05f,
        MinFuel = 0.15f,
        MaxFuel = 0.25f,
      },
      new()
      {
        Key = "highcoke",
        MinFlux = 0.05f,
        MinFuel = 0.25f,
      },
    ];
  #endregion
}

/// <summary>
/// One named burden grade: a lang-keyed label (<c>iwex:burden-profile-{Key}</c>) plus inclusive
/// composition bands (fractions 0..1 of the total mix). A <c>BurdenMix</c> matches when each of its
/// iron/flux/fuel fractions falls within the corresponding band. Pure data - the classifier lives in
/// <see cref="Items.Burden"/>.
/// </summary>
public class BurdenProfile
{
  /// <summary>Grade key; the mixer/tooltip show <c>iwex:burden-profile-{Key}</c>.</summary>
  public string Key { get; set; } = "";

  public float MinIron { get; set; }
  public float MaxIron { get; set; } = 1f;
  public float MinFlux { get; set; }
  public float MaxFlux { get; set; } = 1f;
  public float MinFuel { get; set; }
  public float MaxFuel { get; set; } = 1f;
}
