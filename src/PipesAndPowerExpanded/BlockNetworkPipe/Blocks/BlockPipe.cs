using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using PipesAndPowerExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Common;

namespace PipesAndPowerExpanded.BlockNetworkPipe.Blocks;

/// <summary>
/// The base pipe block: a self-orienting node of the unified "pipe" network. Provides the
/// orientation tables shared by every straight/bend/junction variant.
/// </summary>
[BlockRegister]
public partial class BlockPipe : BlockNetworkNode, IBurstablePipe, IExBlockDefProvider
{
  public override string NetworkType => "pipe";

  /// <summary>The mod id, used to build the code-first defs when deriving runtime tables from them.</summary>
  protected const string Domain = "ppex";

  #region Code-first definitions

  /// <summary>
  /// The base pipe blocktypes authored in C# (migrated from
  /// <c>assets/ppex/blocktypes/pipes/{straight,bend,tjunction,xjunction}.json</c>). All four share the
  /// <c>pipe</c> code at distinct asset paths and an identical common surface (<see cref="Common"/>);
  /// each adds only its variant list, shape rotations and collision boxes. The handbook grouping and
  /// the shared surface - copy-pasted across the four files - are authored once here.
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
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
      .Class<BlockPipe>()
      .EntityClass<BlockEntityPipe>()
      .Material(EnumBlockMaterial.Metal)
      .Sound("place", "game:block/anvil")
      .Sound("break", "game:block/anvil")
      .Sound("hit", "game:block/anvil")
      .Sound("walk", "game:walk/stone")
      .MaxStackSize(maxStack)
      .CreativeTab("general", creativeSelector)
      .CreativeTab("ppex", creativeSelector)
      .Handbook(
        "pipe-straight-*",
        "pipe-bend-*",
        "pipe-tjunction-*",
        "pipe-xjunction-*"
      )
      .Behavior("Lockable")
      .TextureByType("*-iron", "iron4", "game:block/metal/sheet-plain/iron4")
      .TextureByType("*-steel", "iron4", "game:block/metal/sheet-plain/steel4")
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(false)
      .SideOpaque(false);

