using System.Linq;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using ExpandedLib.Testing.Doubles;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The type-keyed membership accessor and the two-arm resolver. Vanilla's
/// <c>GetBehavior&lt;T&gt;()</c> returns the FIRST match, so a block entity on two networks needs an
/// accessor that selects by network type instead.
/// </summary>
public class NetworkMembershipTests {
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

  #region Graph registration

  private static TestWorld NewGraphWorld() {
    var w = new TestWorld();
    w.RegisterNetwork("test", sys => new StubNetwork(sys));
    w.RegisterNetwork("molten", sys => new StubNetwork(sys, "molten"));
    return w;
  }

  [Fact]
  public void A_membership_registers_its_position_as_a_graph_node() {
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = TestMemberBlockEntity.With(w, pos, "test");

    w.Initialize(be);

    Assert.NotNull(w.NetworkAt(pos));
  }

  [Fact]
  public void Removing_the_block_unregisters_the_membership() {
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = TestMemberBlockEntity.With(w, pos, "test");
    w.Initialize(be);
    Assert.NotNull(w.NetworkAt(pos));

    be.OnBlockRemoved();

    Assert.Null(w.NetworkAt(pos));
  }

  [Fact]
  public void A_chunk_unload_keeps_the_node_and_leaves_the_block_placed() {
    // The asymmetry the whole design rests on: an unload drops the block entity while the block stays
    // placed, so deregistering here would fracture a live run every time a player walked away.
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = TestMemberBlockEntity.With(w, pos, "test");
    w.Initialize(be);
    Block placed = w.GetBlock(pos);
    Assert.NotNull(w.NetworkAt(pos));

    w.Unload(pos);

    Assert.NotNull(w.NetworkAt(pos));
    Assert.Null(w.GetBlockEntity(pos));
    Assert.Same(placed, w.GetBlock(pos));
  }

  [Fact]
  public void A_network_node_registers_under_its_own_network_type() {
    // BlockEntityFluidIntake is the live case: its NetworkType is a plain auto-property that the save
    // tree fills in, and FromTreeAttributes runs before Initialize. A membership that took the type
    // any earlier than registration would register the wrong network - or none at all.
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = new SeverableNode();
    w.Place(pos, TestNetworkBlock.Create("molten", "ns", id: 906), be);
    var tree = new TreeAttribute();
    tree.SetString("networkType", "molten");
    be.FromTreeAttributes(tree, w.World);

    w.Initialize(be);

    Assert.Equal("molten", w.NetworkAt(pos)?.NetworkType);
  }

  [Fact]
  public void Saved_network_state_survives_a_reload_next_to_a_live_run() {
    // The restore handshake, in the shape that can lose it: joining an existing run makes AddNode
    // broadcast, and that broadcast reaches OnNetworkUpdate and clears the saved state. The state to
    // restore therefore has to be read before registration, not after.
    var w = NewGraphWorld();
    var block = TestNetworkBlock.Create("test", "ns", id: 907);
    var anchor = new BlockPos(0, 0, 0);
    var reloading = new BlockPos(0, 0, 1);
    w.Place(anchor, block, new TaggedNode());
    w.Place(reloading, block, new TaggedNode());
    w.Initialize(w.GetBlockEntity(anchor)!);
    w.Initialize(w.GetBlockEntity(reloading)!);

    BlockNetwork net = w.NetworkAt(anchor)!;
    net.RestoreState("hot");
    net.BroadcastUpdate(w.Accessor); // both nodes cache the state for the next load
    net.RestoreState(null); // the run itself forgets, as a server restart does
    w.RemoveNode(reloading); // and this cell's node goes with its chunk

    w.Reload(reloading);

    Assert.Equal("hot", w.NetworkAt(anchor)?.State);
  }

  /// <summary>A network node that persists whatever network state it was last broadcast, so a reload
  /// can hand it back. <c>StubNetwork</c> state is a plain string.</summary>
  private sealed class TaggedNode : BlockEntityNetworkNode {
    public override string NetworkType { get; set; } = "test";

    protected override object? DeserializeNetworkState(ITreeAttribute tree) =>
      tree.HasAttribute("netTag") ? tree.GetString("netTag") : null;

    protected override void SerializeNetworkState(
      ITreeAttribute tree,
      object? state
    ) {
      if (state is string tag)
        tree.SetString("netTag", tag);
    }
  }

  #endregion

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
