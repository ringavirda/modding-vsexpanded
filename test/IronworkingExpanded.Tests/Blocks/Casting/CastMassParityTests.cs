using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Casting;
using IronworkingExpanded.Items;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The casting catalogue's <b>mass</b> invariant: what a mold holds is what its castings are worth.
/// <para>
/// <b>Why this is worth a file.</b> A pattern declares a <c>capacity</c>; the item it casts declares a
/// <c>materialUnits</c>; nothing in the game connects them. Let them drift and a station either mints metal
/// (pour 600, shake out two 600 u parts) or eats it (pour 600, shake out a 540 u part and lose 60 to
/// nowhere) - and neither shows up as an error, a log line, or a visibly wrong cast. It is the same class of
/// silent hole <see cref="CastPartCatalogueTests"/> covers for outputs that name nothing, one field along.
/// </para>
/// <para>
/// The invariant is stated per <b>lane</b>, because a long-cell pattern's capacity is the whole impression:
/// <c>capacity == cavity.Length × output.materialUnits</c>. A single-cell pattern is the one-lane case of
/// the same equation, so there is one rule rather than two.
/// </para>
/// </summary>
public class CastMassParityTests
{
  private static readonly System.Reflection.Assembly Iwex = typeof(
    PatternItemDefinitions
  ).Assembly;

  #region Capacity == lanes x the cast item's declared mass

