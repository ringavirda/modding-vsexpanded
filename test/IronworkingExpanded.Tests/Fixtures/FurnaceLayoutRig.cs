using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The shared oracle for "does this multiblock's code agree with the layout it ships". Every furnace in
/// the line - iwex's cold blast furnace and cupola, smex's hot blast furnace - is the same machine wearing
/// a different shell, so the geometry checks are one body of assertions parameterised by which anchor is
/// stood up. It lives in the iwex suite because the furnace core, the tuyeres, the taps and the shaft all
/// do (see the homing rule in <c>test/README.md</c>); smex reaches it through the test-project chain.
/// <para>
/// Two oracles are on offer, and they catch different classes of bug:
/// </para>
/// <list type="number">
/// <item><b>North-only</b> (<see cref="LayoutOf"/> + <see cref="AssertFurnaceGeometry"/>) - at angle 0
/// structure-local and world offsets coincide, so this pins the raw offsets against the authored layout
/// without any rotation in the way. A tuyere that drifted one cell off the layout's glyph fails here.</item>
/// <item><b>All four orientations</b> (<see cref="RotatedLayoutOf"/> + <see cref="AssertFurnaceMatrix"/>) -
/// the block entity's own <c>GetGlobalPos</c> is checked against independently-rotated offsets *and*
/// against the structure vanilla's <see cref="MultiblockStructure.InitForUse"/> assembles. That second
/// comparison is the real oracle: it plays <see cref="ExOrientation"/> off against a rotation matrix and
/// fails the moment they disagree at a placed orientation.</item>
/// </list>
/// <para>
/// NOTE: no generic helper with a <c>where T : BlockEntity</c> constraint anywhere in this file. A generic
/// constraint naming a game type is resolved when xUnit reflects over the assembly during discovery -
/// which happens before the module initializer registers <c>VsAssemblyResolver</c> - so it fails the whole
/// assembly with "could not find dependent assembly VintagestoryAPI". Plain concrete helpers avoid that.
/// </para>
/// </summary>
public static class FurnaceLayoutRig
{
  #region Role glyphs

  // The wildcard a functional cell must resolve to in the layout, per role - one source for both oracles.

  public const string TuyereGlyph = "iwex:tuyere*";
  public const string TapGlyph = "iwex:moltenmetaltap*";
  public const string ShaftGlyph = "@(air|coalpile)";
  public const string OutletGlyph = "lpex:pipe-outlet*";

  #endregion

  #region Standing a furnace up

  /// <summary>
  /// Points <paramref name="be"/> at a block coded <c>{blockCode}-{side}</c> carrying the
  /// <c>("side", side)</c> variant the vanilla HorizontalOrientable stamps, then runs the block entity's
  /// own rotation update so its structure angle is whatever it derives - not whatever the test wants.
  /// </summary>
  public static void Orient(BlockEntity be, string blockCode, string side)
  {
    var world = new TestWorld();
    be.Block = TestBlocks.Configure(new Block(), blockCode, 1, ("side", side));
    world.Attach(be);
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
  }

  /// <summary>The structure angle a given side implies, derived here rather than read off the machine.</summary>
  public static int AngleFromSide(string side) =>
    side switch
    {
      "north" => 0,
      "west" => 90,
      "south" => 180,
      "east" => 270,
      _ => throw new ArgumentOutOfRangeException(nameof(side), side, null),
    };

  #endregion

  #region Layout reading

  /// <summary>
  /// The wanted block code at each structure-local cell, read back out of the generated
  /// <c>multiblockStructure</c> attribute (block numbers resolved through <c>blockNumbers</c>) - i.e. the
  /// same table the game builds the structure from, not a restatement of the ASCII drawing.
  /// </summary>
  public static Dictionary<Vec3i, string> LayoutOf(ExBlockDef def)
  {
    JObject structure = (JObject)def.ToJson()["attributes"]!["multiblockStructure"]!;

    var codeByNumber = ((JObject)structure["blockNumbers"]!)
      .Properties()
      .ToDictionary(p => (int)p.Value!, p => p.Name);

    var cells = new Dictionary<Vec3i, string>();
    foreach (JToken offset in (JArray)structure["offsets"]!)
      cells[new Vec3i((int)offset["x"]!, (int)offset["y"]!, (int)offset["z"]!)] =
        codeByNumber[(int)offset["w"]!];
    return cells;
  }

