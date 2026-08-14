using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Casting;
using IronIndustryExpanded.Items;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The casting catalogue's mass invariant: <c>capacity == cavity.Length * output.materialUnits</c>, stated
/// per lane so a single-cell pattern is the one-lane case of the same equation. A pattern's
/// <c>capacity</c> and its output's <c>materialUnits</c> are declared separately and nothing in the game
/// connects them, so drift silently mints or destroys metal rather than raising an error.
/// </summary>
public class CastMassParityTests {
  private static readonly System.Reflection.Assembly Iiex =
    typeof(PatternItemDefinitions).Assembly;

  #region Capacity == lanes x the cast item's declared mass

  [Fact]
  public void Every_pattern_holds_exactly_what_its_castings_are_worth() {
    var wrong = new List<string>();

    foreach (string type in PatternItemDefinitions.PatternTypes) {
      MoldSpec spec = MoldOf(type);

      // Block outputs are skipped: a cast block (the plate and ingot molds) carries no materialUnits,
      // because its mass lives in whatever it drops when broken rather than on the block.
      if (spec.Output.Type != EnumItemClass.Item)
        continue;

      int? piece = MaterialUnitsOf(spec.Output.Code);
      if (piece is null) {
        wrong.Add($"{type} -> {spec.Output.Code} declares no materialUnits");
        continue;
      }

      int expected = spec.Cavity.Length * piece.Value;
      if (spec.Capacity != expected)
        wrong.Add(
          $"{type}: capacity {spec.Capacity} but {spec.Cavity.Length} lane(s) x {piece} = {expected}"
        );
    }

    Assert.True(
      wrong.Count == 0,
      "pattern capacity and cast mass disagree:\n  "
        + string.Join("\n  ", wrong)
    );
  }

  [Fact]
  public void The_parity_check_can_actually_fail() {
    // The test above compares two dynamically read numbers. A reader returning null for everything would
    // make its loop `continue` past every row and pass unconditionally.
    Assert.Null(MaterialUnitsOf(new AssetLocation("iiex:no-such-item")));
    Assert.Equal(
      CastPartItemDefinitions.HeavyPlateUnits,
      MaterialUnitsOf(new AssetLocation("iiex:castplate-heavy"))
    );
    // It must also see through attributesByType, where every variant-grouped mass lives.
    Assert.Equal(
      CastStockItemDefinitions.BilletUnits,
      MaterialUnitsOf(new AssetLocation("iiex:caststock-billet"))
    );
  }

  #endregion

  #region The wheel section

  [Fact]
  public void The_wheel_section_costs_the_same_as_its_fabricated_half() {
    // A cast structural part and its rolled/riveted equivalent are alternatives, not tiers: they cost the
    // same iron and differ only in the plant they demand. docs/design/processes/bending.md pins the
    // fabricated rim at 600 u (a heavyplate 12 x 2 x 10, bent).
    Assert.Equal(600, CastPartItemDefinitions.CastWheelSectionUnits);
  }

  [Fact]
  public void The_wheel_section_is_a_single_lane_cell_pattern() {
    MoldSpec spec = MoldOf("castwheelsection");

    Assert.Equal(MoldSize.Cell, spec.Size);
    Assert.Equal(new AssetLocation("iiex:castwheelsection"), spec.Output.Code);
    Assert.Equal(EnumItemClass.Item, spec.Output.Type);
    // One lane: the cell casts one part per pour, where the long cell casts a ladder of them.
    Assert.Single(spec.Cavity);
  }

  [Fact]
  public void The_shell_and_the_wheel_section_cost_the_SAME_iron() {
    // The equality is the content, not the value: cast and fabricated structural parts are alternatives,
    // not tiers. docs/design/processes/bending.md pins the fabricated shell at 600 u (a boilerplate
    // 15 x 1 x 16, bent and riveted). The density rule would price the drawn shell at 540
    // (216 vx3 x 2.5); mass here is declared, not measured.
    Assert.Equal(600, CastPartItemDefinitions.CastShellUnits);
    Assert.Equal(
      CastPartItemDefinitions.CastWheelSectionUnits,
      CastPartItemDefinitions.CastShellUnits
    );
  }

  [Fact]
  public void The_shell_came_back_to_iwex_and_ships_exactly_ONCE() {
    // `castshell` belongs to iiex because the ladle is built from it. A second definition in another
    // domain does not clash: it registers a distinct item that looks identical in the handbook and never
    // stacks with this one.
    Assert.Contains("castshell", PatternItemDefinitions.PatternTypes);
    Assert.True(
      DefinitionCatalogue.Resolves(
        new JsonItemStack {
          Type = EnumItemClass.Item,
          Code = new AssetLocation("iiex:castshell"),
        },
        "iiex",
        Iiex
      ),
      "iiex must define castshell - the ladle is built from it"
    );
  }

