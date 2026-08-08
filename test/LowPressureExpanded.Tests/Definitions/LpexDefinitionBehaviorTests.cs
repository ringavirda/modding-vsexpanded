using ExpandedLib.Definitions;
using LowPressureExpanded.BlockNetworkPipe.Blocks;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// Behavioural checks on lpex's code-first defs, covering what the byte-for-byte golden oracle
/// (<see cref="LpexDefinitionGoldenTests"/>) does not: a pipe block derives its runtime orientation
/// table from its own def's variant groups rather than from a hand-kept duplicate list.
/// </summary>
public class LpexDefinitionBehaviorTests {
  [Fact]
  public void AllowedOrientations_is_derived_from_the_defs_variant_groups() {
    var outletOrientations = ExDefinitions.OrientationMap(
      BlockPipeOutlet.Definitions("lpex")
    );
    Assert.Equal(["s", "n", "w", "e", "u", "d"], outletOrientations["outlet"]);
  }
}
