using System.Collections.Generic;
using ExpandedLib.Registries.Config;
using Vintagestory.API.Common;

namespace IronworkingExpanded;

/// <summary>
/// JSON-serializable gameplay tunables for Ironworking Expanded: the furnaces, the molten network and
/// the iron-tier machinery this mod owns. Loaded from and written to <c>ModConfig/ex_values.json</c>;
/// the property defaults below apply when the file or a key is missing, and any NaN, infinite or
/// negative value resets to its default on load. Accessed through <see cref="IwexValues"/>, not directly.
/// </summary>
[ExConfigRegister(
  "ex_values.json",
  "iwex",
  LegacyFileNames = new string[] { "iwex_values.json" },
  Manageable = true
)]
public class IwexConfig : IExVersionedConfig {
  /// <summary>Mod version that last wrote this file; drives the <see cref="Migrations"/> resets.
  /// Managed by <see cref="ExConfigRegister{TConfig}"/> - do not set by hand.</summary>
  public string? ConfigVersion { get; set; }

  /// <summary>Version-driven default resets.</summary>
  public static readonly ExConfigMigration[] Migrations =
  [
    // 0.2.0 changed the charge scale's currency from burden units to items (2 a band, 32 a pile block).
    // A saved value carries no marker of which currency it is in, so it is reset rather than converted.
    new()
    {
      ToVersion = "0.2.0",
      ResetFields =
      [
        nameof(ChargeItemsPerBand),
        nameof(CupolaChargeMetalUnitsPerBlock),
      ],
    },
    // 0.3.0 renamed five firebox keys. Deletions need no row - a saved key with no field to bind to is
    // ignored on load - but a rename does, or a retuned old key is stranded in the file while the new
    // name takes the shipped default.
    new()
    {
      ToVersion = "0.3.0",
      ResetFields =
      [
        nameof(FireboxMaxFuelBurnTime),
        nameof(FireboxMeltStartDelay),
        nameof(FireboxMeltIntervalSec),
        nameof(FireboxHeatRatePerSecond),
        nameof(FireboxCoolRatePerSecond),
      ],
    },
  ];

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
  /// Pour temperature (°C) above which a fired-clay tool mold shatters instead of casting. Sits between
  /// the vanilla casting metals (tin bronze 950 up to copper 1084) and the iron family (cast iron ~1150,
  /// wrought iron and steel ~1500), so it bites only on iron-family pours. Gates the small clay molds
  /// that fit the mold pedestal; the large anvil and helve-hammer molds are exempt, and cast-iron molds
  /// never shatter. See <see cref="BlockNetworkMolten.Blocks.ClayHeatGate"/>.
  /// </summary>
  public float ClayMoldHeatCeiling { get; set; } = 1100f;

  /// <summary>
  /// Whether the enhanced mold handling (spill a filled mold moved out of hand, burn the hand holding a
  /// hot one, render and carry the cast) also applies to vanilla clay tool molds. The mod's own cast
  /// molds always have it. Toggle live with <c>/exmod config iwex EnhanceVanillaMolds true|false</c>.
  /// </summary>
  public bool EnhanceVanillaMolds { get; set; } = false;

  /// <summary>Minimum mold-content temperature (°C) that burns a bare-handed player carrying a filled
  /// mold. Always applies to cast molds, and to vanilla clay molds when
  /// <see cref="EnhanceVanillaMolds"/> is on.</summary>
  public float MoldBurnMinTemperature { get; set; } = 200f;

  // The per-connection flow driver (MoltenFlowRate, MoltenMinFlowAmount) lives in exlib's config
  // alongside MoltenNetwork. The cooldown values above are read by iwex's MoltenMetal/canal cells.

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

  // Ignition has no tunable charge threshold: it is positional and pneumatic (a complete raceway course
  // plus air at pressure), not a quantity of charge. Furnace capacity is
  // `BlockEntityFurnaceCore.ChargeCapacityUnits`, derived from the furnace's own geometry.

  #region Air blower / blast
  // Blast pressure is derived from the burden, not fixed per furnace. Coke is the permeable component of
  // the charge column, so a coke-lean burden packs denser and needs a higher pressure to push the same
  // air through, while a coke-rich one blows easily but demands more air. That sets the tier gate: a
  // mechanical blower can force a coke-rich charge, a lean one needs steam-tier pressure and pipe.

