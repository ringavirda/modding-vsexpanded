using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Every <c>iiex</c> network node must declare at least one <c>type</c> variant state. A node without
/// one gets an empty <c>AllowedOrientations</c> (<c>ExDefinitions.OrientationMap</c> has no type states
/// to map from) and becomes impossible to place, with no exception and no log line. A single-state
/// <c>type</c> group is load-bearing for that reason. See <see cref="NetworkNodeContract"/>.
/// </summary>
public class IiexNetworkNodeContractTests {
  private static readonly Assembly Mod =
    typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

  [Fact]
  public void Every_network_node_declares_at_least_one_type_state() {
    var violations = NetworkNodeContract.Violations("iiex", Mod);

    Assert.True(
      violations.Count == 0,
      $"{violations.Count} network node definition(s) break the type-group contract:\n  "
        + string.Join("\n  ", violations)
    );
  }

  [Fact]
  public void Every_network_node_declares_the_scheme_it_actually_ships() {
    var violations = NetworkNodeContract.SchemeViolations(
      "iiex",
      Mod,
      out int defsChecked
    );

    Assert.True(
      violations.Count == 0,
      $"{violations.Count} network node definition(s) break the scheme contract:\n  "
        + string.Join("\n  ", violations)
    );
    Assert.True(defsChecked > 0, "the scheme contract examined no definitions");
  }
}
