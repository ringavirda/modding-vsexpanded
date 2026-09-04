using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkEnergy.BlockEntities;
using IronIndustryExpanded.BlockNetworkEnergy.Blocks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The flywheel: the storage node of the mechanical-energy network. Covers the per-size inertia read from
/// config, the node forming an <c>"mpenergy"</c> run whose reservoir takes that inertia, the per-size
/// footprint, and the vanilla-MP bridge. The energy balance itself (supply, store, draw, capacity,
/// over-speed) is covered by exlib's <c>MpEnergyNetworkStateTests</c>.
/// </summary>
public class FlywheelTests {
  private static BlockFlywheel FlywheelBlock(string size, string orientation) =>
    TestBlocks.Configure(
      new BlockFlywheel(),
      $"iiex:mpenergy-flywheel-{size}-{orientation}",
      1,
      // `type` names the family member; the size lives in its own group (CodePrefixCollision).
      ("type", "flywheel"),
      ("size", size),
      ("orientation", orientation)
    );

  #region Inertia by size

  [Theory]
  [InlineData("normal")]
  [InlineData("large")]
  public void Inertia_is_read_from_config_by_size(string size) {
    float expected =
      size == "large"
        ? IiexValues.FlywheelInertiaLarge
        : IiexValues.FlywheelInertiaNormal;

    var be = new BlockEntityFlywheel { Block = FlywheelBlock(size, "ns") };

    Assert.Equal(expected, be.Inertia);
  }

  [Fact]
  public void The_large_wheel_stores_far_more_than_the_normal_one() {
    // A disc's inertia scales with R^4 * t, so the large wheel must be a large multiple of the normal one.
    // The design targets about 15x.
    Assert.True(
      IiexValues.FlywheelInertiaLarge > IiexValues.FlywheelInertiaNormal * 5f,
      $"large inertia {IiexValues.FlywheelInertiaLarge} is not meaningfully bigger than "
        + $"normal {IiexValues.FlywheelInertiaNormal}"
    );
  }

  #endregion

  #region Network node

  private static (TestWorld world, BlockPos pos) PlacedFlywheel(string size) {
    var world = new TestWorld();
    world.RegisterNetwork("mpenergy", sys => new MpEnergyNetwork(sys));

    var block = FlywheelBlock(size, "ns");
    ReflectionHelpers.SetProperty(block, "Type", size);
    ReflectionHelpers.SetProperty(block, "Orientation", "ns");

    var pos = new BlockPos(0, 0, 0);
    world.Place(pos, block, new BlockEntityFlywheel());
    world.AddNode(pos, "mpenergy");
    return (world, pos);
  }

  [Fact]
  public void A_placed_flywheel_forms_an_mpenergy_network() {
    var (world, pos) = PlacedFlywheel("normal");
    Assert.IsType<MpEnergyNetwork>(world.NetworkAt(pos));
  }

  [Fact]
  public void The_reservoir_takes_its_inertia_from_the_flywheel() {
    var (world, pos) = PlacedFlywheel("large");

    // Before the first tick the run has no reservoir state; the tick walks the nodes, sums the storage
    // inertia and creates one.
    world.Tick();

    var state = ((MpEnergyNetwork)world.NetworkAt(pos)!).State;
    Assert.NotNull(state);
    Assert.Equal(IiexValues.FlywheelInertiaLarge, state!.Inertia, 3);
    // No producer on the run, so the wheel holds no energy and does not spin.
    Assert.Equal(0f, state.StoredEnergy);
    Assert.Equal(0f, state.Speed);
  }

  #endregion

  #region Footprint

  /// <summary>The per-size <c>fillerOffsets</c> table from the code-first def, in the north frame before
  /// placement rotation. The footprint varies by size, so it is authored under <c>attributesByType</c>
  /// rather than the plain <c>attributes</c> node.</summary>
  private static JArray FillerOffsetsJson(string size) {
    ExBlockDef def = BlockFlywheel.Definitions("iiex").Single();
    return (JArray)
      def.ToJson()["attributesByType"]![$"*-{size}-*"]!["fillerOffsets"]!;
  }

  /// <summary>A flywheel block with its per-size footprint loaded into <c>Attributes</c> so the placement
  /// helpers resolve it. The headless harness does not populate <c>Attributes</c> from the def.</summary>
  private static BlockFlywheel PlacedBlock(string size, string orientation) {
    BlockFlywheel block = FlywheelBlock(size, orientation);
    block.Attributes = new JsonObject(
      new JObject {
        ["fillerOffsets"] = (JArray)FillerOffsetsJson(size).DeepClone(),
      }
    );
    return block;
  }

