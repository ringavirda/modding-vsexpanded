using System.Collections.Generic;
using ExpandedLib.Metals;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The two-pass populate that fills <see cref="MetalRegistry"/> at <c>AssetsFinalize</c>, and the
/// <see cref="MetalDef"/> JSON binding a content mod ships. The asset reads themselves need a running
/// game; baseline derivation, overlay precedence and the camelCase to POCO mapping are covered against
/// the asset-free <see cref="MetalCatalogueLoader.Populate"/>.
/// </summary>
[Collection("MetalRegistry")] // shares the process-wide MetalRegistry with MetalRegistryTests
public class MetalCatalogueLoaderTests {
  public MetalCatalogueLoaderTests() => MetalRegistry.Clear();

  #region Pass 1 - baseline derivation
  [Fact]
  public void Baseline_registers_a_game_ingot_entry_per_metal_code() {
    MetalCatalogueLoader.Populate(new[] { "iron", "copper" }, NoOverlays);

    Assert.True(MetalRegistry.TryGet("game:ingot-iron", out var iron));
    Assert.Equal("iron", iron.Code);
    Assert.True(MetalRegistry.TryGet("game:ingot-copper", out _));
    Assert.Equal(2, MetalRegistry.All.Count);
  }

  [Fact]
  public void Baseline_derives_a_metal_listed_twice_only_once() {
    MetalCatalogueLoader.Populate(new[] { "iron", "iron" }, NoOverlays);
    Assert.Single(MetalRegistry.All);
  }

  [Fact]
  public void Baseline_strips_a_domain_qualified_worldproperty_code() {
    MetalCatalogueLoader.Populate(new[] { "game:copper" }, NoOverlays);

    // "game:copper" → short code "copper" → molten item game:ingot-copper.
    Assert.True(MetalRegistry.TryGet("game:ingot-copper", out var def));
    Assert.Equal("copper", def.Code);
  }
  #endregion

  #region Pass 2 - overlay precedence
  [Fact]
  public void Overlay_enriches_the_baseline_entry_for_the_same_metal() {
    var iron = new AssetLocation("game:ingot-iron");
    // Convention glow floor before the overlay...
    MetalCatalogueLoader.Populate(new[] { "iron" }, NoOverlays);
    Assert.Equal(
      ExpandedLib.ExlibValues.MetalGlowMinTemp,
      MetalRegistry.GlowMinTempOf(iron)
    );

    MetalRegistry.Clear();
    MetalCatalogueLoader.Populate(
      new[] { "iron" },
      new[]
      {
        new MetalDef
        {
          Code = "iron",
          MoltenItem = "game:ingot-iron",
          GlowMinTemp = 520f,
        },
      }
    );
    Assert.Equal(520f, MetalRegistry.GlowMinTempOf(iron)); // overlay wins
    Assert.Single(MetalRegistry.All); // replaced, not duplicated
  }

  [Fact]
  public void Overlay_adds_a_metal_absent_from_the_baseline() {
    MetalCatalogueLoader.Populate(
      NoCodes,
      new[]
      {
        new MetalDef { Code = "slag", MoltenItem = "iiex:slag" },
      }
    );

    Assert.True(MetalRegistry.TryGet("iiex:slag", out _));
    Assert.Equal("iiex:slag", MetalRegistry.MoltenItemOf("slag").ToString());
  }

  [Fact]
  public void Overlay_missing_a_required_field_is_skipped_and_warned() {
    var warnings = new List<string>();
    MetalCatalogueLoader.Populate(
      NoCodes,
      new[]
      {
        new MetalDef { Code = "", MoltenItem = "game:ingot-iron" }, // no code
        new MetalDef { Code = "tin", MoltenItem = "" }, // no molten item
        new MetalDef { Code = "iron", MoltenItem = "game:ingot-iron" }, // valid
      },
      warnings.Add
    );

    Assert.Single(MetalRegistry.All);
    Assert.True(MetalRegistry.TryGet("game:ingot-iron", out _));
    Assert.Equal(2, warnings.Count);
  }
  #endregion

  #region MetalDef JSON binding
  [Fact]
  public void MetalDef_binds_camelCase_json_including_nested_alloy() {
    const string json =
      @"{
        ""code"": ""hadfield"",
        ""moltenItem"": ""iiex:ingot-hadfield"",
        ""isAlloy"": true,
        ""glowMinTemp"": 520,
        ""media"": [""molten""],
        ""alloy"": { ""ingredients"": [
          { ""metal"": ""iron"", ""min"": 0.86, ""max"": 0.92 },
          { ""metal"": ""manganese"", ""min"": 0.08, ""max"": 0.14 } ] }
      }";

