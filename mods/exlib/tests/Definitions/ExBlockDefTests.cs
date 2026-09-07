using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using ExpandedLib.Structures;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="ExBlockDef"/> builds the blocktype <see cref="JObject"/> the vanilla object loader
/// consumes. Pins the token shape of each field plus a whole-block parity check against a verbatim copy
/// of a shipped blocktype (iiex's <c>solidifiediron</c>): the emitted JSON must be semantically equal to
/// the hand-authored file it replaces.
/// </summary>
public class ExBlockDefTests {
  // Verbatim copy of iiex/blocktypes/blastfurnace/solidifiediron.json, the golden the builder must
  // reproduce. Inline rather than read from the iiex asset so it survives that file's deletion when
  // the block moves to code-first.
  private const string SolidifiedIronJson = """
    {
      "code": "solidifiediron",
      "class": "iiex.BlockSolidifiedIron",
      "entityClass": "iiex.BlockEntitySolidifiedIron",
      "blockmaterial": "Metal",
      "creativeinventory": { "general": ["*"], "iiex": ["*"] },
      "shape": { "base": "game:block/basic/cube" },
      "textures": { "all": { "base": "game:block/metal/sheet-plain/iron5" } },
      "resistance": 45.0,
      "maxstacksize": 8,
      "requiredMiningTier": 5,
      "sounds": {
        "place": "game:block/anvil",
        "break": "game:block/anvil",
        "hit": "game:block/anvil",
        "walk": "game:walk/stone"
      }
    }
    """;

  [BlockRegister]
  private sealed class CodeFirstBlock : Block { }

  [BlockEntityRegister]
  private sealed class CodeFirstBlockEntity : BlockEntity { }

  #region Full-block parity oracle
  [Fact]
  public void Builder_reproduces_a_real_shipped_blocktype_exactly() {
    ExBlockDef def = ExBlockDef
      .Create("iiex", "solidifiediron")
      .Class("iiex.BlockSolidifiedIron")
      .EntityClass("iiex.BlockEntitySolidifiedIron")
      .Material(EnumBlockMaterial.Metal)
      .CreativeTab("general", "*")
      .CreativeTab("iiex", "*")
      .Shape("game:block/basic/cube")
      .TextureAll("game:block/metal/sheet-plain/iron5")
      .Resistance(45f)
      .MaxStackSize(8)
      .MiningTier(5)
      .Sound("place", "game:block/anvil")
      .Sound("break", "game:block/anvil")
      .Sound("hit", "game:block/anvil")
      .Sound("walk", "game:walk/stone");

    JObject expected = JObject.Parse(SolidifiedIronJson);
    Assert.True(
      JToken.DeepEquals(expected, def.ToJson()),
      "Emitted JSON diverged from the golden blocktype:\n" + def.ToJson()
    );
  }
  #endregion

  #region Type-safe class binding
  [Fact]
  public void Class_of_T_resolves_the_registered_modid_dot_classname_key() {
    JObject json = ExBlockDef
      .Create("test", "x")
      .Class<CodeFirstBlock>()
      .EntityClass<CodeFirstBlockEntity>()
      .ToJson();

    Assert.Equal("test.CodeFirstBlock", (string?)json["class"]);
    Assert.Equal("test.CodeFirstBlockEntity", (string?)json["entityClass"]);
  }

  [Fact]
  public void Class_of_T_matches_KeyFor_so_a_rename_cannot_desync() {
    // The typed overload emits the same string the class registry keys on.
    JObject json = ExBlockDef
      .Create("iiex", "x")
      .Class<CodeFirstBlock>()
      .ToJson();
    Assert.Equal(
      EntityRegistry.KeyFor("iiex", typeof(CodeFirstBlock)),
      (string?)json["class"]
    );
  }
  #endregion

  #region Per-field token shapes
  [Fact]
  public void Code_is_set_from_Create() {
    Assert.Equal(
      "solidifiediron",
      (string?)ExBlockDef.Create("iiex", "solidifiediron").ToJson()["code"]
    );
  }

