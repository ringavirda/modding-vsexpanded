using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Metals;

/// <summary>
/// Generates the derived RESOURCE item family (ingot / plate / bits / rod / nails) for every
/// <see cref="MetalDef"/> that opts in via <see cref="MetalDef.GenerateItemFamily"/>, so a mod-added
/// alloy gets its build-recipe forms without hand-authoring one itemtype per form. Each item is an
/// <see cref="ExItemDef"/> in the metal's OWNING domain (the <see cref="MetalDef.MoltenItem"/> domain)
/// under the code convention <c>{form}-{metalcode}</c> - e.g. cast iron yields
/// <c>iwex:ingot-castiron</c> / <c>metalplate-castiron</c> / … exactly the codes the cupola and the
/// solidified-cast-iron block already resolve.
/// <para>
/// The templates are lifted straight from the vanilla resource itemtypes (shapes/textures/transforms as
/// <c>game:</c> refs), parameterised by the metal's texture / density / melting point, so a generated
/// ingot is behaviour-identical to the hand-authored one it replaces. Tools are deliberately NOT emitted
/// here (a later stage); this pass is resource forms only. The generator is a pure function of its
/// <see cref="MetalDef"/> input - the runtime path (<see cref="ExDefinitionModSystem"/>) feeds it the
/// live <c>config/metals</c> catalogue, the golden harness feeds it the same JSON from the source tree.
/// </para>
/// <para>
/// A generated metal is deliberately kept OFF the vanilla <c>block/metal</c> worldproperty: registering
/// it there would auto-create an anvil-forgeable <c>workitem-&lt;metal&gt;</c> off the seven itemtypes
/// that load from it, contradicting materials.md's "castable, brittle" cast iron. Authoring the family
/// here keeps full control and never leaks a forge path.
/// </para>
/// </summary>
public static class MetalFamilyEmitter
{
  // Fallbacks for a metal that opts in but leaves a field null. None of the shipped metals rely on
  // these (all state texture/density/melt explicitly); they keep the emitter total so a terse def that
  // only flips the flag still produces sane, iron-like items rather than crashing.
  private const int DefaultDensity = 7870;
  private const int DefaultMeltingPoint = 1482;
  private const string DefaultTexture = "game:block/metal/ingot/iron";

  // The forms a buildable metal gets when ItemForms is left null - the set the iron-substitution recipes
  // reference (ingot + the three build stocks). "bits" is opt-in, not a default, because a produced alloy
  // usually sheds shared vanilla scrap rather than its own bit.
  private static readonly string[] DefaultForms = ["ingot", "plate", "rod", "nails"];

  // form token -> (metal, owning domain) -> item def. The token is the JSON authoring key; the built
  // code/asset-path prefix (metalbit for "bits", metalnailsandstrips for "nails") lives inside each factory.
  private static readonly IReadOnlyDictionary<
    string,
    System.Func<MetalDef, string, ExItemDef>
  > Builders = new Dictionary<string, System.Func<MetalDef, string, ExItemDef>>
  {
    ["ingot"] = Ingot,
    ["plate"] = Plate,
    ["bits"] = Bits,
    ["rod"] = Rod,
    ["nails"] = Nails,
  };

  /// <summary>The form tokens the emitter can build; an <see cref="MetalDef.ItemForms"/> entry outside
  /// this set is silently skipped (a metal cannot conjure a form the emitter has no template for).</summary>
  public static IEnumerable<string> KnownForms => Builders.Keys;

  /// <summary>The tool tokens the emitter can build (the <see cref="MetalToolSpec.ToolTypes"/> default set);
  /// a requested type outside this set is silently skipped, exactly like an unknown resource form.</summary>
  public static IEnumerable<string> KnownTools => ToolTemplates.Keys;

  // ---- Tool stats: named presets -> one flat stat block, applied uniformly across the tool set ----
  // The "flat, non-byType" model (survey Area 3): every generated tool of a metal shares ONE durability /
  // attack / mining tier, and each tool paints that one mining speed across the material categories it
  // works. Cast iron's LOW durability - not a per-tool table - is the deliberate brittleness knob the user
  // chose over a hard mold gate: a cast-iron pick mines iron-tier blocks but shatters fast.
  private sealed record ToolStats(
    int Durability,
    double AttackPower,
    int MiningTier,
    double MiningSpeed
  );

