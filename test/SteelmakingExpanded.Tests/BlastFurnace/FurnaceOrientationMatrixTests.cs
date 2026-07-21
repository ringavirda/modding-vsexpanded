using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Newtonsoft.Json.Linq;
using SteelmakingExpanded.BlockStructures.Converter.BlockEntities;
using SteelmakingExpanded.BlockStructures.Converter.Blocks;
using SteelmakingExpanded.BlockStructures.CowperStove.BlockEntities;
using SteelmakingExpanded.BlockStructures.CowperStove.Blocks;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The rotation matrix the north-only <see cref="FurnaceGeometryTests"/> cannot reach. That suite pins
/// every furnace's structure-local offsets against its layout at angle 0, where local == world and the
/// rotation is never exercised; the behavioural rigs then place their own peripherals through the same
/// <c>GetGlobalPos</c> they later read, so a furnace built facing south or east could put its tuyeres,
/// taps and gas outlets one rotation out of step with the blocks the game actually placed and every
/// existing test would still pass - it would just read in game as "the furnace does not work when I face
/// it the other way". This stands each furnace up in all four orientations and checks two independent
/// oracles for every functional cell:
/// <list type="number">
/// <item><b>angle wiring</b> - <c>GetGlobalPos(cell)</c> equals the anchor plus the offset rotated by the
/// angle the test derives itself from the side (north 0, west 90, south 180, east 270), so a furnace that
/// derived the wrong angle - or slipped in a stray +180 like the cowper/converter once did - fails;</item>
/// <item><b>layout agreement</b> - that same world cell lands the matching glyph in the structure the game
/// builds from the anchor's JSON, rotated by <see cref="MultiblockStructure.InitForUse"/>. This is the
/// real oracle: it compares the furnace's own rotation (<see cref="ExOrientation"/>) against vanilla's
/// (a rotation matrix), and fails the moment the two disagree at a placed orientation.</item>
/// </list>
/// Covers the cold blast furnace, the hot blast furnace and the cupola, plus the two machines that face
/// opposite their side variant through the +180 convention - the Bessemer converter control and the
/// cowper stove - which are the exact GetGlobalPos-vs-structure disagreement class Oracle 2 was written
/// to catch (they were once built a half-turn out). The tall hopper is excluded by design - it is not a
/// multiblock structure and cannot be oriented - and that exclusion is pinned at the bottom of the file.
/// </summary>
public class FurnaceOrientationMatrixTests
{
  #region Expected role glyphs

  // The wildcard a functional cell must resolve to in the layout, per role - the same glyphs the north
  // FurnaceGeometryTests asserts, hoisted here so both oracles read one source.
  private const string TuyereGlyph = "iwex:tuyere*";
  private const string TapGlyph = "iwex:moltenmetaltap*";
  private const string ShaftGlyph = "@(air|coalpile)";
  private const string OutletGlyph = "ppex:pipe-outlet*";

  #endregion

  #region Cold blast furnace

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Cold_furnace_functional_cells_track_its_orientation(
    string side
  ) => AssertFurnaceMatrix("cold", side);

  #endregion

  #region Hot blast furnace

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Hot_furnace_functional_cells_track_its_orientation(string side) =>
    AssertFurnaceMatrix("hot", side);

  #endregion

  #region Cupola

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Cupola_functional_cells_track_its_orientation(string side) =>
    AssertFurnaceMatrix("cupola", side);

  #endregion

  #region Bessemer converter (the +180 GetGlobalPos override)