  [Theory]
  [InlineData("normal", 8)] // 3x3x1 minus the principal
  [InlineData("large", 49)] // 5x5x2 minus the principal
  public void The_footprint_reserves_the_disc_volume_minus_the_principal(
    string size,
    int expected
  ) {
    Assert.Equal(expected, FillerOffsetsJson(size).Count);
  }

  [Fact]
  public void The_normal_north_frame_footprint_is_a_thin_in_z_disc_centred_on_the_principal() {
    var cells = FillerOffsetsJson("normal")
      .Select(o => ((int)o["x"]!, (int)o["y"]!, (int)o["z"]!))
      .ToHashSet();

    // ns = shaft along Z: the 3x3 disc lies in X-Y at z=0, centred on the principal's column and sitting on
    // its row (x in -1..1, y in 0..2), the principal (0,0,0) excluded.
    var expected = new HashSet<(int, int, int)>();
    for (int x = -1; x <= 1; x++)
      for (int y = 0; y <= 2; y++)
        if (x != 0 || y != 0)
          expected.Add((x, y, 0));

    Assert.Equal(expected, cells);
  }

  [Fact]
  public void The_large_footprint_is_two_thin_in_z_faces() {
    var zs = FillerOffsetsJson("large").Select(o => (int)o["z"]!).ToHashSet();
    Assert.Equal(new HashSet<int> { 0, 1 }, zs);
  }

  [Theory]
  [InlineData("ns", 0)]
  [InlineData("we", 90)]
  public void The_structure_angle_follows_the_orientation_variant(
    string orientation,
    int expected
  ) {
    Assert.Equal(expected, FlywheelBlock("normal", orientation).StructureAngle);
  }

  #endregion

  #region Vanilla-MP bridge

  [Theory]
  [InlineData(0f, 0f)] // nothing driving it
  [InlineData(0.5f, 1f)] // half rated -> half torque (maxTorque 2, rated 1)
  [InlineData(1f, 2f)] // rated -> full torque
  [InlineData(3f, 2f)] // over-driven -> capped at full
  public void Bridge_drive_torque_scales_with_axle_speed_up_to_rated(
    float axleSpeed,
    float expected
  ) {
    Assert.Equal(
      expected,
      BlockEntityFlywheel.BridgeDriveTorque(
        axleSpeed,
        maxTorque: 2f,
        ratedHubSpeed: 1f
      ),
      3
    );
  }

  [Fact]
  public void A_zero_rated_speed_drives_nothing() {
    // Guard against divide-by-zero if the rated hub speed is misconfigured to 0.
    Assert.Equal(0f, BlockEntityFlywheel.BridgeDriveTorque(5f, 2f, 0f));
  }

  [Fact]
  public void The_normal_hub_hosts_north_and_south_mp_intakes_one_cell_above_the_principal() {
    JToken hub = FillerOffsetsJson("normal")
      .Single(o => o["behaviors"] is JArray);
    Assert.Equal((0, 1, 0), ((int)hub["x"]!, (int)hub["y"]!, (int)hub["z"]!)); // disc centre

    var behaviors = (JArray)hub["behaviors"]!;
    Assert.All(
      behaviors,
      b => Assert.Equal("exlib.BEBehaviorMPFillerPort", (string)b["code"]!)
    );
    Assert.Equal(
      new HashSet<string> { "north", "south" }, // a shaft face each so an axle couples from either side
      behaviors.Select(b => (string)b["face"]!).ToHashSet()
    );
  }

  [Fact]
  public void Both_large_hubs_sit_on_the_shaft_axis_one_per_face_depth() {
    var hubs = FillerOffsetsJson("large")
      .Where(o => o["behaviors"] is JArray)
      .Select(o => ((int)o["x"]!, (int)o["y"]!, (int)o["z"]!))
      .ToHashSet();

    // Both shaft cells (z 0 and 1) carry the ports, so the large wheel is driven from either side too.
    Assert.Equal(new HashSet<(int, int, int)> { (0, 2, 0), (0, 2, 1) }, hubs);
  }

  [Fact]
  public void Rotating_to_we_turns_the_disc_plane_and_ports_onto_the_x_axis() {
    var cells = StructureFillers.FootprintCells(
      PlacedBlock("normal", "we"),
      new BlockPos(0, 0, 0),
      90
    );

    // we = shaft along X: the disc plane rotates X-Y -> Z-Y (every reserved cell on x=0) and the N/S ports
    // become the two X-axis faces (west/east).
    Assert.All(cells, c => Assert.Equal(0, c.Pos.X));
    var hub = cells.Single(c => c.Behaviors is { Length: > 0 });
    var faces = hub.Behaviors!.Select(b => b.ConnectorFace).ToHashSet();
    Assert.Contains(BlockFacing.WEST, faces);
    Assert.Contains(BlockFacing.EAST, faces);
  }

  #endregion
}
