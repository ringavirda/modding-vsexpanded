using System.Reflection;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using ExpandedLib.Registries.Entities;
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
/// A mega-block footprint cell as a graph node. A filler is a plain <see cref="Block"/>, so it could
/// never be walked while the graph resolved a node by block type; it joins through a membership on its
/// block entity instead. The cell's other arm - the fixed <c>PortFace</c>/<c>PortNetworkType</c> a
/// principal writes onto the same block entity - stays what it is, a face a network couples to on a
/// cell that is not itself a node. See docs/design/mechanics/multiblock.md.
/// </summary>
public class FillerNodeTests {
  private static TestWorld NewWorld() {
    var w = new TestWorld();
    w.RegisterNetwork("test", sys => new StubNetwork(sys));
    w.RegisterNetwork("molten", sys => new StubNetwork(sys, "molten"));
    return w;
  }

  #region The retired invariant

  [Fact]
  public void A_filler_cell_bridges_two_nodes_on_opposite_sides_of_itself() {
    var w = NewWorld();
    w.PlaceNode(new BlockPos(0, 0, 0), "test", "ns");
    w.PlaceFillerNode(new BlockPos(0, 0, 1), "test", "ns");
    w.PlaceNode(new BlockPos(0, 0, 2), "test", "ns");
    // The premise the cell exists to prove: nothing about the block in the middle is a network block,
    // so only its membership can carry the run across it. Swapping a node block in here would leave
    // the test green while it stopped proving anything.
    Assert.IsNotAssignableFrom<BlockNetworkNode>(
      w.GetBlock(new BlockPos(0, 0, 1))
    );

    var net = w.NetworkAt(new BlockPos(0, 0, 0));

    Assert.NotNull(net);
    Assert.Equal(3, net!.Nodes.Count);
    Assert.Same(net, w.NetworkAt(new BlockPos(0, 0, 2)));
  }

  [Fact]
  public void The_walk_reaches_a_filler_cell_from_both_directions() {
    // Source and neighbour resolve the same way, so the cell has to be reachable from the node as well
    // as able to reach it. A one-directional answer still bridges when the cells are placed in one
    // order and silently does not in the other.
    var w = NewWorld();
    var node = new BlockPos(0, 0, 0);
    var cell = new BlockPos(0, 0, 1);
    w.PlaceNode(node, "test", "ns");
    w.PlaceFillerNode(cell, "test", "ns");

    Assert.Contains(
      cell,
      w.Networks.GetConnectedNeighbors(w.Accessor, node, "test")
    );
    Assert.Contains(
      node,
      w.Networks.GetConnectedNeighbors(w.Accessor, cell, "test")
    );
  }

  [Fact]
  public void A_filler_cell_bridges_whichever_order_its_neighbours_arrive_in() {
    // The bridge built the other way round: the filler cell is placed first and the two nodes join it.
    var w = NewWorld();
    w.PlaceFillerNode(new BlockPos(0, 0, 1), "test", "ns");
    w.PlaceNode(new BlockPos(0, 0, 2), "test", "ns");
    w.PlaceNode(new BlockPos(0, 0, 0), "test", "ns");

    var net = w.NetworkAt(new BlockPos(0, 0, 2));

    Assert.NotNull(net);
    Assert.Equal(3, net!.Nodes.Count);
    Assert.Same(net, w.NetworkAt(new BlockPos(0, 0, 0)));
  }

  [Fact]
  public void A_filler_cell_with_no_membership_is_still_not_a_node() {
    // The limitation is gone, not inverted: a plain footprint cell is still empty space to the graph,
    // so a mega-block does not start bridging the pipes its footprint happens to touch.
    var w = NewWorld();
    var cell = new BlockPos(0, 0, 1);
    w.PlaceNode(new BlockPos(0, 0, 0), "test", "ns");
    w.PlaceFiller(cell);
    w.PlaceNode(new BlockPos(0, 0, 2), "test", "ns");

    Assert.Null(NetworkMembership.Resolve(w.Accessor, cell, "test"));
    Assert.Null(w.NetworkAt(cell));
    Assert.Single(w.NetworkAt(new BlockPos(0, 0, 0))!.Nodes);
    Assert.NotSame(
      w.NetworkAt(new BlockPos(0, 0, 0)),
      w.NetworkAt(new BlockPos(0, 0, 2))
    );
  }

  [Fact]
  public void A_filler_cell_couples_only_on_the_face_it_was_given() {
    // A single-face port is the common shape and must not open its far side: a cell that coupled on
    // the opposite face too would let a pipe laid against the back of a boiler tap its steam.
    var w = NewWorld();
    var cell = new BlockPos(0, 0, 1);
    w.PlaceFillerNode(cell, "test", "n");

    INetworkMember member = Assert.IsAssignableFrom<INetworkMember>(
      NetworkMembership.Resolve(w.Accessor, cell, "test")
    );

    Assert.True(member.HasConnectorAt(w.Accessor, cell, BlockFacing.NORTH));
    Assert.False(member.HasConnectorAt(w.Accessor, cell, BlockFacing.SOUTH));
  }