  // The converter control keeps _currentAngle at the base side angle and folds the +180 in twice - once
  // into its InitForUse call (initAngleOffset) and once into its GetGlobalPos override - so the two must
  // still agree. Each local offset mirrors the control's private peripheral constants; the glyph is the
  // block number that cell carries in converter/control's shipped layout.
  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Converter_peripherals_track_its_orientation(string side)
  {
    var be = new BlockEntityConverterControl { Pos = new BlockPos(0, 16, 0) };
    Orient(be, $"smex:convertercontrol-{side}", side);
    AssertStructureMatrix(
      be,
      BlockConverterControl.Definitions("smex").Single(),
      "smex:convertercontrol*",
      side,
      [
        (new Vec3i(0, -1, 0), "smex:convertertransmission*", "transmission"),
        (new Vec3i(0, 0, 2), "smex:converterbessemer*", "vessel"),
        (new Vec3i(0, 0, 4), "smex:converter-intake*", "gas intake"),
        (new Vec3i(1, 1, 2), "iwex:moltencanal-tap*", "input tap"),
        (new Vec3i(1, -2, 2), "iwex:moltencanal-start*", "output start"),
      ]
    );
  }

  #endregion

  #region Cowper stove (the +180 baked into the stored angle)

  // The cowper implements the same convention the other way: it bakes the +180 straight into the stored
  // _currentAngle and uses the base GetGlobalPos. Its exhaust outlet, air passthrough, hot-blast outlet
  // and heat-sink column must all rotate onto the cells cowperstove/intake's layout declares for them.
  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Cowper_peripherals_track_its_orientation(string side)
  {
    var be = new BlockEntityCowperStove { Pos = new BlockPos(0, 16, 0) };
    Orient(be, $"smex:cowperstove-{side}", side);
    AssertStructureMatrix(
      be,
      BlockCowperStoveIntake.Definitions("smex").Single(),
      "smex:cowperstove-intake*",
      side,
      [
        (new Vec3i(0, 1, 2), "ppex:pipe-passthrough-*", "air passthrough"),
        (new Vec3i(0, 0, 2), "ppex:pipe-outlet*", "exhaust outlet"),
        (new Vec3i(0, 1, 0), "ppex:pipe-outlet*", "hot-blast outlet"),
        (new Vec3i(0, 0, 1), "smex:cowperstoveheatsink*", "heat sink y0"),
        (new Vec3i(0, 1, 1), "smex:cowperstoveheatsink*", "heat sink y1"),
        (new Vec3i(0, 2, 1), "smex:cowperstoveheatsink*", "heat sink y2"),
        (new Vec3i(0, 3, 1), "smex:cowperstoveheatsink*", "heat sink y3"),
      ]
    );
  }

  #endregion

  #region Matrix body

  /// <summary>
  /// Stands <paramref name="furnace"/> up facing <paramref name="side"/> and checks both oracles for
  /// every functional cell the block entity declares: its tuyere(s), both taps, the shaft centre, and
  /// its gas outlets (none for the cold furnace and the cupola - the loop just runs zero times).
  /// </summary>
  private static void AssertFurnaceMatrix(string furnace, string side)
  {
    int angle = ExpectedAngle(side);
    var (be, def, anchorGlyph) = Stand(furnace, side);
    BlockPos pos = be.Pos;
    Dictionary<Vec3i, string> layout = RotatedLayoutOf(def, angle);

    // The anchor sits at its own layout origin, which every rotation leaves at (0,0,0).
    Assert.Equal(
      $"anchor -> {anchorGlyph}",
      $"anchor -> {At(layout, new Vec3i(0, 0, 0))}"
    );

    foreach (Vec3i tuyere in Cells(be, "TuyereCells"))
      AssertCell(be, layout, pos, angle, tuyere, TuyereGlyph, "tuyere");

    AssertCell(
      be,
      layout,
      pos,
      angle,
      Cell(be, "MetalTapCell"),
      TapGlyph,
      "metal tap"
    );
    AssertCell(
      be,
      layout,
      pos,
      angle,
      Cell(be, "SlagTapCell"),
      TapGlyph,
      "slag tap"
    );
    AssertCell(
      be,
      layout,
      pos,
      angle,
      Cell(be, "ShaftCentre"),
      ShaftGlyph,
      "shaft centre"
    );

    foreach (Vec3i outlet in Cells(be, "GasOutletCells"))
      AssertCell(be, layout, pos, angle, outlet, OutletGlyph, "gas outlet");
  }

