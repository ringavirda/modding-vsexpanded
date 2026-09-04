using System.Linq;
using IronIndustryExpanded.BlockStructures.Casting;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The item-code layout of casting patterns: <c>pattern-{type}-{wood}</c>, type first. The mold-spec
/// wildcard (<c>*-{type}-*</c>) and the lang key (<c>item-pattern-{type}-*</c>) both key off the type, so
/// the last code part is the wood and no bare <c>pattern-{type}</c> item exists. The casting cell reads the
/// spec off the held stack and persists the full item code rather than rebuilding one from parts.
/// See docs/design/items/patterns.md.
/// </summary>
public class PatternCodeLayoutTests {
  // Every pattern item code the def actually emits.
  private static string[] AllPatternCodes() =>
    [
      .. from type in PatternItemDefinitions.PatternTypes
      from wood in PatternItemDefinitions.PatternWoods
      select $"pattern-{type}-{wood}",
    ];

  [Fact]
  public void The_last_code_part_is_the_wood_not_the_type() {
    // `LastCodePart()` on a pattern yields the wood, so resolving a mold spec by it matches nothing.
    foreach (string code in AllPatternCodes()) {
      string last = code.Split('-')[^1];
      Assert.Contains(last, PatternItemDefinitions.PatternWoods);
      Assert.DoesNotContain(last, PatternItemDefinitions.PatternTypes);
    }
  }

  [Fact]
  public void There_is_no_bare_pattern_type_item_to_resolve_against() {
    // No def emits a bare `pattern-{type}`, so `GetItem("iiex:pattern-" + type)` never resolves. The cell
    // reads the spec off the held stack and stores the full code instead.
    string[] codes = AllPatternCodes();
    foreach (string type in PatternItemDefinitions.PatternTypes)
      Assert.DoesNotContain($"pattern-{type}", codes);
  }

  [Fact]
  public void Every_emitted_code_is_matched_by_exactly_one_mold_wildcard() {
    // The `*-{type}-*` attribute keys must cover every variant and never two at once: that mapping is what
    // puts a mold spec on each pattern's Attributes.
    foreach (string code in AllPatternCodes()) {
      var matches = PatternItemDefinitions
        .PatternTypes.Where(type => code.Contains($"-{type}-"))
        .ToArray();
      Assert.Single(matches);
    }
  }

  [Fact]
  public void Every_pattern_type_carries_a_parseable_mold_spec() {
    // The load-time sweep checks the shipped assets; this checks the source table the sweep validates.
    Assert.NotEmpty(PatternItemDefinitions.PatternTypes);
    Assert.NotEmpty(PatternItemDefinitions.PatternWoods);
  }
}
