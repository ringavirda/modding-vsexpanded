using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Industry.Metals;

/// <summary>
/// The tool half of the generated metal item family: named stat presets, one template per tool type, and
/// the builder that turns (metal x template x stats) into an <see cref="ExItemDef"/>.
/// <see cref="MetalFamilyEmitter"/> holds the resource forms and emits both in one pass.
/// <para>
/// A metal opts in through <see cref="MetalDef.Tools"/>. Stats are flat: every generated tool of a metal
/// shares one durability / attack power / mining tier and paints its single mining speed across the
/// categories it works, never a <c>*byType</c> table, so durability is the only brittleness knob.
/// Generated tools stay off the vanilla <c>block/metal</c> worldproperty for the reason given on
/// <see cref="MetalFamilyEmitter"/>.
/// </para>
/// </summary>
internal static class MetalToolEmitter {
  /// <summary>The tool tokens this emitter can build (the <see cref="MetalToolSpec.ToolTypes"/> default
  /// set); a requested type outside this set is skipped.</summary>
  internal static IEnumerable<string> KnownTools => ToolTemplates.Keys;

  /// <summary>
  /// Every tool def for <paramref name="metal"/> in its owning <paramref name="domain"/>, or nothing when
  /// the metal makes no tools: it declares no <see cref="MetalDef.Tools"/> spec, or names the <c>none</c>
  /// preset.
  /// </summary>
  internal static IEnumerable<ExItemDef> Emit(MetalDef metal, string domain) {
    if (metal.Tools is not MetalToolSpec spec)
      yield break;
    if (ResolveStats(spec) is not ToolStats resolved)
      yield break;

    foreach (
      string token in (IEnumerable<string>?)spec.ToolTypes ?? ToolTemplates.Keys
    )
      if (ToolTemplates.TryGetValue(token, out ToolTemplate? template))
        yield return ToolItem(metal, domain, token, template, resolved);
  }

  // ---- Tool stats: named presets -> one flat stat block, applied uniformly across the tool set ----
  private sealed record ToolStats(
    int Durability,
    double AttackPower,
    int MiningTier,
    double MiningSpeed
  );

  // brittle: gold-tier durability at iron-tier hardness. good: steel-tier. standard: iron-tier, used by a
  // spec that names no preset. "none" is absent because it resolves to "emit no tools" before a stat
  // block is looked up.
  private static readonly IReadOnlyDictionary<string, ToolStats> Presets =
    new Dictionary<string, ToolStats>(StringComparer.OrdinalIgnoreCase) {
      ["brittle"] = new(150, 2.0, 4, 6.0),
      ["standard"] = new(1000, 2.25, 4, 7.5),
      ["good"] = new(2600, 2.5, 5, 9.0),
    };

  // Resolves a spec to its flat stats, or null when the metal makes no tools (preset "none"). A null or
  // unrecognised preset falls back to "standard"; an explicit number on the spec overrides only the stat
  // it names, the rest riding the preset.
  private static ToolStats? ResolveStats(MetalToolSpec spec) {
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

    return basis with {
      Durability = spec.Durability ?? basis.Durability,
      AttackPower = spec.AttackPower ?? basis.AttackPower,
      MiningTier = spec.MiningTier ?? basis.MiningTier,
    };
  }

  // ---- Tool templates: everything about a tool type that does not vary with the metal ----
  // The vanilla class and ToolType to bind, the iron-equivalent shape and the texture slot the metal
  // paints, the material categories the tool mines, and the held-animation / behaviour / transform surface
  // taken from the vanilla itemtype. The metal supplies only its texture and the flat stat block.
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