  /// <summary>
  /// The converter and the cowper both face opposite their side variant - the +180 convention. The
  /// converter folds it into its InitForUse angle and its GetGlobalPos override; the cowper bakes it
  /// straight into the stored angle. Either way their peripherals rotate by <c>AngleFromSide + 180</c>,
  /// derived here independently so a dropped or doubled +180 fails Oracle 1, and checked against the
  /// InitForUse-rotated layout so a GetGlobalPos-vs-structure disagreement fails Oracle 2. Unlike the
  /// furnaces these machines expose no cell properties, so each <paramref name="cells"/> entry states the
  /// structure-local offset and the glyph it must resolve to (mirroring the block entity's own constants).
  /// </summary>
  private static void AssertStructureMatrix(
    BlockEntity be,
    ExBlockDef def,
    string anchorGlyph,
    string side,
    (Vec3i local, string glyph, string what)[] cells
  )
  {
    int angle = (ExpectedAngle(side) + 180) % 360;
    BlockPos pos = be.Pos;
    Dictionary<Vec3i, string> layout = RotatedLayoutOf(def, angle);

    // The anchor sits at its own layout origin, which every rotation leaves at (0,0,0).
    Assert.Equal(
      $"anchor -> {anchorGlyph}",
      $"anchor -> {At(layout, new Vec3i(0, 0, 0))}"
    );

    foreach (var (local, glyph, what) in cells)
      AssertCell(be, layout, pos, angle, local, glyph, what);
  }

  /// <summary>
  /// The two oracles for one functional cell. Oracle 1 pins the angle wiring against the shared rotation
  /// math (already covered in ExOrientationTests, used here as a trusted primitive); Oracle 2 pins that
  /// the resulting world cell agrees with the game-built, InitForUse-rotated layout.
  /// </summary>
  private static void AssertCell(
    object be,
    Dictionary<Vec3i, string> layout,
    BlockPos pos,
    int angle,
    Vec3i local,
    string glyph,
    string what
  )
  {
    BlockPos world = Global(be, local);

    // Oracle 1 - the furnace's own GetGlobalPos equals the anchor plus the independently-rotated offset.
    Vec3i r = ExOrientation.RotateOffset(local, angle);
    Assert.Equal(
      new BlockPos(pos.X + r.X, pos.Y + r.Y, pos.Z + r.Z, pos.dimension),
      world
    );

    // Oracle 2 - that world cell lands the expected glyph in the rotated layout the game assembles.
    var worldRel = new Vec3i(world.X - pos.X, world.Y - pos.Y, world.Z - pos.Z);
    Assert.Equal($"{what} -> {glyph}", $"{what} -> {At(layout, worldRel)}");
  }

  #endregion

  #region Furnace standup

  // NOTE: string-only theory data and plain (non-generic) helpers, deliberately - a [Theory] argument or
  // a generic constraint naming a game type is resolved by xUnit's discovery reflection before the module
  // initializer registers VsAssemblyResolver, which fails the whole assembly. FurnaceGeometryTests carries
  // the same caveat; this file follows it.

