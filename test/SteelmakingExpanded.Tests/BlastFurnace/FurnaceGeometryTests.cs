using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Newtonsoft.Json.Linq;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Cross-checks every furnace block entity's structure-local cell offsets against the multiblock
/// layout its anchor block ships. Nothing else can catch a wrong offset: the compiler sees two valid
/// ints and the behavioural tests place their own peripherals at whatever the block entity asks for,
/// so a tuyere that drifted one cell off the layout's 'Y' would pass the whole suite and read in game
/// as "the furnace just does not work". These tests pin the correspondence instead - a tuyere offset
/// must resolve to a tuyere cell in the layout, a tap offset to a tap cell, the shaft centre to the
/// charge column - for the cold blast furnace, the hot blast furnace and the cupola.
/// </summary>
public class FurnaceGeometryTests
{
  #region Layout reading

  /// <summary>
  /// The wanted block code at each structure-local cell, read back out of the generated
  /// <c>multiblockStructure</c> attribute (block numbers resolved through <c>blockNumbers</c>) - i.e.
  /// the same table the game builds the structure from, not a restatement of the ASCII drawing.
  /// </summary>
  private static Dictionary<Vec3i, string> LayoutOf(ExBlockDef def)
  {
    JObject structure = (JObject)
      def.ToJson()["attributes"]!["multiblockStructure"]!;

    var codeByNumber = ((JObject)structure["blockNumbers"]!)
      .Properties()
      .ToDictionary(p => (int)p.Value!, p => p.Name);

    var cells = new Dictionary<Vec3i, string>();
    foreach (JToken offset in (JArray)structure["offsets"]!)
      cells[
        new Vec3i((int)offset["x"]!, (int)offset["y"]!, (int)offset["z"]!)
      ] = codeByNumber[(int)offset["w"]!];
    return cells;
  }

  private static string At(Dictionary<Vec3i, string> layout, Vec3i cell)
  {
    Assert.True(
      layout.ContainsKey(cell),
      $"local cell {cell} is not part of the layout at all"
    );
    return layout[cell];
  }

  private static void AssertCell(
    Dictionary<Vec3i, string> layout,
    Vec3i cell,
    string expectedCode,
    string what
  ) =>
    Assert.Equal($"{what} -> {expectedCode}", $"{what} -> {At(layout, cell)}");

  #endregion

  #region Block-entity offset reading

  // NOTE: no generic helper with a `where T : BlockEntity` constraint. A generic constraint naming a
  // game type is resolved when xUnit reflects over this class during discovery - which happens before
  // the module initializer registers VsAssemblyResolver - so it fails the whole assembly with
  // "could not find dependent assembly VintagestoryAPI". Plain concrete helpers avoid that entirely.

  /// <summary>
  /// The cold furnace's block entity oriented north, so its structure-local offsets and its world
  /// offsets from the anchor coincide and the geometry properties read straight off the instance.
  /// </summary>
  private static BlockEntityBlastFurnaceCold ColdFurnace()
  {
    var be = new BlockEntityBlastFurnaceCold { Pos = new BlockPos(0, 16, 0) };
    Orient(be, "iwex:blastfurnacecore-north");
    return be;
  }

  /// <summary>The hot furnace's block entity, oriented north.</summary>
  private static BlockEntityBlastFurnaceHot HotFurnace()
  {
    var be = new BlockEntityBlastFurnaceHot { Pos = new BlockPos(0, 16, 0) };
    Orient(be, "smex:blastfurnacecore-north");
    return be;
  }

  /// <summary>The cupola's block entity, oriented north.</summary>
  private static BlockEntityCupolaFurnace CupolaFurnace()
  {
    var be = new BlockEntityCupolaFurnace { Pos = new BlockPos(0, 16, 0) };
    Orient(be, "iwex:cupolafurnacecore-north");
    return be;
  }

  private static void Orient(BlockEntity be, string blockCode)
  {
    var world = new TestWorld();
    be.Block = TestBlocks.Configure(
      new Block(),
      blockCode,
      1,
      ("side", "north")
    );
    world.Attach(be);
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
  }

