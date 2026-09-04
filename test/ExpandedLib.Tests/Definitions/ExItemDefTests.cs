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
}
