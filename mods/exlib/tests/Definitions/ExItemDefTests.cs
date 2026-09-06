using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Unit coverage for the item-side fluent builder <see cref="ExItemDef"/>: each method emits the
/// itemtype JSON key the vanilla object loader expects, the asset location targets <c>itemtypes/</c>
/// rather than <c>blocktypes/</c>, and the object-valued transform, recipe and attribute helpers
/// round-trip a POCO. Pure (no registry), so no serialization collection is needed.
/// </summary>
public class ExItemDefTests {
  // A stand-in item class for the type-safe Class<T>() overload.
  private sealed class SampleItem : Item { }

  // A stand-in collectible behavior for the type-safe Behavior<T>() overload.
  [ExpandedLib.Registries.CollectibleBehaviorRegister]
  private sealed class FakeBehavior : CollectibleBehavior {
    public FakeBehavior(CollectibleObject collObj)
      : base(collObj) { }
  }

  [Fact]
  public void Location_targets_the_itemtypes_category_and_carries_code() {
    ExItemDef def = ExItemDef.Create("iiex", "slag");
    Assert.Equal("iiex", def.Domain);
    Assert.Equal("slag", def.Code);
    Assert.Equal("iiex", def.Location.Domain);
    Assert.Equal("itemtypes/slag.json", def.Location.Path);
    Assert.Equal("slag", (string?)def.ToJson()["code"]);
  }

  [Fact]
  public void Create_with_asset_name_decouples_the_path_from_the_code() {
    ExItemDef def = ExItemDef.Create("iiex", "slag", "byproduct/slag");
    Assert.Equal("slag", def.Code);
    Assert.Equal("itemtypes/byproduct/slag.json", def.Location.Path);
  }

  [Fact]
  public void Class_generic_binds_the_registered_key_from_the_type() {
    ExItemDef def = ExItemDef.Create("test", "sample").Class<SampleItem>();
    Assert.Equal("test.SampleItem", (string?)def.ToJson()["class"]);
  }

  [Fact]
  public void Scalars_and_held_animations_emit_their_keys() {
    JObject json = ExItemDef
      .Create("iiex", "burden")
      .MaxStackSize(128)
      .MaterialDensity(300)
      .StorageFlags(4)
      .HeldTpIdleAnimation("holdbothhands")
      .HeldRightReadyAnimation("holdbothhands")
      .Shape("game:item/resource/crushed/normal")
      .ToJson();

    Assert.Equal(128, (int)json["maxstacksize"]!);
    Assert.Equal(300, (int)json["materialDensity"]!);
    Assert.Equal(4, (int)json["storageFlags"]!);
    Assert.Equal("holdbothhands", (string?)json["heldTpIdleAnimation"]);
    Assert.Equal("holdbothhands", (string?)json["heldRightReadyAnimation"]);
    Assert.Equal(
      "game:item/resource/crushed/normal",
      (string?)json["shape"]!["base"]
    );
  }

  [Fact]
  public void Texture_and_texture_all_accumulate_under_textures() {
    JObject json = ExItemDef
      .Create("iiex", "largegear")
      .Texture("rusty-iron", "game:block/metal/ingot/{metal}")
      .Texture("gold", "game:block/metal/ingot/{metal}")
      .ToJson();

    Assert.Equal(
      "game:block/metal/ingot/{metal}",
      (string?)json["textures"]!["rusty-iron"]!["base"]
    );
    Assert.Equal(
      "game:block/metal/ingot/{metal}",
      (string?)json["textures"]!["gold"]!["base"]
    );

    JObject all = ExItemDef
      .Create("iiex", "slag")
      .TextureAll("game:x")
      .ToJson();
    Assert.Equal("game:x", (string?)all["textures"]!["all"]!["base"]);
  }

  [Fact]
  public void VariantGroup_and_VariantStates_round_trip() {
    ExItemDef def = ExItemDef
      .Create("iiex", "gear")
      .VariantGroup("metal", "iron", "steel");
    Assert.Equal(["iron", "steel"], def.VariantStates("metal"));

    var groups = (JArray)def.ToJson()["variantgroups"]!;
    Assert.Single(groups);
    Assert.Equal("metal", (string?)groups[0]["code"]);
  }

