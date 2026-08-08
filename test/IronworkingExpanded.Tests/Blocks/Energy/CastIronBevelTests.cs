using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkEnergy.BlockEntities;
using IronworkingExpanded.BlockNetworkEnergy.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The cast-iron bevel: a shaft junction that branches an mpenergy run onto its perpendicular faces. It
/// presents a connector on every face, and draws a gear on a perpendicular face only where a neighbouring
/// shaft faces back. Covers that derivation, the buffer inertia and the salvage drops.
/// See docs/design/machines/flywheel-and-shafting.md.
/// </summary>
public class CastIronBevelTests {
  private static BlockCastIronBevel BevelBlock(string orientation) {
    var block = TestBlocks.Configure(
      new BlockCastIronBevel(),
      $"iwex:mpenergy-bevel-{orientation}",
      210,
      ("type", "bevel"),
      ("orientation", orientation)
    );
    ReflectionHelpers.SetProperty(block, "Type", "bevel");
    ReflectionHelpers.SetProperty(block, "Orientation", orientation);
    return block;
  }

  private static BlockCastIronShaft ShaftBlock(string orientation, int id) {
    var block = TestBlocks.Configure(
      new BlockCastIronShaft(),
      $"iwex:mpenergy-shaft-{orientation}",
      id,
      ("type", "shaft"),
      ("orientation", orientation)
    );
    ReflectionHelpers.SetProperty(block, "Type", "shaft");
    ReflectionHelpers.SetProperty(block, "Orientation", orientation);
    return block;
  }

  // A bevel at the origin, linked to a world so its neighbour-derived gears can be read.
  private static (
    TestWorld world,
    BlockPos pos,
    BlockEntityCastIronBevel be
  ) Scene(string orientation) {
    var world = new TestWorld();
    var block = BevelBlock(orientation);
    var be = new BlockEntityCastIronBevel {
      Block = block,
      Orientation = orientation,
    };
    var pos = new BlockPos(0, 0, 0);
    world.Place(pos, block, be);
    world.Attach(be);
    return (world, pos, be);
  }

  #region Junction connectors + perpendicular rule

  [Fact]
  public void A_bevel_connects_on_every_face() {
    var world = new TestWorld();
    var block = BevelBlock("ns");
    var pos = new BlockPos(0, 0, 0);
    world.Place(pos, block);

    foreach (BlockFacing face in BlockFacing.ALLFACES)
      Assert.True(
        block.HasConnectorAt(world.Accessor, pos, face),
        $"{face} should connect"
      );
  }

  [Theory]
  [InlineData("ns", "e", true)]
  [InlineData("ns", "w", true)]
  [InlineData("ns", "u", true)]
  [InlineData("ns", "d", true)]
  [InlineData("ns", "n", false)] // axis end: the through-run, not a branch
  [InlineData("ns", "s", false)]
  [InlineData("we", "n", true)]
  [InlineData("we", "s", true)]
  [InlineData("we", "u", true)]
  [InlineData("we", "d", true)]
  [InlineData("we", "e", false)] // axis end
  [InlineData("we", "w", false)]
  [InlineData("ud", "n", true)] // a climbing run branches horizontally
  [InlineData("ud", "s", true)]
  [InlineData("ud", "e", true)]
  [InlineData("ud", "w", true)]
  [InlineData("ud", "u", false)] // axis end
  [InlineData("ud", "d", false)]
  public void A_gear_grows_only_on_a_face_perpendicular_to_the_axis(
    string orientation,
    string face,
    bool perpendicular
  ) {
    Assert.Equal(
      perpendicular,
      BlockCastIronBevel.IsPerpendicular(
        orientation,
        BlockFacing.FromFirstLetter(face[0])
      )
    );
  }

  [Fact]
  public void Every_shaft_orientation_can_become_a_bevel() {
    // A bevel is grown from a shaft of the same axis by code (`mpenergy-bevel-{orientation}`), so a shaft
    // orientation with no matching bevel variant can never branch.
    Assert.Equal(
      BlockCastIronShaft
        .Definitions("iwex")
        .Single()
        .VariantStates("orientation"),
      BlockCastIronBevel
        .Definitions("iwex")
        .Single()
        .VariantStates("orientation")
    );
  }

