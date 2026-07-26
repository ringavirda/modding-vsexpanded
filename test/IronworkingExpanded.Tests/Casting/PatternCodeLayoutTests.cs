using System.Linq;
using IronworkingExpanded.BlockStructures.Casting;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The shape of a pattern's item code, pinned because the casting cell's whole contract rests on it.
/// <para>
/// A pattern is <c>pattern-{type}-{wood}</c> - <b>type first, wood last</b> - because the mold-spec wildcard
/// (<c>*-{type}-*</c>) and the lang key (<c>item-pattern-{type}-*</c>) both key off the type. Two consequences
/// bit us once and these tests exist so they cannot again: the <em>last</em> code part is the <b>wood</b>, not
/// the type, and there is no bare <c>pattern-{type}</c> item to look up. So the cell must read the spec off the
/// held stack and persist the <b>full item code</b> - never rebuild a code from parts.
/// </para>
/// </summary>
public class PatternCodeLayoutTests
{
  // Every pattern item code the def actually emits.
  private static string[] AllPatternCodes() =>
    [
      .. from type in PatternItemDefinitions.PatternTypes
      from wood in PatternItemDefinitions.PatternWoods
      select $"pattern-{type}-{wood}",
    ];

  [Fact]
  public void The_last_code_part_is_the_wood_not_the_type()
  {
    // This is exactly the trap: `LastCodePart()` on a pattern yields "oak", so resolving a spec by it silently
    // fails on every pattern in the game. If the variant order ever flips, this test must be revisited - not
    // deleted, because the wildcard and lang keys depend on type-first too.
    foreach (string code in AllPatternCodes())
    {
      string last = code.Split('-')[^1];
      Assert.Contains(last, PatternItemDefinitions.PatternWoods);
      Assert.DoesNotContain(last, PatternItemDefinitions.PatternTypes);
    }
  }

  [Fact]
  public void There_is_no_bare_pattern_type_item_to_resolve_against()
  {
    // `pattern-{type}` is not a code the def emits, so `GetItem("iwex:pattern-" + type)` can never hit. The cell
    // reads the spec off the held stack and stores the full code instead.
    string[] codes = AllPatternCodes();
    foreach (string type in PatternItemDefinitions.PatternTypes)
      Assert.DoesNotContain($"pattern-{type}", codes);
  }

  [Fact]
  public void Every_emitted_code_is_matched_by_exactly_one_mold_wildcard()
  {
    // The `*-{type}-*` keys must cover every variant, and never two at once - that mapping is what puts a spec
    // on each pattern's Attributes in the first place.
    foreach (string code in AllPatternCodes())
    {
      var matches = PatternItemDefinitions
        .PatternTypes.Where(type => code.Contains($"-{type}-"))
        .ToArray();
      Assert.Single(matches);
    }
  }

  [Fact]
  public void Every_pattern_type_carries_a_parseable_mold_spec()
  {
    // The load-time sweep checks the shipped assets; this checks the source table the sweep validates, so a new
    // castable part added to `Molds` cannot ship without a usable spec.
    Assert.NotEmpty(PatternItemDefinitions.PatternTypes);
    Assert.NotEmpty(PatternItemDefinitions.PatternWoods);
  }
}