  private static Vec3i Cell(object be, string property) =>
    (Vec3i)ReflectionHelpers.GetProperty(be, property)!;

  private static Vec3i[] Cells(object be, string property) =>
    (Vec3i[])ReflectionHelpers.GetProperty(be, property)!;

  #endregion

  #region Shaft geometry

  /// <summary>The layout's chargeable cells - the shaft column the burden actually occupies.</summary>
  private static List<Vec3i> ChargeCells(Dictionary<Vec3i, string> layout) =>
    layout
      .Where(kv => kv.Value == "@(air|coalpile)")
      .Select(kv => kv.Key)
      .ToList();

  /// <summary>
  /// Checks the block entity's shaft box and its solidify cells against the layout rather than
  /// against restated literals. The box must contain every chargeable cell (or the tick's charge walk
  /// silently misses part of the column), and the solidify cells must be exactly the chargeable cells
  /// on the lowest level of the shaft - which is what "the molten pool freezes across the bottommost
  /// layer" means. Derived here, declared there: a layout change that moves the hearth floor fails
  /// this instead of quietly dropping iron into a wall.
  /// </summary>
  private static void AssertShaftMatchesLayout(
    Dictionary<Vec3i, string> layout,
    object be,
    string furnace
  )
  {
    List<Vec3i> charge = ChargeCells(layout);
    Assert.NotEmpty(charge);

    Vec3i min = Cell(be, "ShaftMin");
    Vec3i max = Cell(be, "ShaftMax");
    foreach (Vec3i c in charge)
      Assert.True(
        c.X >= min.X
          && c.X <= max.X
          && c.Y >= min.Y
          && c.Y <= max.Y
          && c.Z >= min.Z
          && c.Z <= max.Z,
        $"{furnace}: charge cell {c} is outside the shaft box {min}..{max}"
      );

    int floorY = charge.Min(c => c.Y);
    var expected = charge
      .Where(c => c.Y == floorY)
      .OrderBy(c => c.X)
      .ThenBy(c => c.Z);
    var declared = Cells(be, "SolidifyCells")
      .OrderBy(c => c.X)
      .ThenBy(c => c.Z);
    Assert.Equal(string.Join(", ", expected), string.Join(", ", declared));
  }

  #endregion

  #region Cold blast furnace

  [Fact]
  public void Cold_furnace_offsets_line_up_with_its_layout()
  {
    var layout = LayoutOf(
      BlockBlastFurnaceCoreCold.Definitions("iwex").Single()
    );
    var be = ColdFurnace();

    // The anchor stands in its own layout, at the layout's origin.
    AssertCell(layout, new Vec3i(0, 0, 0), "iwex:blastfurnacecore-*", "anchor");

    foreach (Vec3i tuyere in Cells(be, "TuyereCells"))
      AssertCell(layout, tuyere, "iwex:tuyere*", "tuyere");

    AssertCell(
      layout,
      Cell(be, "MetalTapCell"),
      "iwex:moltenmetaltap*",
      "iron tap"
    );
    AssertCell(
      layout,
      Cell(be, "SlagTapCell"),
      "iwex:moltenmetaltap*",
      "slag tap"
    );
    AssertCell(
      layout,
      Cell(be, "ShaftCentre"),
      "@(air|coalpile)",
      "shaft centre"
    );
    AssertShaftMatchesLayout(layout, be, "cold furnace");
  }

  [Fact]
  public void Cold_furnace_declares_no_exhaust_outlets()
  {
    // The cold furnace's open top IS its chimney, so its layout ships no pipe outlet to point at.
    var layout = LayoutOf(
      BlockBlastFurnaceCoreCold.Definitions("iwex").Single()
    );
    Assert.DoesNotContain("ppex:pipe-outlet*", layout.Values);
    Assert.Empty(Cells(ColdFurnace(), "GasOutletCells"));
  }

  #endregion

