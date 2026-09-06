using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Shared geometry oracle for the furnace multiblocks - iiex's cold blast furnace and cupola, smex's hot
/// blast furnace - parameterised by which anchor is stood up. <see cref="AssertFurnaceGeometry"/> pins raw
/// offsets against the authored layout at angle 0, where structure-local and world offsets coincide;
/// <see cref="AssertFurnaceMatrix"/> pins <c>GetGlobalPos</c> at all four orientations against both
/// independently-rotated offsets and the structure <see cref="MultiblockStructure.InitForUse"/> assembles.
/// <para>No helper here may carry a <c>where T : BlockEntity</c> constraint: xUnit resolves generic
/// constraints during discovery, before the module initializer registers <c>VsAssemblyResolver</c>.</para>
/// </summary>
public static class FurnaceLayoutRig {
  #region Role glyphs

  // The wildcard a functional cell must resolve to in the layout, per role - one source for both oracles.

  // One wildcarded code for both inlets. The cell states which way the tuyere must open through the
  // layout's Connector mark rather than by pinning the variant, because a network node re-picks its own
  // orientation from its neighbours and is free to contradict a pin. The oracle for that half is
  // AssertConnectorFaces, not the code.
  public const string TuyereGlyph = "iiex:furnace-tuyere-*";

  // One code per tap role, facing-pinned rather than `-*`: a shared code makes a drawing that swapped `T`
  // and `S` indistinguishable from a correct one, and a wildcarded facing lets a tap be fitted the wrong
  // way round and still complete the structure. The letter reads backwards -
  // `BlockEntityFurnaceTap.TryPourMetal` spouts at `Pos.AddCopy(facing.Opposite).DownCopy()`, so the shaft
  // furnaces' east-wall iron notch is declared `-west`.
  public const string IronTapGlyph = "iiex:furnace-irontap-w";
  public const string SlagTapGlyph = "iiex:furnace-slagtap-e";

  // The cupola is mirrored: it drains cast iron out its west wall and skims cinder off its east, so its
  // glyphs are the opposite facings. Both drawings are internally correct - each notch has a free runout
  // cell - so the pair cannot be one set of constants.
  public const string CupolaIronTapGlyph = "iiex:furnace-irontap-e";
  public const string CupolaSlagTapGlyph = "iiex:furnace-slagtap-w";

  /// <summary>
  /// The pair of drain glyphs one furnace's drawing declares. Passed in by the caller, as the tuyere
  /// glyphs are: the shipped furnaces do not agree on them.
  /// </summary>
  public readonly record struct TapGlyphs(string Iron, string Slag) {
    /// <summary>The blast furnaces, cold and hot: iron out east, cinder off west.</summary>
    public static TapGlyphs ShaftFurnace => new(IronTapGlyph, SlagTapGlyph);

    /// <summary>The cupola, the other hand round. See <see cref="CupolaIronTapGlyph"/>.</summary>
    public static TapGlyphs Cupola =>
      new(CupolaIronTapGlyph, CupolaSlagTapGlyph);
  }

  // Domain-wildcarded, and restated here as a literal rather than read off the production layout: an
  // expectation computed from the thing it pins agrees with any value of it. The `*:` is what lets a shaft
  // cell hold `iiex:furnace-chargepile` as well as `game:air`/`game:coalpile` - vanilla's matcher compares
  // domain and path separately, so a bare alternation is implicitly `game:`.
  public const string ShaftGlyph = "*:@(air|coalpile|furnace-chargepile)";

  /// <summary>
  /// The crucible-floor glyph: the same three occupants as <see cref="ShaftGlyph"/> plus the hearth-metal
  /// block the furnace stands its bath in. Inside <c>@(...)</c> the body is a regex, so the metal member
  /// is <c>hearthmetal-.*</c> and the <c>.</c> is not a typo - see <c>IiexCodes.HearthCell</c>. On the two
  /// iwex furnaces this glyph carries <c>FurnaceCellRoles.Pool</c> only; siex's hot furnace also marks it
  /// <c>Chargeable</c>, which is why <see cref="ChargeCells"/> takes its glyphs from the caller.
  /// </summary>
  public const string HearthGlyph =
    "*:@(air|coalpile|furnace-chargepile|hearthmetal-.*)";

  public const string OutletGlyph = "iiex:pipe-outlet*";

  /// <summary>
  /// A reverberatory hearth's fuel-bed glyph - a required block, wildcarded over tier and facing. It must
  /// not admit air, or an empty firebox satisfies the structure. A literal here for the reason
  /// <see cref="ShaftGlyph"/> is.
  /// </summary>
  public const string FireboxGlyph = "iiex:furnace-firebox-*-*";

  #endregion

  #region Standing a furnace up

  /// <summary>
  /// The token a <c>side</c> variant wears in a shipped code - the single letter. Accepts either spelling
  /// in, so a theory can say <c>"west"</c>; a code naming no block fails silently, as a cell that is never
  /// satisfied.
  /// </summary>
  public static string SideToken(string side) =>
    ExOrientation.SideFromAngle(
      ExOrientation.AngleFromSide(side),
      asLetter: true
    );

