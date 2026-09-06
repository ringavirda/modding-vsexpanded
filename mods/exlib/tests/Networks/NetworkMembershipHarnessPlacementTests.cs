using ExpandedLib.Networks;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static ExpandedLib.Tests.NetworkMembershipFixtures;

namespace ExpandedLib.Tests;

public class NetworkMembershipHarnessPlacementTests {
  [Fact]
  public void PlaceNode_gives_the_cell_a_membership_resolvable_by_network_type() {
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);

    w.PlaceNode(pos, "test", "ns");

    Assert.Equal(
      "test",
      NetworkMembership.MemberOf(w.GetBlockEntity(pos), "test")?.NetworkType
    );
    Assert.Equal(
      "test",
      NetworkMembership.Resolve(w.Accessor, pos, "test")?.NetworkType
    );
  }

  [Fact]
  public void PlaceNode_registers_the_cell_exactly_once() {
    // The membership registers itself in Initialize, so a fixture that also called AddNode would add
    // the cell twice - and a second add of an isolated cell builds a whole second network over it,
    // stranding the first. Nodes is a set, so a node count alone never notices.
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);

    w.PlaceNode(pos, "test", "ns");

    Assert.Single(w.Networks.AllNetworks);
    Assert.Single(w.NetworkAt(pos)!.Nodes);
  }

  [Fact]
  public void PlaceMemberBlock_puts_a_membership_on_a_block_that_is_no_connector() {
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);

    w.PlaceMemberBlock(pos, "test", "ns");

    Block placed = w.GetBlock(pos);
    // The premise the cell exists for: nothing about the block makes it part of a network, so every
    // answer below comes from the membership. Swapping a node block in here would leave the guard
    // green while it stopped proving anything.
    Assert.IsNotAssignableFrom<INetworkConnector>(placed);
    Assert.IsNotAssignableFrom<BlockNetworkNode>(placed);

    INetworkMember member = Assert.Single(
      NetworkMembership.MembersOf(w.GetBlockEntity(pos))
    );

    Assert.True(member.HasConnectorAt(w.Accessor, pos, BlockFacing.NORTH));
    Assert.True(member.HasConnectorAt(w.Accessor, pos, BlockFacing.SOUTH));
    Assert.False(member.HasConnectorAt(w.Accessor, pos, BlockFacing.EAST));
  }
}
