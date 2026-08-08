using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The builder surface a code-first machine needs beyond the flat block and pipe cases: one shape spun
/// per orientation, textured overlays, single collision and selection cuboids, a behavior carrying a
/// properties blob, the typed construction-stage table, and the computed filler footprint. Each is
/// pinned to the exact JSON token shape the hand-authored blocktype emitted.
/// </summary>
public class ExBlockDefMachineTests {
  #region Shape (single, spun per orientation)

  [Fact]
  public void ShapeRotateYByType_and_selective_elements_share_one_shape_node() {
    JObject shape = (JObject)
      ExBlockDef
        .Create("d", "c")
        .Shape("iwex:ore/bunker")
        .ShapeRotateYByType("*-n", 180)
        .ShapeRotateYByType("*-e", 90)
        .ShapeSelectiveElements("Root/InputBase/*")
        .ToJson()["shape"]!;

    Assert.Equal("iwex:ore/bunker", (string?)shape["base"]);
    Assert.Equal(180, (int)shape["rotateYByType"]!["*-n"]!);
    Assert.Equal(90, (int)shape["rotateYByType"]!["*-e"]!);
    Assert.Equal(
      ["Root/InputBase/*"],
      shape["selectiveElements"]!.ToObject<string[]>()!
    );
  }

  [Fact]
  public void Shape_base_can_be_set_after_rotations_order_independently() {
    JObject shape = (JObject)
      ExBlockDef
        .Create("d", "c")
        .ShapeRotateYByType("*-s", 0)
        .Shape("iwex:ore/bunker")
        .ToJson()["shape"]!;
    Assert.Equal("iwex:ore/bunker", (string?)shape["base"]);
    Assert.Equal(0, (int)shape["rotateYByType"]!["*-s"]!);
  }

  #endregion

  #region Textures with overlays

  [Fact]
  public void Texture_emits_overlays_when_given() {
    JObject tex = (JObject)
      ExBlockDef
        .Create("d", "c")
        .Texture("fire1", "game:base", "game:overlay-{brick}")
        .ToJson()["textures"]!["fire1"]!;

    Assert.Equal("game:base", (string?)tex["base"]);
    Assert.Equal(
      ["game:overlay-{brick}"],
      tex["overlays"]!.ToObject<string[]>()!
    );
  }

  [Fact]
  public void Texture_without_overlays_emits_no_overlays_key() {
    JObject tex = (JObject)
      ExBlockDef.Create("d", "c").Texture("all", "game:base").ToJson()[
        "textures"
      ]!["all"]!;
    Assert.Null(tex["overlays"]);
  }

  #endregion

  #region Single boxes / drops

  [Fact]
  public void SingleCollisionBox_sets_the_singular_object_not_the_array() {
    JObject json = ExBlockDef
      .Create("d", "c")
      .SingleCollisionBox(0f, 0f, 0f, 1f, 1f, 1f)
      .SingleSelectionBox(0f, 0f, 0f, 1f, 1f, 1f)
      .ToJson();

    Assert.Null(json["collisionboxes"]); // not the plural array form
    Assert.Equal(0f, (float)json["collisionbox"]!["x1"]!);
    Assert.Equal(1f, (float)json["collisionbox"]!["z2"]!);
    Assert.Equal(1f, (float)json["selectionbox"]!["y2"]!);
  }

  [Fact]
  public void NoDrops_emits_an_empty_drops_array() {
    JArray drops = (JArray)
      ExBlockDef.Create("d", "c").NoDrops().ToJson()["drops"]!;
    Assert.Empty(drops);
  }

  #endregion

  #region Entity behavior with properties + construction stages

  [Fact]
  public void EntityBehavior_with_properties_nests_the_blob() {
    JObject beh = (JObject)
      Assert.Single(
        (JArray)
          ExBlockDef
            .Create("d", "c")
            .EntityBehavior("Animatable")
            .EntityBehavior("Custom", new JObject { ["k"] = 1 })
            .ToJson()["entityBehaviors"]!,
        b => (string?)b["name"] == "Custom"
      );
    Assert.Equal(1, (int)beh["properties"]!["k"]!);
  }

  [Fact]
  public void Construction_emits_the_exact_staged_material_table() {
    JArray behaviors = (JArray)
      ExBlockDef
        .Create("iwex", "bunker")
        .EntityBehavior("Animatable")
        .Construction(c =>
          c.Stage(s => s.AddElements("Root/InputBase"))
            .Stage(s =>
              s.Require(
                  "game:burnedbrick-{brick}",
                  8,
                  "iwex:rcc-ingredient-brick"
                )
                .AddElements("Root/Base")
            )
        )
        .ToJson()["entityBehaviors"]!;

    // Order matters (Animatable first), and the RCC behavior carries the typed stages.
    JObject expected = JObject.Parse(
      """
      {
        "entityBehaviors": [
          { "name": "Animatable" },
          { "name": "ExRightClickConstructable", "properties": { "stages": [
            { "addElements": ["Root/InputBase"] },
            { "requireStacks": [{ "type": "item", "code": "game:burnedbrick-{brick}", "name": "iwex:rcc-ingredient-brick", "quantity": 8 }], "addElements": ["Root/Base"] }
          ]}}
        ]
      }
      """
    );
    Assert.True(
      JToken.DeepEquals(expected["entityBehaviors"], behaviors),
      "construction table diverged:\n" + behaviors
    );
  }

  [Fact]
  public void Construction_can_set_a_broken_drops_ratio() {
    JObject props = (JObject)
      Assert.Single(
        (JArray)
          ExBlockDef
            .Create("d", "c")
            .Construction(c =>
              c.BrokenDropsRatio(0.5f).Stage(s => s.AddElements("Root"))
            )
            .ToJson()["entityBehaviors"]!
      )["properties"]!;
    Assert.Equal(0.5f, (float)props["brokenDropsRatio"]!);
  }

  #endregion

  #region Filler footprint serialization

  [Fact]
  public void FillerOffsets_serializes_cells_in_order_omitting_false_attach() {
    JArray offsets = (JArray)
      ExBlockDef
        .Create("d", "c")
        .FillerOffsets([
          new FillerCellSpec(1, 0, 0, AllowAttach: true),
          new FillerCellSpec(0, 0, 1),
        ])
        .ToJson()["attributes"]!["fillerOffsets"]!;

    JArray expected = JArray.Parse(
      """
      [
        { "x": 1, "y": 0, "z": 0, "allowAttach": true },
        { "x": 0, "y": 0, "z": 1 }
      ]
      """
    );
    Assert.True(
      JToken.DeepEquals(expected, offsets),
      "filler offsets diverged:\n" + offsets
    );
  }

  [Fact]
  public void FillerOffsets_validates_the_footprint_at_build() {
    // A duplicate cell must fail the build (the footprint is validated on serialization).
    Assert.Throws<System.ArgumentException>(() =>
      ExBlockDef
        .Create("d", "c")
        .FillerOffsets([
          new FillerCellSpec(1, 0, 0),
          new FillerCellSpec(1, 0, 0),
        ])
    );
  }

  [Fact]
  public void FillerOffsets_from_a_computed_footprint_matches_the_bunker_count() {
    JArray offsets = (JArray)
      ExBlockDef
        .Create("iwex", "bunker")
        .FillerOffsets(StructureFootprint.Rectangle(halfWidth: 1, depth: 6))
        .ToJson()["attributes"]!["fillerOffsets"]!;
    Assert.Equal(17, offsets.Count);
  }

  #endregion
}
