using System.Linq;
using IronIndustryExpanded.Tests;
using SteelIndustryExpanded.BlockStructures.CowperStove.BlockEntities;
using SteelIndustryExpanded.BlockStructures.CowperStove.Blocks;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The cowper stove against the same two-oracle matrix the furnaces run. It implements the +180
/// convention the other way round from <see cref="ConverterOrientationTests"/>: the half-turn is
/// baked into the stored <c>_currentAngle</c> and the base <c>GetGlobalPos</c> is used unchanged.
/// Its exhaust outlet, air passthrough, hot-blast outlet and heat-sink column must all rotate onto
/// the cells cowperstove/intake's layout declares for them.
/// </summary>
public class CowperOrientationTests {
  // Theory data is string-only: a [Theory] argument naming a game type is resolved by xUnit's
  // discovery reflection before the module initializer registers VsAssemblyResolver.

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Cowper_peripherals_track_its_orientation(string side) {
    var be = new BlockEntityCowperStove { Pos = new BlockPos(0, 16, 0) };
    Orient(be, $"siex:cowperstove-{side}", side);
    AssertPlus180Matrix(
      be,
      BlockCowperStoveIntake.Definitions("siex").Single(),
      SiexBlocks.CowperstoveIntake.Any,
      side,
      [
        (new Vec3i(0, 1, 2), "iiex:pipe-cast-passthrough-*", "air passthrough"),
        (new Vec3i(0, 0, 2), "iiex:pipe-outlet*", "exhaust outlet"),
        (new Vec3i(0, 1, 0), "iiex:pipe-outlet*", "hot-blast outlet"),
        (
          new Vec3i(0, 0, 1),
          SiexBlocks.CowperstoveHeatsink.Any,
          "heat sink y0"
        ),
        (
          new Vec3i(0, 1, 1),
          SiexBlocks.CowperstoveHeatsink.Any,
          "heat sink y1"
        ),
        (
          new Vec3i(0, 2, 1),
          SiexBlocks.CowperstoveHeatsink.Any,
          "heat sink y2"
        ),
        (
          new Vec3i(0, 3, 1),
          SiexBlocks.CowperstoveHeatsink.Any,
          "heat sink y3"
        ),
      ]
    );
  }
}