  // Presets (survey D4). brittle ~ gold-tier durability, iron-tier hardness (cast iron); good ~ steel-tier
  // (Bessemer steel); standard ~ iron-tier for any future opt-in that names no preset. "none" is NOT here -
  // it is resolved to "emit no tools" before a stat block is looked up.
  private static readonly IReadOnlyDictionary<string, ToolStats> Presets =
    new Dictionary<string, ToolStats>(StringComparer.OrdinalIgnoreCase)
    {
      ["brittle"] = new(150, 2.0, 4, 6.0),
      ["standard"] = new(1000, 2.25, 4, 7.5),
      ["good"] = new(2600, 2.5, 5, 9.0),
    };

  // Resolve a spec to its flat stats, or null when the metal makes no tools (preset "none"). A null or
  // unrecognised preset falls back to "standard" so a terse spec still yields sane tools; an explicit
  // number on the spec overrides just the stat it names, the rest riding the preset.
  private static ToolStats? ResolveStats(MetalToolSpec spec)
  {
    if (
      spec.Preset != null
      && spec.Preset.Equals("none", StringComparison.OrdinalIgnoreCase)
    )
      return null;

    ToolStats basis = Presets.TryGetValue(
      spec.Preset ?? "standard",
      out ToolStats? found
    )
      ? found!
      : Presets["standard"];

    return basis with
    {
      Durability = spec.Durability ?? basis.Durability,
      AttackPower = spec.AttackPower ?? basis.AttackPower,
      MiningTier = spec.MiningTier ?? basis.MiningTier,
    };
  }

  // ---- Tool templates: everything about a tool type that does NOT vary with the metal ----
  // Which vanilla class + ToolType to bind, the iron-equivalent shape + which texture slot the metal paints,
  // the material categories the tool mines, and the held-animation / behaviour / transform surface lifted
  // from the vanilla itemtype. The metal supplies only its texture + the flat stat block; these supply the
  // rest. Kept OFF the vanilla worldproperty on purpose (see the type remarks): a generated standalone item
  // never leaks an anvil-forgeable workitem the way a worldproperty variant would.
  private sealed record ToolTemplate(
    string ToolType,
    string? ItemClass,
    string ShapeBase,
    string TextureCode,
    int? StorageFlags,
    int WallOffY,
    bool Attacks,
    bool SetsTier,
    string[] MiningCategories,
    string[] Tags,
    string[]? DamagedBy,
    object[] ExtraBehaviors,
    (string Key, string Base)[] ExtraTextures,
    object? TopLevel,
    object? ExtraAttributes,
    object Gui,
    object Ground,
    object TpHand
  );