  // The eight tool types a tool-making metal gets by default. Values transcribed from the vanilla
  // itemtypes/tool/*.json iron-equivalent variants: shape base, texture slot ("metal" vs "material"),
  // mining categories, held animations and model transforms.
  private static readonly IReadOnlyDictionary<
    string,
    ToolTemplate
  > ToolTemplates = new Dictionary<string, ToolTemplate> {
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
      Gui: new {
        rotation = new {
          x = -89,
          y = 47,
          z = 33,
        },
        origin = new {
          x = 0.65,
          y = 0,
          z = 0.47,
        },
        scale = 1.49,
      },
      Ground: new {
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        origin = new {
          x = 0.5,
          y = 0,
          z = 0.5,
        },
        scale = 3.4,
      },
      TpHand: new {
        translation = new {
          x = -0.87,
          y = -0.01,
          z = -0.56,
        },
        rotation = new {
          x = -90,
          y = 0,
          z = 0,
        },
        origin = new {
          x = 0.5,
          y = 0,
          z = 0.5,
        },
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
      TopLevel: new {
        heldRightReadyAnimation = "axeready",
        heldTpHitAnimation = "axechop",
        attackRange = 2,
      },
      ExtraAttributes: new { heldItemPitchFollow = 0.9 },
      Gui: new {
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = -63,
          y = -123,
          z = -180,
        },
        origin = new {
          x = 0.61,
          y = 0,
          z = 0.47,
        },
        scale = 1.58,
      },
      Ground: new {
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        origin = new {
          x = 0.5,
          y = 0,
          z = 0.5,
        },
        scale = 3.6,
      },
      TpHand: new {
        translation = new {
          x = -0.43,
          y = -0.52,
          z = -0.07,
        },
        rotation = new {
          x = 90,
          y = 0,
          z = 0,
        },
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
      TopLevel: new {
        heldTpIdleAnimation = "shovelidle",
        heldRightReadyAnimation = "shovelready",
        heldTpHitAnimation = "shoveldig",
        heldTpUseAnimation = "interactStaticLong",
      },
      ExtraAttributes: null,
      Gui: new {
        rotation = new {
          x = -115,
          y = -28,
          z = 139,
        },
        origin = new {
          x = 0.7,
          y = 0,
          z = 0.55,
        },
      },
      Ground: new {
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        origin = new {
          x = 0.5,
          y = 0,
          z = 0.5,
        },
        scale = 3.6,
      },
      TpHand: new {
        translation = new {
          x = -1.69,
          y = -0.29,
          z = -0.6,
        },
        rotation = new {
          x = 0,
          y = 0,
          z = -10,
        },
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
      ExtraAttributes: new {
        heldItemPitchFollow = 0.9,
        rememberToolModeWhenBroken = true,
      },
      Gui: new {
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = -77,
          y = 46,
          z = 8,
        },
        origin = new {
          x = 0.59,
          y = 0.5,
          z = 0.49,
        },
        scale = 2.6,
      },
      Ground: new {
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        origin = new {
          x = 0.5,
          y = 0.45,
          z = 0.5,
        },
        scale = 4.5,
      },
      TpHand: new {
        translation = new {
          x = -0.65,
          y = -0.48,
          z = -0.52,
        },
        rotation = new {
          x = 90,
          y = 1,
          z = 0,
        },
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
      Gui: new {
        rotate = false,
        translation = new {
          x = 1,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = -77,
          y = 47,
          z = -151,
        },
        origin = new {
          x = 0.5,
          y = 0.5,
          z = 0.38,
        },
        scale = 1.9,
      },
      Ground: new {
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        origin = new {
          x = 0.5,
          y = 0.45,
          z = 0.5,
        },
        scale = 3.6,
      },
      TpHand: new {
        translation = new {
          x = -0.65,
          y = -0.62,
          z = -0.45,
        },
        rotation = new {
          x = 90,
          y = -174,
          z = 0,
        },
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
      ExtraAttributes: new {
        heldItemPitchFollow = 1,
        knifeHitBlockAnimation = "knifecut",
        knifeHitEntityAnimation = "knifestab",
      },
      Gui: new {
        rotate = false,
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = -158,
          y = 0,
          z = 48,
        },
        origin = new {
          x = 0.48,
          y = 0.1,
          z = 0.5,
        },
        scale = 2.41,
      },
      Ground: new {
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = -90,
          y = 0,
          z = 0,
        },
        origin = new {
          x = 0.5,
          y = 0.5,
          z = 0.45,
        },
        scale = 3.6,
      },
      TpHand: new {
        translation = new {
          x = -0.84,
          y = -0.11,
          z = -0.48,
        },
        rotation = new {
          x = 0,
          y = 0,
          z = -15,
        },
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
      TopLevel: new {
        heldRightReadyAnimation = "chiselready",
        heldTpHitAnimation = "chiselhit",
        heldTpUseAnimation = "chiselhit",
      },
      ExtraAttributes: new {
        heldItemPitchFollow = 1,
        alwaysPlayHeldReady = true,
        rememberToolModeWhenBroken = true,
      },
      Gui: new {
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = 9,
          y = 137,
          z = -53,
        },
        origin = new {
          x = 0.44,
          y = 0,
          z = 0.38,
        },
        scale = 2.92,
      },
      Ground: new {
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        origin = new {
          x = 0.5,
          y = 0,
          z = 0.5,
        },
        scale = 4.31,
      },
      TpHand: new {
        translation = new {
          x = -0.5599,
          y = 0.04,
          z = -0.65,
        },
        rotation = new {
          x = -3,
          y = 0,
          z = -170,
        },
        origin = new {
          x = 0.5,
          y = 0,
          z = 0.5,
        },
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
      TopLevel: new {
        heldTpHitAnimation = "scythe",
        heldTpIdleAnimation = "scytheIdle",
        heldRightReadyAnimation = "scytheReady",
        heldTpUseAnimation = "interactStaticLong",
      },
      // codePrefixes/disallowedSuffixes drive the scythe's area harvest of crops and tall grass
      // (vanilla scythe attributes).
      ExtraAttributes: new {
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
      Gui: new {
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = -93,
          y = 30,
          z = 44,
        },
        origin = new {
          x = 0.5,
          y = 0,
          z = 0.05,
        },
        scale = 0.82,
      },
      Ground: new {
        translation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        rotation = new {
          x = 0,
          y = 0,
          z = 0,
        },
        origin = new {
          x = 0.5,
          y = 0,
          z = 0.5,
        },
        scale = 3.4,
      },
      TpHand: new {
        translation = new {
          x = -1.54,
          y = -0.01,
          z = -0.66,
        },
        rotation = new {
          x = -10,
          y = 0,
          z = 0,
        },
        origin = new {
          x = 0.5,
          y = 0,
          z = 0.5,
        },
        scale = 0.8,
      }
    ),
  };

  #region Tool factory (one builder, driven by the per-type template + the flat stat block)

  // One tool item: {toolToken}-{metalcode} in the metal's owning domain, bound to the vanilla tool class
  // so mining, durability, tool modes and held animations run unchanged. Everything type-specific comes
  // from the template; only the texture and the flat stats come from the metal.
  private static ExItemDef ToolItem(
    MetalDef m,
    string domain,
    string token,
    ToolTemplate t,
    ToolStats s
  ) {
    string texture = MetalFamilyEmitter.TextureOf(m);
    ExItemDef def = ExItemDef.Create(
      domain,
      token + "-" + m.Code,
      m.Code + "/" + token
    );
    if (t.ItemClass != null)
      def.Class(t.ItemClass);

    def.RootKey("tool", t.ToolType)
      .Shape(t.ShapeBase)
      .Texture(t.TextureCode, texture);
    foreach ((string key, string baseTexture) in t.ExtraTextures)
      def.Texture(key, baseTexture);

    def.RootKey("tags", t.Tags);
    if (t.DamagedBy != null)
      def.RootKey("damagedby", t.DamagedBy);
    if (t.StorageFlags is int storageFlags)
      def.StorageFlags(storageFlags);

    def.RootKey("durability", s.Durability);
    if (t.Attacks)
      def.RootKey("attackpower", s.AttackPower);
    if (t.SetsTier)
      def.RootKey("tooltier", s.MiningTier);
    if (t.MiningCategories.Length > 0) {
      var speeds = new JObject();
      foreach (string category in t.MiningCategories)
        speeds[category] = s.MiningSpeed;
      def.RootKey("miningspeed", speeds);
    }

    // Behaviour order mirrors the vanilla tools: GroundStorable, any per-type behaviour, then Buffable.
    var behaviors = new JArray { GroundStorable(t.WallOffY) };
    foreach (object behavior in t.ExtraBehaviors)
      behaviors.Add(JToken.FromObject(behavior));
    behaviors.Add(new JObject { ["name"] = "Buffable" });
    def.RootKey("behaviors", behaviors);

    // Held animations + any per-type top-level keys (axe attackRange, saw rotate:false is in the transform).
    MergeTop(def, t.TopLevel);

    def.Attribute("attachableToEntity", new { categoryCode = "toolholding" })
      .Attribute("handbook", new { groupBy = new[] { token + "-*" } });
    if (t.ExtraAttributes != null)
      def.Attributes(t.ExtraAttributes);

    // The vanilla tool tabs (general/items/tools), not the mod resource tab the ingots and plates use.
    def.CreativeTab("general", "*")
      .CreativeTab("items", "*")
      .CreativeTab("tools", "*");

    def.GuiTransform(t.Gui).GroundTransform(t.Ground).TpHandTransform(t.TpHand);
    return def;
  }

  // The wall-placeable ground-storage behaviour every tool shares, parameterised only by wallOffY (how
  // many cells up the tool hangs), the one field that differs across the vanilla tool set.
  private static JObject GroundStorable(int wallOffY) =>
    JObject.FromObject(
      new {
        name = "GroundStorable",
        properties = new {
          layout = "WallHalves",
          wallOffY,
          ctrlKey = true,
          selectionBox = new {
            x1 = 0,
            y1 = 0,
            z1 = 0,
            x2 = 1,
            y2 = 0.1,
            z2 = 1,
          },
          collisionBox = new {
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

  // Merges a POCO's properties as top-level itemtype keys (held*Animation, attackRange). ExItemDef offers
  // only Attributes, which merges into attributes, so this spreads the blob one top-level key at a time.
  private static void MergeTop(ExItemDef def, object? topLevel) {
    if (topLevel == null)
      return;
    foreach (JProperty property in JObject.FromObject(topLevel).Properties())
      def.RootKey(property.Name, property.Value);
  }

  #endregion
}
