using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LowPressureExpanded.BlockStructures.Boiler.Blocks;
using LowPressureExpanded.BlockStructures.Engine.Blocks;
using SteelmakingExpanded.BlockStructures.Converter.Blocks;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Regression guard for the RCC mega-block break/mining JSON, which the headless harness can't load
/// (blocks are configured by hand, not from assets). Reads the shipped block JSON directly and pins:
/// the Bessemer converter and the boiler must NOT drop themselves (control-spawned / built in place,
/// not a placeable frame) and must scatter 80% of their construction cost; the craftable engine keeps
/// its frame-recovery self-drop; and the pickaxe tiers (converter = iron; Watt engine + Cornish
/// boiler = bronze).
/// <para>
/// The two high-pressure machines this guard used to cover (Lancashire boiler, Cornish engine) moved
/// to hpex; smex must not reference hpex, so their cases live in
/// <c>HighPressureExpanded.Tests.HpMegablockDropTierTests</c>.
/// </para>
/// </summary>
public class MegablockDropTierTests
{
  private const string Bessemer =
    "src/SteelmakingExpanded/assets/smex/blocktypes/converter/bessemer.json";
  private const string Watt =
    "src/LowPressureExpanded/assets/lpex/blocktypes/engine/watt.json";
  private const string BoilerCornish =
    "src/LowPressureExpanded/assets/lpex/blocktypes/boiler/cornish.json";

  // Pickaxe tooltier in this game version: bronzes = 3, iron = 4, steel = 5.
  private const int BronzeTier = 3;
  private const int IronTier = 4;

  #region Converter (control-spawned: no self-drop)

  [Fact]
  public void Bessemer_converter_does_not_drop_itself_as_a_block()
  {
    // An explicit empty "drops" array suppresses the auto-populated self-drop. Without it the
    // registry hands the block its own code as a drop - the reported bug.
    JsonElement block = Block(Bessemer);
    Assert.True(
      block.TryGetProperty("drops", out JsonElement drops),
      "bessemer.json must declare \"drops\": [] to suppress the self-drop"
    );
    Assert.Equal(JsonValueKind.Array, drops.ValueKind);
    Assert.Equal(0, drops.GetArrayLength());
  }

  [Fact]
  public void Bessemer_converter_salvage_ratio_defaults_to_80_percent()
  {
    // The salvage fraction moved off the block JSON to the player-tunable config (smex
    // RccBrokenDropsRatio); the behaviour reads it live via ExRccSettings, so the JSON no longer
    // carries brokenDropsRatio and the default lives on the config.
    Assert.Equal(0.8f, new SmexConfig().RccBrokenDropsRatio, 3);
    Assert.False(
      Constructable(Block(Bessemer)).TryGetProperty("brokenDropsRatio", out _),
      "brokenDropsRatio must no longer live in the block JSON (moved to config)"
    );
  }

  [Fact]
  public void Bessemer_converter_needs_an_iron_tier_pickaxe()
  {
    Assert.Equal(IronTier, MiningTier(Block(Bessemer)));
  }

  #endregion

  #region Boilers (built in place: no self-drop)

  [Theory]
  [InlineData(BoilerCornish)]
  public void Boilers_do_not_drop_themselves_as_a_block(string path)
  {
    // Like the converter, a boiler is built in place (RightClickConstructable), not placed from a
    // frame item, so it must declare "drops": [] to suppress the auto-populated self-drop.
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
  public void Engines_still_drop_their_craftable_frame(string path)
  {
    // Engines ARE placeable, craftable frames - breaking one should recover the frame block, so they
    // must NOT carry the converter/boiler empty-drops override.
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

  // Bronze tier: the Watt engine and the iron Cornish boiler. (The steel Lancashire boiler is welded
  // steel like the converter and takes an iron pickaxe - pinned in the hpex suite.)
  [Theory]
  [InlineData(Watt)]
  [InlineData(BoilerCornish)]
  public void Engines_and_the_cornish_boiler_need_a_bronze_tier_pickaxe(
    string path
  )
  {
    Assert.Equal(BronzeTier, MiningTier(Block(path)));
  }

  [Fact]
  public void Engine_and_boiler_salvage_ratio_defaults_to_80_percent()
  {
    // The salvage fraction moved off the block JSON to the player-tunable config (lpex
    // RccBrokenDropsRatio), shared by every lpex engine/boiler and read live via ExRccSettings.
    Assert.Equal(
      0.8f,
      new LowPressureExpanded.LpexConfig().RccBrokenDropsRatio,
      3
    );
  }

  [Theory]
  [InlineData(Watt)]
  [InlineData(BoilerCornish)]
  public void Engines_and_boilers_no_longer_carry_a_json_drop_ratio(string path)
  {
    Assert.False(
      Constructable(Block(path)).TryGetProperty("brokenDropsRatio", out _),
      $"{path} brokenDropsRatio must move to config (lpex RccBrokenDropsRatio)"
    );
  }

  #endregion

  #region Asset loading

  private static int MiningTier(JsonElement block) =>
    block.GetProperty("requiredMiningTier").GetInt32();

  // The ExRightClickConstructable entity behavior's properties node (holds brokenDropsRatio + stages).
  private static JsonElement Constructable(JsonElement block)
  {
    foreach (
      JsonElement b in block.GetProperty("entityBehaviors").EnumerateArray()
    )
    {
      if (
        b.TryGetProperty("name", out JsonElement name)
        && name.GetString() == "ExRightClickConstructable"
      )
        return b.GetProperty("properties");
    }
    throw new Xunit.Sdk.XunitException(
      "block has no ExRightClickConstructable behavior"
    );
  }

  private static JsonElement Block(string repoRelativePath)
  {
    using var doc = JsonDocument.Parse(
      BlockJson(repoRelativePath),
      new JsonDocumentOptions
      {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
      }
    );
    return doc.RootElement.Clone();
  }

  // Every mega-block in this guard is now code-first (no shipped JSON), so read its authored def - the single
  // source of truth - instead of a file. Reading the def here keeps this guard honest through the migration: it
  // pins the same drop/tier/cost the game will load.
  private static string BlockJson(string repoRelativePath) =>
    repoRelativePath switch
    {
      BoilerCornish =>
        BlockBoilerCornish.Definitions("lpex").Single().ToJson().ToString(),
      Watt => BlockEngineWatt.Definitions("lpex").Single().ToJson().ToString(),
      Bessemer =>
        BlockConverterBessemer.Definitions("smex").Single().ToJson().ToString(),
      _ => ReadAssetFile(repoRelativePath),
    };

  private static string ReadAssetFile(string repoRelativePath)
  {
    string full = Path.Combine(
      RepoRoot(),
      repoRelativePath.Replace('/', Path.DirectorySeparatorChar)
    );
    Assert.True(File.Exists(full), $"missing asset: {full}");
    return File.ReadAllText(full);
  }

  private static string RepoRoot()
  {
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
