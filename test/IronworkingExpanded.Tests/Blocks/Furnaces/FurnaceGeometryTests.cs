using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Cross-checks the iron tier's furnaces - the cold blast furnace and the cupola - against the multiblock
/// layouts their anchor blocks ship. Nothing else can catch a wrong offset: the compiler sees two valid
/// ints and the behavioural tests place their own peripherals at whatever the block entity asks for, so a
/// tuyere that drifted one cell off the layout's 'Y' would pass the whole suite and read in game as "the
/// furnace just does not work". These pin the correspondence instead - a tuyere offset must resolve to a
/// tuyere cell in the layout, a tap offset to a tap cell, the shaft centre to the charge column.
/// <para>
/// The oracle itself is <see cref="FurnaceLayoutRig"/>; the hot blast furnace runs the same assertions
/// from the smex suite. These are the north-only checks - <see cref="FurnaceOrientationMatrixTests"/>
/// covers the other three orientations.
/// </para>
/// </summary>
public class FurnaceGeometryTests
{
  #region Furnace standup

  // Note: plain concrete helpers, no generic with a `where T : BlockEntity` constraint - see the caveat
  // on FurnaceLayoutRig.

  /// <summary>
  /// The cold furnace's block entity oriented north, so its structure-local offsets and its world offsets
  /// from the anchor coincide and the geometry properties read straight off the instance.
  /// </summary>
  private static BlockEntityBlastFurnaceCold ColdFurnace()
  {
    var be = new BlockEntityBlastFurnaceCold { Pos = new BlockPos(0, 16, 0) };
    OrientWithLayout(be, ColdDef(), "iwex:furnace-blastcore-tier1-n", "north");
    return be;
  }

  /// <summary>The cupola's block entity, oriented north.</summary>
  private static BlockEntityCupolaFurnace CupolaFurnace()
  {
    var be = new BlockEntityCupolaFurnace { Pos = new BlockPos(0, 16, 0) };
    OrientWithLayout(be, CupolaDef(), "iwex:furnace-cupolacore-tier1-n", "north");
    return be;
  }

  private static ExBlockDef ColdDef() =>
    BlockBlastFurnaceCoreCold.Definitions("iwex").Single();

  private static ExBlockDef CupolaDef() =>
    BlockCupolaFurnaceCore.Definitions("iwex").Single();

  #endregion

  #region Cold blast furnace

  [Fact]
  public void Cold_furnace_offsets_line_up_with_its_layout() =>
    // The anchor glyph is facing-pinned, not `.Any`. The furnace redraw made the core's own cell
    // demand a correctly-facing core, so a furnace built with the anchor turned the wrong way no
    // longer completes -- `MultiblockFacings` rotates the letter with the structure, so this reads
    // `-north` in the drawing and `-east` in a furnace built facing east.
    AssertFurnaceGeometry(
      ColdFurnace(),
      ColdDef(),
      IwexBlocks.FurnaceBlastcore.WithSide(BlockFacing.NORTH),
      "cold furnace",
      TapGlyphs.ShaftFurnace,
      NorthTuyereGlyph,
      SouthTuyereGlyph
    );

  [Fact]
  public void Cold_furnace_declares_no_exhaust_outlets() =>
    // The cold furnace's open top is its chimney, so its layout ships no pipe outlet to point at.
    AssertNoExhaustOutlets(ColdDef());

  #endregion

  #region Cupola

  /// <summary>
  /// The cupola anchors on its own bottom-centre core, exactly like the blast furnaces, so its block
  /// entity's structure-local offsets are read straight off the instance and checked against the core's
  /// shipped layout (no hatch-frame translation any more). Every cell the cupola reads - its single
  /// tuyere, its cast-iron and slag taps, its single-column shaft - must land on the matching glyph, or
  /// the furnace is built but "just does not work".
  /// </summary>
  [Fact]
  public void Cupola_offsets_line_up_with_its_layout() =>
    AssertFurnaceGeometry(
      CupolaFurnace(),
      CupolaDef(),
      // Facing-pinned, as the cold furnace's is - see the note there.
      IwexBlocks.FurnaceCupolacore.WithSide(BlockFacing.NORTH),
      "cupola",
      // The mirrored pair, not the blast furnaces'. The cupola drains cast iron out its west wall and
      // skims cinder off its east, so both glyphs are the opposite facing. See TapGlyphs.
      TapGlyphs.Cupola,
      // One inlet, in the north wall - the cupola is blown from one side only.
      NorthTuyereGlyph
    );

  [Fact]
  public void Cupola_declares_one_tuyere_and_no_exhaust_outlets()
  {
    // The cupola is the narrow furnace: a single tuyere, and (like the cold blast furnace) an open top
    // that is its own stack, so it ships no pipe outlet to point at. The count is worth stating on its own
    // - both blast furnaces mark two, so a role copied from either of them onto this drawing would be
    // caught here rather than read as a plausible cupola.
    Assert.Single(RoleCellsOf(CupolaDef(), CellRole.Tuyere));
    AssertNoExhaustOutlets(CupolaDef());
  }

  #endregion

  #region Structure fillers

  [Theory]
  [InlineData("cold")]
  [InlineData("cupola")]
  public void Filler_cells_name_the_real_filler_block(string furnace)
  {
    // "exlib:filler-block*" matches nothing (the block is exlib:structurefiller), so a layout naming
    // it can never complete - the cold furnace and the cupola both shipped that typo.
    var layout = LayoutOf(furnace == "cold" ? ColdDef() : CupolaDef());

    Assert.DoesNotContain(layout.Values, code => code.Contains("filler-block"));
    Assert.Contains("exlib:structurefiller", layout.Values);
  }

  #endregion
}