  /// <summary>Blast pressure (atm) a burden at <see cref="BfReferenceFuelFrac"/> demands. Where this
  /// lands relative to <see cref="PlatedPipeBurstPressure"/> and the blower ceiling sets the tier
  /// gate.</summary>
  public float BfBlastPressureAtReference { get; set; } = 2.0f;

  /// <summary>Atmospheres added per unit the coke fraction falls below <see cref="BfReferenceFuelFrac"/>,
  /// and subtracted per unit above it. At the shipped defaults a 30% coke burden asks 1.25 atm, a 20%
  /// one 2.0 and a 10% one 2.75 - above both the bellows' ceiling and plated pipe's burst rating.</summary>
  public float BfBlastPressureCokeSensitivity { get; set; } = 7.5f;

  /// <summary>Floor on the derived blast pressure (atm).</summary>
  public float BfBlastPressureMin { get; set; } = 1.2f;

  /// <summary>Ceiling on the derived blast pressure (atm), so a near-cokeless charge cannot demand a
  /// pressure no source can reach.</summary>
  public float BfBlastPressureMax { get; set; } = 6f;

  /// <summary>Lower clamp on the air-draw factor, so a coke-starved burden still draws some air.</summary>
  public float BfTuyereDrawMinFactor { get; set; } = 0.4f;

  /// <summary>Upper clamp on the air-draw factor, so a coke-packed burden's demand stays blowable.</summary>
  public float BfTuyereDrawMaxFactor { get; set; } = 1.8f;
  #endregion

  #region Pipes
  // iwex owns the base pipe block (the plated tier) and the "pipe" network registration, so the
  // pipe-content tunables live here. The generic pipe-network constants (LitresPerPipe, leak rates) live
  // in exlib's own config.

  /// <summary>
  /// Burst pressure (atm) of a plain plated (iwex) pipe segment; the weakest pipe limits a run. Higher
  /// tiers register their own rating (lpex cast 5, hpex rolled 12). It also sets the tier's capacity: a
  /// network holds <c>burst x pipes x litresPerPipe</c>.
  /// </summary>
  public float PlatedPipeBurstPressure { get; set; } = 2.5f;

  /// <summary>
  /// Throughput (L/s) of a plated (iwex) pipe segment: how much the line passes per second, as distinct
  /// from how much it holds. The weakest segment caps the whole run; higher tiers register their own. 50
  /// clears every line the iron tier runs (twin-tub blower 45, hot furnace exhaust 48) and refuses the
  /// steam-tier services above it, so a converter's blast main and a cowper's hot-blast run must be built
  /// in cast pipe. The furnace tuyere is exempt, being an intake port rather than a length of main.
  /// Throughput is derived from tier only, never from bore. See <c>docs/design/mechanics/pipe-network.md</c>.
  /// </summary>
  public float PlatedPipeThroughput { get; set; } = 50f;

  /// <summary>Gas (L/s) a vanilla chimney draws from the network when capping the top connector of a
  /// chimney-ventable fitting (a passthrough / passthrough-bend / outlet). Used by the iwex chimney-vent
  /// strategy that every "pipe" network carries.</summary>
  public float ChimneyGasDrawRate { get; set; } = 16.0f;
  #endregion

  #region Blast furnace - heat balance
  // The furnace has no maximum temperature: it settles where the heat it makes and the heat it loses
  // balance, T_process = T_in - T_loss, floored at ambient. T_in is coke combustion (burden fuel richness
  // x the air reaching the tuyeres) plus any cowper preheat; T_loss is radiation plus the cold mass of
  // the charge plus a cold ambient. Cold and hot blast furnaces differ through those terms rather than
  // through a branch in code. See docs/design/conventions.md.

  /// <summary>Temperature (°C) a lit charge holds on its own, before any coke credit or draught.</summary>
  public float BfCombustionBaseTemp { get; set; } = 950f;

  /// <summary>Temperature (°C) coke combustion adds at the reference coke ratio with full blast supply.</summary>
  public float BfCombustionCokeGain { get; set; } = 900f;

  /// <summary>Coke fraction the combustion gain is calibrated at (the "standard" burden grade midpoint).</summary>
  public float BfReferenceFuelFrac { get; set; } = 0.20f;

  /// <summary>How strongly the combustion gain responds to a coke ratio off <see cref="BfReferenceFuelFrac"/>.</summary>
  public float BfCokeSensitivity { get; set; } = 0.35f;

  /// <summary>Floor on the coke factor, so a fuel-starved burden still burns.</summary>
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

