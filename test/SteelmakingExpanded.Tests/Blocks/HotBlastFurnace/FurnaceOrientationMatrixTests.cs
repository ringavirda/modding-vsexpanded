using System.Linq;
using IronworkingExpanded.Tests;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The hot blast furnace in all four orientations, checked against the two oracles in iwex's
/// <see cref="FurnaceLayoutRig"/>. Only the hot furnace is covered here; the cold blast furnace, the
/// cupola and the tall-hopper exclusion run the same check on iwex types in that suite's matrix. The
/// hot furnace is the only one with gas outlets, so its stack cells are the only ones that rotate.
/// </summary>
public class FurnaceOrientationMatrixTests {
  // Theory data must stay string-only: a [Theory] argument naming a game type is resolved by xUnit's
  // discovery reflection before the module initializer registers VsAssemblyResolver.

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Hot_furnace_functional_cells_track_its_orientation(string side) {
    var be = new BlockEntityBlastFurnaceHot { Pos = new BlockPos(0, 16, 0) };
    OrientWithLayout(
      be,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      $"smex:blastfurnacecore-{side}",
      side
    );
    AssertFurnaceMatrix(
      be,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      "smex:blastfurnacecore-*",
      side,
      TapGlyphs.ShaftFurnace,
      NorthTuyereGlyph,
      SouthTuyereGlyph
    );
  }
}