  [Fact]
  public void CreativeTab_accumulates_and_CreativeCommon_derives_the_mod_tab() {
    JObject tabs = ExItemDef
      .Create("iiex", "slag")
      .CreativeTab("general", "*")
      .CreativeTab("items", "*")
      .ToJson();
    Assert.Equal(
      "*",
      (string?)((JArray)tabs["creativeinventory"]!["general"]!)[0]
    );
    Assert.Equal(
      "*",
      (string?)((JArray)tabs["creativeinventory"]!["items"]!)[0]
    );

    JObject common = ExItemDef
      .Create("iiex", "slag")
      .CreativeCommon("*")
      .ToJson();
    // general + the domain-derived mod tab, no hand-copied mod name.
    Assert.NotNull(common["creativeinventory"]!["general"]);
    Assert.NotNull(common["creativeinventory"]!["iiex"]);
  }

  [Fact]
  public void Transforms_take_a_poco_and_land_at_the_root_transform_keys() {
    JObject json = ExItemDef
      .Create("iiex", "slag")
      .GuiTransform(
        new {
          rotation = new {
            x = 150,
            y = -38,
            z = 0,
          },
          scale = 3.8,
        }
      )
      .TpHandTransform(new { scale = 0.6 })
      .GroundTransform(new { scale = 4.5 })
      .ToJson();

    Assert.Equal(150, (int)json["guiTransform"]!["rotation"]!["x"]!);
    Assert.Equal(3.8, (double)json["guiTransform"]!["scale"]!);
    Assert.Equal(0.6, (double)json["tpHandTransform"]!["scale"]!);
    Assert.Equal(4.5, (double)json["groundTransform"]!["scale"]!);
  }

  [Fact]
  public void CombustibleProps_and_GrindingProps_land_at_the_root() {
    JObject json = ExItemDef
      .Create("iiex", "slag")
      .CombustibleProps(new { meltingPoint = 720 })
      .GrindingProps(
        new { groundStack = new { type = "item", code = "iiex:powderedslag" } }
      )
      .ToJson();

    Assert.Equal(720, (int)json["combustibleProps"]!["meltingPoint"]!);
    Assert.Equal(
      "iiex:powderedslag",
      (string?)json["grindingProps"]!["groundStack"]!["code"]
    );
  }

  [Fact]
  public void Attribute_and_Attributes_populate_the_attributes_object() {
    JObject json = ExItemDef
      .Create("iiex", "slag")
      .Attribute("shatteredStack", new { type = "item", code = "iiex:slag" })
      .Attributes(
        new { dissolveInWater = true, fertilizerTextureCode = "potash" }
      )
      .ToJson();

    Assert.Equal(
      "iiex:slag",
      (string?)json["attributes"]!["shatteredStack"]!["code"]
    );
    Assert.True((bool)json["attributes"]!["dissolveInWater"]!);
    Assert.Equal(
      "potash",
      (string?)json["attributes"]!["fertilizerTextureCode"]
    );
  }

  [Fact]
  public void ToJson_returns_an_independent_clone() {
    ExItemDef def = ExItemDef.Create("iiex", "slag");
    JObject first = def.ToJson();
    first["code"] = "mutated";
    Assert.Equal("slag", (string?)def.ToJson()["code"]);
  }

  [Fact]
  public void VariantGroupFromProperties_codeless_omits_the_code_key() {
    JObject json = ExItemDef
      .Create("d", "c")
      .VariantGroupFromProperties("game:abstract/metal")
      .ToJson();

    var group = (JObject)((JArray)json["variantgroups"]!)[0]!;
    Assert.Null(group["code"]);
    Assert.Equal("game:abstract/metal", (string?)group["loadFromProperties"]);
  }

  [Fact]
  public void SkipVariants_emits_a_wildcard_array() {
    JObject json = ExItemDef
      .Create("d", "c")
      .SkipVariants("gear-waxedcheddar-*", "gear-blue-*")
      .ToJson();
    Assert.Equal(
      ["gear-waxedcheddar-*", "gear-blue-*"],
      json["skipVariants"]!.ToObject<string[]>()!
    );
  }

  [Fact]
  public void ShapeByType_emits_base_plus_only_the_set_rotations() {
    JObject json = ExItemDef
      .Create("d", "c")
      .ShapeByType("*-iron", "iiex:item/gear")
      .ShapeByType("*-steel", "iiex:item/gear", rotateY: 90)
      .ToJson();

    var byType = (JObject)json["shapebytype"]!;
    Assert.Equal("iiex:item/gear", (string?)byType["*-iron"]!["base"]);
    Assert.Null(byType["*-iron"]!["rotateY"]);
    Assert.Equal(90, (int)byType["*-steel"]!["rotateY"]!);
  }

