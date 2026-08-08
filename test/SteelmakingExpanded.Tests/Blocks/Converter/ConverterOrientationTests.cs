using IronworkingExpanded;
using System.Linq;
using IronworkingExpanded.Tests;
using SteelmakingExpanded.BlockStructures.Converter.BlockEntities;
using SteelmakingExpanded.BlockStructures.Converter.Blocks;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The Bessemer converter against the same two-oracle matrix the furnaces run, because it is one of the
/// two machines that face <b>opposite</b> their side variant - the +180 convention - and that is exactly
/// the GetGlobalPos-vs-structure disagreement class the oracle was written to catch (both were once built
/// a half-turn out).
/// <para>
/// The control keeps <c>_currentAngle</c> at the base side angle and folds the +180 in twice - once into
/// its <c>InitForUse</c> call (initAngleOffset) and once into its <c>GetGlobalPos</c> override - so the two
/// must still agree. Each local offset below mirrors the control's private peripheral constants; the glyph
/// is the block number that cell carries in converter/control's shipped layout.
/// </para>
/// </summary>
public class ConverterOrientationTests
{
  // Note: string-only theory data, deliberately - a [Theory] argument naming a game type is resolved by
  // xUnit's discovery reflection before the module initializer registers VsAssemblyResolver.

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Converter_peripherals_track_its_orientation(string side)
  {
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
        (new Vec3i(1, -2, 2), IwexBlocks.MoltenCanalBrickStart.Any, "output start"),
      ]
    );
  }
}
