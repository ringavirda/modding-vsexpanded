using System.Linq;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static ExpandedLib.Tests.NetworkMembershipFixtures;

namespace ExpandedLib.Tests;

public class NetworkMembershipDeclaredConnectorsTests {
  [Fact]
  public void A_memberships_declared_connectors_are_what_INetworkMember_reaches() {
    // Asked through INetworkMember, which is all the graph walk holds: a fresh member on a subclass
    // never enters the interface map, so a test through the concrete type would pass while the real
    // path answered from the base. The block under it is asserted to be no connector at all, leaving
    // the declaration as the only thing that can answer yes.
    var w = new TestWorld();
    var pos = new BlockPos(0, 0, 0);
    var block = TestBlocks.Configure(new Block(), "test:plain", 920);
    TestMemberBlockEntity be = TestMemberBlockEntity.Declaring("test", "ns");
    w.Place(pos, block, be);
    Assert.IsNotAssignableFrom<INetworkConnector>(block);

    INetworkMember member = Assert.Single(NetworkMembership.MembersOf(be));

    Assert.True(member.HasConnectorAt(w.Accessor, pos, BlockFacing.NORTH));
    Assert.True(member.HasConnectorAt(w.Accessor, pos, BlockFacing.SOUTH));
    Assert.False(member.HasConnectorAt(w.Accessor, pos, BlockFacing.EAST));
    Assert.False(member.HasConnectorAt(w.Accessor, pos, BlockFacing.UP));
  }

  [Fact]
  public void A_membership_declaring_nothing_answers_from_its_block() {
    // Every shipped network node relies on this: a membership added to a node block inherits the
    // block's orientation without restating it.
    var w = new TestWorld();
    var pos = new BlockPos(0, 0, 0);
    TestMemberBlockEntity be = TestMemberBlockEntity.Carrying("test");
    w.Place(pos, TestNetworkBlock.Create("test", "we", id: 921), be);

    BEBehaviorNetworkMember member = Assert.Single(
      NetworkMembership.MembersOf(be)
    );
    // The premise, asserted rather than assumed: a membership that had picked up a declaration from
    // somewhere would stop exercising the fallback while leaving this guard green.
    Assert.Empty(member.Connectors);

    Assert.True(
      ((INetworkMember)member).HasConnectorAt(w.Accessor, pos, BlockFacing.WEST)
    );
    Assert.False(
      ((INetworkMember)member).HasConnectorAt(
        w.Accessor,
        pos,
        BlockFacing.NORTH
      )
    );
  }

  [Fact]
  public void A_declared_connector_set_outranks_the_blocks_own() {
    var w = new TestWorld();
    var pos = new BlockPos(0, 0, 0);
    TestNetworkBlock block = TestNetworkBlock.Create("test", "ns", id: 922);
    TestMemberBlockEntity be = TestMemberBlockEntity.Declaring("test", "ud");
    w.Place(pos, block, be);
    // The premise: the two sources disagree on both faces asked about below.
    Assert.True(block.HasConnectorAt(BlockFacing.NORTH));
    Assert.False(block.HasConnectorAt(BlockFacing.UP));

    INetworkMember member = Assert.Single(NetworkMembership.MembersOf(be));

    Assert.True(member.HasConnectorAt(w.Accessor, pos, BlockFacing.UP));
    Assert.False(member.HasConnectorAt(w.Accessor, pos, BlockFacing.NORTH));
  }

  [Fact]
  public void Connectors_declared_in_json_reach_a_membership_that_states_none() {
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);
    TestMemberBlockEntity be = TestMemberBlockEntity.With(w, pos, "test");
    BEBehaviorNetworkMember member = NetworkMembership.MembersOf(be).Single();
    Declare(member, "{\"connectors\":\"ew\"}");

    w.Initialize(be);

    Assert.True(
      ((INetworkMember)member).HasConnectorAt(w.Accessor, pos, BlockFacing.EAST)
    );
    Assert.False(
      ((INetworkMember)member).HasConnectorAt(
        w.Accessor,
        pos,
        BlockFacing.NORTH
      )
    );
  }

  [Fact]
  public void Declared_connectors_lose_to_a_set_configured_in_code_and_say_so() {
    // A host that configured the membership named faces the cell actually exposes - a filler's port
    // arrives already rotated into the placed orientation - while a declaration is written in the
    // unrotated frame, so a declaration that won would move the port. Refused out loud, exactly as a
    // losing networkType declaration is, because silently ignoring one reads as if it took effect.
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);
    TestMemberBlockEntity be = TestMemberBlockEntity.Declaring("test", "ns");
    w.Place(pos, TestBlocks.Configure(new Block(), "test:plain", 923), be);
    BEBehaviorNetworkMember member = NetworkMembership.MembersOf(be).Single();
    Declare(member, "{\"connectors\":\"ew\"}");

    w.Initialize(be);

    Assert.True(
      ((INetworkMember)member).HasConnectorAt(
        w.Accessor,
        pos,
        BlockFacing.NORTH
      )
    );
    Assert.False(
      ((INetworkMember)member).HasConnectorAt(w.Accessor, pos, BlockFacing.EAST)
    );
    AssertErrorLogged(w, "already set", pos, "ew", "ns");
  }
}
