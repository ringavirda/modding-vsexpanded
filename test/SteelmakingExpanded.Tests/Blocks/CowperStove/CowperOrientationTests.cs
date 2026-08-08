using System.Linq;
using IronworkingExpanded.Tests;
using SteelmakingExpanded.BlockStructures.CowperStove.BlockEntities;
using SteelmakingExpanded.BlockStructures.CowperStove.Blocks;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The cowper stove against the same two-oracle matrix the furnaces run. It implements the +180 convention
/// the other way round from <see cref="ConverterOrientationTests"/>: it bakes the half-turn straight into
/// the stored <c>_currentAngle</c> and uses the base <c>GetGlobalPos</c>. Its exhaust outlet, air
/// passthrough, hot-blast outlet and heat-sink column must all rotate onto the cells cowperstove/intake's
/// layout declares for them - it was once built a half-turn out, which no other test could see.
/// </summary>
public class CowperOrientationTests
{
  // Note: string-only theory data, deliberately - a [Theory] argument naming a game type is resolved by
  // xUnit's discovery reflection before the module initializer registers VsAssemblyResolver.

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Cowper_peripherals_track_its_orientation(string side)
  {
    var be = new BlockEntityCowperStove { Pos = new BlockPos(0, 16, 0) };
    Orient(be, $"smex:cowperstove-{side}", side);
    AssertPlus180Matrix(
      be,
      BlockCowperStoveIntake.Definitions("smex").Single(),
      SmexBlocks.CowperstoveIntake.Any,
      side,
      [
        (new Vec3i(0, 1, 2), "lpex:pipe-passthrough-*", "air passthrough"),
        (new Vec3i(0, 0, 2), "lpex:pipe-outlet*", "exhaust outlet"),
        (new Vec3i(0, 1, 0), "lpex:pipe-outlet*", "hot-blast outlet"),
        (new Vec3i(0, 0, 1), SmexBlocks.CowperstoveHeatsink.Any, "heat sink y0"),
        (new Vec3i(0, 1, 1), SmexBlocks.CowperstoveHeatsink.Any, "heat sink y1"),
        (new Vec3i(0, 2, 1), SmexBlocks.CowperstoveHeatsink.Any, "heat sink y2"),
        (new Vec3i(0, 3, 1), SmexBlocks.CowperstoveHeatsink.Any, "heat sink y3"),
      ]
    );
  }
}
