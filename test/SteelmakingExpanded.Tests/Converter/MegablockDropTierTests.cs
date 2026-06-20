using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Regression guard for the RCC mega-block break/mining JSON, which the headless harness can't load
/// (blocks are configured by hand, not from assets). Reads the shipped block JSON directly and pins:
/// the Bessemer converter and the boilers must NOT drop themselves (control-spawned / built in place,
/// not a placeable frame) and must scatter 80% of their construction cost; the craftable engines keep
/// their frame-recovery self-drop; and the pickaxe tiers (converter + steel Lancashire boiler = iron;
/// engines + iron Cornish boiler = bronze).
/// </summary>
public class MegablockDropTierTests
{
  private const string Bessemer =
    "src/SteelmakingExpanded/assets/smex/blocktypes/converter/bessemer.json";
  private const string Watt =
    "src/PipesAndPowerExpanded/assets/ppex/blocktypes/engine/watt.json";
  private const string EngineCornish =
    "src/PipesAndPowerExpanded/assets/ppex/blocktypes/engine/cornish.json";
  private const string Lancashire =
    "src/PipesAndPowerExpanded/assets/ppex/blocktypes/boiler/lancashire.json";
  private const string BoilerCornish =
    "src/PipesAndPowerExpanded/assets/ppex/blocktypes/boiler/cornish.json";

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
  [InlineData(Lancashire)]
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
  [InlineData(EngineCornish)]
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

  #region Construction cost (salvage base)

  // The break salvage scatters brokenDropsRatio (80%) of the consumed stacks across EVERY completed
  // stage. Vanilla rcc.GetDrops omits the LAST stage (a `i < CurrentCompletedStage` off-by-one), which
  // robbed the salvage of the most expensive stage - the Lancashire casing - so a fully built boiler
  // refunded ~40% instead of 80%. ExRightClickConstructable now includes the final stage. This pins
  // the full per-material construction cost the 80% is taken from, so a stage edit can't silently shift
  // it again. (The 1.22 drop code is vanilla-backed and the legacy reimpl is excluded from this target,
  // so the totals are asserted off the shipped JSON.)
  [Fact]
  public void Lancashire_boiler_full_construction_cost_is_pinned()
  {
    Dictionary<string, int> cost = ConstructionCost(Block(Lancashire));

    Assert.Equal(34, cost["metalplate-steel"]); // 10 + 8 + 16
    Assert.Equal(24, cost["metalnailsandstrips-*"]); // 8 + 8 + 8
    Assert.Equal(10, cost["rod-steel"]); // 4 + 6
    Assert.Equal(60, cost["game:burnedbrick-fire"]); // 12 + 48
  }

  // Sums every stage's requireStacks quantity by ingredient code (the full build cost).
  private static Dictionary<string, int> ConstructionCost(JsonElement block)
  {
    var totals = new Dictionary<string, int>();
    foreach (
      JsonElement stage in Constructable(block)
        .GetProperty("stages")
        .EnumerateArray()
    )
    {
      if (!stage.TryGetProperty("requireStacks", out JsonElement stacks))
        continue;
      foreach (JsonElement ing in stacks.EnumerateArray())
      {
        string code = ing.GetProperty("code").GetString()!;
        totals[code] =
          totals.GetValueOrDefault(code)
          + ing.GetProperty("quantity").GetInt32();
      }
    }
    return totals;
  }

  #endregion

  #region Engines + boilers (shared: mining tier, 80% salvage)

  // Bronze tier: both engines and the iron Cornish boiler. The steel Lancashire boiler is welded steel
  // like the converter, so it takes an iron pickaxe - pinned separately.
  [Theory]
  [InlineData(Watt)]
  [InlineData(EngineCornish)]
  [InlineData(BoilerCornish)]
  public void Engines_and_the_cornish_boiler_need_a_bronze_tier_pickaxe(
    string path
  )
  {
    Assert.Equal(BronzeTier, MiningTier(Block(path)));
  }

  [Fact]
  public void Lancashire_boiler_needs_an_iron_tier_pickaxe()
  {
    Assert.Equal(IronTier, MiningTier(Block(Lancashire)));
  }

  [Fact]
  public void Engine_and_boiler_salvage_ratio_defaults_to_80_percent()
  {
    // The salvage fraction moved off the block JSON to the player-tunable config (ppex
    // RccBrokenDropsRatio), shared by every ppex engine/boiler and read live via ExRccSettings.
    Assert.Equal(
      0.8f,
      new PipesAndPowerExpanded.PpexConfig().RccBrokenDropsRatio,
      3
    );
  }

  [Theory]
  [InlineData(Watt)]
  [InlineData(EngineCornish)]
  [InlineData(Lancashire)]
  [InlineData(BoilerCornish)]
  public void Engines_and_boilers_no_longer_carry_a_json_drop_ratio(string path)
  {
    Assert.False(
      Constructable(Block(path)).TryGetProperty("brokenDropsRatio", out _),
      $"{path} brokenDropsRatio must move to config (ppex RccBrokenDropsRatio)"
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
    string full = Path.Combine(
      RepoRoot(),
      repoRelativePath.Replace('/', Path.DirectorySeparatorChar)
    );
    Assert.True(File.Exists(full), $"missing asset: {full}");
    using var doc = JsonDocument.Parse(
      File.ReadAllText(full),
      new JsonDocumentOptions
      {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
      }
    );
    return doc.RootElement.Clone();
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
