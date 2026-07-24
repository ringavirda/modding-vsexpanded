using ExpandedLib.Definitions;
using LowPressureExpanded.BlockNetworkPipe.Blocks;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// Behavioural (non-parity) checks on lpex's code-first defs - the things beyond byte-for-byte JSON that the
/// golden oracle (<see cref="LpexDefinitionGoldenTests"/>) doesn't cover: here, that a pipe block derives its
/// runtime orientation table from its own def's variant groups (no hand-kept duplicate list).
/// </summary>
public class LpexDefinitionBehaviorTests
{
  [Fact]
  public void AllowedOrientations_is_derived_from_the_defs_variant_groups()
  {
    // The runtime table comes from the same variant states the block is generated with.
    var outletOrientations = ExDefinitions.OrientationMap(
      BlockPipeOutlet.Definitions("lpex")
    );
    Assert.Equal(["s", "n", "w", "e", "u", "d"], outletOrientations["outlet"]);
  }
}
