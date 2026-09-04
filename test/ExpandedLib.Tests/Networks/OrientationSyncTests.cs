using ExpandedLib.Blocks.Networks;
using ExpandedLib.Testing;
using ExpandedLib.Testing.Doubles;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// What <see cref="BlockNetworkNode.RecalculateAndSyncOrientations"/> writes into a block entity. The
/// method takes the position it is to update, so <c>this</c> is not the block being updated: wrenching
/// one node runs it against each of that node's neighbours in turn. Everything it writes must therefore
/// come from the block it resolved at that position.
/// </summary>
public class OrientationSyncTests {
  private static TestWorld NewWorld() {
    var w = new TestWorld();
    w.RegisterNetwork("pipe", sys => new StubNetwork(sys, "pipe"));
    return w;
  }

  [Fact]
  public void A_neighbours_block_entity_gets_its_own_orientation_not_the_initiators() {
    // The six calls from Rotate pass a neighbour's position, so the initiator's orientation string is
    // simply the wrong value; it would be persisted and synced to every client watching that cell.
    var w = NewWorld();
    var neighbour = new BlockPos(0, 0, 8); // far enough that the two do not couple

    var initiator = TestNetworkBlock.Create("pipe", "we", id: 970);
    w.Place(new BlockPos(0, 0, 0), initiator);

    var be = new BlockEntityPipe();
    w.Place(neighbour, TestNetworkBlock.Create("pipe", "ns", id: 971), be);
    w.Initialize(be);

    initiator.RecalculateAndSyncOrientations(w.World, neighbour);

    Assert.Equal("ns", be.Orientation);
  }

  [Fact]
  public void Recalculating_a_nodes_own_position_still_writes_that_orientation() {
    // The placement path calls it with `this` at `pos`, where initiator and target coincide. It must
    // keep working, or the fix above would be indistinguishable from writing nothing at all.
    var w = NewWorld();
    var pos = new BlockPos(0, 0, 0);

    var block = TestNetworkBlock.Create("pipe", "we", id: 972);
    var be = new BlockEntityPipe();
    w.Place(pos, block, be);
    w.Initialize(be);

    block.RecalculateAndSyncOrientations(w.World, pos);

    Assert.Equal("we", be.Orientation);
    Assert.NotEmpty(be.PossibleOrientations);
  }
}
