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
      $"iwex:forming-rollingmill-{orientation}",
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
      "iwex:forming-millaxle-we",
      2,
      ("type", "shaft"),
      ("orientation", "we")
    );
    ReflectionHelpers.SetProperty(axle, "Orientation", "we");

    // we axle = a two-ended bus: connectors east and west, none north/south.
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
  public void An_idle_mill_is_a_consumer_that_loads_nothing()
  {
    var be = new BlockEntityRollingMill();

    // It is a consumer (it can load the run), but imposes no torque with nothing under the rolls.
    Assert.IsAssignableFrom<IMpEnergyConsumer>(be);
    Assert.Equal(0f, be.LoadTorque(0f));
  }

  #endregion

  #region The pass - the network's first real demand

  // A mill with stock under the rolls. Hot and shallow enough to bite; `length` is how far it must travel.
  private static BlockEntityRollingMill Rolling(
    float tempC = 1100f,
    float draft = 0.5f,
    float length = 4f
  )
  {
    var be = new BlockEntityRollingMill();
    Assert.True(be.BeginPass(draft, width: 4f, length: length, tempC: tempC));
    return be;
  }

  [Fact]
  public void A_pass_under_the_rolls_actually_loads_the_run()
  {
    // The whole point of this increment: before it, nothing in the mod drew a single N.m from an mpenergy
    // run. An idle mill still draws nothing, so the load is the pass, not the machine.
    var be = Rolling();
    Assert.True(be.IsRolling);
    Assert.True(be.LoadTorque(1f) > 0f);

    be.CancelPass();
    Assert.Equal(0f, be.LoadTorque(1f));
  }

  [Fact]
  public void Cold_stock_is_refused_rather_than_allowed_to_jam_the_run()
  {
    // Below rolling heat the friction collapses, so the rolls cannot pull the piece in at all. Refusing up
    // front is kinder than accepting a pass that would stall the whole line.
    var be = new BlockEntityRollingMill();
    Assert.False(be.BeginPass(draft: 0.5f, width: 4f, length: 4f, tempC: 400f));
    Assert.False(be.IsRolling);
  }

  [Fact]
  public void An_over_deep_gap_is_refused_even_when_the_stock_is_hot()
  {
    var be = new BlockEntityRollingMill();
    Assert.False(be.BeginPass(draft: 99f, width: 4f, length: 4f, tempC: 1200f));
  }

  [Fact]
  public void Only_one_piece_can_be_under_the_rolls_at_a_time()
  {
    var be = Rolling();
    Assert.False(be.BeginPass(draft: 0.5f, width: 4f, length: 4f, tempC: 1100f));
  }

  [Fact]
  public void The_stock_is_drawn_through_at_the_roll_surface_speed()
  {
    var be = Rolling(length: 4f);
    float before = be.Remaining;

    Assert.False(be.AdvancePass(dt: 0.1f, speed: 1f)); // not finished yet
    Assert.True(be.Remaining < before, "the bite should have advanced");

    // A faster run draws the same stock through in fewer ticks.
    var slow = Rolling(length: 4f);
    var fast = Rolling(length: 4f);
    slow.AdvancePass(0.1f, speed: 0.5f);
    fast.AdvancePass(0.1f, speed: 2f);
    Assert.True(fast.Remaining < slow.Remaining);
  }

  [Fact]
  public void A_stalled_run_makes_no_progress_but_does_not_lose_the_pass()
  {
    var be = Rolling(length: 4f);
    float before = be.Remaining;

    Assert.False(be.AdvancePass(dt: 1f, speed: 0f));
    Assert.True(be.IsStalled);
    Assert.Equal(before, be.Remaining, 4); // the piece is still there, mid-bite

    // Spin the run back up and it resumes from where it jammed.
    be.AdvancePass(dt: 1f, speed: 1f);
    Assert.False(be.IsStalled);
    Assert.True(be.Remaining < before);
  }

  [Fact]
  public void The_pass_completes_once_the_stock_has_cleared_the_rolls()
  {
    var be = Rolling(length: 1f);

    Assert.True(be.AdvancePass(dt: 10f, speed: 1f)); // plenty of travel: it clears
    Assert.False(be.IsRolling);
    Assert.Equal(0f, be.LoadTorque(1f)); // and stops loading the run
  }

  [Fact]
  public void Cooler_stock_loads_the_run_harder_than_hotter_stock()
  {
    // Both bite (both are above rolling heat), but the cooler piece resists more - the gradient that makes
    // a player care about the reheat furnace rather than just clearing a hard threshold.
    var hot = Rolling(tempC: 1200f);
    var barely = Rolling(tempC: 900f);
    Assert.True(hot.LoadTorque(1f) <= barely.LoadTorque(1f));
  }

  #endregion
}
