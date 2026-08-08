using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Casting;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The casting catalogue's two invariants: every pattern parses, and every pattern's output names something
/// that actually exists.
/// <para>
/// <b>The second is the one that matters.</b> A mold output naming nothing does not throw and does not
/// log - the stack resolves to null, so the cast fills, hardens, and shake-out yields <i>nothing</i>. The
/// metal is gone and the player cannot tell that from a misrun. Grid-recipe outputs got this check after
/// six of them turned out to name no block; mold outputs are the same failure one layer down.
/// </para>
/// </summary>
public class CastPartCatalogueTests
{
  private static readonly System.Reflection.Assembly Iwex = typeof(
    PatternItemDefinitions
  ).Assembly;

  /// <summary>The <c>mold</c> attribute a pattern of <paramref name="type"/> carries, read out of the
  /// rendered def exactly as the game would read it off the item.</summary>
  private static JsonObject MoldOf(string type)
  {
    ExpandedLib.Definitions.ExItemDef def = DefinitionGoldens
      .Collect("iwex", Iwex)
      .OfType<ExpandedLib.Definitions.ExItemDef>()
      .Single(d => d.Code == "pattern");

    var byType = new JsonObject(def.ToJson()["attributesByType"]!);
    return byType[$"*-{type}-*"]["mold"];
  }

  #region Every pattern parses

  [Theory]
  [MemberData(nameof(PatternTypes))]
  public void Every_pattern_carries_a_parseable_mold_spec(string type)
  {
    Assert.True(
      MoldSpec.TryParse(MoldOf(type), out MoldSpec? spec, out string? error),
      $"{type}: {error}"
    );
    Assert.NotNull(spec);
    Assert.True(spec!.Capacity > 0);
  }

  public static TheoryData<string> PatternTypes()
  {
    var data = new TheoryData<string>();
    foreach (string t in PatternItemDefinitions.PatternTypes)
      data.Add(t);
    return data;
  }

  #endregion

  #region Every output names something real

  [Fact]
  public void Every_pattern_output_names_a_real_item_or_block()
  {
    var missing = new List<string>();

    foreach (string type in PatternItemDefinitions.PatternTypes)
    {
      Assert.True(MoldSpec.TryParse(MoldOf(type), out MoldSpec? spec, out string? err), err);
      if (!DefinitionCatalogue.Resolves(spec!.Output, "iwex", Iwex))
        missing.Add($"{type} -> {spec.Output.Type} {spec.Output.Code}");
    }

    Assert.True(
      missing.Count == 0,
      "pattern outputs naming nothing:\n  " + string.Join("\n  ", missing)
    );
  }

  [Fact]
  public void The_catalogue_check_can_actually_fail()
  {
    // A resolver that returned true unconditionally would make the test above green forever, and it
    // reports on absence - the one shape of check that cannot prove itself by passing.
    var bogus = new JsonItemStack
    {
      Type = EnumItemClass.Item,
      Code = new AssetLocation("iwex:no-such-thing-anywhere"),
    };

    Assert.False(DefinitionCatalogue.Resolves(bogus, "iwex", Iwex));
  }

  [Fact]
  public void An_outside_domain_is_treated_as_resolvable()
  {
    // Deliberate: the headless harness cannot see vanilla's registry, so answering "missing" for every
    // game: output would bury our own dangling codes in noise.
    var vanilla = new JsonItemStack
    {
      Type = EnumItemClass.Item,
      Code = new AssetLocation("game:ingot-iron"),
    };

    Assert.True(DefinitionCatalogue.Resolves(vanilla, "iwex", Iwex));
  }

  #endregion

  #region Long-cell patterns

  [Fact]
  public void The_long_cell_pattern_set_is_exactly_the_cast_stock_ladder()
  {
    // Derived from CastStockItemDefinitions.Forms so the two cannot drift, and pinned here so a fourth
    // cast stock cannot be added without its pattern - or a pattern without its stock.
    // Plural where the impression yields more than one piece: three billets from one pour, two blooms,
    // one slab. The item stays singular (`caststock-billet`), which is why Forms carries both spellings
    // rather than deriving one from the other.
    Assert.Equal(
      ["castbillets", "castblooms", "castslab"],
      PatternItemDefinitions.LongCellPatternTypes
    );
  }

  [Theory]
  [InlineData("castbillets")]
  [InlineData("castblooms")]
  [InlineData("castslab")]
  public void Every_long_cell_pattern_declares_the_longcell_size(string type)
  {
    Assert.True(MoldSpec.TryParse(MoldOf(type), out MoldSpec? spec, out _));
    Assert.Equal(MoldSize.LongCell, spec!.Size);
  }

  [Fact]
  public void Every_other_pattern_is_still_a_cell_pattern()
  {
    foreach (string type in PatternItemDefinitions.PatternTypes)
    {
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
  )
  {
    // The lane ladder, stated as arithmetic: capacity is the whole impression. If it were per lane, a
    // three-lane billet pattern would fill after one billet's worth of metal and shake out three - which
    // is the one thing every other test in the casting suite exists to prevent.
    //
    // Parameterised on the form, and the pattern type is looked up rather than spelt: the two differ by
    // more than a prefix now (`billet` -> `castbillets`, plural), so the old `type["cast".Length..]` slice
    // would have silently produced "billets" and asked UnitsOf for a form that does not exist - which
    // returns 0, and `lanes * 0 == 0` would then have failed loudly only by luck.
    (string _, int piece, string type) = Items
      .CastStockItemDefinitions.Forms.Single(f => f.Form == form);

    Assert.True(MoldSpec.TryParse(MoldOf(type), out MoldSpec? spec, out _));
    Assert.Equal(lanes * piece, spec!.Capacity);
    Assert.Equal(lanes, spec.Cavity.Length);
  }

  #endregion
}