  // The eight tool types a tool-making metal gets by default (survey Area 3 / the MetalToolSpec doc set).
  // Values transcribed from the vanilla itemtypes/tool/*.json (iron-equivalent variant): shape base, the
  // texture slot ("metal" vs "material"), the mining categories, held animations, and the model transforms.
  private static readonly IReadOnlyDictionary<string, ToolTemplate> ToolTemplates =
    new Dictionary<string, ToolTemplate>
    {
      ["pickaxe"] = new(
        ToolType: "pickaxe",
        ItemClass: null,
        ShapeBase: "game:item/tool/pickaxe-iron",
        TextureCode: "metal",
        StorageFlags: 5,
        WallOffY: 1,
        Attacks: true,
        SetsTier: true,
        MiningCategories: ["stone", "ore", "metal"],
        Tags: ["tool", "tool-pickaxe"],
        DamagedBy: ["blockbreaking", "attacking"],
        ExtraBehaviors: [],
        ExtraTextures: [],
        TopLevel: new { heldTpHitAnimation = "pickaxe" },
        ExtraAttributes: new { heldItemPitchFollow = 0.9 },
        Gui: new
        {
          rotation = new { x = -89, y = 47, z = 33 },
          origin = new { x = 0.65, y = 0, z = 0.47 },
          scale = 1.49,
        },
        Ground: new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 3.4,
        },
        TpHand: new
        {
          translation = new { x = -0.87, y = -0.01, z = -0.56 },
          rotation = new { x = -90, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 0.8,
        }
      ),
      ["axe"] = new(
        ToolType: "axe",
        ItemClass: "ItemAxe",
        ShapeBase: "game:item/tool/axe/iron",
        TextureCode: "material",
        StorageFlags: null,
        WallOffY: 1,
        Attacks: true,
        SetsTier: true,
        MiningCategories: ["wood", "plant", "leaves"],
        Tags: ["tool", "tool-axe"],
        DamagedBy: ["blockbreaking", "attacking"],
        ExtraBehaviors:
        [
          new
          {
            name = "AnimationAuthoritative",
            properties = new { onlyOnEntity = true },
          },
        ],
        ExtraTextures: [],
        TopLevel: new
        {
          heldRightReadyAnimation = "axeready",
          heldTpHitAnimation = "axechop",
          attackRange = 2,
        },
        ExtraAttributes: new { heldItemPitchFollow = 0.9 },
        Gui: new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = -63, y = -123, z = -180 },
          origin = new { x = 0.61, y = 0, z = 0.47 },
          scale = 1.58,
        },
        Ground: new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 3.6,
        },
        TpHand: new
        {
          translation = new { x = -0.43, y = -0.52, z = -0.07 },
          rotation = new { x = 90, y = 0, z = 0 },
          scale = 0.95,
        }
      ),
      ["shovel"] = new(
        ToolType: "shovel",
        ItemClass: null,
        ShapeBase: "game:item/tool/shovel-copper",
        TextureCode: "material",
        StorageFlags: null,
        WallOffY: 2,
        Attacks: true,
        SetsTier: false,
        MiningCategories: ["soil", "sand", "gravel", "snow"],
        Tags: ["tool", "tool-shovel"],
        DamagedBy: ["blockbreaking", "attacking"],
        ExtraBehaviors: [],
        ExtraTextures: [],
        TopLevel: new
        {
          heldTpIdleAnimation = "shovelidle",
          heldRightReadyAnimation = "shovelready",
          heldTpHitAnimation = "shoveldig",
          heldTpUseAnimation = "interactStaticLong",
        },
        ExtraAttributes: null,
        Gui: new
        {
          rotation = new { x = -115, y = -28, z = 139 },
          origin = new { x = 0.7, y = 0, z = 0.55 },
        },
        Ground: new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 3.6,
        },
        TpHand: new
        {
          translation = new { x = -1.69, y = -0.29, z = -0.6 },
          rotation = new { x = 0, y = 0, z = -10 },
          scale = 0.8,
        }
      ),
      ["hammer"] = new(
        ToolType: "hammer",
        ItemClass: "ItemHammer",
        ShapeBase: "game:item/tool/hammer",
        TextureCode: "metal",
        StorageFlags: 257,
        WallOffY: 1,
        Attacks: true,
        SetsTier: true,
        MiningCategories: [],
        Tags: ["tool", "tool-hammer"],
        DamagedBy: ["blockbreaking", "attacking"],
        ExtraBehaviors: [new { name = "AnimationAuthoritative" }],
        ExtraTextures: [("wood", "game:item/tool/handle")],
        TopLevel: new { heldTpHitAnimation = "smithingwide" },
        ExtraAttributes: new
        {
          heldItemPitchFollow = 0.9,
          rememberToolModeWhenBroken = true,
        },
        Gui: new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = -77, y = 46, z = 8 },
          origin = new { x = 0.59, y = 0.5, z = 0.49 },
          scale = 2.6,
        },
        Ground: new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0.45, z = 0.5 },
          scale = 4.5,
        },
        TpHand: new
        {
          translation = new { x = -0.65, y = -0.48, z = -0.52 },
          rotation = new { x = 90, y = 1, z = 0 },
          scale = 1,
        }
      ),
      ["saw"] = new(
        ToolType: "saw",
        ItemClass: null,
        ShapeBase: "game:item/tool/saw",
        TextureCode: "metal",
        StorageFlags: null,
        WallOffY: 1,
        Attacks: false,
        SetsTier: true,
        MiningCategories: ["wood", "leaves"],
        Tags: ["tool", "tool-saw"],
        DamagedBy: ["blockbreaking", "attacking"],
        ExtraBehaviors: [new { name = "EntityDeconstructTool" }],
        ExtraTextures: [],
        TopLevel: new { heldTpHitAnimation = "breaktool" },
        ExtraAttributes: null,
        Gui: new
        {
          rotate = false,
          translation = new { x = 1, y = 0, z = 0 },
          rotation = new { x = -77, y = 47, z = -151 },
          origin = new { x = 0.5, y = 0.5, z = 0.38 },
          scale = 1.9,
        },
        Ground: new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0.45, z = 0.5 },
          scale = 3.6,
        },
        TpHand: new
        {
          translation = new { x = -0.65, y = -0.62, z = -0.45 },
          rotation = new { x = 90, y = -174, z = 0 },
          scale = 1,
        }
      ),
      ["knife"] = new(
        ToolType: "knife",
        ItemClass: "ItemKnife",
        ShapeBase: "game:item/tool/knife/copper",
        TextureCode: "material",
        StorageFlags: null,
        WallOffY: 1,
        Attacks: true,
        SetsTier: false,
        MiningCategories: ["plant"],
        Tags: ["weapon", "weapon-melee", "tool-knife", "tool-trimming", "tool"],
        DamagedBy: ["blockbreaking", "attacking"],
        ExtraBehaviors: [new { name = "AnimationAuthoritative" }],
        ExtraTextures: [],
        TopLevel: new { heldTpHitAnimation = "knifestab" },
        ExtraAttributes: new
        {
          heldItemPitchFollow = 1,
          knifeHitBlockAnimation = "knifecut",
          knifeHitEntityAnimation = "knifestab",
        },
        Gui: new
        {
          rotate = false,
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = -158, y = 0, z = 48 },
          origin = new { x = 0.48, y = 0.1, z = 0.5 },
          scale = 2.41,
        },
        Ground: new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = -90, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0.5, z = 0.45 },
          scale = 3.6,
        },
        TpHand: new
        {
          translation = new { x = -0.84, y = -0.11, z = -0.48 },
          rotation = new { x = 0, y = 0, z = -15 },
          scale = 1,
        }
      ),
      ["chisel"] = new(
        ToolType: "Chisel",
        ItemClass: "ItemChisel",
        ShapeBase: "game:item/tool/chisel",
        TextureCode: "metal",
        StorageFlags: null,
        WallOffY: 1,
        Attacks: false,
        SetsTier: false,
        MiningCategories: [],
        Tags: ["tool", "tool-chisel"],
        // Vanilla chisel ships no damagedby (ItemChisel spends durability itself, per microblock edit).
        DamagedBy: null,
        ExtraBehaviors: [],
        ExtraTextures: [],
        TopLevel: new
        {
          heldRightReadyAnimation = "chiselready",
          heldTpHitAnimation = "chiselhit",
          heldTpUseAnimation = "chiselhit",
        },
        ExtraAttributes: new
        {
          heldItemPitchFollow = 1,
          alwaysPlayHeldReady = true,
          rememberToolModeWhenBroken = true,
        },
        Gui: new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 9, y = 137, z = -53 },
          origin = new { x = 0.44, y = 0, z = 0.38 },
          scale = 2.92,
        },
        Ground: new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 4.31,
        },
        TpHand: new
        {
          translation = new { x = -0.5599, y = 0.04, z = -0.65 },
          rotation = new { x = -3, y = 0, z = -170 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 0.76,
        }
      ),
      ["scythe"] = new(
        ToolType: "Scythe",
        ItemClass: "ItemScythe",
        ShapeBase: "game:item/tool/scythe",
        TextureCode: "metal",
        StorageFlags: null,
        WallOffY: 2,
        Attacks: false,
        SetsTier: false,
        MiningCategories: [],
        Tags: ["tool", "tool-scythe"],
        DamagedBy: ["blockbreaking"],
        ExtraBehaviors: [],
        ExtraTextures: [],
        TopLevel: new
        {
          heldTpHitAnimation = "scythe",
          heldTpIdleAnimation = "scytheIdle",
          heldRightReadyAnimation = "scytheReady",
          heldTpUseAnimation = "interactStaticLong",
        },
        // codePrefixes/disallowedSuffixes are load-bearing: they are what makes the scythe area-harvest
        // crops and tall grass (vanilla scythe attributes), not decoration.
        ExtraAttributes: new
        {
          heldItemPitchFollow = 0.5,
          codePrefixes = new[]
          {
            "crop",
            "tallgrass",
            "frostedtallgrass",
            "tallplant-coopersreed-land-normal",
            "tallplant-papyrus-land-normal",
            "tallplant-tule-land-normal",
            "tallplant-coopersreed-water-normal",
            "tallplant-papyrus-water-normal",
            "tallplant-coopersreed-ice-normal",
            "flower-horsetail",
          },
          disallowedSuffixes = new[] { "snow2", "snow3" },
          rememberToolModeWhenBroken = true,
        },
        Gui: new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = -93, y = 30, z = 44 },
          origin = new { x = 0.5, y = 0, z = 0.05 },
          scale = 0.82,
        },
        Ground: new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 3.4,
        },
        TpHand: new
        {
          translation = new { x = -1.54, y = -0.01, z = -0.66 },
          rotation = new { x = -10, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 0.8,
        }
      ),
    };

  /// <summary>
  /// Every resource item def for every opted-in metal in <paramref name="metals"/>, each created in its
  /// owning domain. A metal with <see cref="MetalDef.GenerateItemFamily"/> false - every vanilla / EM
  /// metal, and every metal today that hasn't opted in - contributes nothing.
  /// </summary>
  public static IEnumerable<ExItemDef> Emit(IEnumerable<MetalDef> metals)
  {
    foreach (MetalDef metal in metals)
    {
      if (
        !metal.GenerateItemFamily
        || string.IsNullOrEmpty(metal.Code)
        || string.IsNullOrEmpty(metal.MoltenItem)
      )
        continue;

      // The owning domain is where the metal's molten item lives (iwex:ingot-castiron -> iwex); every
      // generated form is co-located there so {form}-{code} resolves in the same domain.
      string domain = new AssetLocation(metal.MoltenItem).Domain;
      foreach (
        string token in (IEnumerable<string>?)metal.ItemForms ?? DefaultForms
      )
        if (
          Builders.TryGetValue(
            token,
            out System.Func<MetalDef, string, ExItemDef>? build
          )
        )
          yield return build(metal, domain);

      // Tool family (opt-in per metal via Tools; a feedstock like pig iron leaves Tools null and makes
      // none). The preset resolves to one flat stat block; each requested tool template binds a vanilla
      // tool class and paints those stats on. Homed in the same owning domain as the resource forms.
      if (metal.Tools is MetalToolSpec spec)
      {
        ToolStats? stats = ResolveStats(spec);
        if (stats is ToolStats resolved)
          foreach (
            string token in (IEnumerable<string>?)spec.ToolTypes
              ?? ToolTemplates.Keys
          )
            if (ToolTemplates.TryGetValue(token, out ToolTemplate? template))
              yield return ToolItem(metal, domain, token, template, resolved);
      }
    }
  }

  #region Shared surface

  private static string TextureOf(MetalDef m) => m.TexturePath ?? DefaultTexture;

  private static int DensityOf(MetalDef m) => m.Density ?? DefaultDensity;

  private static int MeltOf(MetalDef m) => m.MeltingPoint ?? DefaultMeltingPoint;

  // The metal's own ingot - what every non-ingot form smelts back into (mass-honestly recovers the alloy,
  // not vanilla iron).
  private static string IngotCodeOf(MetalDef m, string domain) =>
    domain + ":ingot-" + m.Code;

  // The density/storage/creative-tab surface every form shares, plus the code + asset path. storageFlags 5
  // and the general + owning-mod tabs mirror the vanilla resource itemtypes these are modelled on.
  private static ExItemDef Begin(MetalDef m, string domain, string codePrefix) =>
    ExItemDef
      .Create(domain, codePrefix + "-" + m.Code, m.Code + "/" + codePrefix)
      .MaterialDensity(DensityOf(m))
      .StorageFlags(5)
      .CreativeCommon("*");

  #endregion

  #region Form factories (vanilla templates, parameterised by the metal)

  private static ExItemDef Ingot(MetalDef m, string domain)
  {
    string texture = TextureOf(m);
    return Begin(m, domain, "ingot")
      .Class("ItemIngot")
      .MaxStackSize(16)
      .Shape("game:item/ingot")
      // The ingot shape's texture code is #metal (item/ingot.json), not "all".
      .Texture("metal", texture)
      .CombustibleProps(
        new
        {
          meltingPoint = MeltOf(m),
          meltingDuration = 30,
          smeltedRatio = 1,
          smeltedStack = new { type = "item", code = IngotCodeOf(m, domain) },
        }
      )
      // What a shattered ingot mold gives back - the metal's shared scrap (SolidDrop), the same bit
      // MoltenChisel recovers via SolidDropOf, so shatter and chisel agree. Convention fallback: the
      // metal's own bit in its domain.
      .Attribute(
        "shatteredStack",
        new
        {
          type = "item",
          code = m.SolidDrop ?? domain + ":metalbit-" + m.Code,
        }
      )
      .Raw(
        "behaviors",
        new[]
        {
          new
          {
            name = "GroundStorable",
            properties = new
            {
              layout = "Stacking",
              placeRemoveSound = "game:sounds/block/ingot",
              stackingModel = "game:block/metal/ingotpile",
              stackingTextures = new { metal = texture },
              modelItemsToStackSizeRatio = 1,
              upSolid = true,
              stackingCapacity = 64,
              transferQuantity = 1,
              bulkTransferQuantity = 4,
              collisionBox = new
              {
                x1 = 0,
                y1 = 0,
                z1 = 0,
                x2 = 1,
                y2 = 0.125,
                z2 = 1,
              },
              cbScaleYByLayer = 0.125,
            },
          },
        }
      )
      .GuiTransform(
        new
        {
          translation = new { x = 2, y = 0, z = 0 },
          rotation = new { x = 149, y = -36, z = 0 },
          origin = new { x = 0.5, y = 0.1, z = 0.5 },
          scale = 3.5,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -0.77, y = -0.15, z = -0.64 },
          rotation = new { x = 0, y = -71, z = 18 },
          origin = new { x = 0.5, y = 0.1, z = 0.5 },
          scale = 0.75,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 4.8,
        }
      );
  }

  private static ExItemDef Bits(MetalDef m, string domain) =>
    Begin(m, domain, "metalbit")
      // ItemNugget only exists to show the unit yield in the tooltip - same as vanilla metalbit.
      .Class("ItemNugget")
      .MaxStackSize(128)
      .Shape("game:item/nugget")
      // The nugget shape's texture code is #ore (item/nugget.json), not "all".
      .Texture("ore", TextureOf(m))
      // 20 bits -> 1 ingot, exactly like vanilla metalbit, so chiselled scrap re-melts mass-honestly.
      .CombustibleProps(
        new
        {
          meltingPoint = MeltOf(m),
          meltingDuration = 30,
          smeltedRatio = 20,
          smeltedStack = new { type = "item", code = IngotCodeOf(m, domain) },
        }
      )
      .Raw(
        "behaviors",
        new[]
        {
          new
          {
            name = "GroundStorable",
            properties = new
            {
              layout = "Messy12",
              collisionBox = new
              {
                x1 = 0,
                y1 = 0,
                z1 = 0,
                x2 = 1,
                y2 = 0.125,
                z2 = 1,
              },
              bulkTransferQuantity = 4,
            },
          },
        }
      )
      .GuiTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 176, y = 132, z = -21 },
          origin = new { x = 0.5, y = 0.07, z = 0.5 },
          scale = 5.61,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -0.8, y = -0.1, z = -0.7 },
          rotation = new { x = 5, y = 82, z = 16 },
          origin = new { x = 0.5, y = 0.1, z = 0.5 },
          scale = 0.7,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 5,
        }
      );

  private static ExItemDef Plate(MetalDef m, string domain)
  {
    string texture = TextureOf(m);
    return Begin(m, domain, "metalplate")
      .Class("ItemMetalPlate")
      .MaxStackSize(8)
      .Shape("game:item/plate")
      .Texture("metal", texture)
      // 200 units of metal in, one 200-unit plate out (-> 2 ingots) - mass-conserving.
      .CombustibleProps(
        new
        {
          meltingPoint = MeltOf(m),
          meltingDuration = 30,
          smeltedRatio = 1,
          smeltedStack = new
          {
            type = "item",
            code = IngotCodeOf(m, domain),
            stacksize = 2,
          },
        }
      )
      .Raw(
        "behaviors",
        new[]
        {
          new
          {
            name = "GroundStorable",
            properties = new
            {
              layout = "Stacking",
              upSolid = true,
              placeRemoveSound = "game:sounds/block/plate",
              stackingModel = "game:block/metal/platepile",
              stackingTextures = new { metal = texture },
              collisionBox = new
              {
                x1 = 0.125,
                y1 = 0,
                z1 = 0.125,
                x2 = 0.875,
                y2 = 0.0625,
                z2 = 0.875,
              },
              cbScaleYByLayer = 1,
              modelItemsToStackSizeRatio = 1,
              stackingCapacity = 16,
              transferQuantity = 1,
              bulkTransferQuantity = 4,
            },
          },
        }
      )
      .GuiTransform(
        new
        {
          translation = new { x = 3, y = 0, z = 0 },
          rotation = new { x = -30, y = -44, z = -180 },
          origin = new { x = 0.5, y = 0.0625, z = 0.5 },
          scale = 1.85,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -0.7, y = 0.1, z = -0.53 },
          rotation = new { x = 94, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0.1, z = 0.5 },
          scale = 0.75,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 90, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 3.31,
        }
      );
  }

  private static ExItemDef Rod(MetalDef m, string domain) =>
    // Vanilla part/rod.json: no bound class (plain Item), its own rod-pile ground model.
    Begin(m, domain, "rod")
      .MaxStackSize(16)
      .Shape("game:item/rod")
      .Texture("metal", TextureOf(m))
      .CombustibleProps(
        new
        {
          meltingPoint = MeltOf(m),
          meltingDuration = 30,
          smeltedRatio = 1,
          smeltedStack = new { type = "item", code = IngotCodeOf(m, domain) },
        }
      )
      .Raw(
        "behaviors",
        new[]
        {
          new
          {
            name = "GroundStorable",
            properties = new
            {
              layout = "Stacking",
              placeRemoveSound = "game:sounds/block/ingot",
              stackingModel = "game:item/rod-pile",
              upSolid = false,
              modelItemsToStackSizeRatio = 1,
              stackingCapacity = 32,
              transferQuantity = 1,
              bulkTransferQuantity = 8,
              collisionBox = new
              {
                x1 = 0,
                y1 = 0,
                z1 = 0,
                x2 = 1,
                y2 = 0.14,
                z2 = 1,
              },
              cbScaleYByLayer = 0.15,
            },
          },
        }
      )
      .GuiTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = -48, y = 134, z = 171 },
          origin = new { x = 0.53, y = 0.2, z = 0.4 },
          scale = 2.8,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -0.9, y = -0.05, z = -0.78 },
          rotation = new { x = 85, y = 0, z = 2 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 0.68,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 4,
        }
      );

  private static ExItemDef Nails(MetalDef m, string domain) =>
    // Vanilla resource/metalnailsandstrips.json: plain Item, Messy12 ground layout. The tong-held shape
    // behaviour is intentionally dropped - a cast/brittle alloy is never tong-worked.
    Begin(m, domain, "metalnailsandstrips")
      .MaxStackSize(32)
      .Shape("game:item/resource/metalnailsandstrips")
      .Texture("metal", TextureOf(m))
      // 4 nails-and-strips -> 1 ingot, like vanilla.
      .CombustibleProps(
        new
        {
          meltingPoint = MeltOf(m),
          meltingDuration = 30,
          smeltedRatio = 4,
          smeltedStack = new
          {
            type = "item",
            code = IngotCodeOf(m, domain),
            stacksize = 1,
          },
        }
      )
      .Raw(
        "behaviors",
        new[]
        {
          new
          {
            name = "GroundStorable",
            properties = new
            {
              layout = "Messy12",
              collisionBox = new
              {
                x1 = 0,
                y1 = 0,
                z1 = 0,
                x2 = 1,
                y2 = 0.125,
                z2 = 1,
              },
            },
          },
        }
      )
      .GuiTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = -30, y = -46, z = -180 },
          origin = new { x = 0.58, y = 0.07, z = 0.55 },
          scale = 3.55,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -0.7, y = -0.2, z = -0.6 },
          rotation = new { x = 94, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0.1, z = 0.5 },
          scale = 0.75,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 90, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 4,
        }
      );

  #endregion

  #region Tool factory (one builder, driven by the per-type template + the flat stat block)

  // One tool item: {toolToken}-{metalcode} in the metal's owning domain, binding the vanilla tool class so
  // the whole tool pipeline (mining, durability, tool modes, held animations) runs unchanged. Everything
  // type-specific comes from the template; only the texture + the flat stats come from the metal. Stats are
  // written FLAT (a single durability / attackpower / tooltier, one miningspeed per category) - never a
  // *byType* table - exactly as the survey specified.
  private static ExItemDef ToolItem(
    MetalDef m,
    string domain,
    string token,
    ToolTemplate t,
    ToolStats s
  )
  {
    string texture = TextureOf(m);
    ExItemDef def = ExItemDef.Create(
      domain,
      token + "-" + m.Code,
      m.Code + "/" + token
    );
    if (t.ItemClass != null)
      def.Class(t.ItemClass);

    def.Raw("tool", t.ToolType).Shape(t.ShapeBase).Texture(t.TextureCode, texture);
    foreach ((string key, string baseTexture) in t.ExtraTextures)
      def.Texture(key, baseTexture);

    def.Raw("tags", t.Tags);
    if (t.DamagedBy != null)
      def.Raw("damagedby", t.DamagedBy);
    if (t.StorageFlags is int storageFlags)
      def.StorageFlags(storageFlags);

    def.Raw("durability", s.Durability);
    if (t.Attacks)
      def.Raw("attackpower", s.AttackPower);
    if (t.SetsTier)
      def.Raw("tooltier", s.MiningTier);
    if (t.MiningCategories.Length > 0)
    {
      var speeds = new JObject();
      foreach (string category in t.MiningCategories)
        speeds[category] = s.MiningSpeed;
      def.Raw("miningspeed", speeds);
    }

    // Behaviour order mirrors the vanilla tools: GroundStorable, any per-type behaviour, then Buffable.
    var behaviors = new JArray { GroundStorable(t.WallOffY) };
    foreach (object behavior in t.ExtraBehaviors)
      behaviors.Add(JToken.FromObject(behavior));
    behaviors.Add(new JObject { ["name"] = "Buffable" });
    def.Raw("behaviors", behaviors);

    // Held animations + any per-type top-level keys (axe attackRange, saw rotate:false is in the transform).
    MergeTop(def, t.TopLevel);

    def.Attribute("attachableToEntity", new { categoryCode = "toolholding" })
      .Attribute("handbook", new { groupBy = new[] { token + "-*" } });
    if (t.ExtraAttributes != null)
      def.Attributes(t.ExtraAttributes);

    // Match the vanilla tool tabs (general/items/tools) so a cast-iron pick sits beside the game's own
    // picks in the creative Tools tab - not the mod resource tab the ingots/plates use.
    def.CreativeTab("general", "*").CreativeTab("items", "*").CreativeTab("tools", "*");

    def.GuiTransform(t.Gui).GroundTransform(t.Ground).TpHandTransform(t.TpHand);
    return def;
  }

  // The wall-placeable ground-storage behaviour every tool shares, parameterised only by wallOffY (how many
  // cells up the tool hangs) - the one field that differs across the vanilla tool set.
  private static JObject GroundStorable(int wallOffY) =>
    JObject.FromObject(
      new
      {
        name = "GroundStorable",
        properties = new
        {
          layout = "WallHalves",
          wallOffY,
          ctrlKey = true,
          selectionBox = new
          {
            x1 = 0,
            y1 = 0,
            z1 = 0,
            x2 = 1,
            y2 = 0.1,
            z2 = 1,
          },
          collisionBox = new
          {
            x1 = 0,
            y1 = 0,
            z1 = 0,
            x2 = 0,
            y2 = 0,
            z2 = 0,
          },
        },
      }
    );

  // Merge a POCO's properties as TOP-LEVEL itemtype keys (held*Animation, attackRange, …). The sibling of
  // ExItemDef.Attributes, which merges into attributes; there is no top-level-merge on the builder, so this
  // spreads the template's TopLevel blob one key at a time.
  private static void MergeTop(ExItemDef def, object? topLevel)
  {
    if (topLevel == null)
      return;
    foreach (JProperty property in JObject.FromObject(topLevel).Properties())
      def.Raw(property.Name, property.Value);
  }

  #endregion
}