  /// <summary>Blast-supply fraction (arrived air / demanded air) below which a lit furnace counts as
  /// air-starved: sustained operation under this floor counts a disruption toward extinguish, after the
  /// extinguish grace. A weak but present blast above the floor keeps the furnace alive and cold on
  /// natural draught. 0 disables air-starvation extinguish.</summary>
  public float BfStarvationSupplyFrac { get; set; } = 0.1f;

  /// <summary>Degrees of <c>T_in</c> gained per degree the blast is preheated above ambient. Only a
  /// charged cowper raises the pipe temperature at the tuyere, so this is the hot-blast mechanic.</summary>
  public float BfPreheatCoefficient { get; set; } = 0.35f;

  /// <summary>Baseline heat loss (°C) radiated through the stack.</summary>
  public float BfRadiationLossBase { get; set; } = 120f;

  /// <summary>Heat loss (°C) from cold charge mass with the furnace loaded to its capacity
  /// (<c>BlockEntityFurnaceCore.ChargeCapacityUnits</c>). Linear in load, so half-loaded pays half. The
  /// denominator must be the geometry-derived capacity: sized to a fire threshold instead, a furnace
  /// reads full at a fraction of its charge and pays the whole penalty while mostly empty.</summary>
  public float BfChargeLossFull { get; set; } = 310f;

  /// <summary>Ambient temperature (°C) the loss term is calibrated at; only colder than this costs heat.</summary>
  public float BfAmbientReferenceTemp { get; set; } = 20f;

  /// <summary>Heat loss (°C) per degree the ambient sits below <see cref="BfAmbientReferenceTemp"/>.</summary>
  public float BfAmbientLossPerDegree { get; set; } = 1.0f;

  /// <summary>Ambient temperature (°C) assumed when the climate is unavailable (unloaded chunk, headless).</summary>
  public float BfAmbientFallbackTemp { get; set; } = 20f;

  /// <summary>How fast (°C/s) the hearth climbs toward its process temperature.</summary>
  public float FireboxHeatRatePerSecond { get; set; } = 4f;

  /// <summary>How fast (°C/s) the hearth falls back toward its process temperature.</summary>
  public float FireboxCoolRatePerSecond { get; set; } = 4f;

  /// <summary>
  /// Units of carbon the raceway burns each second at full blast, before the air factor. Air is the
  /// reagent, so this is scaled by <c>HeatBalance.AirFactor</c> rather than run flat: a stopped blower
  /// slows the fire to natural draught, and a lit shaft runs until its carbon is gone. Stated per tuyere,
  /// not per furnace, so a wider furnace has more throughput and not only more capacity. Calibrated so
  /// the two-tuyere cold blast furnace burns 0.35 u/s in total.
  /// </summary>
  public float BfRacewayCarbonPerTuyerePerSecond { get; set; } = 0.175f;

  /// <summary>
  /// Charge units of burden the raceway melts per unit of carbon burned, before <c>MeltSpeedFactor</c> -
  /// the mod's coke rate, inverted. Production is metered by the carbon and has no rate of its own.
  /// 4.0 is the reference grade read as a ratio: a burden at <c>BfReferenceFuelFrac</c> (20% coke) is
  /// 80:20, so 4 units of burden per unit of carbon eats the column in the proportion it was laid.
  /// Dimensionless, so no per-furnace scaling is needed. See <c>docs/design/layered-charge.md</c>
  /// § "What sets the rate: the blast".
  /// </summary>
  public float BfBurdenPerCarbonUnit { get; set; } = 4f;

  /// <summary>
  /// Gas heat capacity, in charge units per degree, that one unit of carbon burned makes: how much charge
  /// that gas can warm by a degree for every degree the gas itself falls. Per unit burned, not per unit
  /// present, so a thick course of coke at the raceway cannot heat a shaft indefinitely. Quantity only -
  /// the gas temperature comes from <c>ComputeHeatBalance</c>'s <c>T_process</c>. Calibrated so a full
  /// 4-level cold column climbs most of the way to the flame over a few minutes.
  /// </summary>
  public float BfRacewayGasPerCokeUnit { get; set; } = 20f;

  /// <summary>
  /// The share of the gas's remaining excess temperature that one unit of charge absorbs as the gas
  /// passes it; a band of <c>U</c> units takes <c>1 - (1-this)^U</c>. Stated per unit, never per band,
  /// since bands are cut wherever charging and descent leave them. At the shipped value a full 4-level
  /// cold column takes about nine tenths of the heat out of the gas before the stockline.
  /// </summary>
  public float BfShaftGasTransferFrac { get; set; } = 0.02f;