  [Fact]
  public void TextureByType_maps_a_wildcard_to_a_texture_key_base() {
    JObject json = ExItemDef
      .Create("d", "c")
      .TextureByType("*-iron", "iron4", "game:block/metal/sheet-plain/iron4")
      .ToJson();
    Assert.Equal(
      "game:block/metal/sheet-plain/iron4",
      (string?)json["texturesByType"]!["*-iron"]!["iron4"]!["base"]
    );
  }

  [Fact]
  public void TextureByType_emits_overlays_when_supplied() {
    JObject json = ExItemDef
      .Create("d", "c")
      .TextureByType(
        "*",
        "front1",
        "game:block/clay/brick/four/running/cream1",
        "game:block/clay/brick/four/running/{brick}1"
      )
      .ToJson();

    var tex = json["texturesByType"]!["*"]!["front1"]!;
    Assert.Equal(
      "game:block/clay/brick/four/running/cream1",
      (string?)tex["base"]
    );
    Assert.Equal(
      ["game:block/clay/brick/four/running/{brick}1"],
      tex["overlays"]!.ToObject<string[]>()!
    );
  }

  [Fact]
  public void Behavior_by_name_by_type_and_with_properties() {
    JObject json = ExItemDef
      .Create("iiex", "c")
      .Behavior("GroundStorable")
      .Behavior<FakeBehavior>()
      .Behavior("GroundStorable", new { layout = "SingleCenter" })
      .ToJson();

    var behaviors = (JArray)json["behaviors"]!;
    Assert.Equal("GroundStorable", (string?)behaviors[0]!["name"]);
    // Typed overload resolves the registered {modid}.{ClassName} key, same as the class binding.
    Assert.Equal("iiex.FakeBehavior", (string?)behaviors[1]!["name"]);
    Assert.Equal(
      "SingleCenter",
      (string?)behaviors[2]!["properties"]!["layout"]
    );
  }

  [Fact]
  public void Handbook_sets_the_attributes_handbook_groupBy() {
    JObject json = ExItemDef
      .Create("d", "c")
      .Handbook("gear-iron-*", "gear-steel-*")
      .ToJson();
    Assert.Equal(
      ["gear-iron-*", "gear-steel-*"],
      json["attributes"]!["handbook"]!["groupBy"]!.ToObject<string[]>()!
    );
  }

  [Fact]
  public void HandbookExclude_sets_the_nested_handbook_exclude_flag() {
    JObject json = ExItemDef.Create("d", "c").HandbookExclude().ToJson();
    Assert.True((bool)json["attributes"]!["handbook"]!["exclude"]!);
    Assert.Null(json["handbook"]); // the handbook system reads attributes.handbook, not a top-level key
  }

  [Fact]
  public void AttributeByType_accumulates_a_typed_byType_map_under_attributes() {
    JObject json = ExItemDef
      .Create("d", "c")
      .AttributeByType("widthByType", "*-large", 2)
      .AttributeByType("widthByType", "*-small", 1)
      .ToJson();

    var byType = (JObject)json["attributes"]!["widthByType"]!;
    Assert.Equal(2, (int)byType["*-large"]!);
    Assert.Equal(1, (int)byType["*-small"]!);
  }

  [Fact]
  public void RootKeyByType_accumulates_a_top_level_byType_map() {
    JObject json = ExItemDef
      .Create("d", "c")
      .RootKeyByType("tpHandTransformByType", "*-large", new { scale = 0.6 })
      .RootKeyByType("tpHandTransformByType", "*-small", new { scale = 1.2 })
      .ToJson();

    var byType = (JObject)json["tpHandTransformByType"]!;
    Assert.Equal(0.6, (double)byType["*-large"]!["scale"]!);
    Assert.Equal(1.2, (double)byType["*-small"]!["scale"]!);
  }

