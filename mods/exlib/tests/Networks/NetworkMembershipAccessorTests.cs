using System.Linq;
using ExpandedLib.Networks;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The type-keyed membership accessor and the two-arm resolver. Vanilla's
/// <c>GetBehavior&lt;T&gt;()</c> returns the FIRST match, so a block entity on two networks needs an
/// accessor that selects by network type instead.
/// </summary>
public class NetworkMembershipAccessorTests {
  [Fact]
  public void MemberOf_selects_by_network_type_not_by_clr_type() {
    var w = new TestWorld();
    var be = TestMemberBlockEntity.With(
      w,
      new BlockPos(0, 0, 0),
      "pipe",
      "molten"
    );

    Assert.Equal("pipe", NetworkMembership.MemberOf(be, "pipe")?.NetworkType);
    Assert.Equal(
      "molten",
      NetworkMembership.MemberOf(be, "molten")?.NetworkType
    );
    Assert.Null(NetworkMembership.MemberOf(be, "mpenergy"));
  }

  [Fact]
  public void MembersOf_returns_every_membership() {
    var w = new TestWorld();
    var be = TestMemberBlockEntity.With(
      w,
      new BlockPos(0, 0, 0),
      "pipe",
      "molten"
    );

    Assert.Equal(2, NetworkMembership.MembersOf(be).Count());
  }

  [Fact]
  public void A_block_entity_with_no_membership_answers_empty_rather_than_throwing() {
    var w = new TestWorld();
    var be = TestMemberBlockEntity.With(w, new BlockPos(0, 0, 0));

    Assert.Empty(NetworkMembership.MembersOf(be));
    Assert.Null(NetworkMembership.MemberOf(be, "pipe"));
  }

  [Fact]
  public void Resolve_prefers_a_membership_behaviour_over_the_block() {
    var w = new TestWorld();
    var pos = new BlockPos(0, 0, 0);
    // A network block AND a membership: the behaviour answers, so the type is the behaviour's.
    var be = TestMemberBlockEntity.With(w, pos, "molten");
    w.Place(pos, TestNetworkBlock.Create("pipe", "ns", id: 901), be);

    Assert.Equal(
      "molten",
      NetworkMembership.Resolve(w.Accessor, pos, "molten")?.NetworkType
    );
  }

  [Fact]
  public void Resolve_falls_back_to_the_block_when_no_block_entity_exists() {
    // The chunk-unload case: OnBlockUnloaded leaves the block placed and drops the block entity, so
    // a walk that only asked the block entity would fracture every network a player walked away from.
    var w = new TestWorld();
    var pos = new BlockPos(0, 0, 0);
    w.Place(pos, TestNetworkBlock.Create("pipe", "ns", id: 902));

    Assert.Equal(
      "pipe",
      NetworkMembership.Resolve(w.Accessor, pos, "pipe")?.NetworkType
    );
  }

  [Fact]
  public void Resolve_answers_null_for_a_different_network_type() {
    var w = new TestWorld();
    var pos = new BlockPos(0, 0, 0);
    w.Place(pos, TestNetworkBlock.Create("pipe", "ns", id: 903));

    Assert.Null(NetworkMembership.Resolve(w.Accessor, pos, "molten"));
  }

  [Fact]
  public void Resolve_reads_a_memberships_per_cell_network_type() {
    // Both arms have to key on NetworkTypeAt. Keying the behaviour arm on the declared NetworkType
    // instead leaves a membership whose type varies by cell - which is what a filler cell is -
    // unfindable at a position where the equivalent block would be found.
    var w = new TestWorld();
    var pos = new BlockPos(0, 0, 0);
    TestMemberBlockEntity.WithPerCellType(
      w,
      pos,
      declared: "pipe",
      atCell: "molten"
    );

    Assert.Equal(
      "molten",
      NetworkMembership
        .Resolve(w.Accessor, pos, "molten")
        ?.NetworkTypeAt(w.Accessor, pos)
    );
    Assert.Null(NetworkMembership.Resolve(w.Accessor, pos, "pipe"));
  }

  [Fact]
  public void Resolve_answers_null_for_a_plain_block() {
    var w = new TestWorld();
    var pos = new BlockPos(0, 0, 0);

    Assert.Null(NetworkMembership.Resolve(w.Accessor, pos, "pipe"));
  }

  [Fact]
  public void A_connectors_own_per_cell_answer_is_what_INetworkMember_reaches() {
    // The graph walk asks through INetworkMember, while a block answers the position-aware pair as
    // ordinary public class members - the structure filler reads its port off the cell's block
    // entity. Were the interface default reached instead, the filler would fall back to its
    // block-wide "no connector, no network" and a boiler port would stop joining its pipe.
    var w = new TestWorld();
    var pos = new BlockPos(0, 0, 0);
    var filler = TestBlocks.Configure(
      new BlockStructureFiller(),
      "exlib:structurefiller",
      904
    );
    w.Place(
      pos,
      filler,
      new BlockEntityStructureFiller {
        PortFace = "n",
        PortNetworkType = "pipe",
      }
    );

    INetworkMember member = filler;

    Assert.Equal("pipe", member.NetworkTypeAt(w.Accessor, pos));
    Assert.True(member.HasConnectorAt(w.Accessor, pos, BlockFacing.NORTH));
    Assert.False(member.HasConnectorAt(w.Accessor, pos, BlockFacing.SOUTH));
  }

  [Fact]
  public void A_network_blocks_per_cell_override_is_what_INetworkMember_reaches() {
    // The other shape: BlockNetworkNode declares the position-aware connector test as a class
    // virtual and a junction such as the bevel gear overrides it. Its orientation carries no
    // connectors, so only the override can answer true.
    var w = new TestWorld();
    var pos = new BlockPos(0, 0, 0);
    var junction = TestBlocks.Configure(
      new AllFacesNode(),
      "test:junction",
      905
    );
    w.Place(pos, junction);

    INetworkMember member = junction;

    Assert.False(junction.HasConnectorAt(BlockFacing.NORTH));
    Assert.True(member.HasConnectorAt(w.Accessor, pos, BlockFacing.NORTH));
  }

  /// <summary>A network block that decides its connectors per cell rather than from an orientation
  /// string, by overriding the position-aware form only.</summary>
  private sealed class AllFacesNode : BlockNetworkNode {
    public override string NetworkType => "test";

    public override bool HasConnectorAt(
      IBlockAccessor world,
      BlockPos pos,
      BlockFacing face
    ) => true;
  }
}