  // Migrated from assets/ppex/blocktypes/pipes/straight.json (2026-07-14).
  private static ExBlockDef Straight(string domain)
  {
    const string s = "ppex:pipes/straight";
    return Common(domain, "pipes/straight", 16, "*-straight-ns-*")
      .VariantGroup("type", "straight")
      .VariantGroup("orientation", "ns", "we", "ud")
      .VariantGroup("material", "iron", "steel")
      .ShapeByType("*-straight-ns-*", s)
      .ShapeByType("*-straight-we-*", s, rotateY: 90)
      .ShapeByType("*-straight-ud-*", s, rotateX: 90)
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f);
  }

  // Migrated from assets/ppex/blocktypes/pipes/bend.json (2026-07-14).
  private static ExBlockDef Bend(string domain)
  {
    const string s = "ppex:pipes/bend";
    return Common(domain, "pipes/bend", 8, "*-bend-nw-*")
      .VariantGroup("type", "bend")
      .VariantGroup(
        "orientation",
        "nw", "se", "en", "ws", "un", "us", "uw", "ue", "dn", "ds", "dw", "de"
      )
      .VariantGroup("material", "iron", "steel")
      .ShapeByType("*-bend-nw-*", s)
      .ShapeByType("*-bend-en-*", s, rotateY: 270)
      .ShapeByType("*-bend-se-*", s, rotateY: 180)
      .ShapeByType("*-bend-ws-*", s, rotateY: 90)
      .ShapeByType("*-bend-dn-*", s, rotateZ: 90)
      .ShapeByType("*-bend-de-*", s, rotateY: 270, rotateZ: 90)
      .ShapeByType("*-bend-ds-*", s, rotateY: 180, rotateZ: 90)
      .ShapeByType("*-bend-dw-*", s, rotateY: 90, rotateZ: 90)
      .ShapeByType("*-bend-un-*", s, rotateZ: 270)
      .ShapeByType("*-bend-ue-*", s, rotateY: 270, rotateZ: 270)
      .ShapeByType("*-bend-us-*", s, rotateY: 180, rotateZ: 270)
      .ShapeByType("*-bend-uw-*", s, rotateY: 90, rotateZ: 270)
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 0.6875f)
      .CollisionBox(0f, 0.3125f, 0.3125f, 0.6875f, 0.6875f, 0.6875f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 0.6875f)
      .SelectionBox(0f, 0.3125f, 0.3125f, 0.6875f, 0.6875f, 0.6875f);
  }

  // Migrated from assets/ppex/blocktypes/pipes/tjunction.json (2026-07-14).
  private static ExBlockDef TJunction(string domain)
  {
    const string s = "ppex:pipes/tjunction";
    return Common(domain, "pipes/tjunction", 8, "*-tjunction-uns-*")
      .VariantGroup("type", "tjunction")
      .VariantGroup(
        "orientation",
        "uns", "uwe", "dns", "dwe", "nes", "esw",
        "swn", "wne", "dnu", "deu", "dsu", "dwu"
      )
      .VariantGroup("material", "iron", "steel")
      .ShapeByType("*-tjunction-wne-*", s)
      .ShapeByType("*-tjunction-nes-*", s, rotateY: 270)
      .ShapeByType("*-tjunction-esw-*", s, rotateY: 180)
      .ShapeByType("*-tjunction-swn-*", s, rotateY: 90)
      .ShapeByType("*-tjunction-uwe-*", s, rotateX: 90)
      .ShapeByType("*-tjunction-uns-*", s, rotateX: 90, rotateZ: 90)
      .ShapeByType("*-tjunction-dwe-*", s, rotateX: 270)
      .ShapeByType("*-tjunction-dns-*", s, rotateX: 270, rotateZ: 90)
      .ShapeByType("*-tjunction-dnu-*", s, rotateZ: 90)
      .ShapeByType("*-tjunction-deu-*", s, rotateY: 270, rotateZ: 90)
      .ShapeByType("*-tjunction-dsu-*", s, rotateY: 180, rotateZ: 90)
      .ShapeByType("*-tjunction-dwu-*", s, rotateY: 90, rotateZ: 90)
      .CollisionBox(0f, 0.3125f, 0.3125f, 1f, 0.6875f, 0.6875f)
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 0.3125f)
      .SelectionBox(0f, 0.3125f, 0.3125f, 1f, 0.6875f, 0.6875f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 0.3125f);
  }

  // Migrated from assets/ppex/blocktypes/pipes/xjunction.json (2026-07-14).
  private static ExBlockDef XJunction(string domain)
  {
    const string s = "ppex:pipes/xjunction";
    return Common(domain, "pipes/xjunction", 8, "*-xjunction-nswe-*")
      .VariantGroup("type", "xjunction")
      .VariantGroup("orientation", "nswe", "nsud", "weud")
      .VariantGroup("material", "iron", "steel")
      .ShapeByType("*-xjunction-nswe-*", s)
      .ShapeByType("*-xjunction-nsud-*", s, rotateZ: 90)
      .ShapeByType("*-xjunction-weud-*", s, rotateY: 90, rotateZ: 90)
      .CollisionBox(0f, 0.3125f, 0.3125f, 1f, 0.6875f, 0.6875f)
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .SelectionBox(0f, 0.3125f, 0.3125f, 1f, 0.6875f, 0.6875f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f);
  }

  #endregion

  /// <summary>
  /// Pipe metal from the <c>material</c> variant (iron/steel). Blocks without the
  /// variant (brick passthrough/outlet) read as iron.
  /// </summary>
  public string Material => Variant["material"] ?? "iron";

  /// <summary>
  /// Pressure (atm) above which this pipe bursts - the weakest pipe limits a run.
  /// Iron 5, steel 10.
  /// </summary>
  public virtual float BurstPressure =>
    Material switch
    {
      "steel" => PpexValues.SteelPipeBurstPressure,
      _ => PpexValues.IronPipeBurstPressure,
    };

  /// <summary>
  /// Whether this pipe takes part in over-pressure failure. Only a plain pipe segment - the four
  /// structural variants of the base <see cref="BlockPipe"/> class (straight/bend/tjunction/
  /// xjunction) - bursts and caps a run's pressure. Every specialised pipe (valve, outlet,
  /// passthrough, tuyere, …) is a subclass and is exempt: it neither bursts nor limits the
  /// pressure, so a new subclass is non-bursting by default unless it deliberately opts back in.
  /// </summary>
  public virtual bool CanBurst => GetType() == typeof(BlockPipe);

  private Dictionary<string, string[]>? _allowedOrientations;

  /// <summary>
  /// Derived from THIS block's own code-first defs (resolved by its runtime type), so the orientation
  /// states live once - in the variant groups - and every pipe subclass inherits the right map with no
  /// duplicated list. Cached on first read.
  /// </summary>
  public override Dictionary<string, string[]> AllowedOrientations =>
    _allowedOrientations ??= ExDefinitions.OrientationMap(
      ExDefinitions.DefinitionsOf(GetType(), Domain)
    );

  /// <summary>A type's default orientation is the first state it lists - which matches every pipe
  /// variant's fallback, so this is derived too instead of a hand-kept table.</summary>
  protected override string GetFallbackOrientation(string? type) =>
    type != null
    && AllowedOrientations.TryGetValue(type, out string[]? states)
    && states.Length > 0
      ? states[0]
      : "ns";
}
