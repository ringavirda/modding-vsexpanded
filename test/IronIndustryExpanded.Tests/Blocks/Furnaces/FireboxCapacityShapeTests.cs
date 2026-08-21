using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// A firebox furnace's capacity is its marked cells, not the box around them. The two agree for every
/// reverberatory hearth in the mod, which is why a bounding box went unnoticed; the coke oven's chambers
/// straddle a shared wall, and there the box counts brick a player can never load.
/// <para>
/// The consequence is not a rounding error. <c>ChargeCapacityUnits</c> and the sealed
/// <c>MinChargeToIgnite</c> are both derived from the count, so an over-count sets a threshold above what
/// the machine can physically hold and the furnace simply never lights - with nothing reported. See
/// <c>docs/design/mechanics/heat-balance.md</c>.
/// </para>
/// </summary>
public class FireboxCapacityShapeTests {
  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>
  /// Capacity read off a stood machine. Named by reflection because it is the branch's own protected
  /// arithmetic and the point is that the number a furnace runs on is right, not that a helper is.
  /// </summary>
  private static int CapacityOf(BlockEntityFireboxFurnace furnace) =>
    (int)ReflectionHelpers.GetProperty(furnace, "ChargeCapacityUnits")!;

  /// <summary>
  /// The coke oven: twelve chamber cells in two blocks of six, and a bounding box of fourteen. Before the
  /// count was taken off the cells, the oven's ignition floor asked for two cells of coal that do not
  /// exist and it could never have been lit.
  /// </summary>
  [Fact]
  public void A_split_firebox_counts_its_cells_and_not_the_box_around_them() {
    var oven = new BlockEntityCokeOven();
    Stand(
      oven,
      BlockCokeOvenCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-cokeovencore",
      "north"
    );

    // The premise: the drawing really is split, or this case proves nothing about split fireboxes.
    var xs = oven.FireboxCells.Select(c => c.X).ToList();
    int box =
      (xs.Max() - xs.Min() + 1)
      * oven.FireboxCells.Select(c => c.Y).Distinct().Count()
      * oven.FireboxCells.Select(c => c.Z).Distinct().Count();
    Assert.Equal(14, box);
    Assert.Equal(12, oven.FireboxCells.Count);

    Assert.Equal(12 * IiexValues.FireboxMixPerCell, CapacityOf(oven));
  }

  /// <summary>
  /// The reheat furnace, whose two firebox cells are adjacent: box and count agree, so the correction
  /// leaves every shipped hearth exactly where it was. Without this the fix reads as a free change.
  /// </summary>
  [Fact]
  public void A_solid_firebox_is_unchanged_by_counting_cells() {
    var furnace = new BlockEntityHeatingFurnace();
    Stand(
      furnace,
      BlockHeatingFurnaceCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-heatingcore-tier1",
      "north"
    );

    Assert.Equal(2, furnace.FireboxCells.Count);
    Assert.Equal(2 * IiexValues.FireboxMixPerCell, CapacityOf(furnace));
  }
}