  /// <summary>
  /// The wanted block code at each world-relative cell of the structure the game builds from the anchor's
  /// definition, rotated by <paramref name="angle"/> through vanilla <see cref="MultiblockStructure"/> -
  /// the independent half of the rotated oracle. Loaded and rotated exactly the way the production block
  /// entity does (<c>AsObject&lt;MultiblockStructure&gt;().InitForUse(angle)</c>); the test block carries
  /// no attributes, so this reads the definition rather than the instance.
  /// </summary>
  public static Dictionary<Vec3i, string> RotatedLayoutOf(
    ExBlockDef def,
    int angle
  )
  {
    JObject json = (JObject)def.ToJson()["attributes"]!["multiblockStructure"]!;

    // Authored glyph per block number, read straight off the JSON exactly as LayoutOf does, so a
    // domainless or wildcard code - notably the shaft's "@(air|coalpile)" - keeps its authored form
    // rather than being re-domained to "game:@(air|coalpile)" by an AssetLocation round-trip.
    var codeByNumber = ((JObject)json["blockNumbers"]!)
      .Properties()
      .ToDictionary(p => (int)p.Value!, p => p.Name);

    // The rotation itself comes from vanilla MultiblockStructure - deserialized and rotated the same way
    // the production block entity does at placement.
    MultiblockStructure structure = new JsonObject(
      json
    ).AsObject<MultiblockStructure>()!;
    structure.InitForUse(angle);

    var cells = new Dictionary<Vec3i, string>();
    foreach (BlockOffsetAndNumber o in structure.TransformedOffsets)
      cells[new Vec3i(o.X, o.Y, o.Z)] = codeByNumber[o.W];
    return cells;
  }

  public static string At(Dictionary<Vec3i, string> layout, Vec3i cell)
  {
    Assert.True(
      layout.ContainsKey(cell),
      $"local cell {cell} is not part of the layout at all"
    );
    return layout[cell];
  }

  /// <summary>
  /// Asserts the glyph at one layout cell. Both sides are formatted with <paramref name="what"/> so a
  /// failure names the role that is wrong, not just two block codes.
  /// </summary>
  public static void AssertGlyph(
    Dictionary<Vec3i, string> layout,
    Vec3i cell,
    string expectedCode,
    string what
  ) => Assert.Equal($"{what} -> {expectedCode}", $"{what} -> {At(layout, cell)}");

  #endregion

  #region Block-entity offset reading

  public static Vec3i Cell(object be, string property) =>
    (Vec3i)ReflectionHelpers.GetProperty(be, property)!;

  public static Vec3i[] Cells(object be, string property) =>
    (Vec3i[])ReflectionHelpers.GetProperty(be, property)!;

  public static BlockPos Global(object be, Vec3i local) =>
    (BlockPos)
      ReflectionHelpers.Invoke(be, "GetGlobalPos", local.X, local.Y, local.Z)!;

  #endregion

  #region Shaft geometry

  /// <summary>The layout's chargeable cells - the shaft column the burden actually occupies.</summary>
  public static List<Vec3i> ChargeCells(Dictionary<Vec3i, string> layout) =>
    layout.Where(kv => kv.Value == ShaftGlyph).Select(kv => kv.Key).ToList();

  /// <summary>
  /// Checks the block entity's shaft box and its solidify cells against the layout rather than against
  /// restated literals. The box must contain every chargeable cell (or the tick's charge walk silently
  /// misses part of the column), and the solidify cells must be exactly the chargeable cells on the lowest
  /// level of the shaft - which is what "the molten pool freezes across the bottommost layer" means.
  /// Derived here, declared there: a layout change that moves the hearth floor fails this instead of
  /// quietly dropping iron into a wall.
  /// </summary>
  public static void AssertShaftMatchesLayout(
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
    var declared = Cells(be, "SolidifyCells").OrderBy(c => c.X).ThenBy(c => c.Z);
    Assert.Equal(string.Join(", ", expected), string.Join(", ", declared));
  }

  #endregion

  #region The north-only oracle

  /// <summary>
  /// Checks every functional cell a furnace's block entity declares against its anchor's authored layout,
  /// at angle 0 where structure-local and world offsets coincide: the anchor at the origin, its tuyere(s),
  /// both taps, the shaft centre, the shaft box and solidify floor, and any gas outlets it declares (the
  /// loop just runs zero times for the open-topped furnaces). Returns the outlet cells so the caller can
  /// state its own furnace's outlet expectation - "none, the open top is the chimney" or "some, and they
  /// must land on pipe outlets" - which is the one thing that genuinely differs between them.
  /// </summary>
  public static Vec3i[] AssertFurnaceGeometry(
    BlockEntity be,
    ExBlockDef def,
    string anchorGlyph,
    string furnace
  )
  {
    Dictionary<Vec3i, string> layout = LayoutOf(def);

    // The anchor stands in its own layout, at the layout's origin.
    AssertGlyph(layout, new Vec3i(0, 0, 0), anchorGlyph, "anchor");

    foreach (Vec3i tuyere in Cells(be, "TuyereCells"))
      AssertGlyph(layout, tuyere, TuyereGlyph, "tuyere");

    AssertGlyph(layout, Cell(be, "MetalTapCell"), TapGlyph, "metal tap");
    AssertGlyph(layout, Cell(be, "SlagTapCell"), TapGlyph, "slag tap");
    AssertGlyph(layout, Cell(be, "ShaftCentre"), ShaftGlyph, "shaft centre");
    AssertShaftMatchesLayout(layout, be, furnace);

    Vec3i[] outlets = Cells(be, "GasOutletCells");
    foreach (Vec3i outlet in outlets)
      AssertGlyph(layout, outlet, OutletGlyph, "gas outlet");
    return outlets;
  }

