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
/// <see cref="IExBlockDefProvider"/>: iwex the plated tier, lpex the cast tier, hpex the rolled
/// tier. One material per tier, so there is no <c>material</c> variant group.
/// </summary>
[BlockRegister]
public partial class BlockPipe
  : BlockNetworkNode,
    IBurstablePipe,
    IThroughputLimitedPipe,
    IExBlockDefProvider {
  public override string NetworkType => "pipe";

  #region Code-first definitions

  /// <summary>The plain pipe segments for a tier <paramref name="domain"/>, registered by each tier's
  /// own provider. Yields nothing for exlib itself, which authors the factories but ships no pipe
  /// content. Must still return the real segments for a tier domain:
  /// <see cref="BlockNetworkNode.AllowedOrientations"/> derives its map from them.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    domain == "exlib" ? [] : Segments(domain);

  /// <summary>
  /// The four plain pipe segments (straight / bend / T / X junction) for one tier
  /// <paramref name="domain"/>. All four share the <c>pipe</c> code at distinct asset paths and an
  /// identical common surface (<see cref="Common"/>); each adds only its variant list, shape rotations
  /// and collision boxes. Each tier ships its own shapes at <c>{domain}:pipe/*</c>.
  /// </summary>
  public static IEnumerable<ExBlockDef> Segments(string domain) =>
    [Straight(domain), Bend(domain), TJunction(domain), XJunction(domain)];

  // The surface shared by every pipe blocktype; each type overlays its variants/shapes/boxes.
  private static ExBlockDef Common(
    string domain,
    string assetName,
    int maxStack,
    string creativeSelector
  ) =>
    ExBlockDef
      .Create(domain, "pipe", assetName)
      // Shared base class + BE, registered by exlib; every tier's segments bind to these keys.
      .Class("exlib.BlockPipe")
      .EntityClass("exlib.BlockEntityPipe")
      .Material(EnumBlockMaterial.Metal)
      .MetalSounds()
      .MaxStackSize(maxStack)
      .CreativeTab("general", creativeSelector)
      .CreativeTab(domain, creativeSelector)
      .Handbook(
        "pipe-straight-*",
        "pipe-bend-*",
        "pipe-tjunction-*",
        "pipe-xjunction-*"
      )
      .Behavior("Lockable")
      // No blanket texture override: each tier's shape declares its own texture map, and the shapes
      // disagree on key names (the plated bend calls its body sheet "iron42" where the straight
      // calls it "iron4"), so a single override would repaint some segments and miss others.
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(false)
      .SideOpaque(false);

  private static ExBlockDef Straight(string domain) {
    string s = $"{domain}:pipe/straight";
    return Common(domain, "pipe/straight", 16, "*-straight-ns")
      .VariantGroup("type", "straight")
      .VariantGroup("orientation", "ns", "we", "ud")
      .ShapeByType("*-straight-ns", s)
      .ShapeByType("*-straight-we", s, rotateY: 90)
      .ShapeByType("*-straight-ud", s, rotateX: 90)
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f);
  }

  private static ExBlockDef Bend(string domain) {
    string s = $"{domain}:pipe/bend";
    return Common(domain, "pipe/bend", 8, "*-bend-nw")
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

  private static ExBlockDef TJunction(string domain) {
    string s = $"{domain}:pipe/tjunction";
    return Common(domain, "pipe/tjunction", 8, "*-tjunction-uns")
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

  private static ExBlockDef XJunction(string domain) {
    string s = $"{domain}:pipe/xjunction";
    return Common(domain, "pipe/xjunction", 8, "*-xjunction-nswe")
      .VariantGroup("type", "xjunction")
      .VariantGroup("orientation", "nswe", "nsud", "weud")
      .ShapeByType("*-xjunction-nswe", s)
      .ShapeByType("*-xjunction-nsud", s, rotateZ: 90)
      .ShapeByType("*-xjunction-weud", s, rotateY: 90, rotateZ: 90)
      .CollisionBox(0f, 0.3125f, 0.3125f, 1f, 0.6875f, 0.6875f)
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .SelectionBox(0f, 0.3125f, 0.3125f, 1f, 0.6875f, 0.6875f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f);
  }

  #endregion

  #region Burst rating (per-tier)

  // Each pipe tier registers its plain-segment burst pressure from its own config in ModSystem.Start.
  // Resolved by the segment's own domain, so a run of mixed tiers is capped by its weakest segment.
  private static readonly Dictionary<string, Func<float>> _burstByDomain =
    new();

  private const float DefaultBurstPressure = 5f;

  /// <summary>Registers the plain-segment burst pressure (atm) for a tier <paramref name="domain"/>.</summary>
  public static void RegisterBurst(string domain, Func<float> burstPressure) =>
    _burstByDomain[domain] = burstPressure;

  /// <summary>
  /// Pressure (atm) above which this pipe bursts - the weakest pipe limits a run. Read from the
  /// per-tier registry keyed by this block's domain (falls back to <see cref="DefaultBurstPressure"/>
  /// if the owning mod never registered one).
  /// </summary>
  public virtual float BurstPressure =>
    _burstByDomain.TryGetValue(Code.Domain, out var f)
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
  // LitresPerPipe) or how hard it can be pressurised (burst). Registered per domain from each mod's
  // ModSystem, like the burst rating and the joint family; a run is capped by its weakest segment.
  private static readonly Dictionary<string, Func<float>> _throughputByDomain =
    new();

  private const float DefaultThroughput = 120f;

  /// <summary>Registers the throughput (L/s) for a tier <paramref name="domain"/>.</summary>
  public static void RegisterThroughput(
    string domain,
    Func<float> throughput
  ) => _throughputByDomain[domain] = throughput;

  /// <summary>
  /// Litres per second this pipe will pass - the smallest across a run caps the whole run. Read from
  /// the per-tier registry keyed by this block's domain (falls back to
  /// <see cref="DefaultThroughput"/> if the owning mod never registered one).
  /// <para>
  /// Only a plain segment limits throughput, the same rule as <see cref="CanBurst"/>. Fittings are
  /// exempt because some are a machine's own port on a single-node network rather than a length of
  /// main (the iwex tuyere), and limiting there would cap every furnace at that tier's rate.
  /// </para>
  /// </summary>
  public virtual float MaxThroughput =>
    !CanBurst ? float.MaxValue
    : _throughputByDomain.TryGetValue(Code.Domain, out var f) ? f()
    : DefaultThroughput;

  #endregion

  #region Joint family (which tiers physically couple)

  // A pipe tier's joint, an axis independent of its pressure rating. The plated (iwex) and cast
  // (lpex) tiers are both square in section and bolted through flanges, so they mate; the rolled
  // (hpex) tier is octagonal and welded, with no flange to bolt to, so it mates only with itself.
  // Registered per domain from each mod's ModSystem, like the burst rating.
  private static readonly Dictionary<string, string> _jointByDomain = new();

  /// <summary>Default joint family: the bolted flange.</summary>
  public const string FlangedJoint = "flanged";

  /// <summary>Joint family of the rolled (HP) tier; it bolts to nothing.</summary>
  public const string WeldedJoint = "welded";

  /// <summary>Registers the joint family for a tier <paramref name="domain"/>.</summary>
  public static void RegisterJoint(string domain, string jointFamily) =>
    _jointByDomain[domain] = jointFamily;

  /// <summary>
  /// The coupling this pipe presents, resolved from the per-tier registry by its own domain. Two pipes
  /// join only when these match.
  /// </summary>
  public virtual string JointFamily =>
    _jointByDomain.TryGetValue(Code.Domain, out string? joint)
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
