using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="ExBlockDef"/> builds the exact blocktype <see cref="JObject"/> the vanilla object loader
/// consumes. The load-bearing guarantee is PARITY: the emitted JSON must match a hand-authored
/// blocktype byte-for-byte (semantically), so an injected code-first block is indistinguishable from
/// the file it replaces. Each field's token shape is pinned, plus a full-block parity oracle against a
/// verbatim copy of a real shipped blocktype (iwex's <c>solidifiediron</c>).
/// </summary>
public class ExBlockDefTests
{
  // A verbatim copy of iwex/blocktypes/blastfurnace/solidifiediron.json - the golden the builder must
  // reproduce. Kept inline (not read from the iwex asset) so it survives that file's later deletion
  // when the block migrates to code-first.
  private const string SolidifiedIronJson =
    """
    {
      "code": "solidifiediron",
      "class": "iwex.BlockSolidifiedIron",
      "entityClass": "iwex.BlockEntitySolidifiedIron",
      "blockmaterial": "Metal",
      "creativeinventory": { "general": ["*"], "iwex": ["*"] },
      "shape": { "base": "game:block/basic/cube" },
      "textures": { "all": { "base": "game:block/metal/sheet-plain/iron5" } },
      "resistance": 45.0,
      "maxstacksize": 8,
      "requiredMiningTier": 5,
      "mineTool": "pickaxe",
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
  public void Builder_reproduces_a_real_shipped_blocktype_exactly()
  {
    ExBlockDef def = ExBlockDef
      .Create("iwex", "solidifiediron")
      .Class("iwex.BlockSolidifiedIron")
      .EntityClass("iwex.BlockEntitySolidifiedIron")
      .Material(EnumBlockMaterial.Metal)
      .CreativeTab("general", "*")
      .CreativeTab("iwex", "*")
      .Shape("game:block/basic/cube")
      .TextureAll("game:block/metal/sheet-plain/iron5")
      .Resistance(45f)
      .MaxStackSize(8)
      .MiningTier(5)
      .MineTool(EnumTool.Pickaxe)
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
  public void Class_of_T_resolves_the_registered_modid_dot_classname_key()
  {
    JObject json = ExBlockDef
      .Create("test", "x")
      .Class<CodeFirstBlock>()
      .EntityClass<CodeFirstBlockEntity>()
      .ToJson();

    Assert.Equal("test.CodeFirstBlock", (string?)json["class"]);
    Assert.Equal("test.CodeFirstBlockEntity", (string?)json["entityClass"]);
  }

  [Fact]
  public void Class_of_T_matches_KeyFor_so_a_rename_cannot_desync()
  {
    // The whole point of the typed overload: it produces the SAME string the class registry uses.
    JObject json = ExBlockDef.Create("iwex", "x").Class<CodeFirstBlock>().ToJson();
    Assert.Equal(
      EntityRegistry.KeyFor("iwex", typeof(CodeFirstBlock)),
      (string?)json["class"]
    );
  }
  #endregion

  #region Per-field token shapes
  [Fact]
  public void Code_is_set_from_Create()
  {
    Assert.Equal(
      "solidifiediron",
      (string?)ExBlockDef.Create("iwex", "solidifiediron").ToJson()["code"]
    );
  }

  [Fact]
  public void Material_emits_the_enum_name()
  {
    Assert.Equal(
      "Metal",
      (string?)
        ExBlockDef
          .Create("d", "c")
          .Material(EnumBlockMaterial.Metal)
          .ToJson()["blockmaterial"]
    );
  }

  [Fact]
  public void MineTool_is_lower_cased_to_match_the_vanilla_convention()
  {
    Assert.Equal(
      "pickaxe",
      (string?)
        ExBlockDef.Create("d", "c").MineTool(EnumTool.Pickaxe).ToJson()["mineTool"]
    );
  }

  [Fact]
  public void Shape_emits_a_base_reference_object()
  {
    JObject json = ExBlockDef.Create("d", "c").Shape("game:block/basic/cube").ToJson();
    Assert.Equal("game:block/basic/cube", (string?)json["shape"]!["base"]);
  }

  [Fact]
  public void Textures_accumulate_across_calls()
  {
    JObject json = ExBlockDef
      .Create("d", "c")
      .Texture("side", "game:a")
      .Texture("top", "game:b")
      .ToJson();
    Assert.Equal("game:a", (string?)json["textures"]!["side"]!["base"]);
    Assert.Equal("game:b", (string?)json["textures"]!["top"]!["base"]);
  }

  [Fact]
  public void CreativeTab_emits_a_selector_array_per_tab()
  {
    JObject json = ExBlockDef
      .Create("d", "c")
      .CreativeTab("general", "*-a-*", "*-b-*")
      .ToJson();
    var arr = (JArray)json["creativeinventory"]!["general"]!;
    Assert.Equal(["*-a-*", "*-b-*"], arr.ToObject<string[]>()!);
  }

  [Fact]
  public void Attribute_nests_a_poco_under_attributes()
  {
    JObject json = ExBlockDef
      .Create("d", "c")
      .Attribute("fillHeight", 0.5f)
      .Attribute("offsets", new[] { 1, 2, 3 })
      .ToJson();
    Assert.Equal(0.5f, (float)json["attributes"]!["fillHeight"]!);
    Assert.Equal([1, 2, 3], json["attributes"]!["offsets"]!.ToObject<int[]>()!);
  }

  [Fact]
  public void Raw_sets_an_arbitrary_top_level_token_as_the_escape_hatch()
  {
    JObject json = ExBlockDef
      .Create("d", "c")
      .Raw("someFutureField", new JValue(42))
      .ToJson();
    Assert.Equal(42, (int)json["someFutureField"]!);
  }
  #endregion

  #region Location
  [Fact]
  public void Location_targets_the_blocktypes_json_the_loader_filters_on()
  {
    AssetLocation loc = ExBlockDef.Create("iwex", "solidifiediron").Location;
    Assert.Equal("iwex", loc.Domain);
    Assert.Equal("blocktypes/solidifiediron.json", loc.Path);
  }
  #endregion
}
