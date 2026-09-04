using System;
using System.Linq;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using ExpandedLib.Testing.Doubles;
using Newtonsoft.Json.Linq;
using NSubstitute;
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

  /// <summary>Puts a JSON <c>networkType</c> declaration on <paramref name="be"/>'s one membership,
  /// as <c>CreateBehaviors</c> does for a behaviour a block names in its <c>entityBehaviors</c>.</summary>
  private static void Declare(BlockEntity be, string networkType) =>
    Declare(
      NetworkMembership.MembersOf(be).Single(),
      $"{{\"networkType\":\"{networkType}\"}}"
    );

  /// <summary>Puts the JSON body <paramref name="json"/> on <paramref name="member"/> as its
  /// declaration.</summary>
  private static void Declare(BEBehaviorNetworkMember member, string json) =>
    member.properties = new JsonObject(JToken.Parse(json));

  /// <summary>
  /// Asserts the logger recorded an error whose format carries <paramref name="fragment"/> and whose
  /// arguments name every one of <paramref name="mustMention"/>. A log level has no observable other
  /// than the logger, and the format+args overload is pinned on purpose: rewriting the call as an
  /// interpolated string would give up the deferred formatting, which is a change worth noticing.
  /// </summary>
  private static void AssertErrorLogged(
    TestWorld w,
    string fragment,
    params object[] mustMention
  ) =>
    w
      .Api.Logger.Received()
      .Error(
        Arg.Is<string>(f => f.Contains(fragment, StringComparison.Ordinal)),
        Arg.Is<object[]>(args => mustMention.All(m => args.Contains(m)))
      );

  [Fact]
  public void An_attached_but_uninitialised_node_still_deregisters_on_removal() {
    // The harness pattern thirteen fixtures use: Attach hands the block entity an api, the graph node
    // is added by hand, and nothing ever calls Initialize. Only the block entity's own api is set that
    // way, so a teardown gated on the behaviour's would quietly never run and every one of those
    // fixtures would model something weaker than it reads as.
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = new SeverableNode();
    w.Place(pos, TestNetworkBlock.Create("test", "ns", id: 911), be);
    w.Attach(be);
    ReflectionHelpers.SetProperty(be, nameof(be.NetworkSystem), w.Networks);
    w.AddNode(pos, "test");
    // The premise, asserted rather than assumed: without it a harness that starts initialising
    // behaviours turns this guard into a tautology while leaving it green.
    Assert.Null(NetworkMembership.MembersOf(be).Single().Api);
    Assert.NotNull(w.NetworkAt(pos));

    be.OnBlockRemoved();

    Assert.Null(w.NetworkAt(pos));
  }

  [Fact]
  public void A_membership_naming_no_network_joins_nothing_rather_than_throwing() {
    // Registering a blank type throws out of AddNode's factory lookup, and this runs inside a chunk
    // load, so one bad declaration would take a world down. The cell is left off the graph instead -
    // and logged as an error, because a node silently outside its network reads to a player as "my
    // pipes stopped working" and there is nothing else to go on.
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = TestMemberBlockEntity.With(w, pos, "");

    w.Initialize(be);

    Assert.Null(w.NetworkAt(pos));
    AssertErrorLogged(w, "names no network type", pos);
  }

  [Fact]
  public void A_declared_network_type_reaches_a_membership_that_names_none() {
    // The one shape the JSON key is for: a membership with no other source. The write goes through
    // the hosted membership's forwarding setter, so the block entity ends up holding it too.
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = new SeverableNode { NetworkType = "" };
    w.Place(pos, TestNetworkBlock.Create("molten", "ns", id: 914), be);
    Declare(be, "molten");

    w.Initialize(be);

    Assert.Equal("molten", be.NetworkType);
    Assert.Equal("molten", w.NetworkAt(pos)?.NetworkType);
  }

  [Fact]
  public void A_declared_network_type_loses_to_the_block_entitys_own_and_says_so() {
    // A block entity that already names its network owns that answer - it is a compiled contract, and
    // every node family but the fluid intake implements the setter as a no-op, so a declaration that
    // "won" would vanish on the way through and register under the constant regardless. Refused out
    // loud instead, because both silent outcomes read to the author as if the JSON took effect.
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = new SeverableNode();
    w.Place(pos, TestNetworkBlock.Create("test", "ns", id: 915), be);
    Declare(be, "molten");

    w.Initialize(be);

    Assert.Equal("test", be.NetworkType);
    Assert.Equal("test", w.NetworkAt(pos)?.NetworkType);
    AssertErrorLogged(w, "already names", pos, "molten", "test");
  }

  [Fact]
  public void A_reloaded_block_entity_rejoins_the_node_its_chunk_left_behind() {
    // The path the design rests on, and the opposite of a removal: an unload keeps the node, so the
    // returning block entity must find it already there and skip AddNode. Registering again would
    // build a second network over the same cell and strand the first.
    var w = NewGraphWorld();
    var pos = new BlockPos(0, 0, 0);
    w.Place(
      pos,
      TestNetworkBlock.Create("test", "ns", id: 912),
      new TaggedNode()
    );
    w.Initialize(w.GetBlockEntity(pos)!);
    BlockNetwork before = w.NetworkAt(pos)!;

    w.Reload(pos);

    Assert.Same(before, w.NetworkAt(pos));
    Assert.Single(w.Networks.AllNetworks);
  }

  [Fact]
  public void A_hosted_membership_answers_its_block_entitys_type_through_the_interface() {
    // Read as INetworkMember, which is all the graph walk holds, and against a production node rather
    // than a double: interface mapping is fixed at the class listing the interface, so an override
    // that never entered the map compiles and is simply never called. Asked before Initialize as well,
    // where a membership holding its own copy of the type would still answer the base's empty default.
    var w = NewGraphWorld();
    w.RegisterNetwork("pipe", sys => new StubNetwork(sys, "pipe"));
    var pos = new BlockPos(0, 0, 0);
    var be = new BlockEntityPipe();
    w.Place(pos, TestNetworkBlock.Create("pipe", "ns", id: 913), be);

    INetworkMember member = Assert.Single(NetworkMembership.MembersOf(be));

    Assert.Equal("pipe", member.NetworkType);
    Assert.Equal("pipe", member.NetworkTypeAt(w.Accessor, pos));

    w.Initialize(be);

    Assert.Equal("pipe", w.NetworkAt(pos)?.NetworkType);
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

  #region Declared connectors

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

  #endregion

  #region Harness placement

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
