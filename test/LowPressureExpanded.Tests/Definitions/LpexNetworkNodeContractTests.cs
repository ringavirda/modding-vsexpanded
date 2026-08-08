using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// Every <c>lpex</c> network node must declare at least one <c>type</c> variant state. Without one
/// <c>ExDefinitions.OrientationMap</c> yields an empty <c>AllowedOrientations</c> and the block
/// becomes impossible to place, with no exception and no log line, so a single-state <c>type</c>
/// group is load-bearing even where it looks redundant. See <see cref="NetworkNodeContract"/>.
/// </summary>
public class LpexNetworkNodeContractTests {
  private static readonly Assembly Mod =
    typeof(LowPressureExpanded.LpexConfig).Assembly;

  [Fact]
  public void Every_network_node_declares_at_least_one_type_state() {
    var violations = NetworkNodeContract.Violations("lpex", Mod);

    Assert.True(
      violations.Count == 0,
      $"{violations.Count} network node definition(s) break the type-group contract:\n  "
        + string.Join("\n  ", violations)
    );
  }
}