  [Fact]
  public void Positional_transforms_emit_translation_rotation_origin_and_scale() {
    JObject json = ExItemDef
      .Create("d", "c")
      .GuiTransform(0, 3, 0, 0, 90, 0, 0.5, 0, 0.5, 1.33)
      .FpHandTransform(-0.87, -0.01, -0.56, -90, 0, 0, 0, 0, 0, 0.8)
      .TpHandTransform(-0.87, -0.01, -0.56, -90, 0, 0, 0.5, 0, 0.5, 0.8)
      .GroundTransform(0, 0, 0, 0, 0, 0, 0, 0, 0, 2.5)
      .ToJson();

    Assert.Equal(3, (int)json["guiTransform"]!["translation"]!["y"]!);
    Assert.Equal(90, (int)json["guiTransform"]!["rotation"]!["y"]!);
    Assert.Equal(0.5, (double)json["guiTransform"]!["origin"]!["x"]!);
    Assert.Equal(1.33, (double)json["guiTransform"]!["scale"]!);

    Assert.Equal(0.8, (double)json["fpHandTransform"]!["scale"]!);
    Assert.Equal(-90, (int)json["fpHandTransform"]!["rotation"]!["x"]!);

    Assert.Equal(0.5, (double)json["tpHandTransform"]!["origin"]!["x"]!);
    Assert.Equal(0.8, (double)json["tpHandTransform"]!["scale"]!);

    Assert.Equal(2.5, (double)json["groundTransform"]!["scale"]!);
  }

  #region Parity with the block builder

  // Real itemtype keys the item builder doesn't yet have a dedicated typed method for, but which the
  // generic Attribute/AttributeByType/RootKey/RootKeyByType escape hatches already cover - not a gap
  // this parity check is about.

  // Block-only method names: the JSON key each writes has no equivalent on ItemType/CollectibleType
  // (verified against the vendored ItemType.cs/CollectibleType.cs/BlockType.cs), or the method exists
  // only to serve a block-specific mechanism (block-entity behaviors, megablocks, world placement and
  // orientation, the block-code emitter). One reason per name.
  private static readonly HashSet<string> BlockOnlyMethodNames =
    new() {
      // No block-entity analogue: items carry no BlockEntity.
      "EntityClass",
      "EntityBehavior",
      // BlockType-only fields (verified absent from CollectibleType/ItemType).
      "Material",
      "Resistance",
      "Replaceable",
      "WalkSpeedMultiplier",
      "MiningTier", // requiredMiningTier; an item's ToolTier is a different, unmirrored concept.
      "LightAbsorption",
      "NoDrops",
      "Drop",
      "CollisionBox",
      "SelectionBox",
      "SingleCollisionBox",
      "SingleSelectionBox",
      "SideSolid",
      "SideOpaque",
      "SideAo",
      "EmitSideAo",
      "NonSolid", // derived from SideSolid/SideOpaque, both BlockType-only.
      "SolidNonOpaque",
      "RenderPass", // no shipped itemtype sets renderpass; chunk render passes are a placed-block concept.
      "FaceCullMode", // culls a placed block's faces against its neighbours; no itemtype equivalent.
      "DrawType", // selects how a placed block is meshed in the chunk; no itemtype equivalent.
      // sounds.* is a BlockType-only field (an item's HeldSounds is a separate, unrelated key).
      "Sound",
      "Sounds",
      "MetalSounds",
      "SoundByTool",
      "SoundByType",
      // World placement/orientation: items are never placed with a facing side.
      "ShapeRotateYByType",
      "ShapeSpunPerOrientation",
      "ShapeByTypePerOrientation",
      "SideVariant",
      "NetworkOriented",
      // MineTool is a dead no-op on the block builder (mineTool is not a key the loader reads);
      // never had an item counterpart to propagate.
      "MineTool",
      // Derives a placed/legend code from VariantGroups for BlockCodeEmitter's generated {Mod}Blocks
      // table; items have no code-emitter counterpart.
      "WithVariant",
      // Megablocks are blocks; items cannot be (part of) a multiblock structure.
      "FillerOffsets",
      "FillerOffsetsByType",
      "Construction",
      "Multiblock",
      "MultiblockLayout",
    };

  [Fact]
  public void Every_block_builder_method_that_applies_to_items_exists_on_the_item_builder() {
    var blockMethodNames = typeof(ExBlockDef)
      .GetMethods(BindingFlags.Public | BindingFlags.Instance)
      .Where(m => !m.IsSpecialName) // drop property accessors (get_Domain, get_Code, ...)
      .Select(m => m.Name)
      .Distinct()
      .Except(BlockOnlyMethodNames);

    var itemMethodNames = typeof(ExItemDef)
      .GetMethods(BindingFlags.Public | BindingFlags.Instance)
      .Where(m => !m.IsSpecialName)
      .Select(m => m.Name)
      .ToHashSet();

    var missing = blockMethodNames.Where(n => !itemMethodNames.Contains(n)).ToList();
    Assert.True(
      missing.Count == 0,
      "ExItemDef is missing a method for: " + string.Join(", ", missing)
    );
  }

  #endregion
}