  [Fact]
  public void Material_emits_the_enum_name() {
    Assert.Equal(
      "Metal",
      (string?)
        ExBlockDef.Create("d", "c").Material(EnumBlockMaterial.Metal).ToJson()[
          "blockmaterial"
        ]
    );
  }

  [Fact]
  public void MineTool_is_a_no_op_since_the_game_never_reads_the_key() {
#pragma warning disable CS0618 // exercising the obsolete no-op on purpose
    JObject json = ExBlockDef
      .Create("d", "c")
      .MineTool(EnumTool.Pickaxe)
      .ToJson();
#pragma warning restore CS0618
    Assert.Null(json["mineTool"]);
  }

  [Fact]
  public void Shape_emits_a_base_reference_object() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .Shape("game:block/basic/cube")
      .ToJson();
    Assert.Equal("game:block/basic/cube", (string?)json["shape"]!["base"]);
  }

  [Fact]
  public void Textures_accumulate_across_calls() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .Texture("side", "game:a")
      .Texture("top", "game:b")
      .ToJson();
    Assert.Equal("game:a", (string?)json["textures"]!["side"]!["base"]);
    Assert.Equal("game:b", (string?)json["textures"]!["top"]!["base"]);
  }

  [Fact]
  public void CreativeTab_emits_a_selector_array_per_tab() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .CreativeTab("general", "*-a-*", "*-b-*")
      .ToJson();
    var arr = (JArray)json["creativeinventory"]!["general"]!;
    Assert.Equal(["*-a-*", "*-b-*"], arr.ToObject<string[]>()!);
  }

  [Fact]
  public void Attribute_nests_a_poco_under_attributes() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .Attribute("fillHeight", 0.5f)
      .Attribute("offsets", new[] { 1, 2, 3 })
      .ToJson();
    Assert.Equal(0.5f, (float)json["attributes"]!["fillHeight"]!);
    Assert.Equal([1, 2, 3], json["attributes"]!["offsets"]!.ToObject<int[]>()!);
  }

  [Fact]
  public void RootKey_sets_an_arbitrary_top_level_token_as_the_escape_hatch() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .RootKey("someFutureField", new JValue(42))
      .ToJson();
    Assert.Equal(42, (int)json["someFutureField"]!);
  }

  [Fact]
  public void RootKey_from_a_poco_sets_a_top_level_object_for_root_transforms() {
    // The object overload, for block-root transforms whose shape varies per block (here: no rotation).
    JObject json = ExBlockDef
      .Create("d", "c")
      .RootKey(
        "guiTransform",
        new {
          translation = new {
            x = 0,
            y = 3,
            z = 0,
          },
          scale = 1.33,
        }
      )
      .ToJson();
    Assert.Equal(3, (int)json["guiTransform"]!["translation"]!["y"]!);
    Assert.Equal(1.33, (double)json["guiTransform"]!["scale"]!);
    Assert.Null(json["guiTransform"]!["rotation"]);
  }
  #endregion

  #region Location
  [Fact]
  public void Location_targets_the_blocktypes_json_the_loader_filters_on() {
    AssetLocation loc = ExBlockDef.Create("iiex", "solidifiediron").Location;
    Assert.Equal("iiex", loc.Domain);
    Assert.Equal("blocktypes/solidifiediron.json", loc.Path);
  }

  [Fact]
  public void A_distinct_asset_name_separates_the_path_from_the_shared_code() {
    // Several pipe defs share code "pipe" but need unique asset paths.
    ExBlockDef def = ExBlockDef.Create("iiex", "pipe", "pipes/straight");
    Assert.Equal("pipe", (string?)def.ToJson()["code"]);
    Assert.Equal("blocktypes/pipes/straight.json", def.Location.Path);
    Assert.Equal("iiex", def.Location.Domain);
  }
  #endregion

  #region Variant blocks (groups / *ByType / behaviors / physics)

  [BlockBehaviorRegister]
  private sealed class FakeBehavior(Block block) : BlockBehavior(block);

  [BlockEntityBehaviorRegister]
  private sealed class FakeEntityBehavior(BlockEntity be)
    : BlockEntityBehavior(be);

  [Fact]
  public void VariantGroup_appends_ordered_code_and_states_entries() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .VariantGroup("type", "straight")
      .VariantGroup("material", "iron", "steel")
      .ToJson();

    var groups = (JArray)json["variantgroups"]!;
    Assert.Equal(2, groups.Count);
    Assert.Equal("type", (string?)groups[0]!["code"]); // order preserved
    Assert.Equal(["straight"], groups[0]!["states"]!.ToObject<string[]>()!);
    Assert.Equal("material", (string?)groups[1]!["code"]);
    Assert.Equal(
      ["iron", "steel"],
      groups[1]!["states"]!.ToObject<string[]>()!
    );
  }

  [Fact]
  public void VariantGroupFromProperties_emits_a_loadFromProperties_entry() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .VariantGroupFromProperties("side", "abstract/horizontalorientation")
      .ToJson();
    var group = (JObject)((JArray)json["variantgroups"]!)[0]!;
    Assert.Equal("side", (string?)group["code"]);
    Assert.Equal(
      "abstract/horizontalorientation",
      (string?)group["loadFromProperties"]
    );
  }

  [Fact]
  public void ShapeByType_emits_base_plus_only_the_set_rotations() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .ShapeByType("*-ns-*", "iiex:pipes/straight")
      .ShapeByType("*-we-*", "iiex:pipes/straight", rotateY: 90)
      .ShapeByType("*-ud-*", "iiex:pipes/straight", rotateX: 90)
      .ToJson();

    var byType = (JObject)json["shapebytype"]!;
    Assert.Equal("iiex:pipes/straight", (string?)byType["*-ns-*"]!["base"]);
    Assert.Null(byType["*-ns-*"]!["rotateY"]); // unset rotations are omitted
    Assert.Equal(90, (int)byType["*-we-*"]!["rotateY"]!);
    Assert.Null(byType["*-we-*"]!["rotateX"]);
    Assert.Equal(90, (int)byType["*-ud-*"]!["rotateX"]!);
  }

  [Fact]
  public void TextureByType_maps_a_wildcard_to_a_texture_key_base() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .TextureByType("*-iron", "iron4", "game:block/metal/sheet-plain/iron4")
      .TextureByType("*-steel", "iron4", "game:block/metal/sheet-plain/steel4")
      .ToJson();

    Assert.Equal(
      "game:block/metal/sheet-plain/iron4",
      (string?)json["texturesByType"]!["*-iron"]!["iron4"]!["base"]
    );
    Assert.Equal(
      "game:block/metal/sheet-plain/steel4",
      (string?)json["texturesByType"]!["*-steel"]!["iron4"]!["base"]
    );
  }

  [Fact]
  public void Behavior_by_name_and_by_type_append_name_entries() {
    JObject json = ExBlockDef
      .Create("iiex", "c")
      .Behavior("Lockable")
      .Behavior<FakeBehavior>()
      .ToJson();

    var behaviors = (JArray)json["behaviors"]!;
    Assert.Equal("Lockable", (string?)behaviors[0]!["name"]);
    // Typed overload resolves the registered {modid}.{ClassName} key, same as the class binding.
    Assert.Equal("iiex.FakeBehavior", (string?)behaviors[1]!["name"]);
  }

  [Fact]
  public void Behavior_with_properties_appends_a_name_and_properties_entry() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .Behavior("GroundStorable", new { layout = "SingleCenter" })
      .ToJson();

    var behavior = (JObject)((JArray)json["behaviors"]!)[0]!;
    Assert.Equal("GroundStorable", (string?)behavior["name"]);
    Assert.Equal("SingleCenter", (string?)behavior["properties"]!["layout"]);
  }

  [Fact]
  public void VariantGroupFromProperties_codeless_omits_the_code_key() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .VariantGroupFromProperties("game:abstract/horizontalorientation")
      .ToJson();

    var group = (JObject)((JArray)json["variantgroups"]!)[0]!;
    Assert.Null(group["code"]);
    Assert.Equal(
      "game:abstract/horizontalorientation",
      (string?)group["loadFromProperties"]
    );
  }

  [Fact]
  public void SoundByType_accumulates_a_typed_byType_map_under_sounds() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .SoundByType("break", "*-snow", "game:block/snow")
      .SoundByType("break", "*-free", "game:block/gravel")
      .ToJson();

    var breakByType = (JObject)json["sounds"]!["breakByType"]!;
    Assert.Equal("game:block/snow", (string?)breakByType["*-snow"]);
    Assert.Equal("game:block/gravel", (string?)breakByType["*-free"]);
  }

  [Fact]
  public void Drop_appends_entries_with_an_optional_quantity() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .Drop("block", "slagpath-free")
      .Drop("item", "game:rod-iron", quantity: 4)
      .ToJson();

    var drops = (JArray)json["drops"]!;
    Assert.Equal("slagpath-free", (string?)drops[0]!["code"]);
    Assert.Null(drops[0]!["quantity"]);
    Assert.Equal(4, (int)drops[1]!["quantity"]!);
  }

  [Fact]
  public void SideSolid_from_a_poco_sets_per_face_flags() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .SideSolid(new { all = true, up = false })
      .ToJson();

    Assert.True((bool)json["sidesolid"]!["all"]!);
    Assert.False((bool)json["sidesolid"]!["up"]!);
  }

  [Fact]
  public void WalkSpeedMultiplier_emits_a_double_matching_the_parsed_json_value() {
    // 1.3 has no exact float representation; the double path must equal JSON-parsed 1.3 (a float would not).
    JObject json = ExBlockDef
      .Create("d", "c")
      .WalkSpeedMultiplier(1.3)
      .ToJson();
    Assert.Equal(JToken.Parse("1.3"), json["walkspeedmultiplier"]);
  }

  [Fact]
  public void HandbookExclude_sets_the_nested_handbook_exclude_flag() {
    JObject json = ExBlockDef.Create("d", "c").HandbookExclude().ToJson();
    Assert.True((bool)json["attributes"]!["handbook"]!["exclude"]!);
    Assert.Null(json["handbook"]); // the handbook system reads attributes.handbook, not a top-level key
  }

  [Fact]
  public void DrawType_sets_the_drawtype_key() {
    JObject json = ExBlockDef.Create("d", "c").DrawType("json").ToJson();
    Assert.Equal("json", (string?)json["drawtype"]);
  }

  [Fact]
  public void FillerOffsets_emits_per_cell_hosted_behaviors() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .FillerOffsets([
        new FillerCellSpec(-1, 0, 0),
        new FillerCellSpec(
          0,
          1,
          0,
          AllowAttach: true,
          Behaviors:
          [
            new FillerBehaviorSpec("exlib.BEBehaviorMPFillerPort", "west"),
          ]
        ),
      ])
      .ToJson();

    var cells = (JArray)json["attributes"]!["fillerOffsets"]!;
    Assert.Null(cells[0]!["behaviors"]); // a plain cell emits no behaviors key
    var port = (JObject)((JArray)cells[1]!["behaviors"]!)[0]!;
    Assert.Equal("exlib.BEBehaviorMPFillerPort", (string?)port["code"]);
    Assert.Equal("west", (string?)port["face"]);
    Assert.True((bool)cells[1]!["allowAttach"]!);
  }

  [Fact]
  public void EntityBehavior_by_name_and_by_type_append_entityBehaviors_entries() {
    JObject json = ExBlockDef
      .Create("iiex", "c")
      .EntityBehavior("Animatable")
      .EntityBehavior<FakeEntityBehavior>()
      .ToJson();

    var behaviors = (JArray)json["entityBehaviors"]!;
    Assert.Equal("Animatable", (string?)behaviors[0]!["name"]);
    Assert.Equal("iiex.FakeEntityBehavior", (string?)behaviors[1]!["name"]);
  }

  [Fact]
  public void SoundByTool_nests_a_per_tool_hit_and_break_override() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .Sound("place", "game:block/ceramicplace")
      .SoundByTool(
        EnumTool.Pickaxe,
        "game:block/rock-hit-pickaxe",
        "game:block/rock-break-pickaxe"
      )
      .ToJson();

    Assert.Equal("game:block/ceramicplace", (string?)json["sounds"]!["place"]);
    var pick = json["sounds"]!["byTool"]!["Pickaxe"]!;
    Assert.Equal("game:block/rock-hit-pickaxe", (string?)pick["hit"]);
    Assert.Equal("game:block/rock-break-pickaxe", (string?)pick["break"]);
  }

  [Fact]
  public void TextureByType_emits_overlays_when_supplied() {
    JObject json = ExBlockDef
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
  public void Collision_and_selection_boxes_emit_cuboid_arrays() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .ToJson();

    var box = (JObject)((JArray)json["collisionboxes"]!)[0]!;
    Assert.Equal(0.3125f, (float)box["x1"]!);
    Assert.Equal(0f, (float)box["z1"]!);
    Assert.Equal(1f, (float)box["z2"]!);
    Assert.Single((JArray)json["selectionboxes"]!);
  }

  [Fact]
  public void Handbook_sets_the_attributes_handbook_groupBy() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .Handbook("pipe-straight-*", "pipe-bend-*")
      .ToJson();
    Assert.Equal(
      ["pipe-straight-*", "pipe-bend-*"],
      json["attributes"]!["handbook"]!["groupBy"]!.ToObject<string[]>()!
    );
  }

  [Fact]
  public void VariantStates_reads_a_groups_explicit_states_back() {
    ExBlockDef def = ExBlockDef
      .Create("d", "c")
      .VariantGroup("type", "straight")
      .VariantGroup("orientation", "ns", "we", "ud");

    Assert.Equal(["straight"], def.VariantStates("type"));
    Assert.Equal(["ns", "we", "ud"], def.VariantStates("orientation"));
    Assert.Empty(def.VariantStates("material")); // absent group -> empty
  }

  [Fact]
  public void VariantStates_is_empty_for_a_worldproperty_sourced_group() {
    ExBlockDef def = ExBlockDef
      .Create("d", "c")
      .VariantGroupFromProperties("side", "abstract/horizontalorientation");
    Assert.Empty(def.VariantStates("side"));
  }

  [Fact]
  public void Render_and_side_flags_emit_the_expected_keys() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(false)
      .SideOpaque(false)
      .ToJson();

    Assert.Equal("OpaqueNoCull", (string?)json["renderpass"]);
    Assert.Equal("NeverCull", (string?)json["faceCullMode"]);
    Assert.Equal(0, (int)json["lightAbsorption"]!);
    Assert.False((bool)json["sidesolid"]!["all"]!);
    Assert.False((bool)json["sideopaque"]!["all"]!);
  }

  [Fact]
  public void RenderPass_enum_overload_matches_the_string_overload() {
    JObject fromEnum = ExBlockDef
      .Create("d", "c")
      .RenderPass(EnumChunkRenderPass.OpaqueNoCull)
      .ToJson();
    JObject fromString = ExBlockDef
      .Create("d", "c")
      .RenderPass("OpaqueNoCull")
      .ToJson();
    Assert.True(JToken.DeepEquals(fromEnum, fromString));
  }

  [Fact]
  public void FaceCullMode_enum_overload_matches_the_string_overload() {
    JObject fromEnum = ExBlockDef
      .Create("d", "c")
      .FaceCullMode(EnumFaceCullMode.NeverCull)
      .ToJson();
    JObject fromString = ExBlockDef
      .Create("d", "c")
      .FaceCullMode("NeverCull")
      .ToJson();
    Assert.True(JToken.DeepEquals(fromEnum, fromString));
  }

  [Fact]
  public void DrawType_enum_overload_matches_the_string_overload() {
    JObject fromEnum = ExBlockDef
      .Create("d", "c")
      .DrawType(EnumDrawType.JSON)
      .ToJson();
    JObject fromString = ExBlockDef.Create("d", "c").DrawType("JSON").ToJson();
    Assert.True(JToken.DeepEquals(fromEnum, fromString));
  }

  #endregion
}