  /// <summary>
  /// The <c>fuel</c>-role value that counts as one unit of carbon. Set to coke's, so coke reads 1.0 and
  /// every calibration constant in this file is stated in coke. A fuel item's carbon is its role value
  /// over this reference, so <c>game:coke</c> (value 2) is 1.0 and <c>game:charcoal</c> (value 1) is 0.5;
  /// a fuel granted the role with no value takes the registry fallback of 1.0, half of coke. The per-fuel
  /// values live in <c>assets/iwex/config/materialroles.json</c>, read through
  /// <see cref="ExpandedLib.Materials.MaterialRoleRegistry"/>. Moving this key changes the meaning of every coke figure here.
  /// </summary>
  public float BfFuelCarbonReference { get; set; } = 2f;

  /// <summary>
  /// Iron units the blast furnace recovers per unit of ore content in the burden it melts.
  /// <c>docs/design/mechanics/metal-recovery.md</c> § "The ladder" fixes recovery at 8.5 u per nugget,
  /// and the chain is one nugget to one crushed item to one burden item, so per ore unit and per nugget
  /// are the same number. Must stay stated per ore unit: a per-charge-unit or per-band figure is this
  /// times the ore share (<c>BurdenMix.IronFrac</c>), which moves with the burden recipe.
  /// <c>OreRecoveryGuardRailTests</c> pins it.
  /// </summary>
  public float BfIronPerOreUnit { get; set; } = 8.5f;

  /// <summary>Slag units the blast furnace renders per unit of ore content, alongside
  /// <see cref="BfIronPerOreUnit"/>. Held at a 6:1 iron-to-slag ratio, stated rather than derived so a
  /// recovery change moves the ratio only when intended.</summary>
  public float BfSlagPerOreUnit { get; set; } = 8.5f / 6f;

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

  /// <summary>Fraction of a pile's coke that survives at the bottom of the shaft, on the tuyeres.
  /// 0 = burned to nothing.</summary>
  public float BfBurnoutFuelRetainedBottom { get; set; } = 0f;

  /// <summary>Fraction of a pile's coke that survives at the top of the shaft, which the blast never
  /// reached. Everything between is interpolated by height.</summary>
  public float BfBurnoutFuelRetainedTop { get; set; } = 0.4f;
  #endregion

  #region Hearth sizing (the crucible pool)

  /// <summary>Metal units in one 1/16 band of a hearth cell. Derived: a block is 16³ voxels at
  /// <c>1 vx³ = 2.5 u</c> = 10 240 u, so one sixteenth is 640. Charge units and metal units are the same
  /// currency, so this is comparable with <c>ChargeUnitsPerBlock</c>.</summary>
  public int HearthUnitsPerBand { get; set; } = 640;

  /// <summary>
  /// The band the slag spout's channel floor sits at, 10 of 16, and so the level the crucible overflows
  /// at while that tap is open. Read off <c>assets/editable/shapes/furnace-block-slagtap.json</c>, whose
  /// top-level (so absolute) <c>TapCanal</c> element is at Y 10-11; the iron tap's sits at Y 2-3, which is
  /// why that one drains the floor. Iron and slag share one volume rather than separate budgets, so an
  /// unflushed slag layer takes space iron could occupy: 3 cells x 10 bands x 640 u = 19 200 u. See
  /// <c>docs/design/layered-charge.md</c> § "Sizing - the band is the number".
  /// </summary>
  public int HearthSlagSpoutBand { get; set; } = 10;

  #endregion

  #region Blast furnace
  /// <summary>Temperature (°C) the hearth must reach (and hold) to start melting iron.</summary>
  public float BfIronMeltingPoint { get; set; } = 1482f;

  /// <summary>Maximum molten iron (units) the furnace can hold before stalling.</summary>
  public float BfMaxMoltenIron { get; set; } = 2400f;

  /// <summary>Maximum molten slag (units) the furnace can hold before stalling.</summary>
  public float BfMaxMoltenSlag { get; set; } = 600f;

  /// <summary>Seconds a fired furnace burns before it extinguishes.</summary>
  public int FireboxMaxFuelBurnTime { get; set; } = 1200;

  /// <summary>Seconds above the melting point before the furnace transitions to the melting phase.</summary>
  public float FireboxMeltStartDelay { get; set; } = 300f;

  /// <summary>Seconds between melt cycles while melting.</summary>
  public float FireboxMeltIntervalSec { get; set; } = 10f;

