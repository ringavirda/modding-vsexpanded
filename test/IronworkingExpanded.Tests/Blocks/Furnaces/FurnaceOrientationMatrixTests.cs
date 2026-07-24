using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The rotation matrix the north-only <see cref="FurnaceGeometryTests"/> cannot reach, for the iron tier's
/// furnaces. That suite pins every furnace's structure-local offsets against its layout at angle 0, where
/// local == world and the rotation is never exercised; the behavioural rigs then place their own
/// peripherals through the same <c>GetGlobalPos</c> they later read, so a furnace built facing south or
/// east could put its tuyeres and taps one rotation out of step with the blocks the game actually placed
/// and every existing test would still pass - it would just read in game as "the furnace does not work
/// when I face it the other way".
/// <para>
/// <see cref="FurnaceLayoutRig.AssertFurnaceMatrix"/> stands each furnace up in a given orientation and
/// checks two independent oracles for every functional cell - the furnace's own angle wiring, and
/// agreement with the structure vanilla assembles. The hot blast furnace runs the same matrix from the
/// smex suite, alongside the two +180 machines the oracle was written to catch.
/// </para>
/// </summary>
public class FurnaceOrientationMatrixTests
{
  // NOTE: string-only theory data and plain (non-generic) helpers, deliberately - a [Theory] argument or
  // a generic constraint naming a game type is resolved by xUnit's discovery reflection before the module
  // initializer registers VsAssemblyResolver, which fails the whole assembly.

  #region Cold blast furnace

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Cold_furnace_functional_cells_track_its_orientation(string side)
  {
    var be = new BlockEntityBlastFurnaceCold { Pos = new BlockPos(0, 16, 0) };
    Orient(be, $"iwex:blastfurnacecore-{side}", side);
    AssertFurnaceMatrix(
      be,
      BlockBlastFurnaceCoreCold.Definitions("iwex").Single(),
      "iwex:blastfurnacecore-*",
      side
    );
  }

  #endregion

  #region Cupola

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Cupola_functional_cells_track_its_orientation(string side)
  {
    var be = new BlockEntityCupolaFurnace { Pos = new BlockPos(0, 16, 0) };
    Orient(be, $"iwex:cupolafurnacecore-{side}", side);
    AssertFurnaceMatrix(
      be,
      BlockCupolaFurnaceCore.Definitions("iwex").Single(),
      "iwex:cupolafurnacecore-*",
      side
    );
  }

  #endregion

  #region Tall hopper (excluded from the matrix by design)

  /// <summary>
  /// The tall hopper is not a multiblock structure and cannot join the matrix above: it declares no
  /// <c>side</c>/<c>orientation</c> variant (so it has no four orientations to stand up), it hardcodes
  /// <see cref="ExpandedLib.Blocks.Structures.BlockFilledMegastructure.StructureAngle"/> to 0, its single
  /// filler sits on the Y axis directly above the base (which no rotation moves), and its drip walks an
  /// orientation-blind column set. There is therefore no rotated world position to assert. What could
  /// regress is that invariance itself, so pin it: both facts are what make the hopper rotation-proof.
  /// </summary>
  [Fact]
  public void Tall_hopper_geometry_is_rotation_invariant_by_design()
  {
    BlockHopperTall hopper = TestBlocks.Configure(
      new BlockHopperTall(),
      "iwex:hopper-tall",
      1
    );
    Assert.Equal(0, hopper.StructureAngle);

    var columns = ((int dx, int dz)[])
      typeof(BlockEntityHopperTall)
        .GetField("Columns", BindingFlags.NonPublic | BindingFlags.Static)!
        .GetValue(null)!;
    Assert.Equal(
      new (int, int)[] { (0, 0), (0, -1), (0, 1), (-1, 0), (1, 0) },
      columns
    );
  }

  #endregion
}