  /// <summary>
  /// Points <paramref name="be"/> at a block coded <c>{blockCode}</c> carrying the <c>("side", …)</c>
  /// variant its <c>ExOrientable</c> behaviour stamps, then runs the block entity's own rotation update so
  /// the structure angle is derived rather than assigned.
  /// </summary>
  public static void Orient(
    BlockEntityMultiblockStructure be,
    string blockCode,
    string side
  ) {
    var world = new TestWorld();
    be.Block = TestBlocks.Configure(
      new Block(),
      blockCode,
      1,
      ("side", SideToken(side))
    );
    world.Attach(be);
    be.ApplyStructureRotation();
  }

  /// <summary>
  /// <see cref="Orient"/> plus <paramref name="def"/>'s real <c>attributes</c> on the placed block, so the
  /// machine loads its layout, oriented-part table and cell roles without a footprint being raised around
  /// it. Bare <see cref="Orient"/> leaves the block attribute-less, so the machine sees
  /// <c>MultiblockCellRoles.None</c> and derives no shaft box. Use <see cref="Stand"/> instead when the
  /// machine has to complete.
  /// </summary>
  public static void OrientWithLayout(
    BlockEntityMultiblockStructure be,
    ExBlockDef def,
    string blockCode,
    string side
  ) {
    Orient(be, blockCode, side);
    if (def.ToJson()["attributes"] is not JObject attributes)
      throw new InvalidOperationException(
        $"Block definition '{def.Code}' has no attributes, so it carries no layout."
      );
    be.Block.Attributes = new JsonObject(attributes);
    // The angle was already resolved against a block with no layout, so the load has to be re-run:
    // SetStructureAngle short-circuits UpdateStructureRotation only once _structure is non-null.
    be.ApplyStructureRotation();
  }

  /// <summary>
  /// Stands <paramref name="anchor"/> up at <paramref name="pos"/> on a real block coded
  /// <c>{blockCode}-{side}</c>, raises <paramref name="def"/>'s footprint around it and - when
  /// <paramref name="complete"/> - lets the machine complete itself. Nothing forces
  /// <c>StructureComplete</c>, so a wrong angle shows up as a machine that never completes.
  /// <see cref="StructureRig.Around"/> attaches the def's real <c>attributes</c> to the placed block.
  /// </summary>
  public static StructureRig Stand(
    BlockEntityMultiblockStructure anchor,
    ExBlockDef def,
    BlockPos pos,
    string blockCode,
    string side,
    bool complete = true
  ) {
    var world = new TestWorld();
    world.World.Side.Returns(EnumAppSide.Server);

    anchor.Pos = pos.Copy();
    string token = SideToken(side);
    anchor.Block = TestBlocks.Configure(
      new Block(),
      $"{blockCode}-{token}",
      1,
      ("side", token)
    );
    world.Place(anchor.Pos, anchor.Block, anchor);
    world.Attach(anchor);

    StructureRig rig = StructureRig.Around(
      world,
      anchor,
      def,
      AngleFromSide(side)
    );
    return complete ? rig.Complete() : rig.Raise();
  }

  /// <summary>
  /// Replaces the rig's stand-ins in every cell the drawing marks <c>Firebox</c> with a real firebox block
  /// and a bed holding <paramref name="unitsPerCell"/> of coke. The real code satisfies the same
  /// <see cref="FireboxGlyph"/>, so the structure stays complete; zero units stands a lit-capable furnace
  /// up with nothing to light.
  /// </summary>
  public static void LoadFireboxes(
    StructureRig rig,
    BlockEntityFireboxFurnace furnace,
    int unitsPerCell,
    string fuelCode = "game:coke",
    System.Func<BlockPos, bool>? where = null
  ) {
    Item coke = rig.World.RegisterItem(fuelCode);
    foreach (BlockPos cell in furnace.FireboxCells.ToList()) {
      // A cell outside the filter still gets its real block, or the structure stops being complete; it
      // simply gets nothing in the bed.
      int units = where == null || where(cell) ? unitsPerCell : 0;
      var be = new BlockEntityFirebox { Pos = cell.Copy() };
      var bed = new BEBehaviorFirebox(be);
      if (units > 0)
        bed.TryAdd(new ItemStack(coke, units), units);
      ReflectionHelpers.SetField(
        be,
        "Behaviors",
        new List<BlockEntityBehavior> { bed }
      );
      rig.Occupy(
        cell,
        TestBlocks.Configure(
          new Block(),
          "iiex:furnace-firebox-tier1-n",
          900,
          ("side", "north")
        ),
        be
      );
    }
  }