  /// <summary>Molten iron (units) produced per melt cycle. Unread by src - the live yield path is
  /// <see cref="BfIronPerOreUnit"/> - and kept only because <c>OreRecoveryGuardRailTests</c> derives the
  /// recovery figure from this and <see cref="BfBlastMixPerMeltCycle"/>.</summary>
  public float BfIronPerMeltCycle { get; set; } = 60f;

  /// <summary>Blast-mix consumed per melt cycle. Unread by src, kept only for
  /// <c>OreRecoveryGuardRailTests</c>.</summary>
  public int BfBlastMixPerMeltCycle { get; set; } = 16;

  /// <summary>Air/blast (L/s) the blast furnace draws through each tuyere at the reference coke fraction.
  /// The live draw scales with the burden's coke content, clamped by
  /// <see cref="BfTuyereDrawMinFactor"/>/<see cref="BfTuyereDrawMaxFactor"/>.</summary>
  public float TuyereIntakeVolume { get; set; } = 14f;

  /// <summary>Units of pool a tap considers per drain tick. Set to the molten network's own rate so tap,
  /// canal and bed move metal at one speed.</summary>
  public int TapDrainPerTick { get; set; } = 50;

  /// <summary>Fraction of those units that actually leave the iron taphole. Below 1 it models the notch
  /// throttling a full hearth.</summary>
  public float TapIronStackFactor { get; set; } = 0.6f;

  /// <summary>Fraction of those units that leave the cinder notch. Higher than iron's: slag is thinner and
  /// runs freely.</summary>
  public float TapSlagStackFactor { get; set; } = 0.8f;
  #endregion

  #region Cupola furnace
  // The cupola is the same furnace machine as the blast furnace (it inherits the fire/melt/drain/residue
  // core), run as a scrap re-melter: its remelt burden melts into cast iron (1200 C, below wrought iron's
  // 1482) out the lower tap, slag out the upper. It runs slower through geometry rather than an interval
  // key - a cupola block holds ~3 000 metal units against an ore shaft's 32 items, so `ChargeUnitScale`
  // stretches the same carbon rate over a far larger charge. Roughly two cupolas keep pace with one blast
  // furnace, drifting with how hard each is blown.

  /// <summary>Temperature (°C) the hearth must reach and hold to melt cast iron - the near-eutectic
  /// remelt point, far below wrought iron's.</summary>
  public float CupolaCastIronMeltingPoint { get; set; } = 1200f;

  /// <summary>Maximum molten cast iron (units) the cupola holds before stalling.</summary>
  public float CupolaMaxMoltenCastIron { get; set; } = 1200f;

  /// <summary>Maximum molten slag (units) the cupola holds before stalling.</summary>
  public float CupolaMaxMoltenSlag { get; set; } = 300f;

  /// <summary>Molten cast iron (units) produced per melt cycle. Held equal to the blast furnace's
  /// per-cycle yield, so the cupola's slower output comes from its charge scale alone.</summary>
  public float CupolaCastIronPerMeltCycle { get; set; } = 60f;

  /// <summary>Molten slag (units) produced per melt cycle.</summary>
  public float CupolaSlagPerMeltCycle { get; set; } = 8f;

  /// <summary>Remelt burden consumed per melt cycle.</summary>
  public int CupolaBlastMixPerMeltCycle { get; set; } = 12;

  /// <summary>Air/blast (L/s) the cupola draws through its single tuyere.</summary>
  public float CupolaTuyereIntakeVolume { get; set; } = 12f;
  #endregion

  #region Firebox (reverberatory) furnaces
  // A firebox holds a fire, not a charge column, so its ignition threshold is stated per cell and the
  // machine multiplies by the firebox it declares (see BlockEntityFireboxFurnace.FireboxCellCount): the
  // puddling furnace's firebox is one cell and the reheat furnace's is two.

  /// <summary>
  /// Fuel units per firebox cell that must be loaded before a reverberatory hearth will light. A cell
  /// holds <see cref="FireboxLayersPerCell"/> x <see cref="FireboxUnitsPerLayer"/> = 12 units, so the
  /// default is one cell's capacity on a hearth of any size. Must stay at or under that product, or no
  /// firebox can fire. <c>FurnaceBranchGuards.NoFireboxAsksForMoreThanItsCellsCanHold</c> asserts it.
  /// </summary>
  public int FireboxMixPerCell { get; set; } = 12;

