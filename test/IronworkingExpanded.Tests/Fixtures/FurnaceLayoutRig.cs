using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using NSubstitute;
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
/// Note: no generic helper with a <c>where T : BlockEntity</c> constraint anywhere in this file. A generic
/// constraint naming a game type is resolved when xUnit reflects over the assembly during discovery -
/// which happens before the module initializer registers <c>VsAssemblyResolver</c> - so it fails the whole
/// assembly with "could not find dependent assembly VintagestoryAPI". Plain concrete helpers avoid that.
/// </para>
/// </summary>
public static class FurnaceLayoutRig
{
  #region Role glyphs

  // The wildcard a functional cell must resolve to in the layout, per role - one source for both oracles.

  // Orientation-pinned, not wildcarded. A tuyere is walled in on three sides, so its cell admits
  // exactly one connector face - and `BlockNetworkNode.RecalculateAndSyncOrientations` exchanges a placed
  // node onto that face as soon as the walls go up. The drawing can therefore state it, and these two are
  // rotated with the structure by `MultiblockFacings` (single direction letters rotate: see
  // `ExOrientation.IsHorizontalSideWord`, which takes `n`/`s`/`e`/`w` as well as the full words).
  public const string NorthTuyereGlyph = "iwex:furnace-tuyere-n";
  public const string SouthTuyereGlyph = "iwex:furnace-tuyere-s";

  // Two glyphs since the taps were typed. They used to be one code drawn twice, so a
  // drawing that swapped `T` and `S` was indistinguishable from a correct one - every assertion here
  // could only be played off the pair. Distinct codes make each role's glyph checkable on its own.
  // Facing-pinned since the furnace redraw, not `-*`. Wildcarding the facing is what let a tap be
  // fitted the wrong way round and still complete the structure.
  // The letter reads backwards. `BlockEntityFurnaceTap.TryPourMetal` spouts at
  // `Pos.AddCopy(facing.Opposite).DownCopy()`, so a tap faces into the furnace and drains out the
  // other way: the shaft furnaces' iron notch is in their east wall and is declared `-west`.
  public const string IronTapGlyph = "iwex:furnace-irontap-w";
  public const string SlagTapGlyph = "iwex:furnace-slagtap-e";

  // And the cupola is mirrored, which is why these cannot be one pair of constants. It drains cast
  // iron out its west wall and skims cinder off its east - the opposite hand to the shaft furnaces -
  // so its glyphs are the opposite facings too. Both drawings are internally correct (each notch has a
  // free runout cell); they simply do not agree with each other, and a rig that assumed they did
  // reported the cupola's perfectly good iron tap as the wrong block.
  public const string CupolaIronTapGlyph = "iwex:furnace-irontap-e";
  public const string CupolaSlagTapGlyph = "iwex:furnace-slagtap-w";

  /// <summary>
  /// The pair of drain glyphs one furnace's drawing declares, passed in by the caller for the same
  /// reason the tuyere glyphs are: the shipped furnaces do not agree on them, so a shared constant can
  /// only be right for one of them.
  /// </summary>
  public readonly record struct TapGlyphs(string Iron, string Slag)
  {
    /// <summary>The blast furnaces, cold and hot: iron out east, cinder off west.</summary>
    public static TapGlyphs ShaftFurnace => new(IronTapGlyph, SlagTapGlyph);

    /// <summary>The cupola, the other hand round. See <see cref="CupolaIronTapGlyph"/>.</summary>
    public static TapGlyphs Cupola => new(CupolaIronTapGlyph, CupolaSlagTapGlyph);
  }
  // Domain-wildcarded, and restated here as a literal rather than read off the production layout: an
  // expectation computed from the thing it pins agrees with any value of it. The `*:` is what lets a shaft
  // cell hold `iwex:furnace-chargepile` as well as `game:air`/`game:coalpile` - vanilla's matcher compares domain
  // and path separately, so a bare alternation is implicitly `game:` and no edit inside it can cross domains.
  public const string ShaftGlyph = "*:@(air|coalpile|furnace-chargepile)";

