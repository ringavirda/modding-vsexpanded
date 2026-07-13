using System.Collections.Generic;
using ExpandedLib.Metals;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The two-pass populate that fills <see cref="MetalRegistry"/> at <c>AssetsFinalize</c>, and the
/// <see cref="MetalDef"/> JSON binding a content mod ships. The asset reads themselves need a running
/// game (verified in-game); the load-bearing logic - baseline derivation, overlay precedence, and the
/// camelCase→POCO mapping - is pinned here against the asset-free <see cref="MetalCatalogueLoader.Populate"/>.
/// </summary>
[Collection("MetalRegistry")] // shares the process-wide MetalRegistry with MetalRegistryTests
public class MetalCatalogueLoaderTests
{
  public MetalCatalogueLoaderTests() => MetalRegistry.Clear();

  #region Pass 1 - baseline derivation
  [Fact]
  public void Baseline_registers_a_game_ingot_entry_per_metal_code()
  {
    MetalCatalogueLoader.Populate(new[] { "iron", "copper" }, NoOverlays);

    Assert.True(MetalRegistry.TryGet("game:ingot-iron", out var iron));
    Assert.Equal("iron", iron.Code);
    Assert.True(MetalRegistry.TryGet("game:ingot-copper", out _));
    Assert.Equal(2, MetalRegistry.All.Count);
  }

  [Fact]
  public void Baseline_derives_a_metal_listed_twice_only_once()
  {
    MetalCatalogueLoader.Populate(new[] { "iron", "iron" }, NoOverlays);
    Assert.Single(MetalRegistry.All);
  }

  [Fact]
  public void Baseline_strips_a_domain_qualified_worldproperty_code()
  {
    MetalCatalogueLoader.Populate(new[] { "game:copper" }, NoOverlays);

    // "game:copper" → short code "copper" → molten item game:ingot-copper.
    Assert.True(MetalRegistry.TryGet("game:ingot-copper", out var def));
    Assert.Equal("copper", def.Code);
  }
  #endregion

  #region Pass 2 - overlay precedence
  [Fact]
  public void Overlay_enriches_the_baseline_entry_for_the_same_metal()
  {
    var iron = new AssetLocation("game:ingot-iron");
    // Convention glow floor before the overlay...
    MetalCatalogueLoader.Populate(new[] { "iron" }, NoOverlays);
    Assert.Equal(ExpandedLib.ExlibValues.MetalGlowMinTemp, MetalRegistry.GlowMinTempOf(iron));

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
  public void Overlay_adds_a_metal_absent_from_the_baseline()
  {
    MetalCatalogueLoader.Populate(
      NoCodes,
      new[] { new MetalDef { Code = "slag", MoltenItem = "iwex:slag" } }
    );

    Assert.True(MetalRegistry.TryGet("iwex:slag", out _));
    Assert.Equal("iwex:slag", MetalRegistry.MoltenItemOf("slag").ToString());
  }

  [Fact]
  public void Overlay_missing_a_required_field_is_skipped_and_warned()
  {
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
  public void MetalDef_binds_camelCase_json_including_nested_alloy()
  {
    const string json =
      @"{
        ""code"": ""hadfield"",
        ""moltenItem"": ""iwex:ingot-hadfield"",
        ""isAlloy"": true,
        ""glowMinTemp"": 520,
        ""media"": [""molten""],
        ""alloy"": { ""ingredients"": [
          { ""metal"": ""iron"", ""min"": 0.86, ""max"": 0.92 },
          { ""metal"": ""manganese"", ""min"": 0.08, ""max"": 0.14 } ] }
      }";

    var def = JsonConvert.DeserializeObject<MetalDef>(json)!;

    Assert.Equal("hadfield", def.Code);
    Assert.Equal("iwex:ingot-hadfield", def.MoltenItem);
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
  public void MetalDef_minimal_json_leaves_every_optional_null()
  {
    var def = JsonConvert.DeserializeObject<MetalDef>(
      @"{ ""code"": ""pigiron"", ""moltenItem"": ""iwex:ingot-pigiron"" }"
    )!;

    Assert.Equal("pigiron", def.Code);
    Assert.Equal("iwex:ingot-pigiron", def.MoltenItem);
    Assert.Null(def.SolidDrop);
    Assert.Null(def.GlowMinTemp);
    Assert.Null(def.Media);
    Assert.Null(def.Alloy);
    Assert.False(def.IsAlloy);
  }
  #endregion

  private static readonly string[] NoCodes = System.Array.Empty<string>();
  private static readonly MetalDef[] NoOverlays = System.Array.Empty<MetalDef>();
}
