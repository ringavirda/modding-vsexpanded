using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Structures;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Cross-checks the iron tier's furnaces - the cold blast furnace and the cupola - against the multiblock
/// layouts their anchor blocks ship: a tuyere offset must resolve to a tuyere cell in the layout, a tap
/// offset to a tap cell, the shaft centre to the charge column. Nothing else catches a wrong offset, since
/// the compiler sees two valid ints and the behavioural tests place their peripherals at whatever the block
/// entity asks for. The oracle is <see cref="FurnaceLayoutRig"/>; the hot blast furnace runs the same
/// assertions from the smex suite. These are the north-only checks -
/// <see cref="FurnaceOrientationMatrixTests"/> covers the other three orientations.
/// </summary>
public class FurnaceGeometryTests {
  #region Furnace standup

  // Plain concrete helpers, no generic with a `where T : BlockEntity` constraint - see the caveat on
  // FurnaceLayoutRig.

  /// <summary>
  /// The cold furnace's block entity oriented north, so its structure-local offsets and its world offsets
  /// from the anchor coincide and the geometry properties read straight off the instance.
  /// </summary>
  private static BlockEntityBlastFurnaceCold ColdFurnace() {
    var be = new BlockEntityBlastFurnaceCold { Pos = new BlockPos(0, 16, 0) };
    OrientWithLayout(be, ColdDef(), "iiex:furnace-blastcore-tier1-n", "north");
    return be;
  }

  /// <summary>The cupola's block entity, oriented north.</summary>
  private static BlockEntityCupolaFurnace CupolaFurnace() {
    var be = new BlockEntityCupolaFurnace { Pos = new BlockPos(0, 16, 0) };
    OrientWithLayout(
      be,
      CupolaDef(),
      "iiex:furnace-cupolacore-tier1-n",
      "north"
    );
    return be;
  }

  private static ExBlockDef ColdDef() =>
    BlockBlastFurnaceCoreCold.Definitions("iiex").Single();

  private static ExBlockDef CupolaDef() =>
    BlockCupolaFurnaceCore.Definitions("iiex").Single();

  #endregion

  #region Cold blast furnace

  [Fact]
  public void Cold_furnace_offsets_line_up_with_its_layout() =>
    // The anchor glyph is facing-pinned rather than `.Any`: the core's own cell demands a correctly
    // facing core, and `MultiblockFacings` rotates the letter with the structure, so the drawing reads
    // `-north` and a furnace built facing east reads `-east`.
    AssertFurnaceGeometry(
      ColdFurnace(),
      ColdDef(),
      IiexBlocks.FurnaceBlastcore.WithSide(BlockFacing.NORTH),
      "cold furnace",
      TapGlyphs.ShaftFurnace,
      // The shaft glyph alone: the crucible course below it is pool, not burden.
      [ShaftGlyph],
      "n",
      "s"
    );

  [Fact]
  public void Cold_furnace_declares_no_exhaust_outlets() =>
    // The cold furnace's open top is its chimney, so its layout ships no pipe outlet to point at.
    AssertNoExhaustOutlets(ColdDef());

  #endregion

  #region Cupola

  /// <summary>
  /// The cupola anchors on its own bottom-centre core, like the blast furnaces, so its structure-local
  /// offsets are read straight off the instance and checked against the core's shipped layout. Every cell
  /// it reads - the single tuyere, the cast-iron and slag taps, the single-column shaft - must land on the
  /// matching glyph.
  /// </summary>
  [Fact]
  public void Cupola_offsets_line_up_with_its_layout() =>
    AssertFurnaceGeometry(
      CupolaFurnace(),
      CupolaDef(),
      // Facing-pinned, as the cold furnace's is - see the note there.
      IiexBlocks.FurnaceCupolacore.WithSide(BlockFacing.NORTH),
      "cupola",
      // The mirrored pair: the cupola drains cast iron out its west wall and skims cinder off its east,
      // so both glyphs are the opposite facing. See TapGlyphs.
      TapGlyphs.Cupola,
      // The shaft glyph alone, as on the cold furnace.
      [ShaftGlyph],
      // One inlet, in the north wall - the cupola is blown from one side only.
      "n"
    );

  [Fact]
  public void Cupola_declares_one_tuyere_and_no_exhaust_outlets() {
    // The cupola is the narrow furnace: a single tuyere, and an open top that is its own stack, so no
    // pipe outlet. Both blast furnaces mark two tuyeres, so a role copied from either drawing onto this
    // one is caught by the count.
    Assert.Single(RoleCellsOf(CupolaDef(), FurnaceCellRoles.Tuyere));
    AssertNoExhaustOutlets(CupolaDef());
  }

  #endregion

  #region Structure fillers

  [Theory]
  [InlineData("cold")]
  [InlineData("cupola")]
  public void Filler_cells_name_the_real_filler_block(string furnace) {
    // "exlib:filler-block*" matches nothing - the block is exlib:structurefiller - so a layout naming it
    // can never complete.
    var layout = LayoutOf(furnace == "cold" ? ColdDef() : CupolaDef());

    Assert.DoesNotContain(layout.Values, code => code.Contains("filler-block"));
    Assert.Contains("exlib:structurefiller", layout.Values);
  }

  #endregion
}
