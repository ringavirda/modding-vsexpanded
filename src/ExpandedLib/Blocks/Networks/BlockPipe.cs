using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// The base pipe block: a self-orienting node of the unified "pipe" network. Provides the
/// orientation tables shared by every straight/bend/junction variant.
/// <para>
/// Lives in exlib with the network framework. Every tier reuses
/// this exact class and its block entity by referencing the registered class keys <c>exlib.BlockPipe</c> /
/// <c>exlib.BlockEntityPipe</c> and calling <see cref="Segments"/> from a thin per-mod
/// <see cref="IExBlockDefProvider"/>: iwex supplies the plated tier, lpex the cast tier, hpex the
/// rolled tier. There is one material per tier - the tier is the domain, and there is deliberately
/// no <c>material</c> variant group.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockPipe
  : BlockNetworkNode,
    IBurstablePipe,
    IThroughputLimitedPipe,
    IExBlockDefProvider
{
  public override string NetworkType => "pipe";

  #region Code-first definitions

  /// <summary>The plain pipe segments for a tier <paramref name="domain"/>. Every tier registers them
  /// through its own thin provider (iwex's <c>PlatedPipeDefinitions</c>, lpex's <c>CastPipeDefinitions</c>,
  /// hpex's <c>RolledPipeDefinitions</c>); the framework's own scan gets nothing - exlib authors the
  /// factories but ships no pipe content itself. The declared factory must still return the real segments
  /// for tier domains: <see cref="BlockNetworkNode.AllowedOrientations"/> derives its map from it.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    domain == "exlib" ? [] : Segments(domain);

  /// <summary>
  /// The four plain pipe segments (straight / bend / T / X junction) for one tier
  /// <paramref name="domain"/>. All four share the <c>pipe</c> code at distinct asset paths and an
  /// identical common surface (<see cref="Common"/>); each adds only its variant list, shape rotations
  /// and collision boxes. <b>Each tier ships its own shapes</b> at <c>{domain}:pipe/*</c> - plated
  /// plate-and-rivet for iwex, cast for lpex, rolled for hpex - so a tier is a different model, not
  /// just a different tint on one.
  /// </summary>
  public static IEnumerable<ExBlockDef> Segments(string domain) =>
    [Straight(domain), Bend(domain), TJunction(domain), XJunction(domain)];

  // The surface identical across every pipe blocktype; each type overlays its variants/shapes/boxes.
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
      // No blanket texture override: each tier's shape declares its own texture map now. It also
      // could not work if it tried - the shapes do not agree on key names (the plated bend calls its
      // body sheet "iron42" where the straight calls it "iron4"), so overriding one key would repaint
      // some segments and quietly miss others.
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(false)
      .SideOpaque(false);

  // Migrated from assets/lpex/blocktypes/pipe/straight.json; shapes shared in iwex.
  private static ExBlockDef Straight(string domain)
  {
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

  // Migrated from assets/lpex/blocktypes/pipe/bend.json.
  private static ExBlockDef Bend(string domain)
  {
    string s = $"{domain}:pipe/bend";
    return Common(domain, "pipe/bend", 8, "*-bend-nw")
      .VariantGroup("type", "bend")
      .VariantGroup(
        "orientation",
        "nw", "se", "en", "ws", "un", "us", "uw", "ue", "dn", "ds", "dw", "de"
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

  // Migrated from assets/lpex/blocktypes/pipe/tjunction.json.
  private static ExBlockDef TJunction(string domain)
  {
    string s = $"{domain}:pipe/tjunction";
    return Common(domain, "pipe/tjunction", 8, "*-tjunction-uns")
      .VariantGroup("type", "tjunction")
      .VariantGroup(
        "orientation",
        "uns", "uwe", "dns", "dwe", "nes", "esw",
        "swn", "wne", "dnu", "deu", "dsu", "dwu"
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

  // Migrated from assets/lpex/blocktypes/pipe/xjunction.json.
  private static ExBlockDef XJunction(string domain)
  {
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

  // Each pipe tier registers its plain-segment burst pressure from its own config in ModSystem.Start
  // (iwex plated, lpex cast, ...). Resolved by the segment's own domain, so a run of mixed tiers is
  // capped by whichever tier's segment is weakest - exactly as the old per-material rating worked.
  private static readonly Dictionary<string, Func<float>> _burstByDomain = new();

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
    _burstByDomain.TryGetValue(Code.Domain, out var f) ? f() : DefaultBurstPressure;

  /// <summary>
  /// Whether this pipe takes part in over-pressure failure. Only a plain pipe segment - the
  /// structural variants of the base <see cref="BlockPipe"/> class (straight/bend/tjunction/
  /// xjunction) - bursts and caps a run's pressure. Every specialised pipe (valve, outlet,
  /// passthrough, tuyere, …) is a subclass and is exempt: it neither bursts nor limits the
  /// pressure, so a new subclass is non-bursting by default unless it deliberately opts back in.
  /// </summary>
  public virtual bool CanBurst => GetType() == typeof(BlockPipe);

  #endregion

  #region Throughput (per-tier)

  // How much a tier's pipe will pass per second, as opposed to how much a run holds (nodes x
  // LitresPerPipe) or how hard it can be pressurised (burst). Registered per domain from each mod's
  // ModSystem, exactly like the burst rating and the joint family - a run is capped by its weakest
  // segment.
  private static readonly Dictionary<string, Func<float>> _throughputByDomain = new();

  private const float DefaultThroughput = 120f;

  /// <summary>Registers the throughput (L/s) for a tier <paramref name="domain"/>.</summary>
  public static void RegisterThroughput(string domain, Func<float> throughput) =>
    _throughputByDomain[domain] = throughput;

  /// <summary>
  /// Litres per second this pipe will pass - the smallest across a run caps the whole run. Read from
  /// the per-tier registry keyed by this block's domain (falls back to
  /// <see cref="DefaultThroughput"/> if the owning mod never registered one).
  /// <para>
  /// <b>Only a plain segment limits throughput - the same rule as <see cref="CanBurst"/>, and for a
  /// sharper reason than symmetry.</b> An earlier draft limited on every subclass, arguing a valve is
  /// still a length of the tier's pipe and does not widen the line. That is true of a valve and false of
  /// the case that matters: the <b>tuyere</b> is an <c>iwex</c> block on its own single-node network,
  /// because it is the furnace's own intake port rather than a length of main - so limiting on it would
  /// cap <i>every</i> furnace in the suite at the iron tier's rate forever, including smex's hot blast
  /// furnace. That is the exact opposite of what a pipe tier is for.
  /// </para>
  /// <para>
  /// The "splice a valve to widen your line" worry the old rule guarded against is not reachable: the
  /// plain segments a run is built from still limit it, and nobody builds a run entirely of fittings.
  /// </para>
  /// </summary>
  public virtual float MaxThroughput =>
    !CanBurst ? float.MaxValue
    : _throughputByDomain.TryGetValue(Code.Domain, out var f) ? f()
    : DefaultThroughput;

  #endregion

  #region Joint family (which tiers physically couple)

  // A pipe tier's joint, which is a different axis from its pressure rating. The plated (iwex) and
  // cast (lpex) tiers are both square in section and bolted through flanges, so they mate; the rolled
  // (hpex) tier is octagonal and welded, with no flange to bolt to, so it mates only with itself.
  // Registered per domain from each mod's ModSystem, exactly like the burst rating.
  private static readonly Dictionary<string, string> _jointByDomain = new();

  /// <summary>The joint every tier gets until its mod says otherwise - the bolted flange.</summary>
  public const string FlangedJoint = "flanged";

  /// <summary>The welded joint of the rolled (HP) tier, which bolts to nothing.</summary>
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
  /// A pipe couples to another pipe only when both present the same joint. Anything that is not a pipe
  /// - a machine port, a condenser, a fluid intake - is unaffected: those are ports on a machine, not
  /// a length of pipe, and a tier is not expected to bring its own boiler.
  /// <para>
  /// Because every fitting (valve, outlet, passthrough) is a <see cref="BlockPipe"/> subclass, this
  /// also stops a rolled run from reaching the cast tier's fittings - which is correct and deliberate:
  /// the HP tier needs its own fittings, and until it has them a rolled run is segments and machine
  /// ports only.
  /// </para>
  /// </summary>
  public override bool AcceptsNeighbour(Block neighbour) =>
    neighbour is not BlockPipe other || other.JointFamily == JointFamily;

  #endregion

  // AllowedOrientations + GetFallbackOrientation are inherited from BlockNetworkNode, which derives both from
  // this block's own code-first defs (resolved by runtime type) - so every pipe subclass gets the right map
  // with no duplicated list and no hand-kept fallback table.
}
