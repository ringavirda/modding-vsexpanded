using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Migrations;
using ExpandedLib.Testing;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Every code the mods' recipes and construction stages point at must name something one of them
/// registers. Nothing checked this in either direction before, and neither failure mode is loud: a
/// recipe with a dead ingredient stops matching and the player sees an item that cannot be made, while
/// <c>ExConstruction</c> refuses an unresolvable non-wildcard require outright, so the structure cannot
/// be raised in survival or creative.
/// <para>
/// It lives here because a reference crosses domains - a siex construction stage requires an iiex pipe -
/// and this is the only suite that references all three mods. A per-mod suite testing the same thing
/// would have to treat every foreign code as resolvable, which is exactly the gap that let a converter
/// ship unbuildable.
/// </para>
/// </summary>
public class CrossModReferenceTests {
  /// <summary>The registries a reference may land in. A code outside these domains is vanilla's or a
  /// third party's, and this harness holds nothing to judge it against.</summary>
  private static readonly Dictionary<string, Assembly> Domains = new() {
    ["exlib"] = typeof(BlockMigrationModSystem).Assembly,
    ["iiex"] = typeof(IronIndustryExpanded.IiexBlocks).Assembly,
    ["siex"] = typeof(SiexBlocks).Assembly,
  };

  private static IReadOnlyList<ReferencedCodes.Reference> All() =>
    [
      .. Domains.SelectMany(d =>
        ReferencedCodes
          .InRecipes(d.Key, d.Value)
          .Concat(ReferencedCodes.InDefinitions(d.Key, d.Value))
      ),
    ];

  #region The contract

  [Fact]
  public void Every_referenced_code_names_something_a_mod_registers() {
    IReadOnlyList<ReferencedCodes.Reference> bad = ReferencedCodes.Unresolvable(
      All(),
      Domains
    );

    Assert.True(
      bad.Count == 0,
      $"{bad.Count} authored code(s) name nothing any mod registers. A recipe one stops matching "
        + "silently; a construction one makes the structure unbuildable in every game mode:\n  "
        + string.Join("\n  ", bad.Select(r => r.ToString()))
    );
  }

  [Fact]
  public void No_layout_in_any_mod_pins_a_network_nodes_orientation() {
    // Cross-mod for the same reason the rest of this suite is: siex's hot furnace pins iiex's tuyere, so
    // only a check holding all three registries at once can tell that code from a player-oriented one.
    IReadOnlyList<string> pinned = PinnedNetworkNodes.Violations(
      out int codesChecked,
      [.. Domains.Select(d => (d.Key, d.Value))]
    );

    Assert.True(
      pinned.Count == 0,
      $"{pinned.Count} layout cell(s) pin a node that re-picks its own orientation:\n  "
        + string.Join("\n  ", pinned)
    );
    Assert.True(
      codesChecked > 0,
      "no pinned codes examined - is collection broken?"
    );
  }

  [Fact]
  public void No_definition_names_one_of_our_codes_without_its_domain() {
    IReadOnlyList<ReferencedCodes.Reference> ambiguous =
      ReferencedCodes.BareButOurs(All(), Domains);

    Assert.True(
      ambiguous.Count == 0,
      $"{ambiguous.Count} reference(s) omit the domain on a code one of the mods registers. An "
        + "unqualified code parses as game:, so this resolves to vanilla's collectible or to nothing "
        + "at all - never to ours by intent:\n  "
        + string.Join("\n  ", ambiguous.Select(r => r.ToString()))
    );
  }

  #endregion

  #region Guards on the check itself

  [Fact]
  public void The_check_sees_references_of_every_origin() {
    // Each origin is collected by its own reader over a different JSON shape, so one silently
    // returning nothing - a renamed behavior, a recipe shape the reader does not know - would leave
    // the contract above passing over an entire class of reference.
    var byOrigin = All()
      .Where(r => Domains.ContainsKey(r.Domain))
      .GroupBy(r => r.Origin)
      .ToDictionary(g => g.Key, g => g.Count());

    Assert.All(
      System.Enum.GetValues<ReferencedCodes.Origin>(),
      origin =>
        Assert.True(
          byOrigin.GetValueOrDefault(origin) > 0,
          $"No {origin} reference into a mod domain was collected at all - the reader for that "
            + "shape found nothing, and the resolution check covers none of them."
        )
    );
  }

  [Fact]
  public void A_large_minority_of_references_is_ours_and_the_rest_is_vanillas() {
    // Roughly 530 of 930 references land in a mod domain once placeholders expand; the rest are
    // `game:` codes this harness holds no registry for and deliberately leaves alone. Both halves are
    // asserted because "the check passed" and "the check skipped it" read identically otherwise, and
    // a collector that quietly stopped walking most definitions would still satisfy the per-origin
    // guard above. The floor sits well under the real count: content only adds references.
    IReadOnlyList<ReferencedCodes.Reference> all = All();
    int checkable = ReferencedCodes.Checkable(all, Domains);

    Assert.True(
      checkable > 450,
      $"only {checkable} of {all.Count} references land in a mod domain, where ~530 do today - the "
        + "collectors are skipping definitions they used to walk."
    );
    Assert.True(
      all.Count > checkable,
      "every reference resolved to a mod domain, which cannot be right while the recipes are "
        + "built out of vanilla stock - the vanilla codes are being dropped before they are counted."
    );
  }

  #endregion
}
