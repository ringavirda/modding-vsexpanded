using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using IronIndustryExpanded.Items;
using IronIndustryExpanded.Recipes.Grid;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The substitution rule: a merely-structural joint takes a nail or a rivet, so a shop that has built the
/// riveter need not keep an anvil going for nails as well.
/// <para>
/// Vintage Story has no OR across item codes - a grid ingredient names one code family, and an RCC stage's
/// <c>requireStacks</c> is an AND list - so substitution is one recipe per fastener. That makes it a
/// property of the recipe corpus rather than of any one ingredient, which is what these check.
/// See docs/design/items/fasteners.md and STATE.md.
/// </para>
/// </summary>
public class FastenerSubstitutionTests {
  private const string Domain = "iiex";
  private const string Nails = "game:metalnailsandstrips-*";

  private static string Rivet =>
    $"{Domain}:{FastenerItemDefinitions.RivetCode}";

  /// <summary>
  /// The six sites the rule reaches, by the block each makes. Named one at a time rather than counted per
  /// provider: their providers make plenty of other nailed things, and the rule was scoped to these six on
  /// purpose - past them the duplication starts multiplying against the two gear routes.
  /// </summary>
  public static TheoryData<string> Sites =>
    [
      "iiex:pipe-plated-straight-ns",
      "iiex:pipe-plated-bend-nw",
      "iiex:pipe-plated-tjunction-uns",
      "iiex:pipe-plated-xjunction-nswe",
      "iiex:hopper-tall-n",
      "iiex:molten-barrel-plated",
    ];

  /// <summary>
  /// Every grid recipe iiex emits at the three providers the rule touches. A def carrying one recipe
  /// serialises as an object and a def carrying several as an array, so both shapes are flattened here.
  /// </summary>
  private static IEnumerable<JObject> Recipes() =>
    new[]
    {
      PlatedPipeRecipeDefinitions.Definitions(Domain),
      FurnaceRecipeDefinitions.Definitions(Domain),
      MoltenRecipeDefinitions.Definitions(Domain),
    }
      .SelectMany(defs => defs)
      .SelectMany(def =>
        def.ToJson() is JArray many
          ? many.Cast<JObject>()
          : [(JObject)def.ToJson()]
      );

  private static IEnumerable<string> IngredientCodes(JObject recipe) =>
    recipe["ingredients"] is JObject ingredients
      ? ingredients
        .Properties()
        .Select(p => p.Value["code"]?.ToString())
        .Where(c => c != null)
        .Select(c => c!)
      : [];

  /// <summary>The recipes making <paramref name="output"/> that fasten with <paramref name="fastener"/>.</summary>
  private static List<JObject> Making(string output, string fastener) =>
    [
      .. Recipes()
        .Where(r => r["output"]?["code"]?.ToString() == output)
        .Where(r => IngredientCodes(r).Contains(fastener)),
    ];

  /// <summary>
  /// Each of the six is craftable both ways, exactly once each. A missing twin is a route a player who has
  /// moved to rivets cannot craft at all; a duplicate one is two recipes racing for the same pattern.
  /// </summary>
  [Theory]
  [MemberData(nameof(Sites))]
  public void Each_site_is_craftable_with_either_fastener(string output) {
    Assert.Single(Making(output, Nails));
    Assert.Single(Making(output, Rivet));
  }

  /// <summary>
  /// The twins share their pattern and differ in the fastener alone. That is what keeps them from
  /// colliding: two recipes on one pattern whose ingredient sets overlap leave the loser silently
  /// unresolvable, and nails and rivets are disjoint codes.
  /// </summary>
  [Theory]
  [MemberData(nameof(Sites))]
  public void A_twin_differs_from_its_pair_in_the_fastener_alone(string output) {
    JObject nailed = Making(output, Nails).Single();
    JObject riveted = Making(output, Rivet).Single();

    Assert.Equal(
      nailed["ingredientPattern"]?.ToString(),
      riveted["ingredientPattern"]?.ToString()
    );

    string[] Others(JObject r) =>
      [
        .. IngredientCodes(r)
          .Where(c => c != Nails && c != Rivet)
          .OrderBy(c => c),
      ];
    Assert.Equal(Others(nailed), Others(riveted));
  }

  /// <summary>
  /// The rule is scoped, and the scope is the point. A site outside the six keeps nails alone - the
  /// twin-tub blower is the control - so a careless widening that riveted everything would read as a
  /// failure here rather than as a quieter pass.
  /// </summary>
  [Fact]
  public void A_site_outside_the_rule_is_still_nails_only() {
    Assert.Single(Making("iiex:furnace-twintubblower-n", Nails));
    Assert.Empty(Making("iiex:furnace-twintubblower-n", Rivet));
  }
}