  /// <summary>
  /// The crucible-floor glyph. Same three occupants as <see cref="ShaftGlyph"/> plus the hearth-metal
  /// block, because the furnace's own pool is a legitimate occupant of the hearth course.
  /// <para>
  /// The metal member is <c>hearthmetal-.*</c> and the <c>.</c> is <b>not</b> a typo - inside
  /// <c>@(…)</c> the body is a regex, so a bare <c>*</c> matches the wrong set entirely. See
  /// <c>IwexCodes.HearthCell</c>, which carries the measurement.
  /// </para>
  /// <para>
  /// It is a <b>different string</b> from <see cref="ShaftGlyph"/> but carries the same
  /// <c>CellRole.Chargeable</c>, which is why <see cref="ChargeCells"/> has to name both. Burden
  /// stands on the crucible floor while the furnace runs; a literal-glyph oracle that matched only
  /// the shaft would disagree with the role route by exactly the hearth row - and did, from the
  /// furnace redraw until this was added.
  /// </para>
  /// </summary>
  public const string HearthGlyph =
    "*:@(air|coalpile|furnace-chargepile|hearthmetal-.*)";

  public const string OutletGlyph = "lpex:pipe-outlet*";

  /// <summary>
  /// A reverberatory hearth's fuel-bed glyph - a required block, wildcarded over tier and facing.
  /// <para>
  /// This was <c>@(air|coalpile)</c> once, and the change is the point of the firebox
  /// block: a legend that admits <b>air</b> means an empty firebox satisfies the structure, so a
  /// reverberatory furnace completed with no fuel cell built at all. Restated here as a literal rather
  /// than read off the production layout, for the reason <see cref="ShaftGlyph"/> is: an expectation
  /// computed from the thing it pins agrees with any value of it.
  /// </para>
  /// </summary>
  public const string FireboxGlyph = "iwex:furnace-firebox-*-*";

  #endregion

  #region Standing a furnace up

  /// <summary>
  /// The token a <c>side</c> variant actually wears in a shipped code - the <b>single letter</b>, since
  /// the side respelling.
  /// <para>
  /// <b>The rig converts so the tests do not.</b> A theory says <c>"west"</c> because a direction is
  /// what the case is about; the spelling of that direction inside a block code is the rig's business,
  /// exactly as <c>WithSide(BlockFacing)</c> is the generated table's. Without one conversion point a
  /// respelling has to be chased through every fixture, and the failure it leaves behind is silent -
  /// a code naming no block simply fails to satisfy a cell, and the structure "does not complete".
  /// </para>
  /// Accepts either spelling in, so a caller already holding a letter is not forced to round-trip.
  /// </summary>
  public static string SideToken(string side) =>
    ExOrientation.SideFromAngle(ExOrientation.AngleFromSide(side), asLetter: true);

  /// <summary>
  /// Points <paramref name="be"/> at a block coded <c>{blockCode}</c> carrying the <c>("side", …)</c>
  /// variant its <c>ExOrientable</c> behaviour stamps, then runs the block entity's own rotation update
  /// so its structure angle is whatever it derives - not whatever the test wants.
  /// </summary>
  public static void Orient(BlockEntity be, string blockCode, string side)
  {
    var world = new TestWorld();
    be.Block = TestBlocks.Configure(new Block(), blockCode, 1, ("side", SideToken(side)));
    world.Attach(be);
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
  }

  /// <summary>
  /// <see cref="Orient"/>, plus <paramref name="def"/>'s real <c>attributes</c> on the placed block - so the
  /// machine can load its layout, its oriented-part table and its <b>cell roles</b> without a whole footprint
  /// being raised around it.
  /// <para>
  /// Needed since geometry moved into the drawings. The bare <see cref="Orient"/> gives the block no attributes at all, so a machine
  /// stood up that way sees <c>MultiblockCellRoles.None</c> - which was harmless while geometry was
  /// hand-declared on the block entity and is not now that the shaft box is the bounding box of the drawing's
  /// own <see cref="CellRole.Chargeable"/>/<see cref="CellRole.Firebox"/> marks. A test that only needs the
  /// geometry (no completion, no world cells to fill) uses this; one that needs the machine to complete uses
  /// <see cref="Stand"/>.
  /// </para>
  /// </summary>
  public static void OrientWithLayout(
    BlockEntity be,
    ExBlockDef def,
    string blockCode,
    string side
  )
  {
    Orient(be, blockCode, side);
    if (def.ToJson()["attributes"] is not JObject attributes)
      throw new InvalidOperationException(
        $"Block definition '{def.Code}' has no attributes, so it carries no layout."
      );
    be.Block.Attributes = new JsonObject(attributes);
    // The angle was already resolved against a block with no layout, so the load has to be re-run: the
    // machine's own UpdateStructureRotation is short-circuited by SetStructureAngle only once _structure
    // is non-null, which it is not yet.
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
  }

