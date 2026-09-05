using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkEnergy.BlockEntities;
using IronIndustryExpanded.BlockNetworkEnergy.Blocks;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The cast-iron shaft, the buffer node of an mp-energy transmission run. It connects along its own axis
/// only, so a line of shafts joins end to end and a bend needs a bevel gear; it carries a small inertia;
/// and placing one forms an <c>mpenergy</c> network.
/// </summary>
public class CastIronShaftTests {
  private static BlockCastIronShaft ShaftBlock(string orientation) {
    var block = TestBlocks.Configure(
      new BlockCastIronShaft(),
      $"iiex:mpenergy-shaft-{orientation}",
      200,
      ("type", "shaft"),
      ("orientation", orientation)
    );
    ReflectionHelpers.SetProperty(block, "Type", "shaft");
    ReflectionHelpers.SetProperty(block, "Orientation", orientation);
    return block;
  }

  [Fact]
  public void An_ns_shaft_connects_north_and_south_only() {
    var shaft = ShaftBlock("ns");
    Assert.True(shaft.HasConnectorAt(BlockFacing.NORTH));
    Assert.True(shaft.HasConnectorAt(BlockFacing.SOUTH));
    Assert.False(shaft.HasConnectorAt(BlockFacing.EAST));
    Assert.False(shaft.HasConnectorAt(BlockFacing.WEST));
  }

  [Fact]
  public void A_we_shaft_connects_east_and_west_only() {
    var shaft = ShaftBlock("we");
    Assert.True(shaft.HasConnectorAt(BlockFacing.EAST));
    Assert.True(shaft.HasConnectorAt(BlockFacing.WEST));
    Assert.False(shaft.HasConnectorAt(BlockFacing.NORTH));
    Assert.False(shaft.HasConnectorAt(BlockFacing.SOUTH));
  }

  [Fact]
  public void The_shaft_is_a_small_buffer_node() {
    var be = new BlockEntityCastIronShaft();

    Assert.IsAssignableFrom<IMpEnergyStorage>(be);
    Assert.Equal(IiexValues.ShaftInertia, be.Inertia, 3);
    Assert.True(
      IiexValues.ShaftInertia < IiexValues.FlywheelInertiaNormal,
      "a shaft buffers a little, far less than a dedicated flywheel"
    );
  }

  [Fact]
  public void A_placed_shaft_forms_an_mpenergy_network() {
    var world = new TestWorld();
    world.RegisterNetwork("mpenergy", sys => new MpEnergyNetwork(sys));

    var pos = new BlockPos(0, 0, 0);
    world.Place(pos, ShaftBlock("ns"), new BlockEntityCastIronShaft());
    world.AddNode(pos, "mpenergy");

    Assert.IsType<MpEnergyNetwork>(world.NetworkAt(pos));
  }
}