  /// <summary>
  /// Fuel units one drawn layer of a firebox holds, per cell. The shape draws six stacked slabs
  /// (<c>CokeL1</c>..<c>CokeL6</c>), so the pool's capacity is <see cref="FireboxLayersPerCell"/> x this x
  /// the furnace's firebox cell count: a two-cell reheat firebox costs twice a one-cell puddling firebox
  /// for the same visible fill, though the player interacts once.
  /// </summary>
  public int FireboxUnitsPerLayer { get; set; } = 2;

  /// <summary>Drawn fuel layers in one firebox cell. Pinned by the shape, which has exactly six
  /// <c>CokeL*</c> elements: raising this draws layers that do not exist and the top of the bed stops
  /// rendering. Change the shape first.</summary>
  public int FireboxLayersPerCell { get; set; } = 6;
  #endregion

  #region Burdenmaker
  // See docs/design/machines/burdenmaker.md. Capacities: ore 512 (one full raw charge), bunker 1152,
  // flux 205 (512 / 2.5, the drawn hopper width ratio). The flux hopper is storage rather than a measure:
  // at a ~5% target flux fraction a full 512-unit ore charge wants only ~27 lime, so 205 is about seven
  // batches, and the player judges the ratio from the readout.

  /// <summary>Maximum crushed/roasted iron ore (units) the burdenmaker's wide hopper holds.</summary>
  public int BurdenmakerOreCapacity { get; set; } = 512;

  /// <summary>Maximum flux (units) the burdenmaker's narrow hopper holds.</summary>
  public int BurdenmakerFluxCapacity { get; set; } = 205;

  /// <summary>Maximum burden (units) the shared basin holds before it must be emptied.</summary>
  public int BurdenmakerBunkerCapacity { get; set; } = 1152;
  #endregion

  #region Tall hopper
  // The tall hopper is a passthrough burden tank, not storage: burden goes in the top and drips
  // continuously into the furnace shaft below it. It holds one burden stack of any family, and the
  // furnace it feeds decides acceptance - the hopper never gates.

  /// <summary>Maximum burden (units) the tall hopper's tank holds - one burden stack (the burden item
  /// stacks to 128, so the default fills exactly one stack).</summary>
  public int HopperTallCapacity { get; set; } = 128;

  /// <summary>Burden units the tall hopper drips into the shaft each second while it has a target.</summary>
  public int HopperTallDropPerSecond { get; set; } = 8;

  // There is no pile cap and no drop-depth key. A column's ceiling is its own chargeable cell count times
  // `ChargeItemsPerBand * BandsPerBlock` (32 items a block), so a separate cap could only under-cut it,
  // and the hopper resolves the furnace core through the multiblock anchor link, not by scanning down.
  #endregion

  #region Shaft charge columns
  /// <summary>
  /// Items one band of a shaft column holds. A pile block is
  /// <see cref="BlockStructures.Furnaces.ChargeColumn.BandsPerBlock"/> bands, so it takes
  /// <c>16 x 2 = 32</c> items, part coke and part burden, laid as bands. The quantum is items for every
  /// material alike; what an item is worth appears only when it converts. Applies to the ore charge
  /// (coke + burden); a remelt pile takes coke as items, then metal by units up to
  /// <see cref="CupolaChargeMetalUnitsPerBlock"/>. See <c>docs/design/layered-charge.md</c>.
  /// </summary>
  public int ChargeItemsPerBand { get; set; } = 2;

  /// <summary>
  /// Metal (units) one remelt pile block accepts, on top of its coke. A bit (5 u), a chunk (25 u) and a
  /// whole pig (375 u) each contribute their own units toward it. 3 000 u is 8 pigs against 8 bands of
  /// coke, coke taking about half a cupola's pile height because it is bulky (~0.5 t/m³ against loose pig
  /// at ~4.6); it divides cleanly into 8 pigs, 120 chunks or 600 bits. A full 4-cell cupola shaft is
  /// 12 000 u of charge, not of yield.
  /// </summary>
  public int CupolaChargeMetalUnitsPerBlock { get; set; } = 3000;
  #endregion

  // The burdenmaker has no drain-rate key: it has no mechanism to hang a throughput lever on. Per-item
  // fuel carbon values (coke 2, charcoal 0.5) live in the fuel material-role in
  // assets/iwex/config/materialroles.json, read via MaterialRoleRegistry.ValueOf - not here.

  #region Recipe balance
  /// <summary>Active ironworking recipe cost level - <c>"normal"</c> or <c>"cheap"</c>. Toggled in-game
  /// by <c>/exmod recipes iwex &lt;level&gt;</c>; the per-recipe numbers live in the separate
  /// <see cref="IwexRecipeConfig"/> catalogue. Applied on the next world reload.</summary>
  public string RecipeLevel { get; set; } = "normal";
  #endregion

