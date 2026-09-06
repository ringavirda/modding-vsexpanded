using System.Linq;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The connector check: a layout cell marked <c>Connector</c> is satisfied only by an occupant whose
/// network connector opens the way the drawing says. Until this existed the only thing catching a
/// backwards tuyere was a hard orientation pin in the legend - which a network node's own neighbour scan
/// is free to overwrite.
/// <para>
/// Driven on a probe layout rather than a furnace, and through <see cref="StructureRig"/> rather than by
/// assigning <c>StructureComplete</c>: the machine completes itself or it does not.
/// </para>
/// </summary>
public class MultiblockConnectorsTests {
  private static readonly BlockPos Anchor = new(0, 10, 0);

  #region The demand

  [Fact]
  public void A_node_facing_into_the_structure_leaves_it_incomplete() {
    Assert.False(Scene.WithNode("s").Completes());
  }

  [Fact]
  public void A_node_facing_the_way_the_drawing_says_completes_it() {
    Assert.True(Scene.WithNode("n").Completes());
  }

  [Fact]
  public void A_node_whose_faces_are_a_superset_completes_it_too() {
    // The demand is a subset test, so a legitimate re-pick by the network - a straight run through the
    // wall rather than a stub into it - does not break a structure that already stands.
    Assert.True(Scene.WithNode("ns").Completes());
  }

  [Fact]
  public void A_plain_block_cannot_satisfy_a_connector_cell() {
    // The wanted code is a wildcard, so a block whose code happens to match would otherwise pass.
    // Nothing that is not on a network can answer an outward face.
    Assert.False(Scene.WithPlainBlock().Completes());
  }

  [Fact]
  public void The_demanded_face_turns_with_the_structure() {
    // Authored north; at 90 degrees the cell wants west, and a node still wearing north no longer
    // answers. A demand that did not rotate would pass here and fail on three of the four facings.
    Assert.False(Scene.WithNode("n", angle: 90).Completes());
    Assert.True(Scene.WithNode("w", angle: 90).Completes());
  }

  [Fact]
  public void A_misfaced_cell_is_reported_as_the_face_it_wants() {
    Scene scene = Scene.WithNode("s");

    scene.Completes();

    Assert.Contains("open to 'n'", scene.Rig.MissingReport);
  }

  #endregion

  #region The layout

  [Fact]
  public void A_layout_marking_no_connector_emits_no_attribute() {
    // The additive guarantee the facings and roles siblings shipped on: a layout that marks nothing
    // emits nothing, so no existing def moves and no migration is owed.
    JToken? attributes = ExBlockDef
      .Create("exlib", "probe", "plain")
      .MultiblockLayout(l =>
        l.Legend('M', "exlib:probe-*")
          .Legend('F', "exlib:filler")
          .Layer(0, "MF")
      )
      .ToJson()["attributes"];

    Assert.Null(attributes?["multiblockConnectors"]);
  }

  [Fact]
  public void A_connector_on_a_glyph_the_drawing_never_uses_is_refused() {
    // The same silent-empty-set failure a role glyph has, and the worse half of it: an undrawn demand
    // reads as a structure with no facing requirement at all, which completes with the node backwards.
    var thrown = Assert.Throws<System.InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "probe", "undrawn")
        .MultiblockLayout(l =>
          l.Legend('M', "exlib:probe-*")
            .Legend('Y', "exlib:probe-node-*")
            .Connector('Y', BlockFacing.NORTH)
            .Layer(0, "M")
        )
    );

    Assert.Contains("never draws", thrown.Message);
  }

  [Fact]
  public void A_connector_on_a_glyph_with_no_legend_entry_is_refused() {
    var thrown = Assert.Throws<System.InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "probe", "nolegend")
        .MultiblockLayout(l =>
          l.Legend('M', "exlib:probe-*")
            .Connector('Y', BlockFacing.NORTH)
            .Layer(0, "M")
        )
    );

    Assert.Contains("no Legend entry", thrown.Message);
  }

  [Fact]
  public void Two_faces_on_one_cell_are_both_demanded() {
    // The passthrough's shape: it must connect both ways through the wall, which is why Connector takes
    // several faces and why the emitted table is keyed by face rather than by cell.
    var connectors = (JObject)
      ExBlockDef
        .Create("exlib", "probe", "both")
        .MultiblockLayout(l =>
          l.Legend('M', "exlib:probe-*")
            .Legend('P', "exlib:probe-node-*")
            .Connector('P', BlockFacing.NORTH, BlockFacing.SOUTH)
            .Layer(0, "MP")
        )
        .ToJson()["attributes"]!["multiblockConnectors"]!;

    Assert.Equal(["n", "s"], connectors.Properties().Select(p => p.Name));
  }

  #endregion

  #region The scene

  /// <summary>
  /// A two-cell probe: the machine, and one cell south of it the drawing wants open to the north - the
  /// tuyere's own shape, reduced to what the check needs.
  /// </summary>
  private sealed class Scene {
    private readonly TestMegablock _machine;

    private Scene(StructureRig rig, TestMegablock machine) {
      Rig = rig;
      _machine = machine;
    }

    public StructureRig Rig { get; }

    public static Scene WithNode(string token, int angle = 0) =>
      Build(
        angle,
        TestNetworkBlock.Create("test", token, 900, $"exlib:probe-node-{token}")
      );

    public static Scene WithPlainBlock() =>
      Build(0, TestBlocks.Configure(new Block(), "exlib:probe-node-n", 901));

    private static Scene Build(int angle, Block occupant) {
      var world = new TestWorld();
      var machine = new TestMegablock { Angle = angle };
      world.Place(
        Anchor,
        TestBlocks.Configure(new Block(), "exlib:probe-n", 1),
        machine
      );
      world.Attach(machine);

      StructureRig rig = StructureRig.Around(world, machine, Def(), angle);
      rig.Occupy(rig.Cell(0, 0, 1), occupant);
      return new Scene(rig, machine);
    }

    /// <summary>Raises the footprint and lets the machine's own monitor decide.</summary>
    public bool Completes() {
      Rig.Raise();
      Rig.World.Initialize(_machine);
      return Rig.AwaitCompletion();
    }

    private static ExBlockDef Def() =>
      ExBlockDef
        .Create("exlib", "probe")
        .MultiblockLayout(l =>
          l.Legend('M', "exlib:probe-*")
            .Legend('Y', "exlib:probe-node-*")
            .Connector('Y', BlockFacing.NORTH)
            .Layer(0, "M\nY")
        );
  }

  #endregion
}
