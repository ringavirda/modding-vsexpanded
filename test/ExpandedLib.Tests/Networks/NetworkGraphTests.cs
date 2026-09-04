using ExpandedLib.Blocks.Networks;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using ExpandedLib.Testing.Doubles;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Exercises the graph engine in <see cref="BlockNetworkModSystem"/> - node add/merge, BFS
/// fracture on removal, and root rebuild - using a medium-less <see cref="StubNetwork"/> so only
/// topology behaviour is under test.
/// </summary>
public class NetworkGraphTests {
  private static TestWorld NewWorld() {
    var w = new TestWorld();
    w.RegisterNetwork("test", sys => new StubNetwork(sys));
    return w;
  }

  /// <summary>Places a straight "ns" run along +Z at z=0..count-1, each cell a block entity carrying
  /// a membership that registers the cell as it is placed - so the first is isolated and every later
  /// one merges into the run, which is the order the engine places in.</summary>
  private static BlockPos[] BuildLine(TestWorld w, int count) {
    var positions = new BlockPos[count];
    for (int z = 0; z < count; z++) {
      positions[z] = new BlockPos(0, 0, z);
      w.PlaceNode(positions[z], "test", "ns");
    }
    return positions;
  }

  [Fact]
  public void AddNode_isolated_creates_standalone_network() {
    var w = NewWorld();
    var pos = new BlockPos(0, 0, 0);
    w.Place(pos, TestNetworkBlock.Create("test", "ns", 1));

    w.AddNode(pos, "test");

    var net = w.NetworkAt(pos);
    Assert.NotNull(net);
    Assert.Single(net!.Nodes);
  }

  [Fact]
  public void AddNode_with_an_unregistered_type_logs_and_adds_no_node() {
    // AddNode runs inside chunk load, so this used to throw a world down over one mistyped network
    // declaration. Only an isolated node reaches the factory - one placed against an existing run
    // joins that network instead - so the crash was intermittent and position-dependent, which is the
    // worst shape for a first-time user of the framework.
    var w = NewWorld();
    var pos = new BlockPos(0, 0, 0);
    w.Place(pos, TestNetworkBlock.Create("gass", "ns", 1));

    w.AddNode(pos, "gass");

    Assert.Null(w.NetworkAt(pos));
  }

  [Fact]
  public void AddNode_adjacent_same_type_merges_into_one_network() {
    var w = NewWorld();
    var positions = BuildLine(w, 3);

    var net = w.NetworkAt(positions[0]);
    Assert.NotNull(net);
    Assert.Equal(3, net!.Nodes.Count);
    // All cells resolve to the very same network instance.
    Assert.Same(net, w.NetworkAt(positions[1]));
    Assert.Same(net, w.NetworkAt(positions[2]));
  }

  [Fact]
  public void RemoveNode_middle_fractures_into_two_networks() {
    var w = NewWorld();
    var positions = BuildLine(w, 3);

    w.RemoveNode(positions[1]);

    Assert.Null(w.NetworkAt(positions[1]));
    var left = w.NetworkAt(positions[0]);
    var right = w.NetworkAt(positions[2]);
    Assert.NotNull(left);
    Assert.NotNull(right);
    Assert.NotSame(left, right);
    Assert.Single(left!.Nodes);
    Assert.Single(right!.Nodes);
  }

  [Fact]
  public void RemoveNode_end_keeps_remainder_connected() {
    var w = NewWorld();
    var positions = BuildLine(w, 3);

    w.RemoveNode(positions[2]);

    var net = w.NetworkAt(positions[0]);
    Assert.NotNull(net);
    Assert.Equal(2, net!.Nodes.Count);
    Assert.Same(net, w.NetworkAt(positions[1]));
  }

  [Fact]
  public void A_plain_block_carrying_a_membership_bridges_two_nodes() {
    // Membership is a property of the cell, not a kind of block. Bridging is the only assertion that
    // can fail: an isolated AddNode always creates a standalone network, so a lone member-bearing
    // cell reads as "on a network" whether or not the walk can see it.
    var w = NewWorld();
    w.PlaceNode(new BlockPos(0, 0, 0), "test", "ns");
    w.PlaceMemberBlock(new BlockPos(0, 0, 1), "test", "ns");
    w.PlaceNode(new BlockPos(0, 0, 2), "test", "ns");

    var net = w.NetworkAt(new BlockPos(0, 0, 0));

    Assert.NotNull(net);
    Assert.Equal(3, net!.Nodes.Count);
    Assert.Same(net, w.NetworkAt(new BlockPos(0, 0, 2)));
  }

  [Fact]
  public void The_walk_reaches_a_plain_member_from_both_directions() {
    // The source and the neighbour sides resolve a cell separately, so flipping one alone leaves a
    // member-bearing block walkable from but never to - a network that exists in one direction only.
    var w = NewWorld();
    var node = new BlockPos(0, 0, 0);
    var member = new BlockPos(0, 0, 1);
    w.PlaceNode(node, "test", "ns");
    w.PlaceMemberBlock(member, "test", "ns");
    // The premise: nothing about the placed block puts it on a network, so only its membership can.
    Assert.IsNotAssignableFrom<INetworkConnector>(w.GetBlock(member));

    Assert.Equal(
      member,
      Assert.Single(w.Networks.GetConnectedNeighbors(w.Accessor, node, "test"))
    );
    Assert.Equal(
      node,
      Assert.Single(
        w.Networks.GetConnectedNeighbors(w.Accessor, member, "test")
      )
    );
  }

  [Fact]
  public void RebuildFromRoot_preserves_root_network_state() {
    var w = NewWorld();
    var positions = BuildLine(w, 3);
    ((StubNetwork)w.NetworkAt(positions[0])!).Tag = "hot";

    var rebuilt = w.Networks.RebuildFromRoot(w.Accessor, positions[0], "test");

    Assert.NotNull(rebuilt);
    Assert.Equal(3, rebuilt!.Nodes.Count);
    Assert.Equal("hot", ((StubNetwork)rebuilt).Tag);
  }
}
