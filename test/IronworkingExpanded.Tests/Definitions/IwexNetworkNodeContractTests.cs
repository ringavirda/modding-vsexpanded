using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Every <c>iwex</c> network node must declare at least one <c>type</c> variant state.
/// <para>
/// A node that loses its <c>type</c> group does not fail loudly - it gets an <b>empty</b>
/// <c>AllowedOrientations</c> (<c>ExDefinitions.OrientationMap</c> has nothing to contribute when
/// there are no type states) and simply becomes impossible to place. No exception, no log line, no failing
/// assertion. See <see cref="NetworkNodeContract"/> for why this is the trap the code-naming rename
/// walks straight into: the redundant-looking single-state <c>type</c> on a stuttering code
/// (once <c>tuyere-tuyere-*</c>, now <c>furnace-tuyere-*</c>) is load-bearing, and it is the
/// <i>code</i> that moved, not the group.
/// </para>
/// </summary>
public class IwexNetworkNodeContractTests
{
  private static readonly Assembly Mod = typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

  [Fact]
  public void Every_network_node_declares_at_least_one_type_state()
  {
    var violations = NetworkNodeContract.Violations("iwex", Mod);

    Assert.True(
      violations.Count == 0,
      $"{violations.Count} network node definition(s) break the type-group contract:\n  "
        + string.Join("\n  ", violations)
    );
  }
}