  /// <summary>
  /// Puts a real crucible hearth in the furnace's one firebox cell, with <paramref name="unitsPerCell"/>
  /// of fuel in its bed, and re-runs the outlet scan so the furnace picks it up. Its own helper rather
  /// than <see cref="LoadFireboxes"/>, which places the firebox blocktype: this cell's drawing requires
  /// the hearth, so the wrong code there would stop the structure completing.
  /// </summary>
  public static void SeatCrucibleHearth(
    StructureRig rig,
    BlockEntityFireboxFurnace furnace,
    int unitsPerCell,
    string fuelCode = "game:coke"
  ) {
    Item coke = rig.World.RegisterItem(fuelCode);
    foreach (BlockPos cell in furnace.FireboxCells.ToList()) {
      var be = new BlockEntityCrucibleHearth { Pos = cell.Copy() };
      var bed = new BEBehaviorFirebox(be);
      if (unitsPerCell > 0)
        bed.TryAdd(new ItemStack(coke, unitsPerCell), unitsPerCell);
      ReflectionHelpers.SetField(
        be,
        "Behaviors",
        new List<BlockEntityBehavior> { bed }
      );
      rig.Occupy(
        cell,
        TestBlocks.Configure(
          new Block(),
          "iiex:furnace-cruciblehearth-tier1-n",
          905,
          ("side", "north")
        ),
        be
      );
    }
    ReflectionHelpers.Invoke(furnace, "ScanForOutlets");
  }

  /// <summary>
  /// The coke oven's crown lids and drawing doors, in the drawing's own frame: layer 3 row 1 and layer 1
  /// row 3, at columns 2 and 6 against <c>Origin(-4, -2)</c>. Spelled here rather than read off
  /// <c>BlockEntityCokeOven</c>, so an expectation cannot agree with its subject by construction.
  /// </summary>
  public static readonly Vec3i[] CokeOvenLids = [new(-2, 3, -1), new(2, 3, -1)];

  /// <summary>The coke oven's drawing doors, one per chamber.</summary>
  public static readonly Vec3i[] CokeOvenDoors = [new(-2, 1, 1), new(2, 1, 1)];

  /// <summary>
  /// Puts a real charge lid and drawing door in each of the oven's four closure cells and returns them.
  /// Without these the rig's stand-ins satisfy the layout but carry no block entity, and the seal reads a
  /// missing closure as open - correctly, and every bake would then do nothing.
  /// <para>
  /// <paramref name="place"/> narrows which cells get a block entity; the rest still get the block the
  /// layout requires, which is what a broken and replaced lid looks like from the core's side.
  /// </para>
  /// </summary>
  public static List<BlockEntityChargeDoor> CloseCokeOven(
    StructureRig rig,
    string side = "north",
    System.Func<Vec3i, bool>? place = null
  ) {
    var closures = new List<BlockEntityChargeDoor>();
    // The layout rotates a part's required facing with the structure, so the authored `s` has to turn
    // with it or the oven never completes at three of the four sides.
    string letter = ExOrientation.RotateOrientationToken(
      "s",
      ExOrientation.AngleFromSide(side)
    );

    foreach (
      (Vec3i local, string type) in CokeOvenLids
        .Select(l => (l, "chargelid"))
        .Concat(CokeOvenDoors.Select(d => (d, "chargedoor")))
    ) {
      BlockPos cell = rig.Cell(local.X, local.Y, local.Z);
      BlockEntityChargeDoor? door =
        place == null || place(local)
          ? new BlockEntityChargeDoor { Pos = cell.Copy() }
          : null;
      rig.Occupy(
        cell,
        TestBlocks.Configure(
          new Block(),
          $"iiex:furnace-{type}-{letter}",
          800,
          (
            "side",
            ExOrientation.SideFromAngle(ExOrientation.AngleFromSide(side))
          )
        ),
        door
      );
      if (door != null)
        closures.Add(door);
    }
    return closures;
  }

  /// <summary>
  /// The structure angle a given side implies, derived here rather than read off the machine. Delegates to
  /// <see cref="ExOrientation.AngleFromSide"/>, which accepts either spelling, so <c>"west"</c> and
  /// <c>"w"</c> cannot disagree about which way the furnace points.
  /// </summary>
  public static int AngleFromSide(string side) =>
    ExOrientation.IsHorizontalSideWord(side)
      ? ExOrientation.AngleFromSide(side)
      : throw new ArgumentOutOfRangeException(nameof(side), side, null);

  #endregion

  #region Odd-shaped fixture layouts