  [Fact]
  public void Every_pattern_holds_exactly_what_its_castings_are_worth()
  {
    var wrong = new List<string>();

    foreach (string type in PatternItemDefinitions.PatternTypes)
    {
      MoldSpec spec = MoldOf(type);

      // Block outputs are skipped, and deliberately: a cast block (the plate and ingot molds) carries no
      // materialUnits, because its mass lives in whatever it drops when broken rather than on the block.
      // Those two are the only ones, and forcing an attribute onto them to satisfy a test would be the
      // test dictating the model.
      if (spec.Output.Type != EnumItemClass.Item)
        continue;

      int? piece = MaterialUnitsOf(spec.Output.Code);
      if (piece is null)
      {
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
      "pattern capacity and cast mass disagree:\n  " + string.Join("\n  ", wrong)
    );
  }

  [Fact]
  public void The_parity_check_can_actually_fail()
  {
    // The test above reports on agreement between two numbers it reads dynamically. If the reader
    // silently returned null for everything, the loop would `continue` past every row and pass forever.
    Assert.Null(MaterialUnitsOf(new AssetLocation("iwex:no-such-item")));
    Assert.Equal(
      CastPartItemDefinitions.HeavyPlateUnits,
      MaterialUnitsOf(new AssetLocation("iwex:castplate-heavy"))
    );
    // And it must see through attributesByType, which is where every variant-grouped mass lives.
    Assert.Equal(
      CastStockItemDefinitions.BilletUnits,
      MaterialUnitsOf(new AssetLocation("iwex:caststock-billet"))
    );
  }

  #endregion

  #region The wheel section

  [Fact]
  public void The_wheel_section_costs_the_same_as_its_fabricated_half()
  {
    // N3: a cast structural part and its rolled/riveted equivalent are alternatives, not tiers, so they
    // must cost the same iron and differ only in the plant they demand. docs/design/processes/bending.md
    // pins the fabricated rim at 600 u (a heavyplate 12 x 2 x 10, bent). If the constant drifts off 600 the
    // cast route becomes quietly cheaper and the choice stops being a choice - which no other test notices.
    // `castshell` carries the same number for the same reason, and is pinned just below - it lives in
    // iwex because the ladle is built from it.
    Assert.Equal(600, CastPartItemDefinitions.CastWheelSectionUnits);
  }

  [Fact]
  public void The_wheel_section_is_a_single_lane_cell_pattern()
  {
    MoldSpec spec = MoldOf("castwheelsection");

    Assert.Equal(MoldSize.Cell, spec.Size);
    Assert.Equal(new AssetLocation("iwex:castwheelsection"), spec.Output.Code);
    Assert.Equal(EnumItemClass.Item, spec.Output.Type);
    // One lane: the cell casts one part per pour, unlike the long cell's ladder.
    Assert.Single(spec.Cavity);
  }

  [Fact]
  public void The_shell_and_the_wheel_section_cost_the_SAME_iron()
  {
    // Both are 600 u, and that equality is the content — not the value. N3 makes cast and fabricated
    // structural parts alternatives, not tiers, and `bending.md` pins the fabricated shell at 600 u
    // (a boilerplate 15 x 1 x 16, bent and riveted). If the cast route drifted below it, the choice between
    // plant — cupola + pattern versus mill + roller + rivets — would collapse into "cast is just cheaper".
    // The density rule would price the drawn shell at 540 (216 vx³ x 2.5). Mass is declared, not measured.
    Assert.Equal(600, CastPartItemDefinitions.CastShellUnits);
    Assert.Equal(
      CastPartItemDefinitions.CastWheelSectionUnits,
      CastPartItemDefinitions.CastShellUnits
    );
  }

  [Fact]
  public void The_shell_came_back_to_iwex_and_ships_exactly_ONCE()
  {
    // `castshell` belongs to iwex because the ladle is built from it. It once lived in lpex, so a stale
    // definition there is a live risk. The failure this guards is not a clash:
    // a leftover lpex definition would register a second castshell in a different domain, and the two would
    // be different items that look identical in the handbook and never stack.
    Assert.Contains("castshell", PatternItemDefinitions.PatternTypes);
    Assert.True(
      DefinitionCatalogue.Resolves(
        new JsonItemStack
        {
          Type = EnumItemClass.Item,
          Code = new AssetLocation("iwex:castshell"),
        },
        "iwex",
        Iwex
      ),
      "iwex must define castshell - the ladle is built from it"
    );
  }

  [Fact]
  public void Both_flywheel_grids_are_diagram_led_and_spend_four_then_eight_sections()
  {
    // The wheel section exists for the flywheel; if the recipe stopped spending it the item would become
    // an orphan with nothing to say so. Counting the glyph in the shipped pattern is what pins that.
    IReadOnlyList<JObject> grids = GridsOf("flywheel");

    Assert.Equal(4, GlyphCount(grids[0], "iwex:castwheelsection"));
    Assert.Equal(8, GlyphCount(grids[1], "iwex:castwheelsection"));

    // And both are diagram-led. Per docs/design/diagram-crafting.md a diagram-crafted recipe is the plan
    // plus a flat bill of materials - one cell per distinct ingredient carrying its quantity - never a
    // picture of the product drawn in the grid. An earlier version spread the four rim sections across the
    // corners of a 3x3 to look like a wheel; it reads nicely and is the wrong idiom, because the grid then
    // encodes the assembly the diagram is already responsible for. Asserting the diagram and the one-cell
    // rule together is what keeps that from creeping back.
    // Each size spends its own plan - a 5x5x2 wheel is a different drawing from a 3x3x1 one, and the art
    // says so (diag-mpenergy-flywheel and diag-mpenergy-flywheellarge are both drawn).
    Assert.Equal(1, GlyphCount(grids[0], "iwex:diagram-mpenergy-flywheel"));
    Assert.Equal(1, GlyphCount(grids[1], "iwex:diagram-mpenergy-flywheellarge"));

    foreach (JObject grid in grids)
    {
      var ingredients = (JObject)grid["ingredients"]!;
      string pattern = (string)grid["ingredientPattern"]!;
      foreach (KeyValuePair<string, JToken?> pair in ingredients)
        Assert.Equal(1, pattern.Count(c => c.ToString() == pair.Key));
    }
  }

  [Theory]
  [InlineData("iwex:diagram-mpenergy-flywheel")]
  [InlineData("iwex:diagram-mpenergy-flywheellarge")]
  public void The_flywheel_diagrams_are_declared_variants(string code)
  {
    // A recipe naming a diagram type the itemtype does not declare resolves to nothing and the recipe simply
    // never loads - silent, and exactly the failure RecipeCodes was written for one layer up.
    Assert.True(
      DefinitionCatalogue.Resolves(
        new JsonItemStack { Type = EnumItemClass.Item, Code = new AssetLocation(code) },
        "iwex",
        Iwex
      )
    );
  }

  #endregion

  #region Reading the defs

  /// <summary>The parsed <c>mold</c> spec a pattern of <paramref name="type"/> carries.</summary>
  private static MoldSpec MoldOf(string type)
  {
    ExItemDef def = DefinitionGoldens
      .Collect("iwex", Iwex)
      .OfType<ExItemDef>()
      .Single(d => d.Code == "pattern");

    var byType = new JsonObject(def.ToJson()["attributesByType"]!);
    Assert.True(
      MoldSpec.TryParse(byType[$"*-{type}-*"]["mold"], out MoldSpec? spec, out string? error),
      $"{type}: {error}"
    );
    return spec!;
  }

  /// <summary>
  /// The <c>materialUnits</c> the itemtype behind <paramref name="code"/> declares, or null if no def
  /// claims that code. Reads <c>attributes</c> first and then <c>attributesByType</c>, which is the order
  /// the engine resolves them in - a variant-grouped item puts its mass in the latter.
  /// </summary>
  private static int? MaterialUnitsOf(AssetLocation code)
  {
    foreach (ExItemDef def in DefinitionGoldens.Collect("iwex", Iwex).OfType<ExItemDef>())
    {
      if (def.Domain != code.Domain)
        continue;
      // The def's own code is the stem; a variant-grouped item's concrete code extends it with `-state`
      // suffixes, so the stem must match either exactly or as a prefix segment.
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

  /// <summary>The grid recipes of the named iwex recipe def, in emit order.</summary>
  private static IReadOnlyList<JObject> GridsOf(string code)
  {
    ExRecipeDef def = DefinitionGoldens
      .Collect("iwex", Iwex)
      .OfType<ExRecipeDef>()
      .Single(d => d.Code == code);

    return [.. ((JArray)def.ToJson()).Cast<JObject>()];
  }

  /// <summary>How many cells of <paramref name="grid"/>'s pattern spend <paramref name="itemCode"/> -
  /// glyph occurrences times that glyph's per-cell quantity.</summary>
  private static int GlyphCount(JObject grid, string itemCode)
  {
    var ingredients = (JObject)grid["ingredients"]!;
    string pattern = (string)grid["ingredientPattern"]!;
    int total = 0;

    foreach (KeyValuePair<string, JToken?> pair in ingredients)
    {
      if ((string?)pair.Value?["code"] != itemCode)
        continue;
      int per = (int?)pair.Value?["quantity"] ?? 1;
      total += pattern.Count(c => c.ToString() == pair.Key) * per;
    }

    return total;
  }

  #endregion
}
