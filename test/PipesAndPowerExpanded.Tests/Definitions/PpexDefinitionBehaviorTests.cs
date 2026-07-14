using ExpandedLib.Definitions;
using PipesAndPowerExpanded.BlockNetworkPipe.Blocks;
using Xunit;

namespace PipesAndPowerExpanded.Tests;

/// <summary>
/// Behavioural (non-parity) checks on ppex's code-first defs - the things beyond byte-for-byte JSON that the
/// golden oracle (<see cref="PpexDefinitionGoldenTests"/>) doesn't cover: here, that a pipe block derives its
/// runtime orientation table from its own def's variant groups (no hand-kept duplicate list).
/// </summary>
public class PpexDefinitionBehaviorTests
{
  [Fact]
  public void AllowedOrientations_is_derived_from_the_defs_variant_groups()
  {
    // The runtime table comes from the same variant states the block is generated with.
    var outletOrientations = ExDefinitions.OrientationMap(
      BlockPipeOutlet.Definitions("ppex")
    );
    Assert.Equal(["s", "n", "w", "e", "u", "d"], outletOrientations["outlet"]);
  }
}