  /// <summary>
  /// Builds the requested furnace's block entity facing <paramref name="side"/> (block code
  /// <c>-{side}</c> + the <c>("side", side)</c> variant the vanilla HorizontalOrientable stamps), runs its
  /// rotation update, and returns it alongside its anchor block's shipped definition and core glyph.
  /// </summary>
  private static (BlockEntity be, ExBlockDef def, string anchorGlyph) Stand(
    string furnace,
    string side
  )
  {
    switch (furnace)
    {
      case "cold":
        var cold = new BlockEntityBlastFurnaceCold
        {
          Pos = new BlockPos(0, 16, 0),
        };
        Orient(cold, $"iwex:blastfurnacecore-{side}", side);
        return (
          cold,
          BlockBlastFurnaceCoreCold.Definitions("iwex").Single(),
          "iwex:blastfurnacecore-*"
        );
      case "hot":
        var hot = new BlockEntityBlastFurnaceHot
        {
          Pos = new BlockPos(0, 16, 0),
        };
        Orient(hot, $"smex:blastfurnacecore-{side}", side);
        return (
          hot,
          BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
          "smex:blastfurnacecore-*"
        );
      case "cupola":
        var cupola = new BlockEntityCupolaFurnace
        {
          Pos = new BlockPos(0, 16, 0),
        };
        Orient(cupola, $"iwex:cupolafurnacecore-{side}", side);
        return (
          cupola,
          BlockCupolaFurnaceCore.Definitions("iwex").Single(),
          "iwex:cupolafurnacecore-*"
        );
      default:
        throw new ArgumentOutOfRangeException(nameof(furnace), furnace, null);
    }
  }

  private static void Orient(BlockEntity be, string blockCode, string side)
  {
    var world = new TestWorld();
    be.Block = TestBlocks.Configure(new Block(), blockCode, 1, ("side", side));
    world.Attach(be);
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
  }

  #endregion

  #region Layout + offset reading

  /// <summary>
  /// The wanted block code at each world-relative cell of the structure the game builds from the anchor's
  /// definition, rotated by <paramref name="angle"/> through vanilla <see cref="MultiblockStructure"/> -
  /// the independent half of Oracle 2. Loaded and rotated exactly the way the production block entity does
  /// (<c>AsObject&lt;MultiblockStructure&gt;().InitForUse(angle)</c>); the test block carries no attributes,
  /// so this reads the definition rather than the instance.
  /// </summary>
  private static Dictionary<Vec3i, string> RotatedLayoutOf(
    ExBlockDef def,
    int angle
  )
  {
    JObject json = (JObject)def.ToJson()["attributes"]!["multiblockStructure"]!;

    // Authored glyph per block number, read straight off the JSON exactly as FurnaceGeometryTests does,
    // so a domainless or wildcard code - notably the shaft's "@(air|coalpile)" - keeps its authored form
    // rather than being re-domained to "game:@(air|coalpile)" by an AssetLocation round-trip.
    var codeByNumber = ((JObject)json["blockNumbers"]!)
      .Properties()
      .ToDictionary(p => (int)p.Value!, p => p.Name);

    // The rotation itself comes from vanilla MultiblockStructure - the independent half of the oracle,
    // deserialized and rotated the same way the production block entity does at placement.
    MultiblockStructure structure = new JsonObject(
      json
    ).AsObject<MultiblockStructure>()!;
    structure.InitForUse(angle);

    var cells = new Dictionary<Vec3i, string>();
    foreach (BlockOffsetAndNumber o in structure.TransformedOffsets)
      cells[new Vec3i(o.X, o.Y, o.Z)] = codeByNumber[o.W];
    return cells;
  }

  private static string At(Dictionary<Vec3i, string> layout, Vec3i cell)
  {
    Assert.True(
      layout.ContainsKey(cell),
      $"rotated cell {cell} is not part of the layout at all"
    );
    return layout[cell];
  }

  private static Vec3i Cell(object be, string property) =>
    (Vec3i)ReflectionHelpers.GetProperty(be, property)!;

  private static Vec3i[] Cells(object be, string property) =>
    (Vec3i[])ReflectionHelpers.GetProperty(be, property)!;

  private static BlockPos Global(object be, Vec3i local) =>
    (BlockPos)
      ReflectionHelpers.Invoke(be, "GetGlobalPos", local.X, local.Y, local.Z)!;

  private static int ExpectedAngle(string side) =>
    side switch
    {
      "north" => 0,
      "west" => 90,
      "south" => 180,
      "east" => 270,
      _ => throw new ArgumentOutOfRangeException(nameof(side), side, null),
    };

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
