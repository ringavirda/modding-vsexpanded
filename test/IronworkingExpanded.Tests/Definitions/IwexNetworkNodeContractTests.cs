using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Every <c>iwex</c> network node must declare at least one <c>type</c> variant state. A node without
/// one gets an empty <c>AllowedOrientations</c> (<c>ExDefinitions.OrientationMap</c> has no type states
/// to map from) and becomes impossible to place, with no exception and no log line. A single-state
/// <c>type</c> group is load-bearing for that reason. See <see cref="NetworkNodeContract"/>.
/// </summary>
public class IwexNetworkNodeContractTests {
  private static readonly Assembly Mod =
    typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

  [Fact]
  public void Every_network_node_declares_at_least_one_type_state() {
    var violations = NetworkNodeContract.Violations("iwex", Mod);

    Assert.True(
      violations.Count == 0,
      $"{violations.Count} network node definition(s) break the type-group contract:\n  "
        + string.Join("\n  ", violations)
    );
  }
}
