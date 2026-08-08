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

  /// <summary>Version-driven default resets.</summary>
  public static readonly ExConfigMigration[] Migrations =
  [
    // The charge scale changed currency, not just magnitude: a band is now measured in
    // items (2 a band, 32 a pile block) where it was previously 8 burden units. A saved config carries the
    // old numbers with no way to tell which currency they are in, so anything the player tuned against the
    // unit scale reads as a wildly wrong item count - 8 items a band is 4x the shipped charge.
    //
    // These are reset rather than converted deliberately: a conversion would have to assume the saved
    // value was the shipped default scaled by hand, and a player who had already retuned would get a number
    // that is wrong in a new way. Resetting is visible and recoverable; a silent mis-conversion is not.
    new()
    {
      ToVersion = "0.2.0",
      ResetFields =
      [
        nameof(ChargeItemsPerBand),
        nameof(CupolaChargeMetalUnitsPerBlock),
        // `HopperTallPileCap`, `BlastMixRequiredToFire` and `CupolaMixRequiredToFire` were deleted
        // outright, so there is no field to reset. A saved config still carrying them is ignored on
        // load, which is the same outcome a reset would buy. See the 0.3.0 note.
      ],
    },
    // 0.3.0 deleted ten keys and renamed five, and the two halves need different handling.
    //
    // The deletions need no reset row at all - a saved `ex_values.json` still carrying
    // `BlastMixRequiredToFire`, `CupolaMixRequiredToFire`, `CupolaMaxFuelBurnTime`, `CupolaMeltStartDelay`
    // or `CupolaMeltIntervalSec` simply has no field to bind them to and they are ignored on load. Listing
    // a deleted field in `ResetFields` would be a `nameof` that no longer compiles.
    //
    // The renames are the dangerous half and this row is what makes them safe. `BfMaxFuelBurnTime`
    // → `FireboxMaxFuelBurnTime` (and the four siblings) means a player who had retuned the old key gets
    // the shipped default under the new name, silently, while their old value sits in the file doing
    // nothing - the config equivalent of a setting that stopped being read. Resetting the new names makes
    // the change visible: whatever they had tuned is gone either way, and this way the file says so.
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

  // Ignition has no tunable charge threshold: it is positional and pneumatic - a complete raceway
  // course, and air at pressure - not a quantity of charge. How much a furnace holds is
  // `BlockEntityFurnaceCore.ChargeCapacityUnits`, derived from its own geometry. There is likewise no
  // unmanaged-firing path: the only thing that burns is fuel inside a furnace that owns it.

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
  /// set by where this lands relative to <see cref="PlatedPipeBurstPressure"/> and the blower ceiling.</summary>
  public float BfBlastPressureAtReference { get; set; } = 2.0f;

  /// <summary>Atmospheres added per unit the coke fraction falls below <see cref="BfReferenceFuelFrac"/>
  /// (and subtracted per unit above it) - how sharply permeability translates into required pressure.
  /// At the shipped defaults a 30% coke burden asks 1.25 atm, a standard 20% one asks 2.0, and a 10%
  /// one asks 2.75 - which is above both the bellows' ceiling and plated pipe's burst rating, so the
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
  // iwex owns the base pipe block (the plated tier) and the "pipe" network registration, so the
  // pipe-content tunables moved here from lpex. The generic pipe-network constants
  // (LitresPerPipe, leak rates, …) live in exlib's own config (ExlibValues); these are content values.

  /// <summary>
  /// Burst pressure (atm) of a plain plated (iwex) pipe segment - the weakest pipe limits a run. Higher
  /// tiers register their own rating (lpex cast 5, hpex rolled 12).
  /// <para>
  /// This doubles as the tier's <b>capacity</b>: a network holds <c>burst x pipes x litresPerPipe</c>,
  /// so the plated tier is both the low-pressure tier and the small-buffer one. It sits deliberately
  /// just above what a coke-rich burden demands and below what a lean one does - that gap is the gate.
  /// </para>
  /// </summary>
  public float PlatedPipeBurstPressure { get; set; } = 2.5f;

  /// <summary>
  /// Throughput (L/s) of a plated (iwex) pipe segment - how much the line will PASS per second, as
  /// distinct from how much it HOLDS (<c>burst x pipes x litresPerPipe</c>) or how hard it can be
  /// pressurised. The weakest segment caps the whole run. Higher tiers register their own (lpex cast,
  /// hpex rolled).
  /// <para>
  /// <b>First-pass calibration, not playtested - but it binds.</b> Without it the pool
  /// had no rate bound at all and a single segment implicitly passed ~75 L/s. 50 comes from the shipped
  /// flow census: it clears every line the iron tier actually runs - the twin-tub blower at 45 L/s and the
  /// hot furnace's 48 L/s exhaust - while refusing the steam-tier services above it.
  /// </para>
  /// <para>
  /// <b>That refusal is the point, and it is real content:</b> smex's engine blower runs 144 L/s
  /// (<c>AirBlowerOutputPerSecond</c> 48 x an undocumented hard-coded 3), so a converter's blast main and
  /// a cowper's hot-blast run must be built in <b>cast</b> pipe - a hot furnace needs better plumbing than
  /// a cold one. The furnace <b>tuyere</b> is exempt: it is the machine's intake port, not a length of
  /// main, and limiting on it would pin every furnace in the suite to the iron tier's rate forever.
  /// </para>
  /// <para>
  /// This is <b>not</b> bore. There is deliberately no large-bore pipe family, partly because bore
  /// and burst are not independent (hoop stress is <c>p*r/t</c>, so a wider bore bursts <i>lower</i>), so
  /// tier is the only axis this may be derived from - see <c>docs/design/mechanics/pipe-network.md</c>.
  /// </para>
  /// </summary>
  public float PlatedPipeThroughput { get; set; } = 50f;

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

  /// <summary>Heat loss (°C) from cold charge mass with the furnace loaded to its <b>capacity</b>
  /// (<c>BlockEntityFurnaceCore.ChargeCapacityUnits</c>). A thin charge runs hotter but exhausts sooner -
  /// the historically correct trade, and it is linear: half-loaded pays half.
  /// <para>
  /// Note: the denominator must be the furnace's real, geometry-derived capacity - sizing it to a fire
  /// threshold instead makes a furnace read completely full at a fraction of its charge and pay this
  /// whole penalty while mostly empty.
  /// </para></summary>
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
  /// Units of <b>carbon</b> the raceway burns each second at full blast, before the air factor.
  /// <para>
  /// <b>Air is the reagent, so the blast is the throttle.</b> A furnace burns exactly as much carbon as
  /// it has oxygen for - which is why this is scaled by <c>HeatBalance.AirFactor</c> rather than being a
  /// flat rate, and why a stopped blower slows the fire to natural draught instead of leaving it burning
  /// at full tilt off a timer.
  /// </para>
  /// <para>
  /// It is what makes <b>campaign length = coke charged</b> literally true, and it is why
  /// <c>FireboxMaxFuelBurnTime</c> stops being needed: a lit shaft runs until the carbon in it is gone, and how
  /// long that takes is the player's charging decision rather than a countdown.
  /// </para>
  /// </summary>
  /// <para>
  /// <b>Per tuyere, not per furnace.</b> A single whole-furnace rate split across the columns makes
  /// the 16-column hot furnace burn exactly as much carbon per second as the 9-column cold one and the
  /// single-column cupola - so building the bigger machine buys capacity and campaign length but no
  /// <em>throughput</em>, which is not what a bigger furnace is for. Keying it to the tuyeres is also what
  /// keeps "the blast is the throttle" literally true: the tuyeres are where the blast arrives, so more of
  /// them is more air, and more air is more carbon.
  /// </para>
  /// <para>
  /// Calibrated so the two-tuyere cold blast furnace - which every calibration anchor in the suite is
  /// measured on - burns 0.35 u/s in total.
  /// </para>
  public float BfRacewayCarbonPerTuyerePerSecond { get; set; } = 0.175f;

  /// <summary>
  /// Charge units of <b>burden</b> the raceway melts per unit of <b>carbon burned</b>, before
  /// <c>MeltSpeedFactor</c> - the mod's <b>coke rate</b>, inverted.
  /// <para>
  /// <b>Production is metered by the carbon, not by a rate of its own.</b> A flat melt rate is a second
  /// throttle beside the blast, and the two disagree: run the melt faster than the carbon burn and burden
  /// clears out of the raceway far faster than the coke beneath it does, coke banks up in front of the
  /// tuyeres, and the furnace pulses between <c>Firing</c> and <c>Melting</c> while it works the surplus
  /// off - campaign length stops tracking what was charged the moment a furnace starts producing.
  /// </para>
  /// <para>
  /// <b>4.0 is the reference grade read as a ratio</b>: a burden laid at <c>BfReferenceFuelFrac</c> (20 %
  /// coke) is 80 % burden to 20 % coke, so a furnace consuming 4 units of burden per unit of carbon eats
  /// the column in exactly the proportion the player laid it. Charge richer and coke banks up (wasted
  /// fuel); charge leaner and the burden arrives cold (the chill). The optimum is a real decision with a
  /// real penalty either side, which is what the coke rate is for.
  /// </para>
  /// <para>
  /// <b><c>MeltSpeedFactor</c> multiplies it, and that is Neilson.</b> A hotter furnace gets more iron
  /// out of the same carbon - which is exactly what hot blast bought historically, and it means a preheated
  /// furnace <em>wants</em> a leaner charge. The design's formal reference for the whole balance is the
  /// <b>Rist diagram</b>; see <c>docs/design/layered-charge.md</c> § <i>What sets the rate: the blast</i>.
  /// </para>
  /// <para>
  /// Dimensionless, so it needs no per-furnace scaling: both sides are counted in whatever units that
  /// furnace measures its charge in.
  /// </para>
  /// </summary>
  public float BfBurdenPerCarbonUnit { get; set; } = 4f;

  /// <summary>
  /// Gas heat capacity, in charge units per degree, that <b>one unit of carbon burned</b> makes - how much
  /// charge that gas could warm by a degree for every degree the gas itself falls.
  /// <para>
  /// Per unit <b>burned</b>, not per unit present. Reading the coke standing at the raceway rather than
  /// the coke actually consumed lets a thick course heat a shaft for ever off carbon that never runs out.
  /// </para>
  /// <para>
  /// It is the <b>quantity</b> of gas, and only that. How <em>hot</em> the gas is comes from
  /// <c>ComputeHeatBalance</c>'s <c>T_process</c>, where the blast lives. Keeping the two apart is what
  /// lets hot blast reach the same shaft temperature on a leaner burden without a second multiplier: a
  /// cowper raises the temperature term, coke at the raceway raises this one.
  /// </para>
  /// <para>
  /// Calibrated against how long a cold shaft takes to warm through: a full 4-level column climbs most of
  /// the way to the flame over a few minutes rather than instantly or over a campaign.
  /// </para>
  /// </summary>
  public float BfRacewayGasPerCokeUnit { get; set; } = 20f;

  /// <summary>
  /// The share of the gas's remaining excess temperature that <b>one unit</b> of charge absorbs as the
  /// gas passes it. A band of <c>U</c> units therefore takes <c>1 - (1-this)^U</c>.
  /// <para>
  /// Per <b>unit</b>, never per band: bands are cut wherever charging and descent happened to leave
  /// them, so a per-band figure would make a shaft's efficiency depend on the player's charging rhythm.
  /// </para>
  /// <para>
  /// Sets how fast the temperature profile develops, and therefore how visible the flat thermal-reserve
  /// zone in the middle of the shaft is. At the shipped value a full 4-level cold column takes roughly
  /// nine tenths of the heat out of the gas before it reaches the stockline, which is what makes a
  /// well-charged tall furnace exhaust cool.
  /// </para>
  /// </summary>
  public float BfShaftGasTransferFrac { get; set; } = 0.02f;

  /// <summary>
  /// The <c>fuel</c>-role value that counts as <b>one unit of carbon</b> - coke's, so coke reads exactly
  /// 1.0 and every calibration constant here stays stated in coke.
  /// <para>
  /// <b>This is what lets a shaft burn more than one fuel.</b> A raceway spends
  /// <em>carbon</em>, not bands: a unit's worth is its material's role value over this reference, so
  /// <c>game:coke</c> (value 2) is 1.0 and <c>game:charcoal</c> (value 1) is 0.5 - two charcoal items carry
  /// one coke item's carbon. Charcoal was already accepted everywhere in the shaft and burned at coke's
  /// exact rate; this is what finally prices it.
  /// </para>
  /// <para>
  /// <b>The ratio itself lives in <c>assets/iwex/config/materialroles.json</c>, not here.</b> One number
  /// serves every reader of a fuel's carbon - the raceway, the shaft composition read and the burn-out
  /// retention - which is what stops "how good is charcoal" having two authorities.
  /// This key only says which fuel the calibration is <em>written in</em> - move it and every coke figure
  /// in this file changes meaning.
  /// </para>
  /// <para>
  /// A fuel granted the role with <b>no</b> value takes <see cref="MaterialRoleRegistry"/>'s fallback of
  /// 1.0, which against a reference of 2.0 reads as <b>half of coke</b> - so a forgotten value is silently
  /// charcoal rather than silently coke. That is the safe direction (a mod's new fuel under-performs rather
  /// than out-performing coke for free), and <c>FuelRoleGrantTests</c> pins it.
  /// </para>
  /// </summary>
  public float BfFuelCarbonReference { get; set; } = 2f;

  /// <summary>
  /// Iron units the blast furnace recovers per unit of <b>ore content</b> in the burden it melts.
  /// <para>
  /// <b>This is the mod's only downgrade guard, and it is stated in the currency the design's own
  /// anchor uses.</b> <c>docs/design/mechanics/metal-recovery.md</c> § <i>The ladder</i> fixes recovery
  /// at <b>8.5 u per nugget</b> raw
  /// against a vanilla bloomery's 5, and the chain is one nugget → one crushed item → one burden item, so
  /// "per unit of ore content" and "per nugget" are the same number. Never restate it per charge unit
  /// or per band: both of those are the ore share times this, and the ore share
  /// (<c>BurdenMix.IronFrac</c>) <b>moves</b> - taking coke out of the burden lifted it from
  /// ~0.75 to ~0.94. A per-band figure copied forward through such a change silently drops the furnace from
  /// 1.7x bloomery to ~1.36x with every unit total still green.
  /// </para>
  /// <para>
  /// It replaces the <c>BfIronPerMeltCycle</c> / <c>BfBlastMixPerMeltCycle</c> pair, which derived to
  /// exactly <b>5.0 u/nugget</b> - bloomery parity, so the whole ironmaking chain bought the player
  /// nothing per ore over the furnace it replaces. See <c>OreRecoveryGuardRailTests</c>.
  /// </para>
  /// </summary>
  public float BfIronPerOreUnit { get; set; } = 8.5f;

  /// <summary>
  /// Slag units the blast furnace renders per unit of ore content, alongside
  /// <see cref="BfIronPerOreUnit"/>. Kept at a <b>6:1</b> iron-to-slag ratio, which is what the shipped
  /// per-cycle pair (60 : 10) expressed - stated here rather than inherited, so a recovery change moves
  /// the ratio only when someone means it to.
  /// </summary>
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

  /// <summary>Fraction of a pile's coke that survives at the bottom of the shaft, sitting on the
  /// tuyeres where the blast burned hardest. 0 = burned to nothing.</summary>
  public float BfBurnoutFuelRetainedBottom { get; set; } = 0f;

  /// <summary>Fraction of a pile's coke that survives at the top of the shaft, which the blast never
  /// reached. Everything between is interpolated by height.</summary>
  public float BfBurnoutFuelRetainedTop { get; set; } = 0.4f;
  #endregion

  #region Hearth sizing (the crucible pool)

  /// <summary>
  /// Metal units in one 1/16 band of a hearth cell - <b>640</b>, and it is derived, not chosen:
  /// a block is 16³ voxels at <c>1 vx³ = 2.5 u</c> = 10 240 u, so one sixteenth is 640.
  /// <para>
  /// Charge units and metal units are the <b>same currency</b>, so this is the same
  /// quantity domain as <c>ChargeUnitsPerBlock</c> and comparing the two is meaningful.
  /// </para>
  /// </summary>
  public int HearthUnitsPerBand { get; set; } = 640;

  /// <summary>
  /// The band the slag spout's channel floor sits at - <b>10</b> of 16 - and therefore the level the
  /// crucible overflows at while that tap is open.
  /// <para>
  /// <b>This is read off the drawing, not tuned.</b> <c>assets/editable/shapes/furnace-block-slagtap.json</c>
  /// puts its top-level <c>TapCanal</c> element at Y <b>10 → 11</b> (the element is
  /// top-level, so the coordinates are absolute). Iron can only rise to the cinder notch before it starts
  /// running out of it - so the cap is a spout the player can <em>look at</em>, never a constant. The
  /// iron notch is at Y 2 → 3 in the matching iron-tap shape, which is why that one drains the floor.
  /// </para>
  /// <para>
  /// <b>There is one volume here, not two budgets.</b> Iron and slag layer
  /// in a single cell and slag floats, so neglecting the slag tap eats the space iron could occupy until
  /// it is flushed. Separate <c>HearthIronBands</c> / <c>HearthSlagBands</c> caps would be two independent
  /// volumes, which deletes that operating rhythm - and with it the reason the crucible is layered at all,
  /// and the reason the two-tap layout was paid for. <b>Plugging the slag tap is what lets the hearth
  /// fill.</b>
  /// </para>
  /// <para>
  /// <c>docs/design/layered-charge.md</c> § <i>Numbers to derive</i> still offers "2 bands × 3 cells =
  /// 3 840 u" as a starting point. That row is <b>superseded</b> by § <i>Sizing - the band is the
  /// number</i>, which argues the cap from the geometry and accepts the larger pool explicitly: a campaign
  /// is not bounded by one shaft-full, because the player keeps charging a running furnace, so three cells
  /// × 10 bands × 640 u = <b>19 200 u</b> means longer between casts rather than an unreachable ceiling.
  /// The back-pressure that matters is the canal and beds downstream.
  /// </para>
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

  /// <summary>Molten iron (units) produced per melt cycle.
  /// Nothing in src reads this any more - the live yield path is <see cref="BfIronPerOreUnit"/>.
  /// It stays only because <c>OreRecoveryGuardRailTests</c> still derives the recovery figure from
  /// this and <see cref="BfBlastMixPerMeltCycle"/>; delete both together with that test's rederivation.</summary>
  public float BfIronPerMeltCycle { get; set; } = 60f;

  // `BfSlagPerMeltCycle` (10 u per cycle) is deleted: nothing read it anywhere - the slag yield now
  // comes from `BfSlagPerOreUnit`. A saved config still carrying the key is ignored on load.

  /// <summary>Blast-mix consumed per melt cycle.
  /// Same situation as <see cref="BfIronPerMeltCycle"/>: unread in src, kept only for
  /// <c>OreRecoveryGuardRailTests</c>.</summary>
  public int BfBlastMixPerMeltCycle { get; set; } = 16;

  /// <summary>Air/blast (L/s) the blast furnace draws through each tuyere <b>at the reference coke
  /// fraction</b>. The live draw scales with the burden's coke content - air is the oxidant for coke, so
  /// a rich burden burns more of it and needs more air - clamped by
  /// <see cref="BfTuyereDrawMinFactor"/>/<see cref="BfTuyereDrawMaxFactor"/>.</summary>
  public float TuyereIntakeVolume { get; set; } = 14f;

  /// <summary>Units of pool a tap considers per drain tick. Set to the molten network's own rate so tap,
  /// canal and bed all move metal at one speed - it is the same canal. Was a hard-coded 20, which capped
  /// iron at 12 u/s and slag at 16 u/s however the furnace was tuned.</summary>
  public int TapDrainPerTick { get; set; } = 50;

  /// <summary>Fraction of those units that actually leave the iron taphole. Below 1 it models the notch
  /// throttling a full hearth.</summary>
  public float TapIronStackFactor { get; set; } = 0.6f;

  /// <summary>Fraction of those units that leave the cinder notch. Higher than iron's - slag is thinner and
  /// runs freely.</summary>
  public float TapSlagStackFactor { get; set; } = 0.8f;
  #endregion

  #region Cupola furnace
  // The cupola is the same furnace machine as the blast furnace (it inherits the whole
  // fire/melt/drain/residue core), run as a scrap re-melter: its remelt burden melts into CAST IRON
  // (1200 C, well below wrought iron's 1482) which drains out the lower tap, slag out the upper.
  // It is deliberately slower than the blast furnace - and not by any interval key: a shaft's campaign
  // ends when its carbon does, its soak is the counter-current warm-through, and its melt cadence is the
  // descent. The slowdown is geometry: a cupola block holds ~3 000 metal units against an ore shaft's 32
  // items, so `ChargeUnitScale` stretches the same carbon rate over a far larger charge, and roughly two
  // cupolas still keep pace with one blast furnace - the nominal design ratio.
  // (The shared melt-speed factor still scales both with superheat, so the effective ratio drifts a
  // little with how hard each is being blown.)

  /// <summary>Temperature (°C) the hearth must reach (and hold) to melt cast iron - the near-eutectic
  /// remelt point, far below wrought iron's, which is the whole mechanical point of the cupola.</summary>
  public float CupolaCastIronMeltingPoint { get; set; } = 1200f;

  /// <summary>Maximum molten cast iron (units) the cupola holds before stalling - a smaller reservoir
  /// than the blast furnace's, matching the smaller furnace.</summary>
  public float CupolaMaxMoltenCastIron { get; set; } = 1200f;

  /// <summary>Maximum molten slag (units) the cupola holds before stalling.</summary>
  public float CupolaMaxMoltenSlag { get; set; } = 300f;




  /// <summary>Molten cast iron (units) produced per melt cycle. Held equal to the blast furnace's
  /// per-cycle yield so the slowdown comes cleanly from the doubled interval, not a second lever.</summary>
  public float CupolaCastIronPerMeltCycle { get; set; } = 60f;

  /// <summary>Molten slag (units) produced per melt cycle.</summary>
  public float CupolaSlagPerMeltCycle { get; set; } = 8f;

  /// <summary>Remelt burden consumed per melt cycle.</summary>
  public int CupolaBlastMixPerMeltCycle { get; set; } = 12;


  /// <summary>Air/blast (L/s) the cupola draws through its single tuyere.</summary>
  public float CupolaTuyereIntakeVolume { get; set; } = 12f;
  #endregion

  #region Firebox (reverberatory) furnaces
  // A firebox holds a fire, not a charge column, so its ignition threshold cannot be a fixed number the
  // way a shaft's is: the puddling furnace's firebox is one cell and the reheat furnace's is two, and a
  // constant sized for either is wrong for the other. The threshold is therefore per cell and the
  // machine multiplies by the firebox it declares - see BlockEntityFireboxFurnace.FireboxCellCount.

  /// <summary>
  /// Fuel units per firebox cell that must be loaded before a reverberatory hearth will light.
  /// <para>
  /// <b>"Lit" means the bed is full, and the number falls out of the drawn shape.</b> A firebox
  /// cell holds <see cref="FireboxLayersPerCell"/> × <see cref="FireboxUnitsPerLayer"/> = 12 units, so the
  /// default here is exactly one cell's capacity on a hearth of any size. It stopped being an arbitrary
  /// constant the moment <c>iwex:furnace-firebox</c> replaced the vanilla coal pile.
  /// </para>
  /// <para>
  /// Caution: <b>must stay at or under <c>FireboxLayersPerCell × FireboxUnitsPerLayer</c>.</b> Above that
  /// no firebox can ever fire, which is the defect this key exists to make impossible to reintroduce -
  /// a threshold above what the cells can hold once kept both hearths unlightable for the machine's whole
  /// life. <c>FurnaceBranchGuards.NoFireboxAsksForMoreThanItsCellsCanHold</c> asserts it.
  /// </para>
  /// </summary>
  public int FireboxMixPerCell { get; set; } = 12;

  /// <summary>
  /// Fuel units one drawn layer of a firebox holds, <b>per cell</b>. The shape draws six stacked slabs
  /// (<c>CokeL1</c>..<c>CokeL6</c>), so this is what a single visible course of fuel costs and the whole
  /// pool's capacity is <see cref="FireboxLayersPerCell"/> × this × the furnace's firebox cell count.
  /// <para>
  /// Capacity scaling with cell count is what makes "fill one, fill all" honest: the player interacts
  /// once, but a two-cell reheat firebox costs twice a one-cell puddling firebox for the same visible
  /// fill.
  /// </para>
  /// </summary>
  public int FireboxUnitsPerLayer { get; set; } = 2;

  /// <summary>Drawn fuel layers in one firebox cell. <b>Pinned by the shape</b>: the firebox has exactly
  /// six <c>CokeL*</c> elements, so raising this draws layers that do not exist and the top of the bed
  /// stops rendering. Change the shape first.</summary>
  public int FireboxLayersPerCell { get; set; } = 6;
  #endregion

  #region Burdenmaker
  // These three numbers answer `docs/design/machines/burdenmaker.md` § Open 2, which left hopper
  // capacities unset. They are carried from the machines the burdenmaker replaced (the ore mixer and ore
  // bunker) rather than invented, so a player's throughput does not silently change under them:
  //   ore    512 - the ore mixer's MixerMaxRaw, i.e. one full raw charge
  //   bunker 1152 - the ore bunker's BunkerMaxBurden, unchanged
  //   flux   205 - 512 / 2.5, the drawn hopper width ratio (large X -12…15 vs small X 17…28)
  // A player's saved ex_values.json may keep the replaced machines' keys until it is rewritten; an
  // unknown key is ignored on load, so they are inert rather than an error.
  //
  // The flux hopper is deliberately not sized to one batch's worth of lime. At a ~5 % target flux
  // fraction a full 512-unit ore charge wants only ~27 lime, so 205 is about seven batches - which is the
  // point: ore is topped up every heat and flux rarely. The hoppers are storage, not measures. The player
  // judges the ratio from the readout, never from how full the two look.

  /// <summary>Maximum crushed/roasted iron ore (units) the burdenmaker's wide hopper holds.</summary>
  public int BurdenmakerOreCapacity { get; set; } = 512;

  /// <summary>Maximum flux (units) the burdenmaker's narrow hopper holds.</summary>
  public int BurdenmakerFluxCapacity { get; set; } = 205;

  /// <summary>Maximum burden (units) the shared basin holds before it must be emptied.</summary>
  public int BurdenmakerBunkerCapacity { get; set; } = 1152;
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

  // There is deliberately no pile cap and no drop-depth key. A column's ceiling is its own chargeable
  // cell count times `ChargeItemsPerBand * BandsPerBlock` - 32 items a block - so a separate cap could
  // only under-cut it. And the hopper resolves the furnace core through the multiblock anchor link, the
  // same way every other furnace part does, so there is no downward scan left to bound.
  #endregion

  #region Shaft charge columns
  /// <summary>
  /// <b>Items</b> one band of a shaft column holds. A pile block is
  /// <see cref="BlockStructures.Furnaces.ChargeColumn.BandsPerBlock"/> bands, so it takes
  /// <c>16 x 2 = 32</c> items - part coke, part burden, laid as bands.
  /// <para>
  /// <b>A band is measured in items, not units.</b> A per-material unit content cannot be derived
  /// without the burden recipe's mass composition, a design choice <c>layered-charge.md</c>
  /// explicitly refuses to ship ("do not ship the estimate"). Counting items removes the question - the
  /// quantum is 2 items for <i>every</i> material, and what an item is worth appears only when it converts.
  /// </para>
  /// <para>
  /// Applies to the <b>ore</b> charge (blast furnace: coke + burden). A <b>remelt</b> pile (cupola) is
  /// filled differently - coke first as items, then metal by UNITS up to
  /// <see cref="CupolaChargeMetalUnitsPerBlock"/> - because a 5 u bit, a 25 u chunk and a 375 u pig must all
  /// be able to go into the same pile.
  /// </para>
  /// </summary>
  public int ChargeItemsPerBand { get; set; } = 2;

  /// <summary>
  /// Metal (units) one <b>remelt</b> pile block accepts, on top of its coke. A bit (5 u), a chunk (25 u) and
  /// a whole pig (375 u) each contribute their own units toward this - which is how a cupola was charged in
  /// life: whatever scrap you have.
  /// <para>
  /// <b>3 000 u</b> - 8 pigs of metal against 8 bands of coke. The bare
  /// <c>16 x 375 = 6 000</c> is the <i>no-coke</i> figure; coke takes about half a cupola's pile height,
  /// because its ratio to metal is ~1:8-1:10 <b>by weight</b> but coke is bulky (~0.5 t/m³ bulk against loose
  /// pig at ~4.6), so a tonne of it occupies roughly the space of the ten tonnes it melts. Divides cleanly:
  /// 8 pigs, 120 chunks, 600 bits.
  /// </para>
  /// <para>
  /// A full cupola shaft is 4 cells = 12 000 u, which is not over-generous because the cupola
  /// <b>converts rather than creates</b> - 32 pigs in is 32 pigs' worth out, minus melt loss. Unlike the
  /// blast furnace's ~3 672 u <i>yield</i> from 36 cells, a big cupola charge is fewer trips, not free iron.
  /// </para>
  /// </summary>
  public int CupolaChargeMetalUnitsPerBlock { get; set; } = 3000;
  #endregion

  // The burdenmaker deliberately has no drain-rate key: with no mechanism there is no throughput lever
  // to hang a rate on. If a future machine wants one, that is a design decision, not a config key.
  //
  // Per-item fuel carbon values (coke 2, charcoal 0.5) live in the fuel material-role in
  // assets/iwex/config/materialroles.json, read via MaterialRoleRegistry.ValueOf - not here.

  #region Recipe balance
  /// <summary>Active ironworking recipe cost level - <c>"normal"</c> or <c>"cheap"</c>. Toggled in-game
  /// by <c>/exmod recipes iwex &lt;level&gt;</c>; the per-recipe numbers live in the separate
  /// <see cref="IwexRecipeConfig"/> catalogue. Applied on the next world reload.</summary>
  public string RecipeLevel { get; set; } = "normal";
  #endregion

  #region Twin-tub blower
  // The iron tier's only air source: a mechanically driven twin-tub bellows feeding the blast main.
  // Deliberately weaker than the steam tiers' blowers - it makes cold blast at a pressure that just
  // clears BlastPressureThreshold, so an iron-age blast furnace runs but never reaches the hot-blast
  // ceiling. Both output figures are at full axle speed and scale down with it.

  /// <summary>Litres of air per second the blower pushes into its network at <see cref="TwinTubBlowerMaxSpeed"/>.
  /// The default runs a single two-tuyere blast furnace on a coke-rich burden - the draw a rich burden
  /// demands is the highest the iron tier ever has to meet.</summary>
  public float TwinTubBlowerOutputPerSecond { get; set; } = 45f;

  /// <summary>
  /// Pressure ceiling (atm) the blower can raise its network to. It sits under
  /// <see cref="PlatedPipeBurstPressure"/> (bellows must not burst the tier's own pipe) and above what a
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

  #region Rolling mill (the first mpenergy consumer)
  // The mill is where the energy network is finally spent (docs/design/iwex.md, Forming). The physics lives in
  // RollingPass; these are the balance levers. The one that matters most is RollingTempC: below it the stock's
  // friction collapses (delta_max = mu^2 R, ~30x worse cold) and its flow stress climbs, so a cooling piece
  // both refuses to bite and loads harder - the "keep it hot or it jams" coupling, straight out of the model.

  /// <summary>Stock temperature (C) at or above which iron rolls <b>hot</b>: it grips the rolls and deforms
  /// cheaply. Below it the piece is cold-working - it barely bites and resists far harder. Wrought iron is
  /// rolled around a bright orange-yellow heat.</summary>
  [ExConfigRange(0, 3000)]
  public float RollingTempC { get; set; } = 900f;

  /// <summary>How much harder fully-cold stock resists than hot stock (the flow-stress multiplier). Cold
  /// working really is roughly an order of magnitude stiffer; this is what makes a cold pass overdraw the
  /// flywheel and stall the mill.</summary>
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
  /// Scales raw pass torque into the network's torque units, so the mill is balanced against the drive and the
  /// flywheel without touching the physics in <c>RollingPass</c>. <b>The mill's single most important number.</b>
  /// <para>
  /// Calibrated against one bridge drive (<see cref="FlywheelBridgeChargePower"/> = 1 N.m) less friction at
  /// omega_max (0.05*2 + 0.5 = 0.6), i.e. 0.4 N.m of headroom. At 0.02 that yields the intended curve: a
  /// typical hot pass (0.16) <b>runs</b> on a single water wheel; the same pass cold (1.6) <b>stalls</b> - the
  /// keep-it-hot loop; and a wide, deep hot pass (0.58) <b>sags</b> the line, so heavy stock genuinely demands
  /// more drive or a charged flywheel rather than being free. Raise it to make the mill hungrier.
  /// </para>
  /// </summary>
  [ExConfigRange(0, 10_000)]
  public float RollingTorqueScale { get; set; } = 0.02f;

  /// <summary>
  /// Fraction of its excess heat the stock sheds per second. This is what closes the keep-it-hot loop, and it
  /// applies during the carry-back as much as under the rolls.
  /// <para>
  /// Tuned so a single pass finishes comfortably (a piece is still well above rolling heat after ~25 s) but a
  /// full schedule of round trips is not survivable on one heat - so the reheat furnace is a routine part of
  /// rolling rather than an emergency, which is exactly how it worked. Raising it much past this makes a pass
  /// freeze mid-bite, which reads as the mill being broken rather than the stock being cold.
  /// </para>
  /// </summary>
  [ExConfigRange(0, 10)]
  public float RollingCoolRate { get; set; } = 0.005f;

  /// <summary>Ambient temperature (C) the stock cools toward on the mill floor.</summary>
  [ExConfigRange(-50, 500)]
  public float RollingAmbientC { get; set; } = 20f;
  #endregion

  #region Burden grades
  /// <summary>
  /// Named burden grades, matched by <b>flux</b> band. The classifier returns the first profile whose band
  /// contains the mix's flux fraction, so the list is scanned in order and a boundary belongs to the
  /// earlier band. The shipped bands tile 0..1, which makes "off-spec" unreachable unless a player edits a
  /// hole into them. Extend or retune freely in <c>ex_values.json</c> - this is the single source of grade
  /// truth, shared by the burden tooltip and the burdenmaker's readout, so <em>"right"</em> at the machine
  /// and <em>"right"</em> in the hand are one answer. Fractions are 0..1 of the total mix.
  /// </summary>
  public List<BurdenProfile> BurdenProfiles { get; set; } =
  [
    // Burden is graded by flux alone: fuel is charged as its own bands at the hopper and metered at the
    // raceway, so it is not a quality the item carries. Flux being the only quality is what makes a
    // single band list honest: the bands tile 0..1 with no gap, so `offspec` is unreachable unless a
    // player edits a hole into them.
    //
    // Bands are inclusive on both ends and scanned in order, so the boundaries belong to the earlier
    // band: 0.03 reads underfluxed, 0.08 reads standard. Ordering is the tie-break - do not sort this list.
    new() { Key = "underfluxed", MaxFlux = 0.03f },
    new() { Key = "standard", MinFlux = 0.03f, MaxFlux = 0.08f },
    new() { Key = "overfluxed", MinFlux = 0.08f },
  ];
  #endregion
}

/// <summary>
/// One named burden grade: a lang-keyed label (<c>iwex:burden-profile-{Key}</c>) plus an inclusive
/// <b>flux</b> band (fraction 0..1 of the total mix). Pure data - the classifier lives in
/// <see cref="Items.Burden"/>.
/// <para>
/// <b>There are deliberately no iron or fuel bounds.</b> Fuel is not a quality the item carries -
/// it is charged as its own bands. Iron is excluded for a different
/// reason: burden is ore and flux <em>only</em>, so <c>IronFrac ≡ 1 − FluxFrac</c> - an iron bound would
/// be a second knob for one quantity, which is how a band becomes quietly unreachable when the two are
/// edited out of agreement.
/// </para>
/// </summary>
public class BurdenProfile
{
  /// <summary>Grade key; the burdenmaker readout and the tooltip show <c>iwex:burden-profile-{Key}</c>.</summary>
  public string Key { get; set; } = "";

  public float MinFlux { get; set; }
  public float MaxFlux { get; set; } = 1f;
}