    var def = JsonConvert.DeserializeObject<MetalDef>(json)!;

    Assert.Equal("hadfield", def.Code);
    Assert.Equal("iiex:ingot-hadfield", def.MoltenItem);
    Assert.True(def.IsAlloy);
    Assert.Equal(520f, def.GlowMinTemp);
    Assert.Equal(new[] { "molten" }, def.Media);
    Assert.NotNull(def.Alloy);
    Assert.Equal(2, def.Alloy!.Ingredients.Count);
    Assert.Equal("iron", def.Alloy.Ingredients[0].Metal);
    Assert.Equal(0.86f, def.Alloy.Ingredients[0].Min);
    Assert.Equal(0.14f, def.Alloy.Ingredients[1].Max);
  }

  [Fact]
  public void MetalDef_minimal_json_leaves_every_optional_null() {
    var def = JsonConvert.DeserializeObject<MetalDef>(
      @"{ ""code"": ""pigiron"", ""moltenItem"": ""iiex:ingot-pigiron"" }"
    )!;

    Assert.Equal("pigiron", def.Code);
    Assert.Equal("iiex:ingot-pigiron", def.MoltenItem);
    Assert.Null(def.SolidDrop);
    Assert.Null(def.GlowMinTemp);
    Assert.Null(def.Media);
    Assert.Null(def.Alloy);
    Assert.False(def.IsAlloy);

    // The item-family generation fields default to "generate nothing", so a metal that ships no such
    // data is untouched by the emitter.
    Assert.False(def.GenerateItemFamily);
    Assert.Null(def.ItemForms);
    Assert.Null(def.TexturePath);
    Assert.Null(def.Density);
    Assert.Null(def.MeltingPoint);
    Assert.Null(def.Tools);
  }

  [Fact]
  public void MetalDef_binds_the_item_family_generation_fields() {
    const string json =
      @"{
        ""code"": ""castiron"",
        ""moltenItem"": ""iiex:ingot-castiron"",
        ""generateItemFamily"": true,
        ""itemForms"": [""ingot"", ""plate"", ""rod"", ""nails""],
        ""texturePath"": ""game:block/metal/tarnished/iron"",
        ""density"": 7200,
        ""meltingPoint"": 1200,
        ""tools"": {
          ""preset"": ""brittle"",
          ""durability"": 150,
          ""attackPower"": 2.5,
          ""miningTier"": 3,
          ""toolTypes"": [""pickaxe"", ""hammer""]
        }
      }";

    var def = JsonConvert.DeserializeObject<MetalDef>(json)!;

    Assert.True(def.GenerateItemFamily);
    Assert.Equal(new[] { "ingot", "plate", "rod", "nails" }, def.ItemForms);
    Assert.Equal("game:block/metal/tarnished/iron", def.TexturePath);
    Assert.Equal(7200, def.Density);
    Assert.Equal(1200, def.MeltingPoint);
    Assert.NotNull(def.Tools);
    Assert.Equal("brittle", def.Tools!.Preset);
    Assert.Equal(150, def.Tools.Durability);
    Assert.Equal(2.5f, def.Tools.AttackPower);
    Assert.Equal(3, def.Tools.MiningTier);
    Assert.Equal(new[] { "pickaxe", "hammer" }, def.Tools.ToolTypes);
  }

  [Fact]
  public void MetalToolSpec_with_only_a_preset_leaves_the_overrides_null() {
    // A preset names the stat baseline; every override stays null for the emitter to fill in.
    var def = JsonConvert.DeserializeObject<MetalDef>(
      @"{ ""code"": ""bessemersteel"", ""moltenItem"": ""smex:ingot-bessemersteel"",
          ""tools"": { ""preset"": ""good"" } }"
    )!;

    Assert.NotNull(def.Tools);
    Assert.Equal("good", def.Tools!.Preset);
    Assert.Null(def.Tools.Durability);
    Assert.Null(def.Tools.AttackPower);
    Assert.Null(def.Tools.MiningTier);
    Assert.Null(def.Tools.ToolTypes);
  }
  #endregion

  private static readonly string[] NoCodes = System.Array.Empty<string>();
  private static readonly MetalDef[] NoOverlays =
    System.Array.Empty<MetalDef>();
}
