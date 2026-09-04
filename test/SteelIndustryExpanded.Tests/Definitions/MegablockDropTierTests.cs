using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Boiler.Blocks;
using IronIndustryExpanded.BlockStructures.Engine.Blocks;
using SteelIndustryExpanded.BlockStructures.Converter.Blocks;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Pins the RCC mega-block break/mining definitions, which the headless harness does not load (blocks
/// are configured by hand, not from assets). The Bessemer converter and the boiler must not drop
/// themselves (control-spawned or built in place, not a placeable frame) and must scatter 80% of their
/// construction cost; the craftable engine keeps its frame-recovery self-drop; mining tiers are iron for
/// the converter and bronze for the Watt engine and Cornish boiler. The high-pressure machines
/// (Lancashire boiler, Cornish engine) are covered by
/// <c>SteelIndustryExpanded.Tests.HpMegablockDropTierTests</c>, kept separate because they pin a
/// different set of blocks.
/// </summary>
public class MegablockDropTierTests {
  private const string Bessemer =
    "src/SteelIndustryExpanded/assets/siex/blocktypes/converter/bessemer.json";
  private const string Watt =
    "src/IronIndustryExpanded/assets/iiex/blocktypes/engine/watt.json";
  private const string BoilerCornish =
    "src/IronIndustryExpanded/assets/iiex/blocktypes/boiler/cornish.json";

  #region Converter (control-spawned: no self-drop)

  [Fact]
  public void Bessemer_converter_does_not_drop_itself_as_a_block() {
    // An explicit empty "drops" array suppresses the auto-populated self-drop; without it the registry
    // hands the block its own code as a drop.
    JsonElement block = Block(Bessemer);
    Assert.True(
      block.TryGetProperty("drops", out JsonElement drops),
      "bessemer.json must declare \"drops\": [] to suppress the self-drop"
    );
    Assert.Equal(JsonValueKind.Array, drops.ValueKind);
    Assert.Equal(0, drops.GetArrayLength());
  }

  [Fact]
  public void Bessemer_converter_salvage_ratio_defaults_to_80_percent() {
    // The salvage fraction lives on the player-tunable config (siex RccBrokenDropsRatio) and the
    // behaviour reads it live via ExRccSettings, so the block JSON carries no brokenDropsRatio.
    Assert.Equal(0.8f, new SiexConfig().RccBrokenDropsRatio, 3);
    Assert.False(
      DefinitionJson
        .Constructable(Block(Bessemer))
        .TryGetProperty("brokenDropsRatio", out _),
      "brokenDropsRatio must no longer live in the block JSON (moved to config)"
    );
  }

  [Fact]
  public void Bessemer_converter_needs_an_iron_tier_pickaxe() {
    Assert.Equal(
      VanillaToolTiers.Iron,
      DefinitionJson.MiningTier(Block(Bessemer))
    );
  }

  #endregion

  #region Boilers (built in place: no self-drop)

  [Theory]
  [InlineData(BoilerCornish)]
  public void Boilers_do_not_drop_themselves_as_a_block(string path) {
    // A boiler is built in place (RightClickConstructable), not placed from a frame item, so like the
    // converter it must declare "drops": [] to suppress the auto-populated self-drop.
    JsonElement block = Block(path);
    Assert.True(
      block.TryGetProperty("drops", out JsonElement drops),
      $"{path} must declare \"drops\": [] to suppress the self-drop"
    );
    Assert.Equal(JsonValueKind.Array, drops.ValueKind);
    Assert.Equal(0, drops.GetArrayLength());
  }

  #endregion

  #region Engines (craftable frames: self-drop is correct)

  [Theory]
  [InlineData(Watt)]
  public void Engines_still_drop_their_craftable_frame(string path) {
    // Engines are placeable, craftable frames: breaking one recovers the frame block, so they must not
    // carry the converter/boiler empty-drops override.
    JsonElement block = Block(path);
    bool suppressesSelfDrop =
      block.TryGetProperty("drops", out JsonElement drops)
      && drops.ValueKind == JsonValueKind.Array
      && drops.GetArrayLength() == 0;
    Assert.False(
      suppressesSelfDrop,
      $"{path} should keep its frame self-drop (no empty \"drops\")"
    );
  }

  #endregion

  #region Engines + boilers (shared: mining tier, 80% salvage)

  // Bronze tier: the Watt engine and the iron Cornish boiler. The welded-steel Lancashire boiler takes
  // an iron pickaxe like the converter and is pinned in HpMegablockDropTierTests.
  [Theory]
  [InlineData(Watt)]
  [InlineData(BoilerCornish)]
  public void Engines_and_the_cornish_boiler_need_a_bronze_tier_pickaxe(
    string path
  ) {
    Assert.Equal(
      VanillaToolTiers.Bronze,
      DefinitionJson.MiningTier(Block(path))
    );
  }

  [Fact]
  public void Engine_and_boiler_salvage_ratio_defaults_to_80_percent() {
    // The salvage fraction lives on the player-tunable config (iiex RccBrokenDropsRatio), shared by
    // every iiex engine and boiler and read live via ExRccSettings.
    Assert.Equal(
      0.8f,
      new IronIndustryExpanded.IiexConfig().RccBrokenDropsRatio,
      3
    );
  }

  [Theory]
  [InlineData(Watt)]
  [InlineData(BoilerCornish)]
  public void Engines_and_boilers_no_longer_carry_a_json_drop_ratio(string path) {
    Assert.False(
      DefinitionJson
        .Constructable(Block(path))
        .TryGetProperty("brokenDropsRatio", out _),
      $"{path} brokenDropsRatio must move to config (iiex RccBrokenDropsRatio)"
    );
  }

  #endregion

  #region Asset loading

  // MiningTier / Constructable / the lenient def parse live in ExpandedLib.Testing.DefinitionJson,
  // shared with the HP drop-tier suite.
  private static JsonElement Block(string repoRelativePath) =>
    DefinitionJson.Parse(BlockJson(repoRelativePath));

  // Every mega-block in this guard is code-first (no shipped JSON), so its authored def is read instead
  // of a file: that def is the same drop/tier/cost the game loads.
  private static string BlockJson(string repoRelativePath) =>
    repoRelativePath switch {
      BoilerCornish => BlockBoilerCornish
        .Definitions("iiex")
        .Single()
        .ToJson()
        .ToString(),
      Watt => BlockEngineWatt.Definitions("iiex").Single().ToJson().ToString(),
      Bessemer => BlockConverterBessemer
        .Definitions("siex")
        .Single()
        .ToJson()
        .ToString(),
      _ => ReadAssetFile(repoRelativePath),
    };

  private static string ReadAssetFile(string repoRelativePath) {
    string full = Path.Combine(
      RepoRoot(),
      repoRelativePath.Replace('/', Path.DirectorySeparatorChar)
    );
    Assert.True(File.Exists(full), $"missing asset: {full}");
    return File.ReadAllText(full);
  }

  private static string RepoRoot() {
    DirectoryInfo? dir = new(AppContext.BaseDirectory);
    while (
      dir != null
      && !File.Exists(Path.Combine(dir.FullName, "VintageStory.sln"))
    )
      dir = dir.Parent;
    Assert.True(dir != null, "could not locate repo root (VintageStory.sln)");
    return dir!.FullName;
  }

  #endregion
}
