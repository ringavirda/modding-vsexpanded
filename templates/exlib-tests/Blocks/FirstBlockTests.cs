using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace YourMod.Tests;

/// <summary>The ten-minutes-to-a-green-test walk: place a bare block on a <see cref="TestWorld"/>
/// and read it back. Swap <see cref="Block"/> for your own block class and its real code once you
/// have one - this only proves the harness itself is wired up.</summary>
public class FirstBlockTests {
  [Fact]
  public void Places_a_bare_block_and_reads_it_back() {
    var world = new TestWorld();
    var pos = new BlockPos(0, 0, 0);
    Block block = TestBlocks.Configure(new Block(), "YourModProject:examplecode", 1);

    world.Place(pos, block);

    Assert.Equal(block, world.GetBlock(pos));
  }
}
