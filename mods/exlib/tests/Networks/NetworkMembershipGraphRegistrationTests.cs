using System.Linq;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;
using static ExpandedLib.Tests.NetworkMembershipFixtures;

namespace ExpandedLib.Tests;

public class NetworkMembershipGraphRegistrationTests {
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
}
