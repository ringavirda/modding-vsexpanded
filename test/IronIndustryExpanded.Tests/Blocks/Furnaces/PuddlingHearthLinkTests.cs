using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The puddling furnace finding its own hearth. The bed is where the whole process happens and the core
/// is what drives it on the melt cadence, so a core that cannot name its bed has no way to run a heat.
/// <para>
/// Checked at all four facings. <c>ShaftCentrePos</c> is <c>GlobalOf(ShaftCentre)</c>, which applies the
/// structure's own angle, so a second rotation anywhere in the lookup would find brick at three of them
/// and pass at north.
/// </para>
/// </summary>
public class PuddlingHearthLinkTests {
  #region Harness

  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>
  /// A puddling furnace stood at <paramref name="side"/> with a real hearth block and block entity in the
  /// bed cell, placed before the rig raises its stand-ins so nothing has to be replaced afterwards.
  /// </summary>
  private static (
    BlockEntityPuddlingFurnace Furnace,
    BlockEntityPuddlingHearth Bed,
    StructureRig Rig
  ) Stood(string side) {
    var furnace = new BlockEntityPuddlingFurnace();
    var bed = new BlockEntityPuddlingHearth();

    StructureRig rig = Stand(
      furnace,
      BlockPuddlingFurnaceCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-puddlingcore-tier1",
      side,
      complete: false
    );

    // The legend is orientation-pinned, so MultiblockFacings swaps in the rotated letter at check time:
    // a furnace facing south wants its hearth coded -s. The authored letter satisfies north alone.
    string letter = ExOrientation.RotateOrientationToken(
      "n",
      ExOrientation.AngleFromSide(side)
    );
    BlockPos cell = rig.Cell(-2, 0, 0);
    bed.Pos = cell.Copy();
    rig.Occupy(
      cell,
      TestBlocks.Configure(
        new Block(),
        $"iiex:furnace-puddlinghearth-{letter}",
        900,
        ("side", ExOrientation.SideFromAngle(ExOrientation.AngleFromSide(side)))
      ),
      bed
    );
    rig.Complete();
    return (furnace, bed, rig);
  }

  #endregion

  #region The core names its bed

  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_core_resolves_the_hearth_standing_in_its_bed_cell(string side) {
    var (furnace, bed, _) = Stood(side);

    // Never forced: Complete() runs the machine's own monitor tick, so a false here would mean the rig
    // and the machine disagree about what was built, and the lookup below would be measuring nothing.
    Assert.True(
      furnace.StructureComplete,
      $"the furnace did not complete at {side}"
    );

    Assert.Same(bed, furnace.Hearth);
  }

  /// <summary>
  /// The control. A furnace whose bed cell holds no hearth must read null rather than picking up whatever
  /// stand-in the rig raised there, or a broken bed would go unnoticed until the melt cycle threw.
  /// </summary>
  [Fact]
  public void A_furnace_with_no_hearth_in_its_bed_cell_reads_null() {
    var furnace = new BlockEntityPuddlingFurnace();
    Stand(
      furnace,
      BlockPuddlingFurnaceCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-puddlingcore-tier1",
      "north"
    );

    Assert.Null(furnace.Hearth);
  }

  /// <summary>
  /// The bed cell is the one <c>ShaftCentre</c> names, and it turns with the structure. Stated separately
  /// because <see cref="The_core_resolves_the_hearth_standing_in_its_bed_cell"/> would also pass if the
  /// lookup happened to scan a neighbourhood rather than reading one cell.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_bed_cell_is_the_rotated_shaft_centre(string side) {
    var (furnace, bed, rig) = Stood(side);

    Assert.Equal(rig.Cell(-2, 0, 0), bed.Pos);
    Assert.Equal(
      bed.Pos,
      (BlockPos)ReflectionHelpers.GetProperty(furnace, "ShaftCentrePos")!
    );
  }

  #endregion
}