  #region Hot blast furnace

  [Fact]
  public void Hot_furnace_offsets_line_up_with_its_layout()
  {
    var layout = LayoutOf(
      BlockBlastFurnaceCoreHot.Definitions("smex").Single()
    );
    var be = HotFurnace();

    AssertCell(layout, new Vec3i(0, 0, 0), "smex:blastfurnacecore-*", "anchor");

    foreach (Vec3i tuyere in Cells(be, "TuyereCells"))
      AssertCell(layout, tuyere, "iwex:tuyere*", "tuyere");

    // The hot furnace is the one that vents: its outlets must land on the layout's pipe outlets.
    Vec3i[] outlets = Cells(be, "GasOutletCells");
    Assert.NotEmpty(outlets);
    foreach (Vec3i outlet in outlets)
      AssertCell(layout, outlet, "ppex:pipe-outlet*", "gas outlet");

    AssertCell(
      layout,
      Cell(be, "MetalTapCell"),
      "iwex:moltenmetaltap*",
      "iron tap"
    );
    AssertCell(
      layout,
      Cell(be, "SlagTapCell"),
      "iwex:moltenmetaltap*",
      "slag tap"
    );
    AssertCell(
      layout,
      Cell(be, "ShaftCentre"),
      "@(air|coalpile)",
      "shaft centre"
    );
    AssertShaftMatchesLayout(layout, be, "hot furnace");
  }

  #endregion

  #region Cupola

  /// <summary>
  /// The cupola now anchors on its own bottom-centre core, exactly like the blast furnaces, so its
  /// block entity's structure-local offsets are read straight off the instance and checked against the
  /// core's shipped layout (no hatch-frame translation any more). Every cell the cupola reads - its
  /// single tuyere, its cast-iron and slag taps, its single-column shaft - must land on the matching
  /// glyph, or the furnace is built but "just does not work".
  /// </summary>
  [Fact]
  public void Cupola_offsets_line_up_with_its_layout()
  {
    var layout = LayoutOf(BlockCupolaFurnaceCore.Definitions("iwex").Single());
    var be = CupolaFurnace();

    // The anchor stands in its own layout, at the layout's origin.
    AssertCell(layout, new Vec3i(0, 0, 0), "iwex:cupolafurnacecore-*", "anchor");

    foreach (Vec3i tuyere in Cells(be, "TuyereCells"))
      AssertCell(layout, tuyere, "iwex:tuyere*", "tuyere");

    AssertCell(
      layout,
      Cell(be, "MetalTapCell"),
      "iwex:moltenmetaltap*",
      "cast-iron tap"
    );
    AssertCell(
      layout,
      Cell(be, "SlagTapCell"),
      "iwex:moltenmetaltap*",
      "slag tap"
    );
    AssertCell(
      layout,
      Cell(be, "ShaftCentre"),
      "@(air|coalpile)",
      "shaft centre"
    );
    AssertShaftMatchesLayout(layout, be, "cupola");
  }

  [Fact]
  public void Cupola_declares_one_tuyere_and_no_exhaust_outlets()
  {
    // The cupola is the narrow furnace: a single tuyere, and (like the cold blast furnace) an open top
    // that is its own stack, so it ships no pipe outlet to point at.
    var layout = LayoutOf(BlockCupolaFurnaceCore.Definitions("iwex").Single());
    Assert.DoesNotContain("ppex:pipe-outlet*", layout.Values);

    var be = CupolaFurnace();
    Assert.Single(Cells(be, "TuyereCells"));
    Assert.Empty(Cells(be, "GasOutletCells"));
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
    var layout = LayoutOf(
      furnace == "cold"
        ? BlockBlastFurnaceCoreCold.Definitions("iwex").Single()
        : BlockCupolaFurnaceCore.Definitions("iwex").Single()
    );

    Assert.DoesNotContain(layout.Values, code => code.Contains("filler-block"));
    Assert.Contains("exlib:structurefiller", layout.Values);
  }

  #endregion
}
