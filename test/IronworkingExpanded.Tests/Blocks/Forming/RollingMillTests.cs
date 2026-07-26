using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Forming.BlockEntities;
using IronworkingExpanded.BlockStructures.Forming.Blocks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The rolling mill: the iron tier's forming megablock and the first consumer of the mechanical-energy
/// network. These tests pin the Phase-A structural shell - the split footprint (nine fillers + a two-cell
/// axle bus, not eleven fillers), the axle geometry that makes the drive line a bus, and the mill coming
/// alive as an idle <c>mpenergy</c> consumer. The pass simulation (energy draw, gap stepping, feed-side)
/// is Phase C.
/// </summary>
public class RollingMillTests
{
  private static BlockRollingMill MillBlock(string orientation) =>
    TestBlocks.Configure(
      new BlockRollingMill(),
      $"iwex:rollingmill-rollingmill-{orientation}",
      1,
      ("type", "rollingmill"),
      ("orientation", orientation)
    );

  #region Footprint

  /// <summary>The mill's <c>fillerOffsets</c> straight from the code-first def (north/we frame, before
  /// placement rotation).</summary>
  private static JArray FillerOffsetsJson()
  {
    ExBlockDef def = BlockRollingMill.Definitions("iwex").Single();
    return (JArray)def.ToJson()["attributes"]!["fillerOffsets"]!;
  }

  [Fact]
  public void The_footprint_reserves_nine_fillers_not_the_axle_row()
  {
    // 3x2x3 has 11 non-principal cells, but the two west axle cells are graph nodes, not fillers - so the
    // filler footprint is 9: two feed decks (6) + the roll stand (3).
    Assert.Equal(9, FillerOffsetsJson().Count);
  }

  [Fact]
  public void The_footprint_is_the_two_decks_and_the_roll_stand()
  {
    var cells = FillerOffsetsJson()
      .Select(o => ((int)o["x"]!, (int)o["y"]!, (int)o["z"]!))
      .ToHashSet();

    var expected = new HashSet<(int, int, int)>();
    // Feed decks flanking the axle (y=0, z=+/-1) across the mill's width (x -2..0).
    for (int x = -2; x <= 0; x++)
    {
      expected.Add((x, 0, -1));
      expected.Add((x, 0, 1));
    }
    // Roll stand above the axle (y=1, z=0).
    for (int x = -2; x <= 0; x++)
      expected.Add((x, 1, 0));

    Assert.Equal(expected, cells);
  }

  [Fact]
  public void The_footprint_excludes_the_principal_and_the_axle_cells()
  {
    var cells = FillerOffsetsJson()
      .Select(o => ((int)o["x"]!, (int)o["y"]!, (int)o["z"]!))
      .ToHashSet();

    Assert.DoesNotContain((0, 0, 0), cells); // principal
    Assert.DoesNotContain((-1, 0, 0), cells); // axle bus cell
    Assert.DoesNotContain((-2, 0, 0), cells); // axle bus cell
  }

  #endregion

  #region Axle bus

  [Fact]
  public void The_axle_bus_is_the_two_cells_west_of_the_principal_in_the_we_frame()
  {
    var axle = MillBlock("we")
      .AxleCells(new BlockPos(0, 0, 0))
      .Select(p => (p.X, p.Y, p.Z))
      .ToHashSet();

    Assert.Equal(
      new HashSet<(int, int, int)> { (-1, 0, 0), (-2, 0, 0) },
      axle
    );
  }

  [Fact]
  public void Rotating_to_ns_swings_the_axle_bus_onto_the_z_axis()
  {
    // ns = axle along Z: the two bus cells rotate from -X to +Z, so the drive line runs north-south.
    var axle = MillBlock("ns")
      .AxleCells(new BlockPos(0, 0, 0))
      .Select(p => (p.X, p.Y, p.Z))
      .ToHashSet();

    Assert.Equal(
      new HashSet<(int, int, int)> { (0, 0, 1), (0, 0, 2) },
      axle
    );
  }

  [Theory]
  [InlineData("we", 0)]
  [InlineData("ns", 90)]
  public void The_structure_angle_follows_the_orientation_variant(
    string orientation,
    int expected
  )
  {
    Assert.Equal(expected, MillBlock(orientation).StructureAngle);
  }

  [Fact]
  public void An_axle_cell_connects_on_both_shaft_faces_and_drops_nothing()
  {
    var axle = TestBlocks.Configure(
      new BlockRollingMillAxle(),
      "iwex:rollingmillaxle-shaft-we",
      2,
      ("type", "shaft"),
      ("orientation", "we")
    );
    ReflectionHelpers.SetProperty(axle, "Orientation", "we");

    // we axle = a two-ended bus: connectors east AND west, none north/south.
    Assert.True(axle.HasConnectorAt(BlockFacing.EAST));
    Assert.True(axle.HasConnectorAt(BlockFacing.WEST));
    Assert.False(axle.HasConnectorAt(BlockFacing.NORTH));
    Assert.False(axle.HasConnectorAt(BlockFacing.SOUTH));

    // The mill owns all drops; the invisible axle cell never drops itself.
    Assert.Empty(axle.GetDrops(null!, new BlockPos(0, 0, 0), null));
  }

  #endregion

  #region Network node

  private static (TestWorld world, BlockPos pos) PlacedMill()
  {
    var world = new TestWorld();
    world.RegisterNetwork("mpenergy", sys => new MpEnergyNetwork(sys));

    var block = MillBlock("we");
    ReflectionHelpers.SetProperty(block, "Type", "rollingmill");
    ReflectionHelpers.SetProperty(block, "Orientation", "we");

    var pos = new BlockPos(0, 0, 0);
    world.Place(pos, block, new BlockEntityRollingMill());
    world.AddNode(pos, "mpenergy");
    return (world, pos);
  }

  [Fact]
  public void A_placed_mill_forms_an_mpenergy_network()
  {
    var (world, pos) = PlacedMill();
    Assert.IsType<MpEnergyNetwork>(world.NetworkAt(pos));
  }

  [Fact]
  public void The_mill_is_an_idle_mpenergy_consumer()
  {
    var be = new BlockEntityRollingMill();

    // It is a consumer (it can load the run), but Phase A imposes no torque until a pass is worked.
    Assert.IsAssignableFrom<IMpEnergyConsumer>(be);
    Assert.Equal(0f, be.LoadTorque(0f));
  }

  #endregion
}
