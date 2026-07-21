using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkMolten;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The blast-furnace tap: an open/closed spout that hands the furnace's molten metal down into the
/// canal start beneath it. Covers the open/closed toggle, persistence, and the <c>TryPourMetal</c>
/// handoff (gated on being open and on a receiving canal start below).
/// </summary>
public class BlastFurnaceTapTests
{
  private const string Iron = "game:ingot-iron";

  private static TestWorld NewWorld()
  {
    var world = new TestWorld();
    world.RegisterItem(Iron, 1500f);
    return world;
  }

  private static BlockEntityMoltenMetalTap Tap(
    TestWorld world,
    string side = "north"
  )
  {
    var be = new BlockEntityMoltenMetalTap
    {
      Pos = new BlockPos(0, 12, 0),
      Block = TestBlocks.Configure(
        new Block(),
        $"iwex:blastfurnacetap-{side}",
        1,
        ("side", side)
      ),
    };
    world.Place(be.Pos, be.Block, be);
    world.Attach(be);
    return be;
  }

  /// <summary>Places a canal start in the cell the tap pours into (Pos + side.Opposite, one down).</summary>
  private static BlockEntityMoltenCanalStart CanalBelow(
    TestWorld world,
    BlockEntityMoltenMetalTap tap,
    string side = "north"
  )
  {
    var facing = BlockFacing.FromCode(side);
    var pos = tap.Pos.AddCopy(facing.Opposite).DownCopy();
    var start = new BlockEntityMoltenCanalStart
    {
      Block = TestBlocks.Configure(
        new Block(),
        "smex:moltencanalstart-ns",
        2,
        ("type", "start"),
        ("orientation", "ns")
      ),
    };
    world.Place(pos, start.Block, start);
    world.Attach(start);
    return start;
  }

  private static ItemStack IronStack(TestWorld world, int units, float temp) =>
    new(world.World.GetItem(new AssetLocation(Iron))!, units)
    {
      // Temperature carrier so the canal start's pour path works.
    };

  #region Toggle / persistence

  [Fact]
  public void Defaults_closed_and_toggles_open()
  {
    var be = Tap(NewWorld());
    Assert.False(be.IsPouring);
    be.TogglePouring();
    Assert.True(be.IsPouring);
    be.TogglePouring();
    Assert.False(be.IsPouring);
  }

  [Fact]
  public void Pour_state_round_trips_through_the_tree()
  {
    var world = NewWorld();
    var src = Tap(world);
    src.TogglePouring();

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var dst = Tap(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.True(dst.IsPouring);
  }

  #endregion

  #region TryPourMetal

  [Fact]
  public void A_closed_tap_pours_nothing()
  {
    var world = NewWorld();
    var tap = Tap(world);
    CanalBelow(world, tap); // present, but tap is closed

    int accepted = tap.TryPourMetal(IronStack(world, 20, 1400f), 1400f);

    Assert.Equal(0, accepted);
  }

  [Fact]
  public void An_open_tap_hands_metal_to_the_canal_start_below()
  {
    var world = NewWorld();
    var tap = Tap(world);
    var canal = CanalBelow(world, tap);
    tap.TogglePouring();

    int accepted = tap.TryPourMetal(IronStack(world, 20, 1400f), 1400f);

    Assert.True(accepted > 0);
    Assert.True(canal.CellAmount > 0);
    Assert.Equal(Iron, canal.CellMetalType);
  }

  [Fact]
  public void An_open_tap_over_nothing_pours_nothing()
  {
    var world = NewWorld();
    var tap = Tap(world);
    tap.TogglePouring(); // open, but no canal beneath

    Assert.Equal(0, tap.TryPourMetal(IronStack(world, 20, 1400f), 1400f));
  }

  #endregion

  #region Orientation (pour target tracks the side)

  /// <summary>
  /// The tap is a plain oriented block, not a multiblock, so it carries no structure offsets - its one
  /// rotating piece is the pour target, <c>Pos + FromCode(side).Opposite</c> one down. The north case
  /// above pins the <c>Opposite</c>; this stands the tap up facing each side and confirms the pour lands
  /// in that side's target cell (four distinct cells: N->+z, S->-z, E->-x, W->+x). A tap that ignored its
  /// side would keep pouring into north's cell and find no canal at the other three, so every side but
  /// north would fail.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void An_open_tap_pours_into_the_cell_its_side_faces_away_from(
    string side
  )
  {
    var world = NewWorld();
    var tap = Tap(world, side);
    var canal = CanalBelow(world, tap, side);
    tap.TogglePouring();

    int accepted = tap.TryPourMetal(IronStack(world, 20, 1400f), 1400f);

    Assert.True(accepted > 0);
    Assert.True(canal.CellAmount > 0);
  }

  #endregion
}