  /// <summary>
  /// Stands <paramref name="anchor"/> up at <paramref name="pos"/> on a real block coded
  /// <c>{blockCode}-{side}</c>, raises <paramref name="def"/>'s footprint around it and - when
  /// <paramref name="complete"/> - lets the machine complete itself.
  /// <para>
  /// Nothing forces <c>StructureComplete</c>: a wrong angle shows up as a machine that never completes
  /// rather than as a scene that silently tests nothing. Unlike <see cref="Orient"/> this attaches the
  /// def's real <c>attributes</c> to the placed block (<see cref="StructureRig.Around"/> does it), so the
  /// machine can load its layout, its oriented-part table and its cell roles - which is what any test of a
  /// layout-derived cell set needs.
  /// </para>
  /// </summary>
  public static StructureRig Stand(
    BlockEntityMultiblockStructure anchor,
    ExBlockDef def,
    BlockPos pos,
    string blockCode,
    string side,
    bool complete = true
  )
  {
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

  /// <summary>The structure angle a given side implies, derived here rather than read off the machine.
  /// <para>
  /// Delegates to <see cref="ExOrientation.AngleFromSide"/> rather than re-tabulating the four cases:
  /// that one accepts <b>either</b> spelling, so a theory saying <c>"west"</c> and a fixture holding
  /// <c>"w"</c> cannot disagree about which way the furnace points.
  /// </para>
  /// </summary>
  public static int AngleFromSide(string side) =>
    ExOrientation.IsHorizontalSideWord(side)
      ? ExOrientation.AngleFromSide(side)
      : throw new ArgumentOutOfRangeException(nameof(side), side, null);

  #endregion

  #region Odd-shaped fixture layouts

  /// <summary>
  /// A cold-furnace core wearing a hand-drawn footprint whose <see cref="CellRole.Chargeable"/> volume is
  /// exactly the solid box <paramref name="min"/>..<paramref name="max"/>, so the furnace's shaft box - now
  /// the bounding box of that volume - is exactly those corners.
  /// <para>
  /// <b>Why this exists.</b> Three suites each kept their own odd-shaped furnace by overriding
  /// <c>ShaftMin</c>/<c>ShaftMax</c> on a subclass while wearing the <em>shipped</em> square drawing. The
  /// migration deleted both properties, and re-pointing those fixtures at a drawing is not a workaround for
  /// the deletion - it is the fixture finally varying the thing production actually reads. A shipped shaft is
  /// square or a single column, and a square column set is closed under 90 deg rotation, so an asymmetric
  /// drawing is the only shape in which a transposed derivation loop or a set keyed off the rotated world box
  /// is visible at all.
  /// </para>
  /// <para>
  /// <paramref name="brick"/> adds one non-chargeable cell - a cell the furnace <em>owns</em> but which is
  /// outside its shaft box, which is the case "the anchor scan found a furnace and the box still says no".
  /// </para>
  /// <para>
  /// The anchor sits at <c>(0,0,0)</c> and the box must not contain it, or the drawing would need two
  /// glyphs in one cell. Every caller's box starts at <c>y >= 1</c>.
  /// </para>
  /// </summary>
  public static ExBlockDef ShaftBoxDef(
    Vec3i min,
    Vec3i max,
    Vec3i? brick = null
  )
  {
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
      .Create("iwex", "furnace")
      .MultiblockLayout(s =>
      {
        s.Origin(xLeft, zTop)
          .Legend('C', "iwex:furnace-blastcore-*")
          .Legend('#', "game:refractorybricks-good-tier*")
          .Legend('c', ShaftGlyph)
          .Role('c', CellRole.Chargeable);

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
  /// The <b>authored</b> (north-frame) cells a def's layout marks with <paramref name="role"/>, read back
  /// out of the generated <c>multiblockRoles</c> attribute - the authoring half of the role feature, as
  /// <see cref="LayoutOf"/> is the authoring half of the offsets table. Empty for a layout that marks
  /// nothing.
  /// <para>
  /// Read by the enum's <b>name</b> rather than through <c>MultiblockCellRoles</c>, deliberately: a test
  /// that used the production reader could not tell a role that was never emitted from one the reader
  /// silently dropped.
  /// </para>
  /// </summary>
  public static List<Vec3i> RoleCellsOf(ExBlockDef def, CellRole role)
  {
    JToken? roles = def.ToJson()["attributes"]?["multiblockRoles"];
    if (roles?[role.ToString()] is not JArray cells)
      return [];
    return cells
      .Select(c => new Vec3i((int)c["x"]!, (int)c["y"]!, (int)c["z"]!))
      .ToList();
  }

  /// <summary>The role names a def's layout emits at all, in emission order - so a test can state that a
  /// furnace marks a burden column <b>and nothing else fuel-shaped</b>, rather than only that it marks
  /// one.</summary>
  public static List<string> RoleNamesOf(ExBlockDef def) =>
    def.ToJson()["attributes"]?["multiblockRoles"] is JObject roles
      ? roles.Properties().Select(p => p.Name).ToList()
      : [];

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

  /// <summary>
  /// <see cref="AssertGlyph"/> where more than one glyph is legitimate - the shaft centre, which sits on
  /// the crucible course on both shipped furnaces and so reads as the hearth glyph rather than the shaft
  /// one. Names every accepted glyph in the failure so a wrong cell still says what it found.
  /// </summary>
  public static void AssertGlyph(
    Dictionary<Vec3i, string> layout,
    Vec3i cell,
    string[] expectedCodes,
    string what
  )
  {
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

  /// <summary>The layout's chargeable cells - the shaft column the burden actually occupies. The
  /// literal-glyph route, deliberately <b>not</b> how production finds them (it asks the cell's
  /// <see cref="CellRole.Chargeable"/> role), so the two are independent oracles.</summary>
  public static List<Vec3i> ChargeCells(Dictionary<Vec3i, string> layout) =>
    layout
      // Both glyphs. The shaft and the crucible floor are different code strings carrying the same
      // Chargeable role - see HearthGlyph. Matching only the shaft made this oracle disagree with
      // production by exactly the hearth row.
      .Where(kv => kv.Value == ShaftGlyph || kv.Value == HearthGlyph)
      .Select(kv => kv.Key)
      .ToList();

  /// <summary>The layout's fuel-bed cells, by the same literal-glyph route as
  /// <see cref="ChargeCells"/>.</summary>
  public static List<Vec3i> FireboxCells(Dictionary<Vec3i, string> layout) =>
    layout.Where(kv => kv.Value == FireboxGlyph).Select(kv => kv.Key).ToList();

  /// <summary>
  /// The machine's own shaft box, or null when its drawing marks no fuel cell at all. Read off the block
  /// entity through reflection because <c>ShaftBox</c> is <c>protected</c> - the same route the deleted
  /// <c>ShaftMin</c>/<c>ShaftMax</c> reads took.
  /// </summary>
  public static (Vec3i min, Vec3i max)? ShaftBoxOf(object be) =>
    ((Vec3i, Vec3i)?)ReflectionHelpers.GetProperty(be, "ShaftBox");

  /// <summary>
  /// The machine's shaft box in <b>world</b> space for its placed facing - <see cref="ShaftBoxOf"/>'s two
  /// corners rotated and then re-sorted per component. Read off <c>ShaftBounds()</c> through reflection,
  /// which is <c>protected</c>.
  /// <para>
  /// <b>Shared rather than test-local because it is the only route to the re-sort.</b>
  /// <see cref="ShaftBoxOf"/> answers the structure-local box, which is facing-invariant - so an assertion
  /// built on it holds at all four facings for free and can never see a rotation bug. <c>ShaftBounds()</c>
  /// is the one place in the mod where the box meets rotation, and its per-component re-sort is
  /// load-bearing: a quarter turn can swap either horizontal axis, so "whatever the low corner rotated
  /// into" is not a minimum and a walk built on it runs backwards and visits nothing.
  /// </para>
  /// <para>
  /// Until this helper existed the whole re-sort had <b>one</b> test reader in the tree, in iwex, and a
  /// suite that only ever asked <see cref="ShaftBoxOf"/> could not tell. That is why a downstream furnace
  /// asserts against this one rather than adding a fourth local box assertion.
  /// </para>
  /// </summary>
  public static (BlockPos min, BlockPos max)? WorldBoxOf(object be) =>
    ((BlockPos, BlockPos)?)ReflectionHelpers.Invoke(be, "ShaftBounds");

  /// <summary>
  /// The bounding box of the <b>authored</b> fuel cells a def's layout marks - computed here, off the
  /// emitted <c>multiblockRoles</c> attribute, as the independent second route to the box the machine
  /// derives for itself. Null when the drawing marks neither role.
  /// <para>
  /// Deliberately <em>not</em> a call into the production derivation: an expectation computed by the
  /// thing it pins agrees with any value of it. This walks the JSON with its own min/max.
  /// </para>
  /// </summary>
  public static (Vec3i min, Vec3i max)? AuthoredFuelBox(ExBlockDef def)
  {
    List<Vec3i> cells =
    [
      .. RoleCellsOf(def, CellRole.Chargeable),
      .. RoleCellsOf(def, CellRole.Firebox),
    ];
    if (cells.Count == 0)
      return null;
    return (
      new Vec3i(cells.Min(c => c.X), cells.Min(c => c.Y), cells.Min(c => c.Z)),
      new Vec3i(cells.Max(c => c.X), cells.Max(c => c.Y), cells.Max(c => c.Z))
    );
  }

  /// <summary>
  /// The furnace's shaft box is exactly the bounding box of the fuel cells its own drawing marks - stated
  /// through two routes that share no code: the block entity's derivation, and this file's own min/max over
  /// the emitted role attribute.
  /// <para>
  /// What this replaces is <b>weaker</b> than what it says. <c>AssertShaftMatchesLayout</c> asserted the
  /// box <em>contained</em> every chargeable cell, which was a real statement while the corners were
  /// hand-declared and is a tautology now that they are the containing box. Tightness is the statement that
  /// survives: a derivation that padded the box, or took the union with something else, fails here.
  /// </para>
  /// </summary>
  public static void AssertShaftBoxIsTheDrawings(
    BlockEntity be,
    ExBlockDef def,
    string furnace
  )
  {
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
  /// Checks every functional cell of a furnace against its anchor's authored layout, at angle 0 where
  /// structure-local and world offsets coincide: the anchor at the origin, its tuyere(s), both taps, the
  /// shaft centre, the shaft box, and any gas outlets (the loop just runs zero times for the open-topped
  /// furnaces). Returns the outlet cells so the caller can state its own furnace's outlet expectation -
  /// "none, the open top is the chimney" or "some, and they must land on pipe outlets" - which is the one
  /// thing that genuinely differs between them.
  /// <para>
  /// Every functional cell now comes off the layout's own <see cref="CellRole"/> marks rather than off a
  /// property of <paramref name="be"/>, because the properties they were read from are gone. That makes
  /// these loops <b>stronger</b> than what they replace: nothing in the DSL relates a role to the code
  /// its glyph carries, so "every cell marked Tuyere holds <c>iwex:furnace-tuyere-*</c>" is the only statement
  /// anywhere that a <c>Role()</c> call landed on the glyph its author meant.
  /// </para>
  /// <para>
  /// <b>Every role loop but the outlets carries an <c>Assert.NotEmpty</c>.</b> A loop over an empty set
  /// runs zero times and passes, so a role that lost all its cells would slip through silently - the
  /// vacuity this plan has now shipped seven times. The outlet loop is the one exception <em>by design</em>:
  /// "no outlets" is the cold furnace's and the cupola's true answer, so its emptiness is the caller's to
  /// state (<see cref="AssertNoExhaustOutlets"/>, or smex's <c>Assert.NotEmpty(outlets)</c>), which is why
  /// the set is returned rather than asserted here.
  /// </para>
  /// </summary>
  public static List<Vec3i> AssertFurnaceGeometry(
    BlockEntity be,
    ExBlockDef def,
    string anchorGlyph,
    string furnace,
    TapGlyphs taps,
    params string[] tuyereGlyphs
  )
  {
    Dictionary<Vec3i, string> layout = LayoutOf(def);

    // The anchor stands in its own layout, at the layout's origin.
    AssertGlyph(layout, new Vec3i(0, 0, 0), anchorGlyph, "anchor");

    // The tuyere set is the caller's, because the three shaft furnaces do not agree on it: the cold
    // and hot furnaces are blown from both walls, the cupola only from the north. Passing it in is what
    // lets each drawing be checked against its own blast arrangement instead of a shared wildcard.
    AssertRoleGlyphs(def, layout, CellRole.Tuyere, tuyereGlyphs, "tuyere");

    // Both drains, each its own [SingleCell] role on its own glyph and its own block - `T` the iron
    // notch, `S` the cinder notch. These two lines used to pass the same glyph twice, which is what a
    // one-block-two-glyphs family forces; the type split is what lets them differ, and a drawing that
    // swaps the two now fails here instead of being invisible.
    // And the pair is the caller's too, for the same reason the tuyeres are: the cupola drains the
    // opposite hand to the blast furnaces, so a shared constant reported its correct tap as wrong.
    AssertRoleGlyphs(def, layout, CellRole.MetalTap, taps.Iron, "metal tap");
    AssertRoleGlyphs(def, layout, CellRole.SlagTap, taps.Slag, "slag tap");

    // The shaft centre sits on the hearth course on both shipped furnaces, so it reads as the
    // crucible glyph rather than the shaft one.
    AssertGlyph(
      layout,
      Cell(be, "ShaftCentre"),
      ShaftGlyph,
      "shaft centre"
    );

    // The burden column itself, and the box it bounds. The glyph loop is what the deleted
    // AssertShaftMatchesLayout's Assert.NotEmpty(charge) becomes, and it is stronger: it says every cell the
    // drawing marks Chargeable really is a shaft cell, which nothing said before.
    // Both glyphs. The shaft and the crucible floor are different code strings carrying the same
    // Chargeable role - the hearth one also admits solidified metal, because a frozen pool is a
    // legitimate occupant of that course. Accepting only the shaft made this fail on every furnace
    // whose hearth row is chargeable, which is both of them.
    AssertRoleGlyphs(
      def,
      layout,
      CellRole.Chargeable,
      [ShaftGlyph, HearthGlyph],
      "chargeable"
    );
    AssertShaftBoxIsTheDrawings(be, def, furnace);

    // The crucible is drawn with its own glyph so it can carry Pool beside Chargeable, but that glyph
    // points at the shaft's code - so a Pool cell must still read as shaft, exactly like the column above it.
    // Same pair, same reason: the crucible carries Pool beside Chargeable.
    AssertRoleGlyphs(
      def,
      layout,
      CellRole.Pool,
      // HearthGlyph alone - see the note on the north-only oracle above.
      HearthGlyph,
      "pool"
    );

    List<Vec3i> outlets = RoleCellsOf(def, CellRole.GasOutlet);
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
  /// The <b>several-codes</b> form: every cell of <paramref name="role"/> holds one of
  /// <paramref name="glyphs"/>, and every listed glyph is used by at least one of them.
  /// <para>
  /// Needed because a role's cells need not all want the same block. The two tuyeres are the case: each
  /// is walled in on three sides, so each has exactly one orientation its cell admits - <c>n</c> for the
  /// one in the north wall, <c>s</c> for the one in the south - and pinning that is the whole point. A
  /// single-glyph assertion cannot express "these two cells, these two different codes".
  /// </para>
  /// <para>
  /// The second half is what keeps this from being a weakening. Without it a drawing that used the
  /// north tuyere for both cells would pass, since both cells would still hold a listed glyph.
  /// </para>
  /// </summary>
  private static void AssertRoleGlyphs(
    ExBlockDef def,
    Dictionary<Vec3i, string> layout,
    CellRole role,
    string[] glyphs,
    string what
  )
  {
    List<Vec3i> cells = RoleCellsOf(def, role);
    Assert.True(cells.Count > 0, $"{def.Code}: no cell is marked {role}");

    var used = new HashSet<string>();
    foreach (Vec3i cell in cells)
    {
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
  /// Pins that a furnace has no exhaust outlet at all: none marked by the layout, and no pipe outlet in the
  /// layout for one to point at. That is what "the open top is the chimney" means for the cold blast
  /// furnace and the cupola - stated so adding a pipe outlet to one of those layouts fails loudly.
  /// <para>
  /// The two halves are not redundant. The first says the drawing has nowhere to vent; the second says
  /// the drawing does not claim to. Before the role migration the second half read a C# override - and an
  /// override saying "empty" is indistinguishable from a furnace that simply never declared anything, which
  /// is the ambiguity the role removes.
  /// </para>
  /// </summary>
  public static void AssertNoExhaustOutlets(ExBlockDef def)
  {
    Assert.DoesNotContain(OutletGlyph, LayoutOf(def).Values);
    Assert.Empty(RoleCellsOf(def, CellRole.GasOutlet));
  }

  #endregion

  #region The four-orientation oracle

  /// <summary>
  /// Stands the already-oriented <paramref name="be"/> against the layout rotated to
  /// <paramref name="side"/> and checks both oracles for every functional cell: its tuyere(s), both taps,
  /// the shaft centre, its crucible floor, and its gas outlets (none for the cold furnace and the cupola -
  /// the loop just runs zero times). The role-derived sets are fed through the same
  /// <see cref="AssertRotatedCell"/> as the hand-declared ones, so a role cell that rotated the wrong way
  /// lands on a glyph that is not its own.
  /// <para>
  /// Non-empty is asserted for every role but the outlets, for the reason spelled out on
  /// <see cref="AssertFurnaceGeometry"/>: a `foreach` over nothing is a test that ran zero assertions.
  /// </para>
  /// </summary>
  public static void AssertFurnaceMatrix(
    BlockEntity be,
    ExBlockDef def,
    string anchorGlyph,
    string side,
    TapGlyphs taps,
    params string[] tuyereGlyphs
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

    AssertRotatedRole(
      be,
      def,
      layout,
      angle,
      CellRole.Tuyere,
      tuyereGlyphs,
      "tuyere"
    );
    AssertRotatedRole(
      be,
      def,
      layout,
      angle,
      CellRole.MetalTap,
      taps.Iron,
      "metal tap"
    );
    AssertRotatedRole(
      be,
      def,
      layout,
      angle,
      CellRole.SlagTap,
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
      CellRole.Pool,
      // HearthGlyph alone, not the pair. The several-codes form also asserts that every listed
      // glyph is used by some cell - which is what stops one tuyere code covering both tuyeres - and
      // every pool cell is a crucible cell, so naming the shaft glyph here would fail on the half of
      // the assertion that is doing the real work.
      HearthGlyph,
      "pool"
    );

    foreach (Vec3i outlet in RoleCellsOf(def, CellRole.GasOutlet))
      AssertRotatedCell(be, layout, pos, angle, outlet, OutletGlyph, "gas outlet");
  }

  /// <summary>Every cell a def marks with <paramref name="role"/>, rotated and checked through both
  /// oracles - and there is at least one, so the loop cannot pass by running zero times.</summary>
  private static void AssertRotatedRole(
    BlockEntity be,
    ExBlockDef def,
    Dictionary<Vec3i, string> layout,
    int angle,
    CellRole role,
    string glyph,
    string what
  ) => AssertRotatedRole(be, def, layout, angle, role, [glyph], what);

  /// <summary>The <b>several-codes</b> form, for a role whose cells do not all want the same block - see
  /// the sibling <c>AssertRoleGlyphs</c> overload for why the tuyeres need it.
  /// <para>
  /// The glyph is compared in its <b>authored</b> form even here, because <see cref="RotatedLayoutOf"/>
  /// rotates the structure's offsets (vanilla `InitForUse`) and leaves its codes alone - the code rotation
  /// is `MultiblockFacings`, and it happens at completion-check time, not at layout-build time.
  /// </para>
  /// </summary>
  private static void AssertRotatedRole(
    BlockEntity be,
    ExBlockDef def,
    Dictionary<Vec3i, string> layout,
    int angle,
    CellRole role,
    string[] glyphs,
    string what
  )
  {
    List<Vec3i> cells = RoleCellsOf(def, role);
    Assert.True(cells.Count > 0, $"{def.Code}: no cell is marked {role}");

    var used = new HashSet<string>();
    foreach (Vec3i cell in cells)
    {
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
  ) => AssertRotatedCell(be, layout, pos, angle, local, [glyph], what);

  /// <summary>The <b>several-codes</b> form, for a cell more than one glyph may legitimately hold - the
  /// shaft centre, which sits on the crucible course and so reads as the hearth glyph rather than the
  /// shaft one. See <see cref="HearthGlyph"/>.</summary>
  public static void AssertRotatedCell(
    object be,
    Dictionary<Vec3i, string> layout,
    BlockPos pos,
    int angle,
    Vec3i local,
    string[] glyphs,
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
