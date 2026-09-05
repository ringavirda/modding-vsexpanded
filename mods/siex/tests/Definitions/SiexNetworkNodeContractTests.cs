using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Every <c>siex</c> network node must declare at least one <c>type</c> variant state. Without one,
/// <c>ExDefinitions.OrientationMap</c> yields an empty <c>AllowedOrientations</c> and the node becomes
/// impossible to place, with no exception and no log line. A single-state <c>type</c> group is
/// load-bearing even where it looks redundant. See <see cref="NetworkNodeContract"/>.
/// </summary>
public class SiexNetworkNodeContractTests {
  private static readonly Assembly Mod =
    typeof(SteelIndustryExpanded.BlockStructures.SmokeStack.Blocks.BlockSmokeStackIntake).Assembly;

  [Fact]
  public void Every_network_node_declares_at_least_one_type_state() {
    var violations = NetworkNodeContract.Violations("siex", Mod);

    Assert.True(
      violations.Count == 0,
      $"{violations.Count} network node definition(s) break the type-group contract:\n  "
        + string.Join("\n  ", violations)
    );
  }

  [Fact]
  public void Every_network_node_declares_the_scheme_it_actually_ships() {
    var violations = NetworkNodeContract.SchemeViolations(
      "siex",
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
