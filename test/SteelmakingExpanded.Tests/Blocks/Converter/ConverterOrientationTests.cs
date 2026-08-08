using System.Linq;
using IronworkingExpanded;
using IronworkingExpanded.Tests;
using SteelmakingExpanded.BlockStructures.Converter.BlockEntities;
using SteelmakingExpanded.BlockStructures.Converter.Blocks;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The Bessemer converter against the same two-oracle matrix the furnaces run. It is one of the two
/// machines that face opposite their side variant, the +180 convention: the control keeps
/// <c>_currentAngle</c> at the base side angle and folds the half-turn in twice, once into its
/// <c>InitForUse</c> call (initAngleOffset) and once into its <c>GetGlobalPos</c> override, so the
/// two must agree. Each local offset below mirrors the control's private peripheral constants; the
/// glyph is the block number that cell carries in converter/control's shipped layout.
/// </summary>
public class ConverterOrientationTests {
  // Theory data is string-only: a [Theory] argument naming a game type is resolved by xUnit's
  // discovery reflection before the module initializer registers VsAssemblyResolver.

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Converter_peripherals_track_its_orientation(string side) {
    var be = new BlockEntityConverterControl { Pos = new BlockPos(0, 16, 0) };
    Orient(be, $"smex:convertercontrol-{side}", side);
    AssertPlus180Matrix(
      be,
      BlockConverterControl.Definitions("smex").Single(),
      "smex:convertercontrol*",
      side,
      [
        (new Vec3i(0, -1, 0), "smex:convertertransmission*", "transmission"),
        (new Vec3i(0, 0, 2), "smex:converterbessemer*", "vessel"),
        (new Vec3i(0, 0, 4), "smex:converter-intake*", "gas intake"),
        (new Vec3i(1, 1, 2), IwexBlocks.MoltenCanalTap.Any, "input tap"),
        (
          new Vec3i(1, -2, 2),
          IwexBlocks.MoltenCanalBrickStart.Any,
          "output start"
        ),
      ]
    );
  }
}