  /// <summary>
  /// Pins that a furnace has no exhaust outlet at all: none declared by the block entity, and none in the
  /// layout for it to point at. That is what "the open top IS the chimney" means for the cold blast
  /// furnace and the cupola - stated so adding a pipe outlet to one of those layouts fails loudly.
  /// </summary>
  public static void AssertNoExhaustOutlets(BlockEntity be, ExBlockDef def)
  {
    Assert.DoesNotContain(OutletGlyph, LayoutOf(def).Values);
    Assert.Empty(Cells(be, "GasOutletCells"));
  }

  #endregion

  #region The four-orientation oracle

  /// <summary>
  /// Stands the already-oriented <paramref name="be"/> against the layout rotated to
  /// <paramref name="side"/> and checks both oracles for every functional cell it declares: its tuyere(s),
  /// both taps, the shaft centre, and its gas outlets (none for the cold furnace and the cupola - the loop
  /// just runs zero times).
  /// </summary>
  public static void AssertFurnaceMatrix(
    BlockEntity be,
    ExBlockDef def,
    string anchorGlyph,
    string side
  )
  {
    int angle = AngleFromSide(side);
    BlockPos pos = be.Pos;
    Dictionary<Vec3i, string> layout = RotatedLayoutOf(def, angle);

    // The anchor sits at its own layout origin, which every rotation leaves at (0,0,0).
    Assert.Equal(
      $"anchor -> {anchorGlyph}",
      $"anchor -> {At(layout, new Vec3i(0, 0, 0))}"
    );

    foreach (Vec3i tuyere in Cells(be, "TuyereCells"))
      AssertRotatedCell(be, layout, pos, angle, tuyere, TuyereGlyph, "tuyere");

    AssertRotatedCell(
      be,
      layout,
      pos,
      angle,
      Cell(be, "MetalTapCell"),
      TapGlyph,
      "metal tap"
    );
    AssertRotatedCell(
      be,
      layout,
      pos,
      angle,
      Cell(be, "SlagTapCell"),
      TapGlyph,
      "slag tap"
    );
    AssertRotatedCell(
      be,
      layout,
      pos,
      angle,
      Cell(be, "ShaftCentre"),
      ShaftGlyph,
      "shaft centre"
    );

    foreach (Vec3i outlet in Cells(be, "GasOutletCells"))
      AssertRotatedCell(be, layout, pos, angle, outlet, OutletGlyph, "gas outlet");
  }

  /// <summary>
  /// The same matrix for machines that face <b>opposite</b> their side variant - the +180 convention (the
  /// Bessemer converter folds it into its InitForUse angle and its GetGlobalPos override; the cowper bakes
  /// it straight into the stored angle). Either way their peripherals rotate by <c>AngleFromSide + 180</c>,
  /// derived here independently so a dropped or doubled +180 fails oracle 1, and checked against the
  /// InitForUse-rotated layout so a GetGlobalPos-vs-structure disagreement fails oracle 2. Unlike the
  /// furnaces these machines expose no cell properties, so each <paramref name="cells"/> entry states the
  /// structure-local offset and the glyph it must resolve to (mirroring the block entity's own constants).
  /// </summary>
  public static void AssertPlus180Matrix(
    BlockEntity be,
    ExBlockDef def,
    string anchorGlyph,
    string side,
    (Vec3i local, string glyph, string what)[] cells
  )
  {
    int angle = (AngleFromSide(side) + 180) % 360;
    BlockPos pos = be.Pos;
    Dictionary<Vec3i, string> layout = RotatedLayoutOf(def, angle);

    // The anchor sits at its own layout origin, which every rotation leaves at (0,0,0).
    Assert.Equal(
      $"anchor -> {anchorGlyph}",
      $"anchor -> {At(layout, new Vec3i(0, 0, 0))}"
    );

    foreach (var (local, glyph, what) in cells)
      AssertRotatedCell(be, layout, pos, angle, local, glyph, what);
  }

  /// <summary>
  /// The two oracles for one functional cell. Oracle 1 pins the angle wiring against the shared rotation
  /// math (already covered in ExOrientationTests, used here as a trusted primitive); oracle 2 pins that the
  /// resulting world cell agrees with the game-built, InitForUse-rotated layout.
  /// </summary>
  public static void AssertRotatedCell(
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

    // Oracle 1 - the machine's own GetGlobalPos equals the anchor plus the independently-rotated offset.
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
}
