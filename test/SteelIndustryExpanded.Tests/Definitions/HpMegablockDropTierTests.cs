using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ExpandedLib.Testing;
using SteelIndustryExpanded.BlockStructures.Boiler.Blocks;
using SteelIndustryExpanded.BlockStructures.Engine.Blocks;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Break, mining and construction JSON for the two HP mega-blocks, read from each block's authored
/// code-first def because the headless harness configures blocks by hand rather than from assets. Pins
/// that the Lancashire boiler suppresses its self-drop (it is built in place, not placed from a frame),
/// that the Cornish engine keeps its craftable-frame self-drop, the pickaxe tiers, and the Lancashire's
/// full construction cost that the salvage fraction is taken from.
/// </summary>
public class HpMegablockDropTierTests {
  private static JsonElement Lancashire() =>
    DefinitionJson.Parse(
      BlockBoilerLancashire.Definitions("siex").Single().ToJson().ToString()
    );

  private static JsonElement CornishEngine() =>
    DefinitionJson.Parse(
      BlockEngineCornish.Definitions("siex").Single().ToJson().ToString()
    );

  #region Lancashire boiler (built in place: no self-drop)

  [Fact]
  public void Lancashire_boiler_does_not_drop_itself_as_a_block() {
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
  public void Lancashire_boiler_needs_an_iron_tier_pickaxe() {
    // Welded steel, like the Bessemer converter.
    Assert.Equal(
      VanillaToolTiers.Iron,
      DefinitionJson.MiningTier(Lancashire())
    );
  }

  #endregion

  #region Cornish engine (craftable frame: self-drop is correct)

  [Fact]
  public void Cornish_engine_still_drops_its_craftable_frame() {
    // Engines are placeable craftable frames, so breaking one recovers the frame block and they must
    // not carry the boiler's empty-drops override.
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
  public void Cornish_engine_needs_a_bronze_tier_pickaxe() {
    Assert.Equal(
      VanillaToolTiers.Bronze,
      DefinitionJson.MiningTier(CornishEngine())
    );
  }

  #endregion

  #region Construction cost + salvage

  // Break salvage scatters brokenDropsRatio (80%) of the stacks consumed by every completed stage,
  // the final one included. This pins the full per-material construction cost that fraction is taken
  // from, so a stage edit cannot shift it unnoticed.
  [Fact]
  public void Lancashire_boiler_full_construction_cost_is_pinned() {
    Dictionary<string, int> cost = ConstructionCost(Lancashire());

    Assert.Equal(34, cost["metalplate-steel"]); // 10 + 8 + 16
    Assert.Equal(24, cost["metalnailsandstrips-*"]); // 8 + 8 + 8
    Assert.Equal(10, cost["rod-steel"]); // 4 + 6
    Assert.Equal(60, cost["game:burnedbrick-fire"]); // 12 + 48
  }

  [Fact]
  public void Hp_machine_salvage_ratio_defaults_to_80_percent() {
    // The salvage fraction lives on the player-tunable config, read via ExRccSettings keyed by the
    // broken block's Code.Domain, so siex carries its own copy rather than inheriting iiex's.
    Assert.Equal(0.8f, new SiexConfig().RccBrokenDropsRatio, 3);
  }

  [Fact]
  public void Hp_machines_no_longer_carry_a_json_drop_ratio() {
    foreach (JsonElement block in new[] { Lancashire(), CornishEngine() })
      Assert.False(
        DefinitionJson
          .Constructable(block)
          .TryGetProperty("brokenDropsRatio", out _),
        "brokenDropsRatio must move to config (siex RccBrokenDropsRatio)"
      );
  }

  #endregion

  #region Def reading

  // MiningTier / Constructable / the lenient def parse live in ExpandedLib.Testing.DefinitionJson,
  // shared with the steel-line drop-tier suite.

  // Sums every stage's requireStacks quantity by ingredient code (the full build cost).
  private static Dictionary<string, int> ConstructionCost(JsonElement block) {
    var totals = new Dictionary<string, int>();
    foreach (
      JsonElement stage in DefinitionJson
        .Constructable(block)
        .GetProperty("stages")
        .EnumerateArray()
    ) {
      if (!stage.TryGetProperty("requireStacks", out JsonElement stacks))
        continue;
      foreach (JsonElement ing in stacks.EnumerateArray()) {
        string code = ing.GetProperty("code").GetString()!;
        totals[code] =
          totals.GetValueOrDefault(code)
          + ing.GetProperty("quantity").GetInt32();
      }
    }
    return totals;
  }

  #endregion
}