  #endregion

  #region Membership and port on one cell

  [Fact]
  public void A_membership_answers_for_a_cell_that_also_carries_a_port() {
    // Both arms of the resolver can now answer for one footprint cell. The membership wins: it is the
    // cell's own participation and the thing that registered the node, while the port is the fallback
    // for a cell that has none. A port left on the cell therefore cannot move the node's faces.
    var w = NewWorld();
    var cell = new BlockPos(0, 0, 1);
    w.PlaceFillerNode(cell, "test", "ns");
    var be = (BlockEntityStructureFiller)w.GetBlockEntity(cell)!;
    be.PortFace = "e";
    be.PortNetworkType = "test";
    // The premise: the two arms disagree about this cell, so whichever answers is observable.
    Assert.True(
      ((INetworkMember)w.GetBlock(cell)).HasConnectorAt(
        w.Accessor,
        cell,
        BlockFacing.EAST
      )
    );

    INetworkMember? member = NetworkMembership.Resolve(
      w.Accessor,
      cell,
      "test"
    );

    Assert.IsAssignableFrom<BEBehaviorNetworkMember>(member);
    Assert.True(member!.HasConnectorAt(w.Accessor, cell, BlockFacing.NORTH));
    Assert.False(member.HasConnectorAt(w.Accessor, cell, BlockFacing.EAST));
  }

  [Fact]
  public void A_port_still_answers_for_a_network_no_membership_claims() {
    // The port mechanism survives, narrowed to what it always meant: a face another network couples to
    // on a cell that is not a graph member. The lancashire boiler's water intake is one of these.
    var w = NewWorld();
    var cell = new BlockPos(0, 0, 1);
    w.PlaceFillerNode(cell, "test", "n");
    var be = (BlockEntityStructureFiller)w.GetBlockEntity(cell)!;
    be.PortFace = "e";
    be.PortNetworkType = "molten";

    Assert.IsAssignableFrom<BEBehaviorNetworkMember>(
      NetworkMembership.Resolve(w.Accessor, cell, "test")
    );
    Assert.IsAssignableFrom<BlockStructureFiller>(
      NetworkMembership.Resolve(w.Accessor, cell, "molten")
    );
  }

  [Fact]
  public void A_membership_stating_no_faces_couples_on_the_cells_port_face() {
    // The two arms compose rather than compete when the membership states no geometry of its own: a
    // declaration without a face inherits the port's, which turns an existing port cell into a node
    // without restating where it couples.
    var w = NewWorld();
    var cell = new BlockPos(0, 0, 1);
    w.PlaceNode(new BlockPos(0, 0, 0), "test", "ns");
    PlacePortCell(w, cell, "n", "test", Declaring(null));

    Assert.Equal(2, w.NetworkAt(cell)?.Nodes.Count);
    Assert.Same(w.NetworkAt(cell), w.NetworkAt(new BlockPos(0, 0, 0)));
  }

  #endregion

  #region Lifecycle

  [Fact]
  public void Detaching_a_hosted_membership_drops_its_graph_node() {
    // Re-declaring a cell's behaviours detaches the previous set. One dropped from the list without
    // being told the cell is gone would leave a node at a position nothing owns: no block entity would
    // ever deregister it, and the next fracture walk would strand it in a fragment of its own.
    var w = NewWorld();
    var cell = new BlockPos(0, 0, 1);
    w.PlaceFillerNode(cell, "test", "n");
    Assert.NotNull(w.NetworkAt(cell));

    ((BlockEntityStructureFiller)w.GetBlockEntity(cell)!).SetHostedBehaviors(
      null
    );

    Assert.Empty(NetworkMembership.MembersOf(w.GetBlockEntity(cell)));
    Assert.Null(w.NetworkAt(cell));
  }

  [Fact]
  public void A_reloaded_filler_cell_keeps_the_node_its_chunk_left_behind() {
    // A hosted behaviour's saved state is never replayed on the server, so a membership that needed
    // anything out of the save tree would come back mute. It carries nothing: the declaration lives on
    // the filler block entity, which does round-trip, and the membership re-registers from it.
    var w = NewWorld();
    var cell = new BlockPos(0, 0, 1);
    w.PlaceNode(new BlockPos(0, 0, 0), "test", "ns");
    w.PlaceFillerNode(cell, "test", "ns");
    w.PlaceNode(new BlockPos(0, 0, 2), "test", "ns");
    BlockNetwork before = w.NetworkAt(cell)!;
    Assert.Equal(3, before.Nodes.Count);

    w.Reload(cell);

    Assert.Same(before, w.NetworkAt(cell));
    Assert.Equal(3, w.NetworkAt(cell)?.Nodes.Count);
    Assert.Single(w.Networks.AllNetworks);
  }

