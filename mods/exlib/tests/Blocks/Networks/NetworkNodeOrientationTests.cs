using System.Linq;
using ExpandedLib.Blocks.Behaviors;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Testing;
using ExpandedLib.Testing.Doubles;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The two places a <see cref="BlockNetworkNode"/> swaps itself onto another orientation: the wrench
/// (<c>Rotate</c>) and the neighbour scan (<c>RecalculateAndSyncOrientations</c>). Both go through
/// <see cref="BlockBehaviorExOrientable.ApplyOrientation"/>, and both are driven here through a real
/// node on a real graph - <c>ExOrientableRig</c> drives the behaviour in isolation and never through a
/// node, so passing its cases says nothing about this path.
/// </summary>
public class NetworkNodeOrientationTests {
  private static readonly string[] Axis = ["ns", "we", "ud"];
  private static readonly BlockPos Centre = new(0, 0, 0);

  #region The wrench

  [Fact]
  public void A_wrenched_node_wears_the_next_declared_token() {
    var scene = Scene.Standing("ns");

    scene.Wrench(1);

    Assert.Equal("test:node-we", scene.CodeAtCentre);
  }

  [Fact]
  public void A_wrenched_node_is_re_registered_with_its_new_connector_faces() {
    // The exchange sits between RemoveNode and AddNode. Moved out of that sandwich the graph keeps the
    // old connector faces while the block wears the new ones - a run that reads as connected and moves
    // nothing - so the graph is what is asserted, not the block code.
    var scene = Scene.Standing("ns");
    BlockPos alongZ = new(0, 0, 1);
    BlockPos alongX = new(1, 0, 0);
    scene.World.PlaceNode(alongZ, "test", "ns", id: 50);
    scene.World.PlaceNode(alongX, "test", "we", id: 51);
    scene.World.AddNode(Centre, "test");

    Assert.Same(scene.NetworkAtCentre, scene.World.NetworkAt(alongZ));

    scene.Wrench(1);

    Assert.Same(scene.NetworkAtCentre, scene.World.NetworkAt(alongX));
    Assert.NotSame(scene.NetworkAtCentre, scene.World.NetworkAt(alongZ));
  }

  [Fact]
  public void A_wrenched_node_is_marked_dirty_for_the_client() {
    // A headless suite cannot see a missing client mesh update: the block swaps, every server-side
    // assertion passes, and the player keeps looking at the old shape. This assertion is the only
    // thing in the repo that would notice.
    var scene = Scene.Standing("ns");

    scene.Wrench(1);

    scene.World.Accessor.Received().MarkBlockDirty(Centre);
  }

  [Fact]
  public void A_node_with_no_orientable_behaviour_refuses_the_wrench_and_says_so() {
    // Every node def declares the behaviour and a per-mod contract holds them to it, so reaching here
    // means a def was authored without it. Refusing states that; swapping the block by hand instead
    // would keep the wrench working and leave the missing declaration invisible.
    var scene = Scene.Standing("ns");
    scene.StripOrientableBehaviour();

    scene.Wrench(1);

    Assert.Equal("test:node-ns", scene.CodeAtCentre);
    Assert.Contains(
      scene.World.World.Logger.ReceivedCalls(),
      c => c.GetMethodInfo().Name == nameof(ILogger.Error)
    );
  }

  #endregion

  #region The neighbour scan

  [Fact]
  public void A_node_whose_orientation_its_neighbours_forbid_is_swapped_off_it() {
    // Two compatible neighbours on the X axis with no connector facing back forbid `w` and `e`, which
    // leaves this node's own `we` outside the valid set.
    var scene = Scene.Standing("we");
    scene.World.PlaceNode(new BlockPos(1, 0, 0), "test", "ns", id: 50);
    scene.World.PlaceNode(new BlockPos(-1, 0, 0), "test", "ns", id: 51);

    scene.Recalculate();

    Assert.Equal("test:node-ns", scene.CodeAtCentre);
  }

  [Fact]
  public void A_recalculation_that_changes_nothing_exchanges_nothing() {
    // This runs on every neighbour notification, so a second exchange onto the token already worn
    // would be a mesh rebuild and a client packet per notification, forever.
    var scene = Scene.Standing("we");
    scene.World.PlaceNode(new BlockPos(1, 0, 0), "test", "ns", id: 50);
    scene.World.PlaceNode(new BlockPos(-1, 0, 0), "test", "ns", id: 51);
    scene.Recalculate();
    scene.World.Accessor.ClearReceivedCalls();

    scene.Recalculate();

    scene
      .World.Accessor.DidNotReceive()
      .ExchangeBlock(Arg.Any<int>(), Arg.Any<BlockPos>());
  }

  [Fact]
  public void A_recalculated_node_carries_its_new_choices_on_its_block_entity() {
    var scene = Scene.Standing("we");
    scene.World.PlaceNode(new BlockPos(1, 0, 0), "test", "ns", id: 50);
    scene.World.PlaceNode(new BlockPos(-1, 0, 0), "test", "ns", id: 51);

    scene.Recalculate();

    Assert.Equal(["ns", "ud"], scene.Entity.PossibleOrientations);
  }

  #endregion

  #region The scene

  /// <summary>One node of an <c>Axis</c> family standing at <see cref="Centre"/> on a live graph, with
  /// the block entity the two swap paths both require.</summary>
  private sealed class Scene {
    private readonly TestNetworkBlock[] _family;

    private Scene(
      TestWorld world,
      TestNetworkBlock[] family,
      OrientableNode entity
    ) {
      World = world;
      _family = family;
      Entity = entity;
    }

    public TestWorld World { get; }
    public OrientableNode Entity { get; }

    public string? CodeAtCentre => World.GetBlock(Centre).Code?.ToString();

    public ExpandedLib.Networks.BlockNetwork? NetworkAtCentre =>
      World.NetworkAt(Centre);

    public static Scene Standing(string token) {
      var world = new TestWorld();
      world.RegisterNetwork("test", sys => new StubNetwork(sys));

      TestNetworkBlock[] family = TestNetworkBlock.Family(
        "test",
        "test:node",
        "Axis",
        Axis
      );
      foreach (TestNetworkBlock block in family)
        world.Register(block);

      var entity = new OrientableNode {
        Orientation = token,
        PossibleOrientations = Axis,
      };
      world.Place(Centre, family.First(b => b.Orientation == token), entity);
      world.Initialize(entity);

      return new Scene(world, family, entity);
    }

    /// <summary>Turns the wrench <paramref name="dir"/> steps, through the block now at the centre -
    /// never through the one the scene started with, which is a different variant instance.</summary>
    public void Wrench(int dir) {
      var holder = Substitute.For<EntityAgent>();
      holder.World = World.World;

      ((BlockNetworkNode)World.GetBlock(Centre)).Rotate(
        holder,
        new BlockSelection { Position = Centre.Copy(), Face = BlockFacing.UP },
        dir
      );
    }

    public void Recalculate() =>
      ((BlockNetworkNode)World.GetBlock(Centre)).RecalculateAndSyncOrientations(
        World.World,
        Centre
      );

    /// <summary>Takes the behaviour off every variant, leaving a node no swap path can reach. Both
    /// arrays, because <c>GetBehavior</c> reads only one of them and clearing the other would leave the
    /// behaviour findable.</summary>
    public void StripOrientableBehaviour() {
      foreach (TestNetworkBlock block in _family) {
        block.BlockBehaviors = [];
        block.CollectibleBehaviors = [];
      }
    }
  }

  #endregion
}
