using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkPipe;
using SteelIndustryExpanded.BlockStructures.SmokeStack.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The smoke-stack multiblock is a gas-network sink: each production tick it draws
/// <see cref="SiexValues.SmokestackGasIntakeVolume"/> litres of exhaust off the connected pipe network
/// and vents it, keeping the run from choking the furnace. Spanning siex (the sink BE) and iiex (the
/// <see cref="PipeNetwork"/>), it lives in the siex suite as the top mod. Covers the IPipeNode reads on
/// an unbuilt node, the structure-gated draw on a built <see cref="SmokeStackRig"/>, and the
/// serialization round trip.
/// </summary>
public class SmokeStackTests {
  #region IPipeNode reads (an unbuilt, unwired node)

  private static BlockEntitySmokeStack Bare(TestWorld world) {
    var be = new BlockEntitySmokeStack {
      Pos = new BlockPos(0, 0, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "siex:smokestack-intake-tier3-n",
        70,
        ("orientation", "n")
      ),
    };
    world.Attach(be);
    return be;
  }

  [Fact]
  public void Without_a_network_the_node_reports_inert_defaults() {
    var be = Bare(new TestWorld());

    Assert.Equal("pipe", be.NetworkType);
    Assert.Equal(20f, be.Temperature);
    Assert.Equal("", be.Medium);
    Assert.False(be.IsLiquid);
    Assert.Equal(0f, be.Volume);
    Assert.Equal(0f, be.MaxVolume);
  }

  [Theory]
  [InlineData("north", "north", true)]
  [InlineData("north", "south", false)]
  public void HasConnectorAt_matches_the_orientation_face(
    string orientation,
    string face,
    bool expected
  ) {
    var be = Bare(new TestWorld());
    be.Orientation = orientation;

    Assert.Equal(expected, be.HasConnectorAt(BlockFacing.FromCode(face)));
  }

  #endregion

  #region Structure-gated draw

  [Fact]
  public void A_complete_stack_draws_exhaust_off_the_network() {
    var rig = new SmokeStackRig();
    rig.SpillExhaust(200f, 400f);
    float before = rig.MainVolume;

    rig.Tick();

    Assert.Equal(
      SiexValues.SmokestackGasIntakeVolume,
      before - rig.MainVolume,
      1
    );
  }

  [Fact]
  public void An_incomplete_stack_draws_nothing() {
    // Same rig with the structure torn open first: one brick pulled out of the shell and the monitor
    // re-run, so the stack observes its own footprint break before the draw.
    var rig = new SmokeStackRig();
    rig.SpillExhaust(200f, 400f);
    float before = rig.MainVolume;

    rig.World.Place(
      rig.Structure.Cell(-1, 0, 0),
      TestBlocks.Configure(new Block(), "game:air", 0)
    );
    rig.World.AdvanceBlockEntityTime(3000);
    Assert.False(
      rig.Stack.StructureComplete,
      "pulling a brick should break the structure"
    );

    rig.Tick();

    Assert.Equal(before, rig.MainVolume, 3);
  }

  [Fact]
  public void Draw_is_capped_at_what_the_network_holds() {
    // Less than one intake's worth in the run.
    var rig = new SmokeStackRig();
    rig.SpillExhaust(20f, 400f);

    rig.Tick();

    Assert.Equal(0f, rig.MainVolume, 1); // emptied, not driven negative
  }

  #endregion

  #region Serialization

  [Fact]
  public void Node_state_round_trips_through_the_tree() {
    var world = new TestWorld();
    var src = Bare(world);
    src.Orientation = "north";
    src.PossibleOrientations = ["north", "east"];
    ReflectionHelpers.SetField(src, "_lastConsumedAmount", 42f);

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var restored = Bare(world);
    restored.FromTreeAttributes(tree, world.World);

    Assert.Equal("north", restored.Orientation);
    Assert.Equal(new[] { "north", "east" }, restored.PossibleOrientations);
  }

  #endregion
}
