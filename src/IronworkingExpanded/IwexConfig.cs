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

  /// <summary>Ambient temperature (°C) the molten system cools toward.</summary>
  public float MoltenAmbientTemperature { get; set; } = 20f;

  /// <summary>Fraction of a poured metal's temperature the cast-iron mold body soaks up as its own
  /// heat-sink glow (0..1). Higher = the mold glows brighter/longer off a pour.</summary>
  public float CastMoldHeatSinkFraction { get; set; } = 0.7f;

  /// <summary>How fast (°C per second) a cast-iron mold body sheds its heat-sink glow toward ambient.</summary>
  public float CastMoldBodyCooldownPerSecond { get; set; } = 25f;

  /// <summary>
  /// Pour temperature (°C) above which a fired-clay tool mold shatters instead of casting - the ceramic
  /// tier's ceiling. Every vanilla casting metal sits below it (tin bronze 950, black bronze / brass
  /// ~1000, gold 1063, copper 1084) and everything the iron tier adds sits above (cast iron ~1150+,
  /// wrought iron and steel ~1500), so vanilla progression is untouched and the rule bites only when a
  /// player tries to pour iron-family metal into clay. Only the SMALL clay molds (those that fit the mold
  /// pedestal) are gated; the large anvil / helve-hammer molds are cast at iron temperatures and are
  /// exempt. Our own cast-iron molds are a different block class and never shatter. See
  /// <see cref="BlockNetworkMolten.Blocks.ClayHeatGate"/>.
  /// </summary>
  public float ClayMoldHeatCeiling { get; set; } = 1100f;

  /// <summary>
  /// Whether the enhanced mold handling (spill a filled mold moved out of hand, burn the hand holding a
  /// hot one, render/carry the cast) also applies to <b>vanilla clay</b> tool molds. Our own cast molds
  /// always get it; this opts vanilla molds in too. Toggle live with
  /// <c>/exmod config iwex EnhanceVanillaMolds true|false</c>.
  /// </summary>
  public bool EnhanceVanillaMolds { get; set; } = false;

  /// <summary>Minimum mold-content temperature (°C) that burns a bare-handed player carrying a filled
  /// mold. Applies to our cast molds always, and to vanilla clay molds when
  /// <see cref="EnhanceVanillaMolds"/> is on. (Moved here from smex with the mold-safety tick, so the
  /// cast-mold handling this mod owns works without the steelmaking add-on installed.)</summary>
  public float MoldBurnMinTemperature { get; set; } = 200f;

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

  #region Sand casting
  /// <summary>Chance (0..1) that the rammed sand block is returned to the player on shake-out. Green sand is
  /// reconditioned and reused, so the default is <b>1</b> (always returned - a sand-neutral loop); lower it
  /// to make sand a slow consumable (a fraction lost each heat) rather than purely ceremonial.</summary>
  [ExConfigRange(0, 1)]
  public float SandReturnChance { get; set; } = 1f;
  #endregion

  #region Blastmix
  /// <summary>Blast-mix units that must be loaded into the hearth before the furnace can fire.</summary>
  public int BlastMixRequiredToFire { get; set; } = 320;

  /// <summary>Burn time (seconds) granted by a blast-mix charge burning in a coal pile.</summary>
  public int BlastmixBurnTime { get; set; } = 300;
  #endregion

  #region Air blower / blast
  // Blast pressure is NOT a per-furnace constant: it falls out of the burden, the way the heat balance
  // does. Coke is the permeable skeleton of the charge column - the coarse, non-fusing component that
  // keeps gas channels open through the stack - so a LEAN burden packs denser, resists the blast more,
  // and needs a higher pressure to push the same air through it. A rich burden is permeable and blows
  // easily but eats coke and demands far more air to burn it.
  //
  // That is the whole tier gate, and it is a consequence rather than a rule: a mechanically blown iron
  // furnace can always be brute-forced with a coke-rich burden, but the coke-lean burden that actually
  // saves fuel needs pressure only the steam tier can raise - and pipes only the steam tier can build.
  // It is also the historical trade: hot blast's value was cutting coke per ton, and the era that had
  // it also had blowing engines (and stronger pipe) able to force a denser column.

  /// <summary>Blast pressure (atm) a burden at <see cref="BfReferenceFuelFrac"/> demands. The gate is
  /// set by where this lands relative to <see cref="BoltedPipeBurstPressure"/> and the blower ceiling.</summary>
  public float BfBlastPressureAtReference { get; set; } = 2.0f;

  /// <summary>Atmospheres added per unit the coke fraction falls below <see cref="BfReferenceFuelFrac"/>
  /// (and subtracted per unit above it) - how sharply permeability translates into required pressure.
  /// At the shipped defaults a 30% coke burden asks 1.25 atm, a standard 20% one asks 2.0, and a 10%
  /// one asks 2.75 - which is above both the bellows' ceiling AND bolted pipe's burst rating, so the
  /// lean burden is gated by the blower and by the plumbing at once.</summary>
  public float BfBlastPressureCokeSensitivity { get; set; } = 7.5f;

  /// <summary>Floor on the derived blast pressure - no burden is so permeable it blows on nothing.</summary>
  public float BfBlastPressureMin { get; set; } = 1.2f;

  /// <summary>Ceiling on the derived blast pressure, so a near-cokeless charge cannot demand the impossible.</summary>
  public float BfBlastPressureMax { get; set; } = 6f;

  /// <summary>Lower clamp on the air-draw factor, so even a coke-starved burden still breathes.</summary>
  public float BfTuyereDrawMinFactor { get; set; } = 0.4f;

  /// <summary>Upper clamp on the air-draw factor, so a coke-packed burden's demand stays blowable.</summary>
  public float BfTuyereDrawMaxFactor { get; set; } = 1.8f;
  #endregion

  #region Pipes
  // iwex owns the base pipe block (the bolted tier) and the "pipe" network registration, so the
  // pipe-content tunables that used to live in lpex are here now. The generic pipe-network constants
  // (LitresPerPipe, leak rates, …) live in exlib's own config (ExlibValues); these are content values.

  /// <summary>
  /// Burst pressure (atm) of a plain bolted (iwex) pipe segment - the weakest pipe limits a run. Higher
  /// tiers register their own rating (lpex cast 5, hpex rolled 12).
  /// <para>
  /// This doubles as the tier's <b>capacity</b>: a network holds <c>burst x pipes x litresPerPipe</c>,
  /// so the bolted tier is both the low-pressure tier and the small-buffer one. It sits deliberately
  /// just above what a coke-rich burden demands and below what a lean one does - that gap is the gate.
  /// </para>
  /// </summary>
  public float BoltedPipeBurstPressure { get; set; } = 2.5f;

  /// <summary>Gas (L/s) a vanilla chimney draws from the network when capping the top connector of a
  /// chimney-ventable fitting (a passthrough / passthrough-bend / outlet). Used by the iwex chimney-vent
  /// strategy that every "pipe" network carries.</summary>
  public float ChimneyGasDrawRate { get; set; } = 16.0f;
  #endregion

  #region Blast furnace - heat balance
  // The furnace has no maximum temperature. It settles wherever the heat it makes and the heat it
  // loses balance: T_process = T_in - T_loss, floored at ambient. T_in is coke combustion (how rich
  // the burden is in fuel x how much air actually reaches the tuyeres) plus the preheat a cowper
  // puts into the blast; T_loss is radiation plus the cold mass of the charge plus a cold day.
  // That is what makes a cold and a hot blast furnace differ without either of them being a special
  // case in code: a high-coke burden clears iron's melt line on cold blast, a low-coke burden only
  // clears it once a cowper is preheating the air. See docs/design/conventions.md.

  /// <summary>Temperature (°C) a lit charge holds on its own, before any coke credit or draught.</summary>
  public float BfCombustionBaseTemp { get; set; } = 950f;

  /// <summary>Temperature (°C) coke combustion adds at the reference coke ratio with full blast supply.</summary>
  public float BfCombustionCokeGain { get; set; } = 900f;

  /// <summary>Coke fraction the combustion gain is calibrated at (the "standard" burden grade midpoint).</summary>
  public float BfReferenceFuelFrac { get; set; } = 0.20f;

  /// <summary>How strongly the combustion gain responds to a coke ratio off <see cref="BfReferenceFuelFrac"/>.</summary>
  public float BfCokeSensitivity { get; set; } = 0.35f;

  /// <summary>Floor on the coke factor, so even a fuel-starved burden still burns.</summary>
  public float BfMinFuelFactor { get; set; } = 0.35f;

  /// <summary>Ceiling on the coke factor, so piling in coke has diminishing returns.</summary>
  public float BfMaxFuelFactor { get; set; } = 1.25f;

  /// <summary>Coke fraction assumed for charge carrying no burden mix (legacy count-only blast mix).
  /// Keep equal to <see cref="BfReferenceFuelFrac"/>: unstamped charge should read as standard grade.</summary>
  public float BfDefaultFuelFrac { get; set; } = 0.20f;

  /// <summary>Flux fraction assumed for charge carrying no burden mix, so legacy blast mix grades as
  /// standard rather than as a flux shortfall. Keep at or above every grade's flux floor.</summary>
  public float BfDefaultFluxFrac { get; set; } = 0.05f;

  /// <summary>Air factor with no pressurised blast at all - what the stack pulls by natural draught.</summary>
  public float BfNaturalDraughtFactor { get; set; } = 0.5f;

  /// <summary>Blast-supply fraction (arrived air / demanded air) below which a lit furnace is
  /// <b>air-starved</b>: sustained operation under this floor counts a disruption toward extinguish,
  /// so a dead or too-weak blower snuffs the fire after the extinguish grace. A weak-but-present blast
  /// (above this floor) keeps a furnace alive-but-cold on natural draught rather than killing it - only
  /// a near-dry tuyere starves it out. Set to 0 to disable air-starvation extinguish entirely.</summary>
  public float BfStarvationSupplyFrac { get; set; } = 0.1f;

  /// <summary>Degrees of <c>T_in</c> gained per degree the blast is preheated above ambient. This is
  /// the whole hot-blast mechanic: only a charged cowper raises the pipe temperature at the tuyere.</summary>
  public float BfPreheatCoefficient { get; set; } = 0.35f;

  /// <summary>Baseline heat loss (°C) radiated through the stack.</summary>
  public float BfRadiationLossBase { get; set; } = 120f;

  /// <summary>Heat loss (°C) from cold charge mass with the hearth full to <see cref="BlastMixRequiredToFire"/>.
  /// A thin charge runs hotter but exhausts sooner - the historically correct trade.</summary>
  public float BfChargeLossFull { get; set; } = 310f;

  /// <summary>Ambient temperature (°C) the loss term is calibrated at; only colder than this costs heat.</summary>
  public float BfAmbientReferenceTemp { get; set; } = 20f;

  /// <summary>Heat loss (°C) per degree the ambient sits below <see cref="BfAmbientReferenceTemp"/>.</summary>
  public float BfAmbientLossPerDegree { get; set; } = 1.0f;

  /// <summary>Ambient temperature (°C) assumed when the climate is unavailable (unloaded chunk, headless).</summary>
  public float BfAmbientFallbackTemp { get; set; } = 20f;

  /// <summary>How fast (°C/s) the hearth climbs toward its process temperature.</summary>
  public float BfHeatRatePerSecond { get; set; } = 4f;

  /// <summary>How fast (°C/s) the hearth falls back toward its process temperature.</summary>
  public float BfCoolRatePerSecond { get; set; } = 4f;

  /// <summary>Degrees of margin above the melt point worth one full <see cref="BfMeltMarginGain"/> step.</summary>
  public float BfMeltMarginReference { get; set; } = 200f;

  /// <summary>Melt-speed gained per <see cref="BfMeltMarginReference"/> of margin above the melt point.
  /// Set to 0 for a flat melt rate regardless of how hard the furnace is being blown.</summary>
  public float BfMeltMarginGain { get; set; } = 0.75f;

  /// <summary>Slowest the melt cycle can run, as a multiple of the nominal rate.</summary>
  public float BfMeltSpeedMin { get; set; } = 0.5f;

  /// <summary>Fastest the melt cycle can run, as a multiple of the nominal rate.</summary>
  public float BfMeltSpeedMax { get; set; } = 2.0f;
  #endregion

  #region Blast furnace - extinguish residue
  /// <summary>Molten units that freeze into one nugget of the solid product on extinguish.</summary>
  public float BfUnitsPerSolidNugget { get; set; } = 5f;

  /// <summary>Fraction of a pile's coke that survives at the bottom of the shaft, sitting on the
  /// tuyeres where the blast burned hardest. 0 = burned to nothing.</summary>
  public float BfBurnoutFuelRetainedBottom { get; set; } = 0f;

  /// <summary>Fraction of a pile's coke that survives at the top of the shaft, which the blast never
  /// reached. Everything between is interpolated by height.</summary>
  public float BfBurnoutFuelRetainedTop { get; set; } = 0.4f;
  #endregion

  #region Blast furnace
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

  /// <summary>Air/blast (L/s) the blast furnace draws through each tuyere <b>at the reference coke
  /// fraction</b>. The live draw scales with the burden's coke content - air is the oxidant for coke, so
  /// a rich burden burns more of it and needs more air - clamped by
  /// <see cref="BfTuyereDrawMinFactor"/>/<see cref="BfTuyereDrawMaxFactor"/>.</summary>
  public float TuyereIntakeVolume { get; set; } = 14f;
  #endregion

  #region Cupola furnace
  // The cupola is the same furnace machine as the blast furnace (it inherits the whole
  // fire/melt/drain/residue core), run as a scrap re-melter: its remelt burden melts into CAST IRON
  // (1200 C, well below wrought iron's 1482) which drains out the lower tap, slag out the upper.
  // It is deliberately SLOWER than the blast furnace. RATIO: with CupolaMeltIntervalSec = 2 x
  // BfMeltIntervalSec at an equal per-cycle yield, the cupola renders at ~half the blast furnace's
  // nominal throughput, so roughly TWO cupolas keep pace with one blast furnace's pig-iron output.
  // (The shared melt-speed factor still scales both with superheat, so the effective ratio drifts a
  // little with how hard each is being blown - this is the nominal design ratio.)

  /// <summary>Temperature (°C) the hearth must reach (and hold) to melt cast iron - the near-eutectic
  /// remelt point, far below wrought iron's, which is the whole mechanical point of the cupola.</summary>
  public float CupolaCastIronMeltingPoint { get; set; } = 1200f;

  /// <summary>Maximum molten cast iron (units) the cupola holds before stalling - a smaller reservoir
  /// than the blast furnace's, matching the smaller furnace.</summary>
  public float CupolaMaxMoltenCastIron { get; set; } = 1200f;

  /// <summary>Maximum molten slag (units) the cupola holds before stalling.</summary>
  public float CupolaMaxMoltenSlag { get; set; } = 300f;

  /// <summary>Seconds a fired cupola burns before it extinguishes.</summary>
  public int CupolaMaxFuelBurnTime { get; set; } = 1200;

  /// <summary>Seconds above the melting point before the cupola transitions to the melting phase.</summary>
  public float CupolaMeltStartDelay { get; set; } = 240f;

  /// <summary>Seconds between melt cycles while melting. 2x the blast furnace's interval: this is the
  /// primary "slower than the blast furnace" lever (see the ratio note above).</summary>
  public float CupolaMeltIntervalSec { get; set; } = 20f;

  /// <summary>Molten cast iron (units) produced per melt cycle. Held equal to the blast furnace's
  /// per-cycle yield so the slowdown comes cleanly from the doubled interval, not a second lever.</summary>
  public float CupolaCastIronPerMeltCycle { get; set; } = 60f;

  /// <summary>Molten slag (units) produced per melt cycle.</summary>
  public float CupolaSlagPerMeltCycle { get; set; } = 8f;

  /// <summary>Remelt burden consumed per melt cycle.</summary>
  public int CupolaBlastMixPerMeltCycle { get; set; } = 12;

  /// <summary>Remelt burden that must be loaded into the shaft before the cupola can fire - lower than
  /// the blast furnace's, since the cupola's single-column shaft holds less.</summary>
  public int CupolaMixRequiredToFire { get; set; } = 160;

  /// <summary>Air/blast (L/s) the cupola draws through its single tuyere.</summary>
  public float CupolaTuyereIntakeVolume { get; set; } = 12f;
  #endregion

  #region Ore bunker
  /// <summary>Maximum burden (units) a finished ore bunker can hold across all grades.</summary>
  public int BunkerMaxBurden { get; set; } = 1152;
  #endregion

  #region Tall hopper
  // The tall hopper is a passthrough burden tank, not storage: burden goes in the top and drips
  // continuously into the furnace shaft below it. It holds one burden stack (any family) and the
  // furnace it feeds decides acceptance - the hopper never gates. These values shape the drip.

  /// <summary>Maximum burden (units) the tall hopper's tank holds - one burden stack (the burden item
  /// stacks to 128, so the default fills exactly one stack).</summary>
  public int HopperTallCapacity { get; set; } = 128;

  /// <summary>Burden units the tall hopper drips into the shaft each second while it has a target. A
  /// steady trickle that keeps a shaft charged without dumping the whole tank in one tick.</summary>
  public int HopperTallDropPerSecond { get; set; } = 8;

  /// <summary>Maximum burden (units) the hopper piles into one shaft coal-pile cell before it moves up
  /// to the next, so a narrow (single-column) shaft still reaches the fire threshold from few cells.</summary>
  public int HopperTallPileCap { get; set; } = 128;

  /// <summary>How many cells below the hopper it scans (down each candidate column) to find the shaft
  /// coal-pile column it charges. Covers a hopper sitting directly over, or beside-and-above, the shaft.</summary>
  public int HopperTallDropDepth { get; set; } = 8;
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

  // The mixer's per-item fuel (carbon) values (coke 2, charcoal 0.5) now live on the fuel material-role
  // in assets/iwex/config/materialroles.json, read via MaterialRoleRegistry.ValueOf - no longer a config
  // tunable here.

  /// <summary>Burden units the mixer drains into the container below per second while the lids are open.</summary>
  public float MixerDrainPerSecond { get; set; } = 8f;
  #endregion

  #region Recipe balance
  /// <summary>Active ironworking recipe cost level - <c>"normal"</c> or <c>"cheap"</c>. Toggled in-game
  /// by <c>/exmod recipes iwex &lt;level&gt;</c>; the per-recipe numbers live in the separate
  /// <see cref="IwexRecipeConfig"/> catalogue. Applied on the next world reload.</summary>
  public string RecipeLevel { get; set; } = "normal";
  #endregion

  #region Twin-tub blower
  // The iron tier's only air source: a mechanically driven twin-tub bellows feeding the blast main.
  // Deliberately weaker than the steam tiers' blowers - it makes COLD blast at a pressure that just
  // clears BlastPressureThreshold, so an iron-age blast furnace runs but never reaches the hot-blast
  // ceiling. Both output figures are at full axle speed and scale down with it.

  /// <summary>Litres of air per second the blower pushes into its network at <see cref="TwinTubBlowerMaxSpeed"/>.
  /// The default runs a single two-tuyere blast furnace on a coke-rich burden - the draw a rich burden
  /// demands is the highest the iron tier ever has to meet.</summary>
  public float TwinTubBlowerOutputPerSecond { get; set; } = 45f;

  /// <summary>
  /// Pressure ceiling (atm) the blower can raise its network to. It sits under
  /// <see cref="BoltedPipeBurstPressure"/> (bellows must not burst the tier's own pipe) and above what a
  /// coke-rich burden demands, but <b>below what a lean one does</b> - so a mechanical blower can run an
  /// iron furnace on a fuel-hungry charge and can never run the fuel-efficient charge that needs steam.
  /// </summary>
  public float TwinTubBlowerMaxPressure { get; set; } = 2.2f;

  /// <summary>Axle speed at/below which the blower delivers nothing - the bellows barely move.</summary>
  public float TwinTubBlowerMinSpeed { get; set; } = 0.5f;

  /// <summary>Axle speed at/above which the blower delivers its full <see cref="TwinTubBlowerOutputPerSecond"/>.</summary>
  public float TwinTubBlowerMaxSpeed { get; set; } = 1.5f;
  #endregion

  #region Flywheel (mechanical-energy storage)
  // The flywheel is the signature block of the energy MP network (docs/design/mp-energy-network.md): it
  // contributes rotational inertia, and the run's reservoir capacity is derived from it (1/2 I omega_max^2,
  // omega_max = ExlibValues.MpMaxSpeed). Inertia also sets spin-up time. A disc's I scales with R^4 * t, so
  // the large flywheel (5x5x2, ~1.7x radius and 2x thickness of the normal 3x3x1) holds roughly 15x the
  // energy and takes ~15x as long to charge - the two levers that make it the heavy-industry buffer.

  /// <summary>Rotational inertia of the normal (3x3x1) flywheel - the reference disc. Its capacity is
  /// <c>1/2 x this x MpMaxSpeed^2</c> MP.s.</summary>
  [ExConfigRange(0.01, 1_000_000)] // inertia divides derived speed - must stay positive
  public float FlywheelInertiaNormal { get; set; } = 10f;

  /// <summary>Rotational inertia of the large (5x5x2) flywheel - ~15x the normal disc (I scales with
  /// R^4 x thickness), so ~15x the stored energy and spin-up time.</summary>
  [ExConfigRange(0.01, 1_000_000)]
  public float FlywheelInertiaLarge { get; set; } = 150f;

  // The flywheel doubles as the vanilla-MP -> energy bridge: an axle (water wheel, windmill, engine)
  // coupled to its hub feeds the reservoir. This is the iron tier's charge path - mechanical power exists
  // long before steam. The bridge reads the coupled axle's speed off the hosted MP port and injects a
  // proportional power; torque is not modelled here (the whole sub-machine ecosystem is speed-driven).

  /// <summary>Drive torque (N.m) the bridge applies to the mpenergy shaft when the coupled axle turns at (or
  /// above) <see cref="FlywheelBridgeRatedAxleSpeed"/>; scales down linearly below it. At rest this is full
  /// torque so the bridge can start a load, but below the load + idle resistance the shaft never spins up.</summary>
  [ExConfigRange(0, 1_000_000)]
  public float FlywheelBridgeChargePower { get; set; } = 1f;

  /// <summary>Axle speed at/above which the bridge delivers its full <see cref="FlywheelBridgeChargePower"/>.
  /// Below it the drive torque scales down in proportion (vanilla MP rated speed is ~1).</summary>
  [ExConfigRange(0.01, 1000)]
  public float FlywheelBridgeRatedAxleSpeed { get; set; } = 1f;

  /// <summary>Rotational inertia (kg.m^2) a single cast-iron shaft segment adds to its run - the "Buffer" node's
  /// little rotating mass, so a shaft line rides jitter without a dedicated flywheel. Small vs a flywheel.</summary>
  [ExConfigRange(0, 1_000_000)]
  public float ShaftInertia { get; set; } = 0.5f;
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
    // "burnedout" comes first and catches burden the furnace already consumed the coke out of, so a
    // salvaged charge reads "re-coke it" rather than passing for a deliberately low-coke grade. It
    // keeps the same flux floor as every other grade - ProfileLangKey derives its flux-shortfall
    // message from the lowest floor in this list, so a profile without one would silence it.
    new()
    {
      Key = "burnedout",
      MinFlux = 0.05f,
      MaxFuel = 0.02f,
    },
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