  #region Twin-tub blower
  // The iron tier's only air source: a mechanically driven twin-tub bellows feeding the blast main. Both
  // output figures are at full axle speed and scale down with it.

  /// <summary>Litres of air per second the blower pushes into its network at
  /// <see cref="TwinTubBlowerMaxSpeed"/>. The default runs a single two-tuyere blast furnace on a
  /// coke-rich burden, the highest draw the iron tier has to meet.</summary>
  public float TwinTubBlowerOutputPerSecond { get; set; } = 45f;

  /// <summary>Pressure ceiling (atm) the blower can raise its network to. Sits under
  /// <see cref="PlatedPipeBurstPressure"/> so the bellows cannot burst the tier's own pipe, and between
  /// what a coke-rich burden demands and what a lean one does, so it can run a fuel-hungry charge but not
  /// the fuel-efficient one that needs steam.</summary>
  public float TwinTubBlowerMaxPressure { get; set; } = 2.2f;

  /// <summary>Axle speed at/below which the blower delivers nothing.</summary>
  public float TwinTubBlowerMinSpeed { get; set; } = 0.5f;

  /// <summary>Axle speed at/above which the blower delivers its full <see cref="TwinTubBlowerOutputPerSecond"/>.</summary>
  public float TwinTubBlowerMaxSpeed { get; set; } = 1.5f;
  #endregion

  #region Flywheel (mechanical-energy storage)
  // The flywheel contributes rotational inertia to the energy MP network; a run's reservoir capacity is
  // 1/2 I omega_max^2 with omega_max = ExlibValues.MpMaxSpeed, and inertia also sets spin-up time. A
  // disc's I scales with R^4 * t, so the large flywheel (5x5x2) holds roughly 15x the energy of the
  // normal 3x3x1 and takes ~15x as long to charge. See docs/design/mechanics/mp-energy.md.

  /// <summary>Rotational inertia of the normal (3x3x1) flywheel, the reference disc. Its capacity is
  /// <c>1/2 x this x MpMaxSpeed^2</c> MP.s.</summary>
  [ExConfigRange(0.01, 1_000_000)] // inertia divides derived speed - must stay positive
  public float FlywheelInertiaNormal { get; set; } = 10f;

  /// <summary>Rotational inertia of the large (5x5x2) flywheel: ~15x the normal disc (I scales with
  /// R^4 x thickness), so ~15x the stored energy and spin-up time.</summary>
  [ExConfigRange(0.01, 1_000_000)]
  public float FlywheelInertiaLarge { get; set; } = 150f;

  // The flywheel doubles as the vanilla-MP to energy bridge: an axle (water wheel, windmill, engine)
  // coupled to its hub feeds the reservoir. The bridge reads the coupled axle's speed off the hosted MP
  // port and injects a proportional power; torque is not modelled, the ecosystem being speed-driven.

  /// <summary>Drive torque (N.m) the bridge applies to the mpenergy shaft when the coupled axle turns at
  /// or above <see cref="FlywheelBridgeRatedAxleSpeed"/>; scales down linearly below it. At rest this is
  /// full torque so the bridge can start a load, but below the load plus idle resistance the shaft never
  /// spins up.</summary>
  [ExConfigRange(0, 1_000_000)]
  public float FlywheelBridgeChargePower { get; set; } = 1f;

  /// <summary>Axle speed at/above which the bridge delivers its full <see cref="FlywheelBridgeChargePower"/>.
  /// Below it the drive torque scales down in proportion (vanilla MP rated speed is ~1).</summary>
  [ExConfigRange(0.01, 1000)]
  public float FlywheelBridgeRatedAxleSpeed { get; set; } = 1f;

  /// <summary>Rotational inertia (kg.m^2) a single cast-iron shaft segment adds to its run - the "Buffer"
  /// node's rotating mass, so a shaft line rides jitter without a dedicated flywheel.</summary>
  [ExConfigRange(0, 1_000_000)]
  public float ShaftInertia { get; set; } = 0.5f;
  #endregion

  #region Rolling mill (the first mpenergy consumer)
  // The physics lives in RollingPass; these are the balance levers. Below RollingTempC the stock's
  // friction collapses (delta_max = mu^2 R, ~30x worse cold) and its flow stress climbs, so a cooling
  // piece both refuses to bite and loads harder. See docs/design/machines/rolling-mill.md.