  /// <summary>
  /// A cold-furnace core wearing a hand-drawn footprint whose <see cref="FurnaceCellRoles.Chargeable"/> volume is
  /// exactly the solid box <paramref name="min"/>..<paramref name="max"/>, so the shaft box is exactly
  /// those corners. A shipped shaft is square or a single column and a square set is closed under 90 deg
  /// rotation, so only an asymmetric drawing exposes a transposed derivation loop.
  /// </summary>
  /// <param name="brick">One non-chargeable cell the furnace owns that lies outside its shaft box.</param>
  /// <remarks>The anchor sits at <c>(0,0,0)</c> and the box must not contain it, or the drawing would need
  /// two glyphs in one cell; every caller's box starts at <c>y &gt;= 1</c>.</remarks>
  public static ExBlockDef ShaftBoxDef(
    Vec3i min,
    Vec3i max,
    Vec3i? brick = null
  ) {
    int xLeft = Math.Min(Math.Min(min.X, 0), brick?.X ?? 0);
    int xRight = Math.Max(Math.Max(max.X, 0), brick?.X ?? 0);
    int zTop = Math.Min(Math.Min(min.Z, 0), brick?.Z ?? 0);
    int zBottom = Math.Max(Math.Max(max.Z, 0), brick?.Z ?? 0);

    string Row(int y, int z) =>
      string.Join(
        " ",
        Enumerable
          .Range(xLeft, xRight - xLeft + 1)
          .Select(x =>
            y == 0 && x == 0 && z == 0 ? "C"
            : brick is { } b && b.X == x && b.Y == y && b.Z == z ? "#"
            : x >= min.X
            && x <= max.X
            && y >= min.Y
            && y <= max.Y
            && z >= min.Z
            && z <= max.Z
              ? "c"
            : "."
          )
      );

    string Grid(int y) =>
      string.Join(
        "\n",
        Enumerable.Range(zTop, zBottom - zTop + 1).Select(z => Row(y, z))
      );

    var builder = ExBlockDef
      .Create("iiex", "furnace")
      .MultiblockLayout(s => {
        s.Origin(xLeft, zTop)
          .Legend('C', "iiex:furnace-blastcore-*")
          .Legend('#', "game:refractorybricks-good-tier*")
          .Legend('c', ShaftGlyph)
          .Role('c', FurnaceCellRoles.Chargeable);

        // y = 0 carries the anchor; every level of the box carries the volume. A brick outside both still
        // needs its own level drawn.
        var levels = new SortedSet<int> { 0 };
        for (int y = min.Y; y <= max.Y; y++)
          levels.Add(y);
        if (brick is { } b)
          levels.Add(b.Y);
        foreach (int y in levels)
          s.Layer(y, Grid(y));
      });

    return builder;
  }