  [Fact]
  public void IsBevelGearItem_matches_the_gear_code() {
    var gear = new ItemStack(
      TestBlocks.Configure(new Block(), "iwex:bevelgear", 211)
    );
    var other = new ItemStack(
      TestBlocks.Configure(new Block(), "iwex:mpenergy-shaft-ns", 212)
    );

    Assert.True(BlockCastIronBevel.IsBevelGearItem(gear));
    Assert.False(BlockCastIronBevel.IsBevelGearItem(other));
    Assert.False(BlockCastIronBevel.IsBevelGearItem(null));
  }

  #endregion

  #region Gears derived from the neighbours

  [Fact]
  public void A_perpendicular_shaft_grows_a_gear_on_that_face() {
    var (world, pos, be) = Scene("ns");
    // A we-shaft to the east faces back (presents its west connector), so a gear grows on east.
    world.Place(
      pos.AddCopy(BlockFacing.EAST),
      ShaftBlock("we", 260),
      new BlockEntityCastIronShaft()
    );

    Assert.Contains(BlockFacing.EAST, be.GearedFaces());
  }

  [Fact]
  public void An_axis_continuation_shaft_grows_no_gear() {
    var (world, pos, be) = Scene("ns");
    // An ns-shaft to the north continues the run: it connects, but along the axis rather than as a branch.
    world.Place(
      pos.AddCopy(BlockFacing.NORTH),
      ShaftBlock("ns", 261),
      new BlockEntityCastIronShaft()
    );

    Assert.DoesNotContain(BlockFacing.NORTH, be.GearedFaces());
  }

  [Fact]
  public void A_neighbour_that_does_not_face_back_grows_no_gear() {
    var (world, pos, be) = Scene("ns");
    // An ns-shaft to the east runs the wrong way (its connectors are north/south), so nothing meshes there.
    world.Place(
      pos.AddCopy(BlockFacing.EAST),
      ShaftBlock("ns", 262),
      new BlockEntityCastIronShaft()
    );

    Assert.DoesNotContain(BlockFacing.EAST, be.GearedFaces());
  }

  [Fact]
  public void Several_perpendicular_shafts_grow_several_gears() {
    var (world, pos, be) = Scene("ns");
    world.Place(
      pos.AddCopy(BlockFacing.EAST),
      ShaftBlock("we", 263),
      new BlockEntityCastIronShaft()
    );
    world.Place(
      pos.AddCopy(BlockFacing.WEST),
      ShaftBlock("we", 264),
      new BlockEntityCastIronShaft()
    );
    world.Place(
      pos.AddCopy(BlockFacing.UP),
      ShaftBlock("ud", 265),
      new BlockEntityCastIronShaft()
    );

    var geared = be.GearedFaces().ToHashSet();
    Assert.Contains(BlockFacing.EAST, geared);
    Assert.Contains(BlockFacing.WEST, geared);
    Assert.Contains(BlockFacing.UP, geared);
    Assert.DoesNotContain(BlockFacing.DOWN, geared); // no shaft below
  }

  #endregion

  #region Buffer + drops

  [Fact]
  public void The_bevel_inherits_the_shaft_buffer_inertia() {
    // A bevel is still a Buffer node, carrying the shaft's rotating mass.
    Assert.Equal(
      IwexValues.ShaftInertia,
      new BlockEntityCastIronBevel().Inertia,
      3
    );
  }

  [Fact]
  public void A_broken_bevel_returns_the_shaft_and_one_gear() {
    var world = new TestWorld();
    var bevel = BevelBlock("ns");
    var pos = new BlockPos(0, 0, 0);
    world.Place(pos, bevel);
    world.Register(ShaftBlock("ns", 201));
    world.RegisterItem("iwex:bevelgear");

    var drops = bevel.GetDrops(world.World, pos, null);

    Assert.Equal(2, drops.Length);
    Assert.Contains(
      drops,
      d => d.Collectible?.Code?.ToString() == "iwex:mpenergy-shaft-ns"
    );
    Assert.Contains(
      drops,
      d => d.Collectible?.Code?.ToString() == "iwex:bevelgear"
    );
  }

  #endregion
}
