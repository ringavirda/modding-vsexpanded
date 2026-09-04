using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// The base pipe block: a self-orienting node of the unified "pipe" network. Provides the
/// orientation tables shared by every straight/bend/junction variant. Each tier reuses this class
/// and its block entity through the registered class keys <c>exlib.BlockPipe</c> /
/// <c>exlib.BlockEntityPipe</c>, calling <see cref="Segments"/> from a thin per-mod
/// <see cref="IExBlockDefProvider"/>: iiex the plated tier, iiex the cast tier, siex the rolled
/// tier. One material per tier, so there is no <c>material</c> variant group; the tier itself is a
/// variant (<see cref="Tier"/>), which is what lets one domain carry several.
/// </summary>
[BlockRegister]
public partial class BlockPipe
  : BlockNetworkNode,
    IBurstablePipe,
    IThroughputLimitedPipe,
    IExBlockDefProvider {
  public override string NetworkType => "pipe";

  #region Code-first definitions

  /// <summary>Tier name of the plated pipe family - the early loop's bootstrap rung, 2.5 atm and
  /// flanged.</summary>
  public const string PlatedTier = "plated";

  /// <summary>Tier name of the cast pipe family - the steam-era main, 5 atm and flanged.</summary>
  public const string CastTier = "cast";

  /// <summary>Tier name of the rolled pipe family - the HP main, 12 atm and welded.</summary>
  public const string RolledTier = "rolled";

  /// <summary>The defs this class declares itself. Only the <c>type</c> and <c>orientation</c> pairs
  /// are read from these - <see cref="BlockNetworkNode.AllowedOrientations"/> derives its map from
  /// them - so they carry no tier and are never registered: the shipped blocktypes come from each
  /// tier's own provider. Yields nothing for exlib itself, which authors the factories but ships no
  /// pipe content.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    domain == "exlib" ? [] : Segments(domain, tier: null);

  /// <summary>
  /// The four plain pipe segments (straight / bend / T / X junction) of one <paramref name="tier"/>
  /// under <paramref name="domain"/>. All four share the <c>pipe</c> code at distinct asset paths and
  /// an identical common surface (<see cref="Common"/>); each adds only its variant list, shape
  /// rotations and collision boxes. Each tier ships its own shapes at <c>{domain}:pipe/{tier}/*</c>.
  /// <para>
  /// A null <paramref name="tier"/> declares no tier axis: the segments come out as
  /// <c>pipe-{type}-{orientation}</c> and take the default rating, throughput and joint. That is the
  /// shape <see cref="Definitions"/> uses to derive orientations, and the one a consumer shipping a
  /// single pipe family wants.
  /// </para>
  /// </summary>
  public static IEnumerable<ExBlockDef> Segments(string domain, string? tier) =>
    [
      Straight(domain, tier),
      Bend(domain, tier),
      TJunction(domain, tier),
      XJunction(domain, tier),
    ];

  /// <summary>
  /// The asset path a tier's segment is declared and drawn at: <c>pipe/{tier}/{leaf}</c>, or
  /// <c>pipe/{leaf}</c> for a tierless family. The tier belongs in the path and not only in the
  /// variant grammar because <see cref="ExDefinitions"/> keys on
  /// <see cref="ExBlockDef.Location"/> - which carries the asset path and no variants - so two tiers
  /// sharing a domain would land on one key and the later registration would replace the earlier,
  /// last-writer-wins and unlogged. The shape lives at the same path for the same reason: two tiers'
  /// segments are different art.
  /// </summary>
  internal static string Asset(string? tier, string leaf) =>
    tier == null ? $"pipe/{leaf}" : $"pipe/{tier}/{leaf}";

  // The surface shared by every pipe blocktype; each type overlays its variants/shapes/boxes.
  private static ExBlockDef Common(
    string domain,
    string? tier,
    string assetName,
    int maxStack,
    string creativeSelector
  ) {
    ExBlockDef def = ExBlockDef
      .Create(domain, "pipe", assetName)
      // Shared base class + BE, registered by exlib; every tier's segments bind to these keys.
      .Class<BlockPipe>()
      .EntityClass<BlockEntityPipe>()
      .Material(EnumBlockMaterial.Metal)
      .MetalSounds()
      .MaxStackSize(maxStack)
      .CreativeTab("general", creativeSelector)
      .CreativeTab(domain, creativeSelector)
      // Grouped per tier, not across them: a groupBy selector with no domain is qualified with the
      // block's own, so today's three domains already give three handbook entries. Widening to
      // `pipe-*-straight-*` would merge them the moment two tiers share a domain.
      .Handbook(HandbookGroups(tier))
      .Behavior("Lockable")
      // No blanket texture override: each tier's shape declares its own texture map, and the shapes
      // disagree on key names (the plated bend calls its body sheet "iron42" where the straight
      // calls it "iron4"), so a single override would repaint some segments and miss others.
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(false)
      .SideOpaque(false);

    // The tier is the high-order axis: `pipe-{tier}-{type}-{orientation}`. Declared before each
    // segment adds `type`, so the leading `*` of every shape and creative selector below absorbs it
    // and they keep matching. Declared last it would break all of them, and silently - a blocktype
    // whose shapebytype matches nothing loads with no shape rather than failing.
    return tier == null ? def : def.VariantGroup("tier", tier);
  }

  // The handbook groups one blocktype's variants into one entry per segment shape, within a tier.
  private static string[] HandbookGroups(string? tier) {
    string prefix = tier == null ? "pipe-" : $"pipe-{tier}-";
    return
    [
      $"{prefix}straight-*",
      $"{prefix}bend-*",
      $"{prefix}tjunction-*",
      $"{prefix}xjunction-*",
    ];
  }

  private static ExBlockDef Straight(string domain, string? tier) {
    string s = $"{domain}:" + Asset(tier, "straight");
    return Common(domain, tier, Asset(tier, "straight"), 16, "*-straight-ns")
      .VariantGroup("type", "straight")
      .VariantGroup("orientation", "ns", "we", "ud")
      .NetworkOriented()
      .ShapeByType("*-straight-ns", s)
      .ShapeByType("*-straight-we", s, rotateY: 90)
      .ShapeByType("*-straight-ud", s, rotateX: 90)
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f);
  }

  private static ExBlockDef Bend(string domain, string? tier) {
    string s = $"{domain}:" + Asset(tier, "bend");
    return Common(domain, tier, Asset(tier, "bend"), 8, "*-bend-nw")
      .VariantGroup("type", "bend")
      .VariantGroup(
        "orientation",
        "nw",
        "se",
        "en",
        "ws",
        "un",
        "us",
        "uw",
        "ue",
        "dn",
        "ds",
        "dw",
        "de"
      )
      .NetworkOriented()
      .ShapeByType("*-bend-nw", s)
      .ShapeByType("*-bend-en", s, rotateY: 270)
      .ShapeByType("*-bend-se", s, rotateY: 180)
      .ShapeByType("*-bend-ws", s, rotateY: 90)
      .ShapeByType("*-bend-dn", s, rotateZ: 90)
      .ShapeByType("*-bend-de", s, rotateY: 270, rotateZ: 90)
      .ShapeByType("*-bend-ds", s, rotateY: 180, rotateZ: 90)
      .ShapeByType("*-bend-dw", s, rotateY: 90, rotateZ: 90)
      .ShapeByType("*-bend-un", s, rotateZ: 270)
      .ShapeByType("*-bend-ue", s, rotateY: 270, rotateZ: 270)
      .ShapeByType("*-bend-us", s, rotateY: 180, rotateZ: 270)
      .ShapeByType("*-bend-uw", s, rotateY: 90, rotateZ: 270)
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 0.6875f)
      .CollisionBox(0f, 0.3125f, 0.3125f, 0.6875f, 0.6875f, 0.6875f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 0.6875f)
      .SelectionBox(0f, 0.3125f, 0.3125f, 0.6875f, 0.6875f, 0.6875f);
  }

  private static ExBlockDef TJunction(string domain, string? tier) {
    string s = $"{domain}:" + Asset(tier, "tjunction");
    return Common(domain, tier, Asset(tier, "tjunction"), 8, "*-tjunction-uns")
      .VariantGroup("type", "tjunction")
      .VariantGroup(
        "orientation",
        "uns",
        "uwe",
        "dns",
        "dwe",
        "nes",
        "esw",
        "swn",
        "wne",
        "dnu",
        "deu",
        "dsu",
        "dwu"
      )
      .NetworkOriented()
      .ShapeByType("*-tjunction-wne", s)
      .ShapeByType("*-tjunction-nes", s, rotateY: 270)
      .ShapeByType("*-tjunction-esw", s, rotateY: 180)
      .ShapeByType("*-tjunction-swn", s, rotateY: 90)
      .ShapeByType("*-tjunction-uwe", s, rotateX: 90)
      .ShapeByType("*-tjunction-uns", s, rotateX: 90, rotateZ: 90)
      .ShapeByType("*-tjunction-dwe", s, rotateX: 270)
      .ShapeByType("*-tjunction-dns", s, rotateX: 270, rotateZ: 90)
      .ShapeByType("*-tjunction-dnu", s, rotateZ: 90)
      .ShapeByType("*-tjunction-deu", s, rotateY: 270, rotateZ: 90)
      .ShapeByType("*-tjunction-dsu", s, rotateY: 180, rotateZ: 90)
      .ShapeByType("*-tjunction-dwu", s, rotateY: 90, rotateZ: 90)
      .CollisionBox(0f, 0.3125f, 0.3125f, 1f, 0.6875f, 0.6875f)
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 0.3125f)
      .SelectionBox(0f, 0.3125f, 0.3125f, 1f, 0.6875f, 0.6875f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 0.3125f);
  }

  private static ExBlockDef XJunction(string domain, string? tier) {
    string s = $"{domain}:" + Asset(tier, "xjunction");
    return Common(domain, tier, Asset(tier, "xjunction"), 8, "*-xjunction-nswe")
      .VariantGroup("type", "xjunction")
      .VariantGroup("orientation", "nswe", "nsud", "weud")
      .NetworkOriented()
      .ShapeByType("*-xjunction-nswe", s)
      .ShapeByType("*-xjunction-nsud", s, rotateZ: 90)
      .ShapeByType("*-xjunction-weud", s, rotateY: 90, rotateZ: 90)
      .CollisionBox(0f, 0.3125f, 0.3125f, 1f, 0.6875f, 0.6875f)
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .SelectionBox(0f, 0.3125f, 0.3125f, 1f, 0.6875f, 0.6875f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f);
  }

  #endregion

  #region Tier

  /// <summary>
  /// The pipe family this block belongs to, from its <c>tier</c> variant, or null for a pipe
  /// declaring no tier axis - every fitting, and any consumer shipping a single family. The tier is
  /// the key to all three per-tier registries below, so a block without one takes their defaults.
  /// <para>
  /// A variant rather than the domain, because a merged mod carries several tiers under one domain
  /// and a tier must still be legible on the block, in its code and in its name.
  /// </para>
  /// </summary>
  public virtual string? Tier => Variant["tier"];

  #endregion

  #region Burst rating (per-tier)

  // Each pipe tier registers its plain-segment burst pressure from its own config in ModSystem.Start.
  // Resolved by the segment's own tier, so a run of mixed tiers is capped by its weakest segment.
  private static readonly Dictionary<string, Func<float>> _burstByTier = new();

  private const float DefaultBurstPressure = 5f;

  /// <summary>Registers the plain-segment burst pressure (atm) for a <paramref name="tier"/>.</summary>
  public static void RegisterBurst(string tier, Func<float> burstPressure) =>
    _burstByTier[tier] = burstPressure;

  /// <summary>
  /// Pressure (atm) above which this pipe bursts - the weakest pipe limits a run. Read from the
  /// per-tier registry keyed by this block's <see cref="Tier"/> (falls back to
  /// <see cref="DefaultBurstPressure"/> if the block names no tier, or its owner never registered one).
  /// </summary>
  public virtual float BurstPressure =>
    Tier != null && _burstByTier.TryGetValue(Tier, out var f)
      ? f()
      : DefaultBurstPressure;

  /// <summary>
  /// Whether this pipe takes part in over-pressure failure. Only the plain segments of the base
  /// <see cref="BlockPipe"/> class (straight, bend, tjunction, xjunction) burst and cap a run's
  /// pressure; every fitting (valve, outlet, passthrough, tuyere) is a subclass and exempt, so a new
  /// subclass is non-bursting unless it overrides this.
  /// </summary>
  public virtual bool CanBurst => GetType() == typeof(BlockPipe);

  #endregion

  #region Throughput (per-tier)

  // How much a tier's pipe passes per second, as distinct from how much a run holds (nodes x
  // LitresPerPipe) or how hard it can be pressurised (burst). Registered per tier from each mod's
  // ModSystem, like the burst rating and the joint family; a run is capped by its weakest segment.
  private static readonly Dictionary<string, Func<float>> _throughputByTier =
    new();

  private const float DefaultThroughput = 120f;

  /// <summary>Registers the throughput (L/s) for a <paramref name="tier"/>.</summary>
  public static void RegisterThroughput(string tier, Func<float> throughput) =>
    _throughputByTier[tier] = throughput;

  /// <summary>
  /// Litres per second this pipe will pass - the smallest across a run caps the whole run. Read from
  /// the per-tier registry keyed by this block's <see cref="Tier"/> (falls back to
  /// <see cref="DefaultThroughput"/> if the block names no tier, or its owner never registered one).
  /// <para>
  /// Only a plain segment limits throughput, the same rule as <see cref="CanBurst"/>. Fittings are
  /// exempt because some are a machine's own port on a single-node network rather than a length of
  /// main (the iiex tuyere), and limiting there would cap every furnace at that tier's rate.
  /// </para>
  /// </summary>
  public virtual float MaxThroughput =>
    !CanBurst ? float.MaxValue
    : Tier != null && _throughputByTier.TryGetValue(Tier, out var f) ? f()
    : DefaultThroughput;

  #endregion

  #region Joint family (which tiers physically couple)

  // A pipe tier's joint, an axis independent of its pressure rating. The plated and cast tiers are
  // both square in section and bolted through flanges, so they mate; the rolled tier is octagonal
  // and welded, with no flange to bolt to, so it mates only with itself. Registered per tier from
  // each mod's ModSystem, like the burst rating.
  private static readonly Dictionary<string, string> _jointByTier = new();

  /// <summary>Default joint family: the bolted flange.</summary>
  public const string FlangedJoint = "flanged";

  /// <summary>Joint family of the rolled (HP) tier; it bolts to nothing.</summary>
  public const string WeldedJoint = "welded";

  /// <summary>Registers the joint family for a <paramref name="tier"/>.</summary>
  public static void RegisterJoint(string tier, string jointFamily) =>
    _jointByTier[tier] = jointFamily;

  /// <summary>
  /// The coupling this pipe presents, resolved from the per-tier registry by its own
  /// <see cref="Tier"/>. Two pipes join only when these match, and a fitting - which names no tier -
  /// takes the flange, so every flanged tier's run reaches them.
  /// </summary>
  public virtual string JointFamily =>
    Tier != null && _jointByTier.TryGetValue(Tier, out string? joint)
      ? joint
      : FlangedJoint;

  /// <summary>
  /// A pipe couples to another pipe only when both present the same joint. Blocks that are not pipes
  /// (machine ports, condensers, fluid intakes) are unaffected and join any tier. Every fitting
  /// (valve, outlet, passthrough) is a <see cref="BlockPipe"/> subclass, so a run reaches only the
  /// fittings of tiers sharing its joint family.
  /// </summary>
  public override bool AcceptsNeighbour(Block neighbour) =>
    neighbour is not BlockPipe other || other.JointFamily == JointFamily;

  #endregion

  // AllowedOrientations and GetFallbackOrientation are inherited from BlockNetworkNode, which derives
  // both from this block's own code-first defs resolved by runtime type, so every pipe subclass gets
  // its own map with no duplicated list and no hand-kept fallback table.
}