  /// <summary>
  /// A cold-furnace core wearing a two-column shaft with a stepped floor: <c>(0,0)</c> is chargeable from
  /// y=1, <c>(0,1)</c> only from y=2 with brick beneath it. The shaft box therefore floors at 1 while one
  /// of its two columns cannot hold charge there.
  /// <para>
  /// Both shipped shafts became uniform-floored when their crucible course stopped being charged, so this
  /// drawing is what separates "fill from the column's own floor" from "fill from the box's" - the
  /// distinction <c>ColumnFloorY</c> and <c>ChargeCellsOf</c> exist for. Do not delete it as unused
  /// because the shipped drawings pass without it.
  /// </para>
  /// </summary>
  public static ExBlockDef SteppedShaftDef() =>
    ExBlockDef
      .Create("iiex", "furnace")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "iiex:furnace-blastcore-*")
          .Legend('#', "game:refractorybricks-good-tier*")
          .Legend('c', ShaftGlyph)
          .Role('c', FurnaceCellRoles.Chargeable)
          .Layer(
            0,
            """
            C
            #
            """
          )
          .Layer(
            1,
            """
            c
            #
            """
          )
          .Layer(
            2,
            """
            c
            c
            """
          )
          .Layer(
            3,
            """
            c
            c
            """
          )
      );

  #endregion

  #region Layout reading

  /// <summary>Forwards to <see cref="LayoutTable.From"/>.</summary>
  public static Dictionary<Vec3i, string> LayoutOf(ExBlockDef def) =>
    LayoutTable.From(def);

  /// <summary>
  /// The authored (north-frame) cells a def's layout marks with <paramref name="role"/>, read out of the
  /// generated <c>multiblockRoles</c> attribute; empty for a layout that marks nothing. Read by the enum's
  /// name rather than through <c>MultiblockCellRoles</c>, so a role that was never emitted stays
  /// distinguishable from one the production reader drops.
  /// </summary>
  public static List<Vec3i> RoleCellsOf(ExBlockDef def, CellRole role) {
    JToken? roles = def.ToJson()["attributes"]?["multiblockRoles"];
    if (roles?[role.ToString()] is not JArray cells)
      return [];
    return cells
      .Select(c => new Vec3i((int)c["x"]!, (int)c["y"]!, (int)c["z"]!))
      .ToList();
  }

  /// <summary>The role names a def's layout emits, in emission order, so a test can state the exact set a
  /// furnace marks rather than only that it marks one.</summary>
  public static List<string> RoleNamesOf(ExBlockDef def) =>
    def.ToJson()["attributes"]?["multiblockRoles"] is JObject roles
      ? roles.Properties().Select(p => p.Name).ToList()
      : [];

  /// <summary>Forwards to <see cref="LayoutTable.Rotated"/> - the independent half of the rotated
  /// oracle.</summary>
  public static Dictionary<Vec3i, string> RotatedLayoutOf(
    ExBlockDef def,
    int angle
  ) => LayoutTable.Rotated(def, angle);

  public static string At(Dictionary<Vec3i, string> layout, Vec3i cell) {
    Assert.True(
      layout.ContainsKey(cell),
      $"local cell {cell} is not part of the layout at all"
    );
    return layout[cell];
  }

  /// <summary>
  /// Asserts the glyph at one layout cell. Both sides are formatted with <paramref name="what"/> so a
  /// failure names the role that is wrong rather than only two block codes.
  /// </summary>
  public static void AssertGlyph(
    Dictionary<Vec3i, string> layout,
    Vec3i cell,
    string expectedCode,
    string what
  ) =>
    Assert.Equal($"{what} -> {expectedCode}", $"{what} -> {At(layout, cell)}");

  /// <summary>
  /// <see cref="AssertGlyph"/> where more than one glyph is legitimate - the shaft centre sits on the
  /// crucible course on both shipped furnaces, so it may read as the hearth glyph. The failure names every
  /// accepted glyph and what it found.
  /// </summary>
  public static void AssertGlyph(
    Dictionary<Vec3i, string> layout,
    Vec3i cell,
    string[] expectedCodes,
    string what
  ) {
    string actual = At(layout, cell);
    Assert.True(
      expectedCodes.Contains(actual),
      $"{what} at {cell} holds '{actual}', not one of {string.Join(" | ", expectedCodes)}"
    );
  }

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

  /// <summary>
  /// The layout's chargeable cells - the shaft column the burden occupies. The literal-glyph route, not
  /// how production finds them (it asks the cell's <see cref="FurnaceCellRoles.Chargeable"/> role), so the two are
  /// independent oracles. <paramref name="glyphs"/> is the caller's: the two iwex furnaces charge
  /// <see cref="ShaftGlyph"/> alone, while siex's hot furnace also charges its crucible course and so
  /// names <see cref="HearthGlyph"/> too. Listing a glyph the drawing does not charge inflates the oracle
  /// by that row and the role comparison fails, which is the point.
  /// </summary>
  public static List<Vec3i> ChargeCells(
    Dictionary<Vec3i, string> layout,
    params string[] glyphs
  ) =>
    layout.Where(kv => glyphs.Contains(kv.Value)).Select(kv => kv.Key).ToList();

  /// <summary>The layout's fuel-bed cells, by the same literal-glyph route as
  /// <see cref="ChargeCells"/>.</summary>
  public static List<Vec3i> FireboxCells(Dictionary<Vec3i, string> layout) =>
    layout.Where(kv => kv.Value == FireboxGlyph).Select(kv => kv.Key).ToList();

  /// <summary>
  /// The machine's own shaft box, or null when its drawing marks no fuel cell. Read off the block entity
  /// through reflection because <c>ShaftBox</c> is <c>protected</c>.
  /// </summary>
  public static (Vec3i min, Vec3i max)? ShaftBoxOf(object be) =>
    ((Vec3i, Vec3i)?)ReflectionHelpers.GetProperty(be, "ShaftBox");

  /// <summary>
  /// The machine's shaft box in world space for its placed facing - <see cref="ShaftBoxOf"/>'s two corners
  /// rotated and then re-sorted per component. Read off <c>ShaftBounds()</c> through reflection, which is
  /// <c>protected</c>. The only route to the re-sort: a quarter turn can swap either horizontal axis, so
  /// "whatever the low corner rotated into" is not a minimum, and <see cref="ShaftBoxOf"/> answers the
  /// facing-invariant local box instead.
  /// </summary>
  public static (BlockPos min, BlockPos max)? WorldBoxOf(object be) =>
    ((BlockPos, BlockPos)?)ReflectionHelpers.Invoke(be, "ShaftBounds");

  /// <summary>
  /// The bounding box of the authored fuel cells a def's layout marks, computed off the emitted
  /// <c>multiblockRoles</c> attribute as the independent second route to the machine's own box. Null when
  /// the drawing marks neither role. Walks the JSON with its own min/max rather than calling the
  /// production derivation, which would agree with any value of what it pins.
  /// </summary>
  public static (Vec3i min, Vec3i max)? AuthoredFuelBox(ExBlockDef def) {
    List<Vec3i> cells =
    [
      .. RoleCellsOf(def, FurnaceCellRoles.Chargeable),
      .. RoleCellsOf(def, FurnaceCellRoles.Firebox),
    ];
    if (cells.Count == 0)
      return null;
    return (
      new Vec3i(cells.Min(c => c.X), cells.Min(c => c.Y), cells.Min(c => c.Z)),
      new Vec3i(cells.Max(c => c.X), cells.Max(c => c.Y), cells.Max(c => c.Z))
    );
  }

  /// <summary>
  /// The furnace's shaft box is the bounding box of the fuel cells its own drawing marks, stated through
  /// two routes that share no code: the block entity's derivation, and this file's min/max over the
  /// emitted role attribute. Equality rather than containment, so a padded box fails here.
  /// </summary>
  public static void AssertShaftBoxIsTheDrawings(
    BlockEntityMultiblockStructure be,
    ExBlockDef def,
    string furnace
  ) {
    (Vec3i min, Vec3i max)? authored = AuthoredFuelBox(def);
    Assert.True(
      authored is not null,
      $"{furnace}: its drawing marks no Chargeable or Firebox cell at all"
    );
    Assert.Equal($"{furnace}: {authored}", $"{furnace}: {ShaftBoxOf(be)}");
  }

  #endregion

  #region The north-only oracle

  /// <summary>
  /// Checks every functional cell of a furnace against its anchor's authored layout at angle 0, where
  /// structure-local and world offsets coincide: the anchor, its tuyere(s), both taps, the shaft centre,
  /// the shaft box and any gas outlets. Cells come off the layout's own <see cref="CellRole"/> marks, and
  /// since the DSL does not relate a role to its glyph's code, these loops are the only statement that a
  /// <c>Role()</c> call landed where its author meant. Every role loop but the outlets asserts non-empty.
  /// </summary>
  /// <returns>The gas-outlet cells. Emptiness is the caller's to state, since "no outlets" is correct for
  /// both iiex furnaces - see <see cref="AssertNoExhaustOutlets"/>.</returns>
  public static List<Vec3i> AssertFurnaceGeometry(
    BlockEntityMultiblockStructure be,
    ExBlockDef def,
    string anchorGlyph,
    string furnace,
    TapGlyphs taps,
    string[] chargeGlyphs,
    params string[] tuyereFaces
  ) {
    Dictionary<Vec3i, string> layout = LayoutOf(def);

    // The anchor stands in its own layout, at the layout's origin.
    AssertGlyph(layout, new Vec3i(0, 0, 0), anchorGlyph, "anchor");

    // Every tuyere cell holds the one wildcarded code, and the drawing states which way each must open.
    // The face set is the caller's, because the three shaft furnaces do not agree on it: the cold and hot
    // furnaces are blown from both walls, the cupola only from the north.
    AssertRoleGlyphs(def, layout, FurnaceCellRoles.Tuyere, TuyereGlyph, "tuyere");
    AssertConnectorFaces(def, FurnaceCellRoles.Tuyere, tuyereFaces);

    // Both drains, each its own [SingleCell] role on its own glyph and block - `T` the iron notch, `S` the
    // cinder notch - so a drawing that swaps the two fails here. The pair is the caller's because the
    // cupola drains the opposite hand to the blast furnaces.
    AssertRoleGlyphs(def, layout, FurnaceCellRoles.MetalTap, taps.Iron, "metal tap");
    AssertRoleGlyphs(def, layout, FurnaceCellRoles.SlagTap, taps.Slag, "slag tap");

    // The shaft centre stands in the burden column proper, a course above the crucible.
    AssertGlyph(layout, Cell(be, "ShaftCentre"), ShaftGlyph, "shaft centre");

    // The burden column and the box it bounds: every cell the drawing marks Chargeable really is a shaft
    // cell. The set is the caller's because the three furnaces no longer agree: the two iwex furnaces
    // charge the shaft alone, while siex's hot furnace still charges its crucible course too, which is
    // the overlap its own remake is to resolve. Passing the pair where only the shaft is marked fails on
    // the several-codes form's second half - the unused glyph - which is what keeps this honest.
    AssertRoleGlyphs(
      def,
      layout,
      FurnaceCellRoles.Chargeable,
      chargeGlyphs,
      "chargeable"
    );
    AssertShaftBoxIsTheDrawings(be, def, furnace);

    // The crucible is drawn with its own glyph so it can carry Pool where the shaft carries the burden.
    AssertRoleGlyphs(
      def,
      layout,
      FurnaceCellRoles.Pool,
      // HearthGlyph alone: every pool cell is a crucible cell, and the several-codes form also requires
      // each listed glyph to be held by some cell.
      HearthGlyph,
      "pool"
    );

    List<Vec3i> outlets = RoleCellsOf(def, FurnaceCellRoles.GasOutlet);
    foreach (Vec3i outlet in outlets)
      AssertGlyph(layout, outlet, OutletGlyph, "gas outlet");
    return outlets;
  }

  /// <summary>Every cell a def marks with <paramref name="role"/> resolves to <paramref name="glyph"/> in
  /// its own layout - and there is at least one, so the loop cannot pass by running zero times.</summary>
  private static void AssertRoleGlyphs(
    ExBlockDef def,
    Dictionary<Vec3i, string> layout,
    CellRole role,
    string glyph,
    string what
  ) => AssertRoleGlyphs(def, layout, role, [glyph], what);

  /// <summary>
  /// The several-codes form: every cell of <paramref name="role"/> holds one of <paramref name="glyphs"/>,
  /// and every listed glyph is used by at least one of them. A role's cells need not all want the same
  /// block - each tuyere admits exactly one orientation, <c>n</c> in the north wall and <c>s</c> in the
  /// south - and the second half is what stops a drawing using the north tuyere for both cells.
  /// </summary>
  private static void AssertRoleGlyphs(
    ExBlockDef def,
    Dictionary<Vec3i, string> layout,
    CellRole role,
    string[] glyphs,
    string what
  ) {
    List<Vec3i> cells = RoleCellsOf(def, role);
    Assert.True(cells.Count > 0, $"{def.Code}: no cell is marked {role}");

    var used = new HashSet<string>();
    foreach (Vec3i cell in cells) {
      string actual = At(layout, cell);
      Assert.True(
        glyphs.Contains(actual),
        $"{def.Code}: the {what} cell {cell} holds '{actual}', not one of "
          + string.Join(" / ", glyphs)
      );
      used.Add(actual);
    }

    foreach (string glyph in glyphs)
      Assert.True(
        used.Contains(glyph),
        $"{def.Code}: no {what} cell holds '{glyph}' - the drawing uses "
          + $"{string.Join(" / ", used)} for all {cells.Count} of them"
      );
  }

  /// <summary>
  /// The outward faces the placed <paramref name="be"/> demands across its tuyere cells, once the whole
  /// structure has been turned to <paramref name="angle"/>: the authored set, each letter rotated. Read
  /// through the machine rather than the drawing, because that is the route completion takes.
  /// </summary>
  private static void AssertRotatedConnectorFaces(
    BlockEntityMultiblockStructure be,
    string[] authoredFaces,
    int angle
  ) {
    var machine = (BlockEntityMultiblockStructure)be;
    var demanded = new List<string>();
    foreach (BlockPos cell in machine.CellsWithRole(FurnaceCellRoles.Tuyere))
      demanded.AddRange(
        machine
          .ConnectorFacesAt(cell)
          .Select(f => ExOrientation.TokenOf(f, asLetter: true))
      );

    string expected = string.Join(
      " ",
      authoredFaces
        .Select(f =>
          ExOrientation.SideFromAngle(
            ExOrientation.AngleFromSide(f) + angle,
            asLetter: true
          )
        )
        .OrderBy(f => f)
    );
    Assert.Equal(expected, string.Join(" ", demanded.OrderBy(f => f)));
  }

  /// <summary>
  /// The authored outward faces a def's layout demands across every cell of <paramref name="role"/>:
  /// exactly <paramref name="faces"/>, with each demanded by some cell and each cell demanding one. The
  /// replacement for the several-codes glyph oracle the orientation pins used to give - without it, a
  /// drawing that marked both inlets outward-north would pass on the wildcard alone.
  /// </summary>
  public static void AssertConnectorFaces(
    ExBlockDef def,
    CellRole role,
    string[] faces
  ) {
    MultiblockConnectors connectors = MultiblockConnectors.FromAttributes(
      new JsonObject((JObject)def.ToJson()["attributes"]!)
    );
    List<Vec3i> cells = RoleCellsOf(def, role);
    Assert.True(cells.Count > 0, $"{def.Code}: no cell is marked {role}");

    var demanded = new List<string>();
    foreach (Vec3i cell in cells) {
      IReadOnlyList<string> at = connectors.OutwardFacesAt(
        (cell.X, cell.Y, cell.Z)
      );
      Assert.True(
        at.Count > 0,
        $"{def.Code}: the {role} cell {cell} demands no outward connector, so a node fitted "
          + "backwards there would complete the structure"
      );
      demanded.AddRange(at);
    }

    Assert.Equal(
      string.Join(" ", faces.OrderBy(f => f)),
      string.Join(" ", demanded.OrderBy(f => f))
    );
  }

  /// <summary>
  /// Pins that a furnace has no exhaust outlet: none marked by the layout, and no pipe outlet in the
  /// drawing for one to point at. On the cold blast furnace and the cupola the open top is the chimney.
  /// </summary>
  public static void AssertNoExhaustOutlets(ExBlockDef def) {
    Assert.DoesNotContain(OutletGlyph, LayoutOf(def).Values);
    Assert.Empty(RoleCellsOf(def, FurnaceCellRoles.GasOutlet));
  }

  #endregion

  #region The four-orientation oracle

  /// <summary>
  /// Stands the already-oriented <paramref name="be"/> against the layout rotated to
  /// <paramref name="side"/> and checks both oracles for every functional cell: its tuyere(s), both taps,
  /// the shaft centre, its crucible floor and its gas outlets. Role-derived sets go through the same
  /// <see cref="AssertRotatedCell"/> as hand-declared ones, so a role cell that rotated the wrong way lands
  /// on a glyph that is not its own. Non-empty is asserted for every role but the outlets.
  /// </summary>
  public static void AssertFurnaceMatrix(
    BlockEntityMultiblockStructure be,
    ExBlockDef def,
    string anchorGlyph,
    string side,
    TapGlyphs taps,
    params string[] tuyereFaces
  ) {
    int angle = AngleFromSide(side);
    BlockPos pos = be.Pos;
    Dictionary<Vec3i, string> layout = RotatedLayoutOf(def, angle);

    // The anchor sits at its own layout origin, which every rotation leaves at (0,0,0).
    Assert.Equal(
      $"anchor -> {anchorGlyph}",
      $"anchor -> {At(layout, new Vec3i(0, 0, 0))}"
    );

    AssertRotatedRole(
      be,
      def,
      layout,
      angle,
      FurnaceCellRoles.Tuyere,
      TuyereGlyph,
      "tuyere"
    );
    // The code is one wildcard at every facing, so what has to turn is the demand. Read off the placed
    // machine rather than off the drawing: the two rotate by different routes, and this is the one that
    // completion consults.
    AssertRotatedConnectorFaces(be, tuyereFaces, angle);
    AssertRotatedRole(
      be,
      def,
      layout,
      angle,
      FurnaceCellRoles.MetalTap,
      taps.Iron,
      "metal tap"
    );
    AssertRotatedRole(
      be,
      def,
      layout,
      angle,
      FurnaceCellRoles.SlagTap,
      taps.Slag,
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

    AssertRotatedRole(
      be,
      def,
      layout,
      angle,
      FurnaceCellRoles.Pool,
      // HearthGlyph alone, not the pair: the several-codes form also requires every listed glyph to be
      // held by some cell, and every pool cell is a crucible cell.
      HearthGlyph,
      "pool"
    );

    foreach (Vec3i outlet in RoleCellsOf(def, FurnaceCellRoles.GasOutlet))
      AssertRotatedCell(
        be,
        layout,
        pos,
        angle,
        outlet,
        OutletGlyph,
        "gas outlet"
      );
  }

  /// <summary>Every cell a def marks with <paramref name="role"/>, rotated and checked through both
  /// oracles - and there is at least one, so the loop cannot pass by running zero times.</summary>
  private static void AssertRotatedRole(
    BlockEntityMultiblockStructure be,
    ExBlockDef def,
    Dictionary<Vec3i, string> layout,
    int angle,
    CellRole role,
    string glyph,
    string what
  ) => AssertRotatedRole(be, def, layout, angle, role, [glyph], what);

  /// <summary>
  /// The several-codes form, for a role whose cells do not all want the same block - see the sibling
  /// <c>AssertRoleGlyphs</c> overload. The glyph is compared in its authored form even here, because
  /// <see cref="RotatedLayoutOf"/> rotates offsets and leaves codes alone; code rotation is
  /// <c>MultiblockFacings</c>, which runs at completion-check time.
  /// </summary>
  private static void AssertRotatedRole(
    BlockEntityMultiblockStructure be,
    ExBlockDef def,
    Dictionary<Vec3i, string> layout,
    int angle,
    CellRole role,
    string[] glyphs,
    string what
  ) {
    List<Vec3i> cells = RoleCellsOf(def, role);
    Assert.True(cells.Count > 0, $"{def.Code}: no cell is marked {role}");

    var used = new HashSet<string>();
    foreach (Vec3i cell in cells) {
      BlockPos world = Global(be, cell);
      var worldRel = new Vec3i(
        world.X - be.Pos.X,
        world.Y - be.Pos.Y,
        world.Z - be.Pos.Z
      );
      string actual = At(layout, worldRel);
      Assert.True(
        glyphs.Contains(actual),
        $"{def.Code} @{angle}: the {what} cell {cell} holds '{actual}', not one of "
          + string.Join(" / ", glyphs)
      );
      used.Add(actual);
      AssertRotatedCell(be, layout, be.Pos, angle, cell, actual, what);
    }

    foreach (string glyph in glyphs)
      Assert.True(
        used.Contains(glyph),
        $"{def.Code} @{angle}: no {what} cell holds '{glyph}'"
      );
  }

  /// <summary>
  /// The same matrix for machines that face opposite their side variant - the +180 convention. Their
  /// peripherals rotate by <c>AngleFromSide + 180</c>, derived here independently so a dropped or doubled
  /// +180 fails oracle 1, and checked against the InitForUse-rotated layout so a GetGlobalPos-vs-structure
  /// disagreement fails oracle 2.
  /// </summary>
  /// <param name="cells">Structure-local offset and the glyph it must resolve to, per cell. These machines
  /// expose no cell properties of their own.</param>
  public static void AssertPlus180Matrix(
    BlockEntityMultiblockStructure be,
    ExBlockDef def,
    string anchorGlyph,
    string side,
    (Vec3i local, string glyph, string what)[] cells
  ) {
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
  /// math, a trusted primitive here and covered in ExOrientationTests; oracle 2 pins that the resulting
  /// world cell agrees with the game-built, InitForUse-rotated layout.
  /// </summary>
  public static void AssertRotatedCell(
    object be,
    Dictionary<Vec3i, string> layout,
    BlockPos pos,
    int angle,
    Vec3i local,
    string glyph,
    string what
  ) => AssertRotatedCell(be, layout, pos, angle, local, [glyph], what);

  /// <summary>The several-codes form, for a cell more than one glyph may legitimately hold - the shaft
  /// centre, which sits on the crucible course. See <see cref="HearthGlyph"/>.</summary>
  public static void AssertRotatedCell(
    object be,
    Dictionary<Vec3i, string> layout,
    BlockPos pos,
    int angle,
    Vec3i local,
    string[] glyphs,
    string what
  ) {
    BlockPos world = Global(be, local);

    // Oracle 1 - GetGlobalPos equals the anchor plus the independently-rotated offset.
    Vec3i r = ExOrientation.RotateOffset(local, angle);
    Assert.Equal(
      new BlockPos(pos.X + r.X, pos.Y + r.Y, pos.Z + r.Z, pos.dimension),
      world
    );

    // Oracle 2 - that world cell lands an expected glyph in the rotated layout the game assembles.
    var worldRel = new Vec3i(world.X - pos.X, world.Y - pos.Y, world.Z - pos.Z);
    string actual = At(layout, worldRel);
    Assert.True(
      glyphs.Contains(actual),
      $"{what} at {worldRel} holds '{actual}', not one of {string.Join(" | ", glyphs)}"
    );
  }

  #endregion
}