  /// <summary>Stock temperature (C) at or above which iron rolls hot: it grips the rolls and deforms
  /// cheaply. Below it the piece is cold-working, barely biting and resisting far harder.</summary>
  [ExConfigRange(0, 3000)]
  public float RollingTempC { get; set; } = 900f;

  /// <summary>How much harder fully-cold stock resists than hot stock (the flow-stress multiplier). At an
  /// order of magnitude a cold pass overdraws the flywheel and stalls the mill.</summary>
  [ExConfigRange(1, 1000)]
  public float RollingColdStressMultiplier { get; set; } = 10f;

  /// <summary>Degrees below <see cref="RollingTempC"/> over which the stock climbs from hot flow stress to the
  /// full <see cref="RollingColdStressMultiplier"/>. A short span makes cooling punishing, a long one forgiving.</summary>
  [ExConfigRange(1, 3000)]
  public float RollingColdSpanC { get; set; } = 400f;

  /// <summary>Radius (in block-space units) of the mill's rolls. It sets both the deepest legal draft
  /// (<c>delta_max = mu^2 R</c>) and the contact arc, so a bigger roll takes deeper passes but loads harder.</summary>
  [ExConfigRange(0.01, 100)]
  public float RollingRollRadius { get; set; } = 4f;

  /// <summary>
  /// Torque (N.m) the stand draws off its run while stock is between the rolls, before the cold multiplier.
  /// The mill has two states and this is the working one; an empty stand draws nothing. Calibrated against
  /// one bridge drive (<see cref="FlywheelBridgeChargePower"/> = 1 N.m) less friction at omega_max (0.6),
  /// leaving 0.4 N.m of headroom: at 0.34 a hot pass runs on a single water wheel with about 15 % to spare
  /// and a piece that drops below rolling heat overdraws it. Raise it to make the mill hungrier.
  /// </summary>
  [ExConfigRange(0, 10_000)]
  public float RollingLoadTorque { get; set; } = 0.34f;

  /// <summary>Fraction of its excess heat the stock sheds per second, during the carry-back as well as
  /// under the rolls. Tuned so a single pass finishes comfortably (still well above rolling heat after
  /// ~25 s) while a full schedule of round trips needs a reheat. Much above this a pass freezes
  /// mid-bite.</summary>
  [ExConfigRange(0, 10)]
  public float RollingCoolRate { get; set; } = 0.005f;

  /// <summary>Ambient temperature (C) the stock cools toward on the mill floor.</summary>
  [ExConfigRange(-50, 500)]
  public float RollingAmbientC { get; set; } = 20f;
  #endregion

  #region Burden grades
  /// <summary>
  /// Named burden grades, matched by flux band; fractions are 0..1 of the total mix. The classifier
  /// returns the first profile whose band contains the mix's flux fraction, so the list is scanned in
  /// order and a boundary belongs to the earlier band. The shipped bands tile 0..1, so "off-spec" is
  /// unreachable unless a player edits a hole into them. Shared by the burden tooltip and the
  /// burdenmaker's readout; extensible and retunable in <c>ex_values.json</c>.
  /// </summary>
  public List<BurdenProfile> BurdenProfiles { get; set; } =
  [
    // Burden is graded by flux alone: fuel is charged as its own bands at the hopper and metered at the
    // raceway, so it is not a quality the item carries. Bands are inclusive on both ends and scanned in
    // order, so a boundary belongs to the earlier band: 0.03 reads underfluxed, 0.08 reads standard.
    // Ordering is the tie-break - do not sort this list.
    new() { Key = "underfluxed", MaxFlux = 0.03f },
    new()
    {
      Key = "standard",
      MinFlux = 0.03f,
      MaxFlux = 0.08f,
    },
    new() { Key = "overfluxed", MinFlux = 0.08f },
  ];
  #endregion
}

/// <summary>
/// One named burden grade: a lang-keyed label (<c>iwex:burden-profile-{Key}</c>) plus an inclusive flux
/// band (fraction 0..1 of the total mix). Pure data; the classifier lives in <see cref="Items.Burden"/>.
/// There are no iron or fuel bounds: burden is ore and flux only, so <c>IronFrac = 1 - FluxFrac</c>, and
/// fuel is charged as its own bands.
/// </summary>
public class BurdenProfile {
  /// <summary>Grade key; the burdenmaker readout and the tooltip show <c>iwex:burden-profile-{Key}</c>.</summary>
  public string Key { get; set; } = "";

  public float MinFlux { get; set; }
  public float MaxFlux { get; set; } = 1f;
}
