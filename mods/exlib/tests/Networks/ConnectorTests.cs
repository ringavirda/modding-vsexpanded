using System.Linq;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Covers connector-level graph queries: reciprocal-connector requirement, open-end detection,
/// dynamic severing, and the static side/compatibility helpers.
/// </summary>
public class ConnectorTests {
  private static TestWorld NewWorld() {
    var w = new TestWorld();
    w.RegisterNetwork("test", sys => new StubNetwork(sys));
    return w;
  }

  [Fact]
  public void GetConnectedNeighbors_requires_reciprocal_connector() {
    var w = NewWorld();
    var a = new BlockPos(0, 0, 0);
    var b = new BlockPos(0, 0, 1); // south of A
    w.Place(a, TestNetworkBlock.Create("test", "ns", 1));
    // "we" exposes east/west connectors only - nothing facing back north at A.
    w.Place(b, TestNetworkBlock.Create("test", "we", 2));

    var neighbours = w.Networks.GetConnectedNeighbors(w.Accessor, a, "test");

    Assert.Empty(neighbours);
  }

  [Fact]
  public void GetConnectedNeighbors_links_when_both_faces_match() {
    var w = NewWorld();
    var a = new BlockPos(0, 0, 0);
    var b = new BlockPos(0, 0, 1);
    var block = TestNetworkBlock.Create("test", "ns", 1);
    w.Place(a, block);
    w.Place(b, block);

    var neighbours = w
      .Networks.GetConnectedNeighbors(w.Accessor, a, "test")
      .ToList();

    Assert.Single(neighbours);
    Assert.Equal(b, neighbours[0]);
  }

  [Fact]
  public void GetOpenConnectorFaces_reports_air_ends() {
    var w = NewWorld();
    var pos = new BlockPos(0, 0, 0);
    var block = TestNetworkBlock.Create("test", "ns", 1);
    w.Place(pos, block);

    var open = w.Networks.GetOpenConnectorFaces(w.Accessor, pos, block);

    Assert.Equal(2, open.Length);
    Assert.Contains(BlockFacing.NORTH, open);
    Assert.Contains(BlockFacing.SOUTH, open);
  }

  [Fact]
  public void GetOpenConnectorFaces_ignores_the_sources_own_severing() {
    // The traversal and the open-end scan share the neighbour-side gates but not the source's. A
    // severed cell contributes no graph edge, yet its faces are still physically joined: a closed
    // valve does not leak and a solidified canal does not grow an end cap where it meets its run.
    var w = NewWorld();
    var a = new BlockPos(0, 0, 0);
    var b = new BlockPos(0, 0, 1);
    var block = TestNetworkBlock.Create("test", "ns", 1);
    w.Place(a, block, new SeverableNode { Broken = true });
    w.Place(b, block);
    // The premise, asserted rather than assumed: severing has taken this cell off the graph, so a
    // guard that stopped severing it would read as green while proving nothing.
    Assert.Empty(w.Networks.GetConnectedNeighbors(w.Accessor, a, "test"));

    var open = w.Networks.GetOpenConnectorFaces(w.Accessor, a, block);

    // North meets air; south meets the neighbour it is coupled to.
    Assert.Equal(BlockFacing.NORTH, Assert.Single(open));
  }

  [Fact]
  public void GetOpenConnectorFaces_ignores_the_source_being_an_endpoint() {
    // Same rule for the other source gate: an endpoint terminates the run rather than passing it on,
    // but the pipe it terminates against is still coupled to it and is no leak.
    var w = NewWorld();
    var a = new BlockPos(0, 0, 0);
    var b = new BlockPos(0, 0, 1);
    var endpoint = TestBlocks.Configure(new EndPointNode(), "test:endpoint", 3);
    w.Place(a, endpoint);
    w.Place(b, TestNetworkBlock.Create("test", "ns", 1));
    // The premise: being an endpoint has taken this cell off the graph.
    Assert.Empty(w.Networks.GetConnectedNeighbors(w.Accessor, a, "test"));

    var open = w.Networks.GetOpenConnectorFaces(w.Accessor, a, endpoint);

    Assert.Equal(BlockFacing.NORTH, Assert.Single(open));
  }

  [Fact]
  public void IsConnectionBroken_severs_then_restores_traversal() {
    var w = NewWorld();
    var a = new BlockPos(0, 0, 0);
    var b = new BlockPos(0, 0, 1);
    var block = TestNetworkBlock.Create("test", "ns", 1);
    var severable = new SeverableNode { Broken = true };
    w.Place(a, block);
    w.Place(b, block, severable);

    Assert.Empty(w.Networks.GetConnectedNeighbors(w.Accessor, a, "test"));

    severable.Broken = false;
    Assert.Single(w.Networks.GetConnectedNeighbors(w.Accessor, a, "test"));
  }

  [Theory]
  [InlineData("n")]
  [InlineData("s")]
  [InlineData("e")]
  [InlineData("w")]
  [InlineData("u")]
  [InlineData("d")]
  public void SideToFace_maps_known_codes(string side) {
    var face = BlockNetworkModSystem.SideToFace(side);
    Assert.NotNull(face);
    Assert.Equal(side[0], face!.Code[0]);
  }

  [Fact]
  public void SideToFace_returns_null_for_unknown() {
    Assert.Null(BlockNetworkModSystem.SideToFace("x"));
    Assert.Null(BlockNetworkModSystem.SideToFace(null));
  }

  [Fact]
  public void IsCompatibleNetworkBlock_matches_only_same_type() {
    var block = TestNetworkBlock.Create("test", "ns", 1);
    Assert.True(BlockNetworkModSystem.IsCompatibleNetworkBlock(block, "test"));
    Assert.False(
      BlockNetworkModSystem.IsCompatibleNetworkBlock(block, "molten")
    );
  }

  /// <summary>A node that terminates a run at its own cell, as the pressure valve does, with a
  /// connector on each end of the north-south axis.</summary>
  private sealed class EndPointNode : BlockNetworkNode {
    public override string NetworkType => "test";

    public override bool IsNetworkEndPoint => true;

    public override bool HasConnectorAt(BlockFacing face) =>
      face == BlockFacing.NORTH || face == BlockFacing.SOUTH;
  }
}
