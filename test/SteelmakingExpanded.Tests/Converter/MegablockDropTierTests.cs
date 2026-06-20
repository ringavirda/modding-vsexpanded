using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Regression guard for the RCC mega-block break/mining JSON, which the headless harness can't load
/// (blocks are configured by hand, not from assets). Reads the shipped block JSON directly and pins:
/// the Bessemer converter must NOT drop itself (it is control-spawned, not a placeable frame) and
/// must scatter 80% of its construction cost; the craftable engines/boilers must keep their
/// frame-recovery self-drop; and the retuned pickaxe tiers (converter = iron, engines/boilers =
/// bronze).
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
  public void Bessemer_converter_scatters_80_percent_of_its_construction_cost()
  {
    JsonElement rcc = Constructable(Block(Bessemer));
    Assert.Equal(
      0.8,
      rcc.GetProperty("brokenDropsRatio").GetDouble(),
      3
    );
  }

  [Fact]
  public void Bessemer_converter_needs_an_iron_tier_pickaxe()
  {
    Assert.Equal(IronTier, MiningTier(Block(Bessemer)));
  }

  #endregion

  #region Engines + boilers (craftable frames: self-drop is correct)

  [Theory]
  [InlineData(Watt)]
  [InlineData(EngineCornish)]
  [InlineData(Lancashire)]
  [InlineData(BoilerCornish)]
  public void Engines_and_boilers_need_a_bronze_tier_pickaxe(string path)
  {
    Assert.Equal(BronzeTier, MiningTier(Block(path)));
  }

  [Theory]
  [InlineData(Watt)]
  [InlineData(EngineCornish)]
  [InlineData(Lancashire)]
  [InlineData(BoilerCornish)]
  public void Engines_and_boilers_still_drop_their_craftable_frame(string path)
  {
    // These ARE placeable, craftable frames - breaking one should recover the frame block, so they
    // must NOT carry the converter's empty-drops override.
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

  [Theory]
  [InlineData(Watt)]
  [InlineData(EngineCornish)]
  [InlineData(Lancashire)]
  [InlineData(BoilerCornish)]
  public void Engines_and_boilers_scatter_80_percent_of_construction_cost(
    string path
  )
  {
    Assert.Equal(
      0.8,
      Constructable(Block(path)).GetProperty("brokenDropsRatio").GetDouble(),
      3
    );
  }

  #endregion

  #region Asset loading

  private static int MiningTier(JsonElement block) =>
    block.GetProperty("requiredMiningTier").GetInt32();

  // The ExRightClickConstructable entity behavior's properties node (holds brokenDropsRatio + stages).
  private static JsonElement Constructable(JsonElement block)
  {
    foreach (JsonElement b in block.GetProperty("entityBehaviors").EnumerateArray())
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