  [Fact]
  public void Both_flywheel_grids_are_diagram_led_and_spend_four_then_eight_sections() {
    // The wheel section exists for the flywheel. A recipe that stopped spending it would leave the item an
    // orphan with nothing to report it, so the glyph is counted in the shipped pattern.
    IReadOnlyList<JObject> grids = GridsOf("flywheel");

    Assert.Equal(4, GlyphCount(grids[0], "iiex:castwheelsection"));
    Assert.Equal(8, GlyphCount(grids[1], "iiex:castwheelsection"));

    // Both grids are diagram-led. Per docs/design/diagram-crafting.md a diagram-crafted recipe is the plan
    // plus a flat bill of materials - one cell per distinct ingredient carrying its quantity - never a
    // picture of the product drawn in the grid. Each size spends its own plan: a 5x5x2 wheel is a
    // different drawing from a 3x3x1 one.
    Assert.Equal(1, GlyphCount(grids[0], "iiex:diagram-mpenergy-flywheel"));
    Assert.Equal(
      1,
      GlyphCount(grids[1], "iiex:diagram-mpenergy-flywheellarge")
    );

    foreach (JObject grid in grids) {
      var ingredients = (JObject)grid["ingredients"]!;
      string pattern = (string)grid["ingredientPattern"]!;
      foreach (KeyValuePair<string, JToken?> pair in ingredients)
        Assert.Equal(1, pattern.Count(c => c.ToString() == pair.Key));
    }
  }

  [Theory]
  [InlineData("iiex:diagram-mpenergy-flywheel")]
  [InlineData("iiex:diagram-mpenergy-flywheellarge")]
  public void The_flywheel_diagrams_are_declared_variants(string code) {
    // A recipe naming a diagram type the itemtype does not declare resolves to nothing, and the recipe
    // then never loads, without an error.
    Assert.True(
      DefinitionCatalogue.Resolves(
        new JsonItemStack {
          Type = EnumItemClass.Item,
          Code = new AssetLocation(code),
        },
        "iiex",
        Iiex
      )
    );
  }

  #endregion

  #region Reading the defs

  /// <summary>The parsed <c>mold</c> spec a pattern of <paramref name="type"/> carries.</summary>
  private static MoldSpec MoldOf(string type) {
    ExItemDef def = DefinitionGoldens
      .Collect("iiex", Iiex)
      .OfType<ExItemDef>()
      .Single(d => d.Code == "pattern");

    var byType = new JsonObject(def.ToJson()["attributesByType"]!);
    Assert.True(
      MoldSpec.TryParse(
        byType[$"*-{type}-*"]["mold"],
        out MoldSpec? spec,
        out string? error
      ),
      $"{type}: {error}"
    );
    return spec!;
  }

  /// <summary>
  /// The <c>materialUnits</c> the itemtype behind <paramref name="code"/> declares, or null if no def
  /// claims that code. Reads <c>attributes</c> first and then <c>attributesByType</c>, which is the order
  /// the engine resolves them in - a variant-grouped item puts its mass in the latter.
  /// </summary>
  private static int? MaterialUnitsOf(AssetLocation code) {
    foreach (
      ExItemDef def in DefinitionGoldens
        .Collect("iiex", Iiex)
        .OfType<ExItemDef>()
    ) {
      if (def.Domain != code.Domain)
        continue;
      // The def's own code is the stem; a variant-grouped item's concrete code extends it with `-state`
      // suffixes, so the stem matches either exactly or as a prefix segment.
      if (code.Path != def.Code && !code.Path.StartsWith(def.Code + "-"))
        continue;

      JObject json = def.ToJson();
      if (json["attributes"]?["materialUnits"] is { } flat)
        return (int)flat;

      if (json["attributesByType"] is JObject byType)
        foreach (KeyValuePair<string, JToken?> pair in byType)
          if (
            WildcardUtil.Match(new AssetLocation(def.Domain, pair.Key), code)
            && pair.Value?["materialUnits"] is { } units
          )
            return (int)units;
    }

    return null;
  }

  /// <summary>The grid recipes of the named iiex recipe def, in emit order.</summary>
  private static IReadOnlyList<JObject> GridsOf(string code) {
    ExRecipeDef def = DefinitionGoldens
      .Collect("iiex", Iiex)
      .OfType<ExRecipeDef>()
      .Single(d => d.Code == code);

    return [.. ((JArray)def.ToJson()).Cast<JObject>()];
  }

  /// <summary>How many cells of <paramref name="grid"/>'s pattern spend <paramref name="itemCode"/> -
  /// glyph occurrences times that glyph's per-cell quantity.</summary>
  private static int GlyphCount(JObject grid, string itemCode) {
    var ingredients = (JObject)grid["ingredients"]!;
    string pattern = (string)grid["ingredientPattern"]!;
    int total = 0;

    foreach (KeyValuePair<string, JToken?> pair in ingredients) {
      if ((string?)pair.Value?["code"] != itemCode)
        continue;
      int per = (int?)pair.Value?["quantity"] ?? 1;
      total += pattern.Count(c => c.ToString() == pair.Key) * per;
    }

    return total;
  }

  #endregion
}
