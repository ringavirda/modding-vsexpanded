using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Casting;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The casting catalogue's two invariants: every pattern parses, and every pattern output names an item
/// or block that exists. An output naming nothing neither throws nor logs - the stack resolves to null,
/// so the cast fills, hardens and shakes out empty, which a player cannot tell from a misrun.
/// </summary>
public class CastPartCatalogueTests {
  private static readonly System.Reflection.Assembly Iiex =
    typeof(PatternItemDefinitions).Assembly;

  /// <summary>The <c>mold</c> attribute a pattern of <paramref name="type"/> carries, read out of the
  /// rendered def exactly as the game would read it off the item.</summary>
  private static JsonObject MoldOf(string type) {
    ExpandedLib.Definitions.ExItemDef def = DefinitionGoldens
      .Collect("iiex", Iiex)
      .OfType<ExpandedLib.Definitions.ExItemDef>()
      .Single(d => d.Code == "pattern");

    var byType = new JsonObject(def.ToJson()["attributesByType"]!);
    return byType[$"*-{type}-*"]["mold"];
  }

  #region Every pattern parses

  [Theory]
  [MemberData(nameof(PatternTypes))]
  public void Every_pattern_carries_a_parseable_mold_spec(string type) {
    Assert.True(
      MoldSpec.TryParse(MoldOf(type), out MoldSpec? spec, out string? error),
      $"{type}: {error}"
    );
    Assert.NotNull(spec);
    Assert.True(spec!.Capacity > 0);
  }

  public static TheoryData<string> PatternTypes() {
    var data = new TheoryData<string>();
    foreach (string t in PatternItemDefinitions.PatternTypes)
      data.Add(t);
    return data;
  }

  #endregion

  #region Every output names something real

  [Fact]
  public void Every_pattern_output_names_a_real_item_or_block() {
    var missing = new List<string>();

    foreach (string type in PatternItemDefinitions.PatternTypes) {
      Assert.True(
        MoldSpec.TryParse(MoldOf(type), out MoldSpec? spec, out string? err),
        err
      );
      if (!DefinitionCatalogue.Resolves(spec!.Output, "iiex", Iiex))
        missing.Add($"{type} -> {spec.Output.Type} {spec.Output.Code}");
    }

    Assert.True(
      missing.Count == 0,
      "pattern outputs naming nothing:\n  " + string.Join("\n  ", missing)
    );
  }

  [Fact]
  public void The_catalogue_check_can_actually_fail() {
    // The test above reports on absence, so a resolver that always returned true would leave it green.
    var bogus = new JsonItemStack {
      Type = EnumItemClass.Item,
      Code = new AssetLocation("iiex:no-such-thing-anywhere"),
    };

    Assert.False(DefinitionCatalogue.Resolves(bogus, "iiex", Iiex));
  }

  [Fact]
  public void An_outside_domain_is_treated_as_resolvable() {
    // The headless harness cannot see vanilla's registry, so codes outside the mod's own domain are
    // treated as resolvable rather than reported as missing.
    var vanilla = new JsonItemStack {
      Type = EnumItemClass.Item,
      Code = new AssetLocation("game:ingot-iron"),
    };

    Assert.True(DefinitionCatalogue.Resolves(vanilla, "iiex", Iiex));
  }

  #endregion

  #region Long-cell patterns

  [Fact]
  public void The_long_cell_pattern_set_is_exactly_the_cast_stock_route() {
    // Derived from CastStockItemDefinitions.Forms so the two cannot drift: a cast stock cannot be added
    // without its pattern, or a pattern without its stock. Pattern names are plural where the impression
    // yields more than one piece (three billets, two blooms, one slab) while the item stays singular
    // (`caststock-billet`), which is why Forms carries both spellings.
    Assert.Equal(
      ["castbillets", "castblooms", "castslab"],
      PatternItemDefinitions.LongCellPatternTypes
    );
  }

  [Theory]
  [InlineData("castbillets")]
  [InlineData("castblooms")]
  [InlineData("castslab")]
  public void Every_long_cell_pattern_declares_the_longcell_size(string type) {
    Assert.True(MoldSpec.TryParse(MoldOf(type), out MoldSpec? spec, out _));
    Assert.Equal(MoldSize.LongCell, spec!.Size);
  }

  [Fact]
  public void Every_other_pattern_is_still_a_cell_pattern() {
    foreach (string type in PatternItemDefinitions.PatternTypes) {
      if (PatternItemDefinitions.LongCellPatternTypes.Contains(type))
        continue;
      Assert.True(MoldSpec.TryParse(MoldOf(type), out MoldSpec? spec, out _));
      Assert.Equal(MoldSize.Cell, spec!.Size);
    }
  }

  [Theory]
  [InlineData("billet", 3)]
  [InlineData("bloom", 2)]
  [InlineData("slab", 1)]
  public void A_long_cell_patterns_capacity_is_its_lane_count_times_the_piece(
    string form,
    int lanes
  ) {
    // Capacity is the whole impression, not one lane: a three-lane billet pattern must take three
    // billets' worth of metal. The pattern type is looked up from Forms rather than derived from the
    // form name, because the two differ by more than a prefix (`billet` -> `castbillets`).
    (string _, int piece, string type) =
      Items.CastStockItemDefinitions.Forms.Single(f => f.Form == form);

    Assert.True(MoldSpec.TryParse(MoldOf(type), out MoldSpec? spec, out _));
    Assert.Equal(lanes * piece, spec!.Capacity);
    Assert.Equal(lanes, spec.Cavity.Length);
  }

  #endregion
}
