using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The rotation the north-only <see cref="FurnaceGeometryTests"/> cannot reach, for the iron tier's
/// furnaces. That suite pins offsets at angle 0, where structure-local equals world, and the behavioural
/// rigs place their peripherals through the same <c>GetGlobalPos</c> they later read, so a furnace built
/// facing south or east could put its tuyeres and taps one rotation out of step with the blocks the game
/// placed and still pass. <see cref="FurnaceLayoutRig.AssertFurnaceMatrix"/> stands each furnace up in a
/// given orientation and checks two independent oracles per functional cell: the furnace's own angle
/// wiring, and agreement with the structure vanilla assembles. The hot blast furnace runs the same matrix
/// from the smex suite.
/// </summary>
public class FurnaceOrientationMatrixTests {
  // String-only theory data and plain (non-generic) helpers: a [Theory] argument or a generic constraint
  // naming a game type is resolved by xUnit's discovery reflection before the module initializer registers
  // VsAssemblyResolver, which fails the whole assembly.

  #region Cold blast furnace

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Cold_furnace_functional_cells_track_its_orientation(string side) {
    var be = new BlockEntityBlastFurnaceCold { Pos = new BlockPos(0, 16, 0) };
    OrientWithLayout(
      be,
      BlockBlastFurnaceCoreCold.Definitions("iiex").Single(),
      $"iiex:furnace-blastcore-tier1-{side}",
      side
    );
    AssertFurnaceMatrix(
      be,
      BlockBlastFurnaceCoreCold.Definitions("iiex").Single(),
      // Facing-pinned: the core's own cell demands a correctly facing core, and MultiblockFacings
      // rotates that letter with the structure.
      IiexBlocks.FurnaceBlastcore.WithSide(BlockFacing.NORTH),
      side,
      TapGlyphs.ShaftFurnace,
      "n",
      "s"
    );
  }

  #endregion

  #region Cupola

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Cupola_functional_cells_track_its_orientation(string side) {
    var be = new BlockEntityCupolaFurnace { Pos = new BlockPos(0, 16, 0) };
    OrientWithLayout(
      be,
      BlockCupolaFurnaceCore.Definitions("iiex").Single(),
      $"iiex:furnace-cupolacore-tier1-{side}",
      side
    );
    AssertFurnaceMatrix(
      be,
      BlockCupolaFurnaceCore.Definitions("iiex").Single(),
      // Facing-pinned: the core's own cell demands a correctly facing core, and MultiblockFacings
      // rotates that letter with the structure.
      IiexBlocks.FurnaceCupolacore.WithSide(BlockFacing.NORTH),
      side,
      // The cupola's own hand, mirrored from the blast furnaces'. See TapGlyphs.
      TapGlyphs.Cupola,
      "n"
    );
  }

  #endregion

  #region Shaft columns

  /// <summary>
  /// A cold furnace with a lopsided shaft: 3 cells on x, 4 on z, off-centre on both and not symmetric
  /// under any rotation. Every shipped shaft is square or a single cell, so the rotation is only visible
  /// on an invented shape and this one is load-bearing for the test below. The shape lives in a drawing
  /// because the shaft box is the bounding box of the cells the layout marks <c>Chargeable</c>.
  /// </summary>
  private static ExBlockDef LopsidedDef() =>
    ShaftBoxDef(new Vec3i(-2, 1, -1), new Vec3i(0, 5, 2));

  /// <summary>
  /// Where a furnace's charge columns stand once it is turned. The keys are structure-local, so the facing
  /// alone decides which world cells they name.
  /// <para>
  /// A second, independent check on a column set derived from the world box rather than the
  /// structure-local one. <c>ShaftColumnsTests</c> holds the first and compares key sets; this case never
  /// looks at the key set - it puts each key back through the furnace's own <c>GetGlobalPos</c> and
  /// compares the resulting footprint against independently rotated offsets, so a key that was already
  /// rotated lands on cells the shaft does not occupy.
  /// </para>
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Shaft_columns_stand_over_the_world_cells_the_facing_puts_them_on(
    string side
  ) {
    var be = new BlockEntityBlastFurnaceCold { Pos = new BlockPos(0, 16, 0) };
    OrientWithLayout(
      be,
      LopsidedDef(),
      $"iiex:furnace-blastcore-tier1-{side}",
      side
    );
    int angle = AngleFromSide(side);
    const int floor = 1; // any level of the shaft does; the columns differ only in (x, z)

    // Where the columns have to be, from the declared box and the shared rotation math, without asking
    // the furnace anything.
    var expected = new List<(int X, int Z)>();
    for (int x = -2; x <= 0; x++)
      for (int z = -1; z <= 2; z++) {
        Vec3i r = ExOrientation.RotateOffset(new Vec3i(x, floor, z), angle);
        expected.Add((be.Pos.X + r.X, be.Pos.Z + r.Z));
      }

    // Where they actually are, one world cell per column the furnace says it owns.
    var actual = be.ShaftColumns.Keys.Select(k => {
      BlockPos world = Global(be, new Vec3i(k.X, floor, k.Z));
      return (X: world.X, Z: world.Z);
    });

    Assert.Equal(12, expected.Count); // the loop is 3x4, so a vacuous pass is not available
    Assert.Equal(
      expected.OrderBy(c => c.X).ThenBy(c => c.Z),
      actual.OrderBy(c => c.X).ThenBy(c => c.Z)
    );
  }

  #endregion

  #region Tall hopper (excluded from the matrix by design)

  /// <summary>
  /// The tall hopper cannot join the matrix above: it is not a multiblock anchor and hardcodes
  /// <see cref="ExpandedLib.Blocks.Structures.BlockFilledMegastructure.StructureAngle"/> to 0, so there
  /// is no rotated structure to stand up.
  /// <para>
  /// Charge columns are keyed structure-local on the furnace core and the hopper asks the core which one
  /// to lay on (<c>NextChargeColumn</c>), so the hopper holds no offset table for a facing to rotate and
  /// no world walk that can reach past the furnace's own footprint. This case guards that absence; the
  /// behavioural half - a hopper at all four facings charging the same nine columns - is
  /// <c>HopperTallTests.A_hopper_charges_the_same_column_set_at_every_facing</c>.
  /// </para>
  /// </summary>
  [Fact]
  public void The_tall_hopper_carries_no_rotated_offset_table_of_its_own() {
    BlockHopperTall hopper = TestBlocks.Configure(
      new BlockHopperTall(),
      "iiex:hopper-tall",
      1
    );
    Assert.Equal(0, hopper.StructureAngle);

    // Both members by name: a table of offsets is only useful if something rotates it, and a rotation is
    // only ever applied to a table, so either one returning brings the whole defect back.
    foreach (string gone in new[] { "Columns", "DripAngle" })
      Assert.True(
        typeof(BlockEntityHopperTall).GetField(
          gone,
          BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance
        ) == null
          && typeof(BlockEntityHopperTall).GetProperty(
            gone,
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance
          ) == null,
        $"BlockEntityHopperTall.{gone} is back - the hopper is deriving shaft geometry again "
          + "instead of asking the furnace core for it"
      );
  }

  #endregion
}
