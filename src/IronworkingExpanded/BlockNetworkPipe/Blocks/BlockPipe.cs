using System;
using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Common;

namespace IronworkingExpanded.BlockNetworkPipe.Blocks;

/// <summary>
/// The base pipe block: a self-orienting node of the unified "pipe" network. Provides the
/// orientation tables shared by every straight/bend/junction variant.
/// <para>
/// Lives in iwex, the lowest mod that ships pipes (bolted tier). Higher tiers reuse this exact class
/// and its block entity by referencing the registered class keys <c>iwex.BlockPipe</c> /
/// <c>iwex.BlockEntityPipe</c> and calling <see cref="Segments"/> from a thin per-mod
/// <see cref="IExBlockDefProvider"/>: lpex supplies the cast tier, hpex the rolled tier. There is one
/// material per tier - the old iron/steel <c>material</c> variant is gone; the tier is the domain.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockPipe : BlockNetworkNode, IBurstablePipe, IExBlockDefProvider
{
  public override string NetworkType => "pipe";

  #region Code-first definitions

  /// <summary>iwex's own (bolted) pipe segments - discovered when the iwex assembly is scanned
  /// (domain <c>iwex</c>). The cast/rolled tiers call <see cref="Segments"/> from their own mods.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) => Segments(domain);

  /// <summary>
  /// The four plain pipe segments (straight / bend / T / X junction) for one tier
  /// <paramref name="domain"/>. All four share the <c>pipe</c> code at distinct asset paths and an
  /// identical common surface (<see cref="Common"/>); each adds only its variant list, shape rotations
  /// and collision boxes. <b>Each tier ships its own shapes</b> at <c>{domain}:pipes/*</c> - bolted
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
      // Shared base class + BE, registered by iwex; every tier's segments bind to these keys.
      .Class("iwex.BlockPipe")
      .EntityClass("iwex.BlockEntityPipe")
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
      // could not work if it tried - the shapes do not agree on key names (the bolted bend calls its
      // body sheet "iron42" where the straight calls it "iron4"), so overriding one key would repaint
      // some segments and quietly miss others.
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(false)
      .SideOpaque(false);

  // Migrated from assets/lpex/blocktypes/pipes/straight.json (2026-07-14); shapes shared in iwex.
  private static ExBlockDef Straight(string domain)
  {
    string s = $"{domain}:pipes/straight";
    return Common(domain, "pipes/straight", 16, "*-straight-ns")
      .VariantGroup("type", "straight")
      .VariantGroup("orientation", "ns", "we", "ud")
      .ShapeByType("*-straight-ns", s)
      .ShapeByType("*-straight-we", s, rotateY: 90)
      .ShapeByType("*-straight-ud", s, rotateX: 90)
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f);
  }

  // Migrated from assets/lpex/blocktypes/pipes/bend.json (2026-07-14).
  private static ExBlockDef Bend(string domain)
  {
    string s = $"{domain}:pipes/bend";
    return Common(domain, "pipes/bend", 8, "*-bend-nw")
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

  // Migrated from assets/lpex/blocktypes/pipes/tjunction.json (2026-07-14).
  private static ExBlockDef TJunction(string domain)
  {
    string s = $"{domain}:pipes/tjunction";
    return Common(domain, "pipes/tjunction", 8, "*-tjunction-uns")
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

  // Migrated from assets/lpex/blocktypes/pipes/xjunction.json (2026-07-14).
  private static ExBlockDef XJunction(string domain)
  {
    string s = $"{domain}:pipes/xjunction";
    return Common(domain, "pipes/xjunction", 8, "*-xjunction-nswe")
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
  // (iwex bolted, lpex cast, ...). Resolved by the segment's own domain, so a run of mixed tiers is
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

  #region Joint family (which tiers physically couple)

  // A pipe tier's JOINT, which is a different axis from its pressure rating. The bolted (iwex) and
  // cast (lpex) tiers are both square in section and bolted through flanges, so they mate; the rolled
  // (hpex) tier is octagonal and WELDED, with no flange to bolt to, so it mates only with itself.
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
