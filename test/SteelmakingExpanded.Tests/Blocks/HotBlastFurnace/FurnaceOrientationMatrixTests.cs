using System.Linq;
using IronworkingExpanded.Tests;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The hot blast furnace stood up in all four orientations, against the two oracles in iwex's
/// <see cref="FurnaceLayoutRig"/>. Only the hot furnace is here: the cold blast furnace, the cupola and
/// the tall-hopper exclusion are the same check on iwex types and live in that suite's matrix. What makes
/// this one worth its own run is the gas outlets - it is the only furnace with any, so it is the only one
/// whose stack cells have a rotation to get wrong.
/// </summary>
public class FurnaceOrientationMatrixTests
{
  // NOTE: string-only theory data, deliberately - a [Theory] argument naming a game type is resolved by
  // xUnit's discovery reflection before the module initializer registers VsAssemblyResolver.

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Hot_furnace_functional_cells_track_its_orientation(string side)
  {
    var be = new BlockEntityBlastFurnaceHot { Pos = new BlockPos(0, 16, 0) };
    Orient(be, $"smex:blastfurnacecore-{side}", side);
    AssertFurnaceMatrix(
      be,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      "smex:blastfurnacecore-*",
      side
    );
  }
}
