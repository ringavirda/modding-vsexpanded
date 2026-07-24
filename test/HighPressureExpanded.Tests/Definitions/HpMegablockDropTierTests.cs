using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HighPressureExpanded.BlockStructures.Boiler.Blocks;
using HighPressureExpanded.BlockStructures.Engine.Blocks;
using Xunit;

namespace HighPressureExpanded.Tests;

/// <summary>
/// Regression guard for the two HP mega-blocks' break/mining/construction JSON, which the headless
/// harness can't load (blocks are configured by hand, not from assets). Reads each block's authored
/// code-first def - the single source of truth - and pins: the Lancashire boiler must NOT drop itself
/// (built in place, not a placeable frame); the Cornish engine keeps its craftable-frame self-drop;
/// the pickaxe tiers (steel Lancashire = iron, engine = bronze); and the Lancashire's full construction
/// cost, which the 80% salvage is taken from.
/// <para>
/// Split out of <c>SteelmakingExpanded.Tests.MegablockDropTierTests</c> when the two HP machines were
/// extracted into hpex - smex must not gain an hpex reference (there is no smex → hpex edge). The LP
/// and Bessemer cases stay in that file.
/// </para>
/// </summary>
public class HpMegablockDropTierTests
{
  // Pickaxe tooltier in this game version: bronze = 3, iron = 4, steel = 5.
  private const int BronzeTier = 3;
  private const int IronTier = 4;

  private static JsonElement Lancashire() =>
    Def(BlockBoilerLancashire.Definitions("hpex").Single().ToJson().ToString());

  private static JsonElement CornishEngine() =>
    Def(BlockEngineCornish.Definitions("hpex").Single().ToJson().ToString());

  #region Lancashire boiler (built in place: no self-drop)

  [Fact]
  public void Lancashire_boiler_does_not_drop_itself_as_a_block()
  {
    // A boiler is built in place (RightClickConstructable), not placed from a frame item, so it must
    // declare "drops": [] to suppress the auto-populated self-drop.
    JsonElement block = Lancashire();
    Assert.True(
      block.TryGetProperty("drops", out JsonElement drops),
      "the Lancashire boiler must declare \"drops\": [] to suppress the self-drop"
    );
    Assert.Equal(JsonValueKind.Array, drops.ValueKind);
    Assert.Equal(0, drops.GetArrayLength());
  }

  [Fact]
  public void Lancashire_boiler_needs_an_iron_tier_pickaxe()
  {
    // Welded steel like the Bessemer converter, unlike the bronze-tier iron machines.
    Assert.Equal(IronTier, MiningTier(Lancashire()));
  }

  #endregion

  #region Cornish engine (craftable frame: self-drop is correct)

  [Fact]
  public void Cornish_engine_still_drops_its_craftable_frame()
  {
    // Engines ARE placeable, craftable frames - breaking one should recover the frame block, so they
    // must NOT carry the boiler's empty-drops override.
    JsonElement block = CornishEngine();
    bool suppressesSelfDrop =
      block.TryGetProperty("drops", out JsonElement drops)
      && drops.ValueKind == JsonValueKind.Array
      && drops.GetArrayLength() == 0;
    Assert.False(
      suppressesSelfDrop,
      "the Cornish engine should keep its frame self-drop (no empty \"drops\")"
    );
  }

  [Fact]
  public void Cornish_engine_needs_a_bronze_tier_pickaxe()
  {
    Assert.Equal(BronzeTier, MiningTier(CornishEngine()));
  }

  #endregion

  #region Construction cost + salvage

  // The break salvage scatters brokenDropsRatio (80%) of the consumed stacks across EVERY completed
  // stage. Vanilla rcc.GetDrops omits the LAST stage (a `i < CurrentCompletedStage` off-by-one), which
  // robbed the salvage of the most expensive stage - the Lancashire casing - so a fully built boiler
  // refunded ~40% instead of 80%. ExRightClickConstructable now includes the final stage. This pins
  // the full per-material construction cost the 80% is taken from, so a stage edit can't silently
  // shift it again.
  [Fact]
  public void Lancashire_boiler_full_construction_cost_is_pinned()
  {
    Dictionary<string, int> cost = ConstructionCost(Lancashire());

    Assert.Equal(34, cost["metalplate-steel"]); // 10 + 8 + 16
    Assert.Equal(24, cost["metalnailsandstrips-*"]); // 8 + 8 + 8
    Assert.Equal(10, cost["rod-steel"]); // 4 + 6
    Assert.Equal(60, cost["game:burnedbrick-fire"]); // 12 + 48
  }

  [Fact]
  public void Hp_machine_salvage_ratio_defaults_to_80_percent()
  {
    // The salvage fraction lives on the player-tunable config, read live via ExRccSettings keyed by
    // the broken block's Code.Domain - so hpex carries its own copy rather than inheriting lpex's.
    Assert.Equal(0.8f, new HpexConfig().RccBrokenDropsRatio, 3);
  }

  [Fact]
  public void Hp_machines_no_longer_carry_a_json_drop_ratio()
  {
    foreach (JsonElement block in new[] { Lancashire(), CornishEngine() })
      Assert.False(
        Constructable(block).TryGetProperty("brokenDropsRatio", out _),
        "brokenDropsRatio must move to config (hpex RccBrokenDropsRatio)"
      );
  }

  #endregion

  #region Def reading

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

  private static JsonElement Def(string json)
  {
    using var doc = JsonDocument.Parse(
      json,
      new JsonDocumentOptions
      {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
      }
    );
    return doc.RootElement.Clone();
  }

  #endregion
}