  [Fact]
  public void A_filler_cell_on_the_client_joins_no_graph() {
    // The client runs the same hosting path, and registration is server-only. A membership that acted
    // on the client would build a second, private graph on every player's machine.
    var w = NewWorld();
    var cell = new BlockPos(0, 0, 1);
    w.Api.Side.Returns(EnumAppSide.Client);

    w.PlaceFillerNode(cell, "test", "ns");

    // The premise: the cell really did host a membership, so the silence below is the side check and
    // not a behaviour that was never built.
    Assert.Single(NetworkMembership.MembersOf(w.GetBlockEntity(cell)));
    Assert.Null(w.NetworkAt(cell));
    Assert.Empty(w.Networks.AllNetworks);
  }

  [Fact]
  public void Breaking_a_filler_cell_takes_its_node_with_it() {
    var w = NewWorld();
    var cell = new BlockPos(0, 0, 1);
    w.PlaceFillerNode(cell, "test", "n");
    Assert.NotNull(w.NetworkAt(cell));

    w.GetBlockEntity(cell)!.OnBlockRemoved();

    Assert.Null(w.NetworkAt(cell));
  }

  #endregion

  #region Registration

  [Fact]
  public void The_membership_behaviour_is_registered_as_a_block_entity_behaviour_class() {
    // Every gate class registration applies, in its order: the assembly scan finds the type, it carries
    // the behaviour-kind attribute, it satisfies the base type that attribute implies, and the key it
    // lands under is the one a fillerOffsets cell names. Without the attribute the class registry
    // cannot build it and a declaring cell logs an unknown-class warning and hosts nothing.
    Assembly exlib = typeof(BEBehaviorNetworkMember).Assembly;

    Assert.Contains(
      typeof(BEBehaviorNetworkMember),
      ReflectionScan.GetCandidateTypes(exlib)
    );
    Assert.NotNull(
      typeof(BEBehaviorNetworkMember).GetCustomAttribute<BlockEntityBehaviorRegisterAttribute>()
    );
    Assert.True(
      typeof(BlockEntityBehavior).IsAssignableFrom(
        typeof(BEBehaviorNetworkMember)
      )
    );
    Assert.Equal(
      TestWorld.NetworkMemberClass,
      EntityRegistry.KeyFor("exlib", typeof(BEBehaviorNetworkMember))
    );
  }

  [Fact]
  public void A_declaring_cell_hosts_a_membership_built_by_the_class_registry() {
    var w = NewWorld();
    var cell = new BlockPos(0, 0, 1);

    w.PlaceFillerNode(cell, "test", "n");

    BEBehaviorNetworkMember member = Assert.Single(
      NetworkMembership.MembersOf(w.GetBlockEntity(cell))
    );
    Assert.Equal("test", member.NetworkType);
    Assert.Equal([BlockFacing.NORTH], member.Connectors);
  }

  #endregion

  #region Machine ports

  [Fact]
  public void A_machine_reads_the_network_across_a_face_into_a_filler_cell() {
    // The one place a network is looked up across a face rather than walked. It resolved the far cell
    // as a block, which cannot see a membership - so a machine sitting against a footprint cell that
    // is a node would read no network at all.
    var w = NewWorld();
    var machine = new BlockPos(0, 0, 0);
    var cell = new BlockPos(0, 0, 1);
    w.PlaceFillerNode(cell, "test", "ns");

    Assert.Same(
      w.NetworkAt(cell),
      w.Networks.GetConnectedNetworkAcross(
        w.Accessor,
        machine,
        BlockFacing.SOUTH
      )
    );
  }

  [Fact]
  public void A_machine_reads_no_network_across_a_face_the_cell_does_not_couple_on() {
    var w = NewWorld();
    var machine = new BlockPos(0, 0, 0);
    w.PlaceFillerNode(new BlockPos(0, 0, 1), "test", "s");

    Assert.Null(
      w.Networks.GetConnectedNetworkAcross(
        w.Accessor,
        machine,
        BlockFacing.SOUTH
      )
    );
  }

  #endregion

  #region Helpers

  /// <summary>One membership declaration for <c>network type "test"</c> on <paramref name="face"/>,
  /// the shape a <c>fillerOffsets</c> cell writes.</summary>
  private static FillerBehavior[] Declaring(BlockFacing? face) =>
    [
      new FillerBehavior(
        TestWorld.NetworkMemberClass,
        face,
        new JsonObject(JToken.Parse("{\"networkType\":\"test\"}"))
      ),
    ];

  /// <summary>Places a footprint cell carrying a fixed port and, optionally, hosted behaviours - the
  /// state a principal such as the boiler leaves behind when it marks its outlet cell.</summary>
  private static BlockEntityStructureFiller PlacePortCell(
    TestWorld w,
    BlockPos pos,
    string portFace,
    string portNetworkType,
    FillerBehavior[]? hosted
  ) {
    w.RegisterBlockEntityBehaviorFactory(
      TestWorld.NetworkMemberClass,
      be => new BEBehaviorNetworkMember(be)
    );
    var be = new BlockEntityStructureFiller {
      Principal = pos.AddCopy(0, -1, 0),
      PortFace = portFace,
      PortNetworkType = portNetworkType,
      HostedBehaviors = hosted,
    };
    w.Place(pos, w.Filler, be);
    w.Initialize(be);
    return be;
  }

  #endregion
}
