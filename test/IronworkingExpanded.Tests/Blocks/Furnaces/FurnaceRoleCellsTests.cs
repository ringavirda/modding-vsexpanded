using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The five cell sets a furnace reads off its own drawing rather than declaring in C#:
/// <see cref="CellRole.Tuyere"/>, <see cref="CellRole.GasOutlet"/>, <see cref="CellRole.Pool"/>,
/// <see cref="CellRole.MetalTap"/> and <see cref="CellRole.SlagTap"/>. Each is checked cell for cell at
/// all four facings; a count would not do, since the cold furnace's two tuyeres are a mirror pair. All
/// but Pool are cross-checked against <c>CellsAccepting</c>, which the layout DSL does not do; Pool's
/// glyph duplicates the shaft glyph, so "the pool is the chargeable cells on the lowest level" stands in
/// instead. The hot blast furnace runs its own copy of this from the smex suite.
/// </summary>
public class FurnaceRoleCellsTests {
  #region Harness

  // Plain concrete helpers only, and no generic constrained on a game type: xUnit's discovery reflection
  // runs before VsAssemblyResolver is registered. Same rule as FurnaceLayoutRig.

  private static readonly BlockPos Anchor = new(0, 16, 0);

  // Codes for the roles with a distinguishing block behind them.
  /// <summary>
  /// The tuyere a cell wants once the furnace faces <paramref name="side"/>: the authored letter (<c>n</c>
  /// for the north wall's inlet, <c>s</c> for the south's) rotated by the structure angle. The legends are
  /// orientation-pinned, so <c>MultiblockFacings</c> swaps in the rotated letter at check time and the
  /// authored letter finds nothing at a rotated facing.
  /// </summary>
  private static AssetLocation Tuyere(string wall, string side) =>
    new(
      "iwex:furnace-tuyere-"
        + ExOrientation.RotateOrientationToken(
          wall,
          ExOrientation.AngleFromSide(side)
        )
    );

  private static readonly AssetLocation PipeOutlet = new(
    "lpex:pipe-outlet-fire-u"
  );

  /// <summary>
  /// The iron tap code a cell accepts on <paramref name="furnace"/> at <paramref name="furnaceSide"/>. The
  /// letter reads backwards: <c>TryPourMetal</c> pours at <c>facing.Opposite</c>, so a tap in the east wall
  /// faces west, and <c>MultiblockFacings</c> rotates that letter with the structure. The drawn side is
  /// per-furnace: the blast furnaces drain iron east (<c>-west</c>) and skim cinder west (<c>-east</c>),
  /// the cupola the other way round.
  /// </summary>
  private static AssetLocation IronTap(string furnace, string furnaceSide) =>
    TapCode("irontap", furnace == "cupola" ? "east" : "west", furnaceSide);

  /// <summary>See <see cref="IronTap"/>.</summary>
  private static AssetLocation SlagTap(string furnace, string furnaceSide) =>
    TapCode("slagtap", furnace == "cupola" ? "west" : "east", furnaceSide);

  /// <summary>The tap code a cell accepts once the structure has been turned to
  /// <paramref name="furnaceSide"/>: the drawn facing, rotated by the furnace's own angle.</summary>
  private static AssetLocation TapCode(
    string type,
    string drawnSide,
    string furnaceSide
  ) =>
    new(
      $"iwex:furnace-{type}-"
        + ExOrientation.SideFromAngle(
          ExOrientation.AngleFromSide(drawnSide) + AngleFromSide(furnaceSide),
          // A `side` group renders a single letter. A spelled-out side names no block, so every lookup
          // answers nothing and the set-equality assertions compare two empties.
          asLetter: true
        )
    );

  /// <summary>The roles every shaft furnace answers non-empty for.</summary>
  private static readonly CellRole[] Migrated =
  [
    CellRole.Tuyere,
    CellRole.GasOutlet,
    CellRole.MetalTap,
    CellRole.SlagTap,
    CellRole.Pool,
  ];

  private static ExBlockDef ColdDef() =>
    BlockBlastFurnaceCoreCold.Definitions("iwex").Single();

  private static ExBlockDef CupolaDef() =>
    BlockCupolaFurnaceCore.Definitions("iwex").Single();

  private static ExBlockDef PuddlingDef() =>
    BlockPuddlingFurnaceCore.Definitions("iwex").Single();

  private static ExBlockDef HeatingDef() =>
    BlockHeatingFurnaceCore.Definitions("iwex").Single();

  private static BlockEntityFurnaceCore Shaft(string furnace, string side) =>
    furnace == "cold" ? Cold(side) : Cupola(side);

  private static BlockEntityBlastFurnaceCold Cold(string side) {
    var core = new BlockEntityBlastFurnaceCold();
    Stand(core, ColdDef(), Anchor, "iwex:furnace-blastcore-tier1", side);
    return core;
  }

  private static BlockEntityCupolaFurnace Cupola(string side) {
    var core = new BlockEntityCupolaFurnace();
    Stand(core, CupolaDef(), Anchor, "iwex:furnace-cupolacore-tier1", side);
    return core;
  }

  /// <summary>
  /// The two reverberatory hearths, raised but not completed: their footprints need blocks the iwex test
  /// host does not register. Raised is enough - the layout loads and every role answers off a real drawing.
  /// </summary>
  private static IEnumerable<BlockEntityFireboxFurnace> Hearths() {
    var puddling = new BlockEntityPuddlingFurnace();
    Stand(
      puddling,
      PuddlingDef(),
      Anchor,
      "iwex:furnace-puddlingcore-tier1",
      "north",
      complete: false
    );
    yield return puddling;

    var heating = new BlockEntityHeatingFurnace();
    Stand(
      heating,
      HeatingDef(),
      Anchor,
      "iwex:furnace-heatingcore-tier1",
      "north",
      complete: false
    );
    yield return heating;
  }

  /// <summary>Cells as one ordered, printable string, so a failure names the set rather than a count.</summary>
  private static string Render(IEnumerable<BlockPos> cells) =>
    string.Join(
      ", ",
      cells
        .Select(p => $"({p.X},{p.Y},{p.Z})")
        .OrderBy(s => s, System.StringComparer.Ordinal)
    );

  /// <summary>
  /// Where a set of structure-local cells lands at <paramref name="angle"/>, computed through the shared
  /// rotation helper - an independent second route, since the furnace answers off vanilla's
  /// <c>InitForUse</c>-rotated offset table.
  /// </summary>
  private static string ExpectedAt(IEnumerable<Vec3i> local, int angle) =>
    Render(
      local.Select(c => {
        Vec3i r = ExOrientation.RotateOffset(c, angle);
        return Anchor.AddCopy(r.X, r.Y, r.Z);
      })
    );

  #endregion

  #region The migration is exact

  [Theory]
  [InlineData("cold", "north")]
  [InlineData("cold", "south")]
  [InlineData("cold", "east")]
  [InlineData("cold", "west")]
  [InlineData("cupola", "north")]
  [InlineData("cupola", "south")]
  [InlineData("cupola", "east")]
  [InlineData("cupola", "west")]
  public void Every_role_accessor_reads_its_own_role_and_nothing_beside_it(
    string furnace,
    string side
  ) {
    BlockEntityFurnaceCore core = Shaft(furnace, side);

    // Reference identity rather than equal contents: on a shipped layout the pool is a subset of the
    // burden column, so a PoolCells answering Chargeable would still be a plausible set of cells.
    Assert.Same(core.CellsWithRole(CellRole.Pool), core.PoolCells);

    // `Single()` is legal here only because the layout build refuses a second cell - see
    // Each_tap_is_exactly_one_cell_and_the_build_refuses_a_second.
    Assert.Equal(
      core.CellsWithRole(CellRole.MetalTap).Single(),
      core.MetalTapPos
    );
    Assert.Equal(
      core.CellsWithRole(CellRole.SlagTap).Single(),
      core.SlagTapPos
    );
  }

  [Theory]
  [InlineData("cold", "north")]
  [InlineData("cold", "south")]
  [InlineData("cold", "east")]
  [InlineData("cold", "west")]
  [InlineData("cupola", "north")]
  [InlineData("cupola", "south")]
  [InlineData("cupola", "east")]
  [InlineData("cupola", "west")]
  public void Every_role_cell_is_a_cell_the_furnace_owns(
    string furnace,
    string side
  ) {
    // Not a rotation check: CellsWithRole and OwnsCell both read TransformedOffsets, so ownership of a
    // role cell is a tautology. The rotation mapping is covered by the four-facing cases further down.
    // What this pins is that OwnsCell discriminates at all (the negative control below) and that it walks
    // the transformed footprint rather than the authored one - pointed at Offsets, it fails at three
    // facings out of four.
    BlockEntityFurnaceCore core = Shaft(furnace, side);

    // The cell one step under the anchor. Every furnace layout starts at layer 0, so this is directly
    // below an owned cell and outside the footprint at every facing.
    Assert.False(
      core.OwnsCell(core.Pos.AddCopy(0, -1, 0)),
      $"{furnace} at {side}: the cell under the anchor is not part of any layer, so it must not be owned"
    );

    foreach (
      CellRole role in new[]
      {
        CellRole.Tuyere,
        CellRole.Pool,
        CellRole.MetalTap,
        CellRole.SlagTap,
      }
    ) {
      Assert.NotEmpty(core.CellsWithRole(role));
      foreach (BlockPos cell in core.CellsWithRole(role))
        Assert.True(
          core.OwnsCell(cell),
          $"{furnace} at {side}: {role} cell {core.LocalOf(cell)} is outside the footprint it completed"
        );
    }
  }

  #endregion

  #region Tuyeres - the role landed on the tuyere glyph

  [Theory]
  [InlineData("cold", "north")]
  [InlineData("cold", "south")]
  [InlineData("cold", "east")]
  [InlineData("cold", "west")]
  [InlineData("cupola", "north")]
  [InlineData("cupola", "south")]
  [InlineData("cupola", "east")]
  [InlineData("cupola", "west")]
  public void The_tuyere_role_answers_exactly_the_cells_a_tuyere_block_may_stand_in(
    string furnace,
    string side
  ) {
    // The independent oracle: nothing in the layout DSL relates a role to the code its glyph carries, so
    // `Role('#', Tuyere)` would build cleanly and answer plausible-looking cells. The two routes rotate
    // differently - CellsAccepting resolves each transformed offset's wanted code, CellsWithRole matches
    // authored offsets and reads the transformed one at that index - hence all four facings.
    //
    // The union has to be the role's cells and each code also has to answer its own cell: a drawing using
    // the north letter for both inlets would pass the union and fail the per-code count.
    BlockEntityFurnaceCore core = Shaft(furnace, side);

    bool blownBothWalls = furnace != "cupola";
    AssetLocation northWall = Tuyere("n", side);
    AssetLocation southWall = Tuyere("s", side);

    IEnumerable<BlockPos> accepted = core.CellsAccepting(northWall);
    if (blownBothWalls)
      accepted = accepted.Concat(core.CellsAccepting(southWall));

    Assert.NotEmpty(core.CellsWithRole(CellRole.Tuyere));
    Assert.Equal(Render(accepted), Render(core.CellsWithRole(CellRole.Tuyere)));

    Assert.Single(core.CellsAccepting(northWall));
    Assert.Equal(blownBothWalls ? 1 : 0, core.CellsAccepting(southWall).Count);
  }

  [Fact]
  public void The_cupola_takes_one_tuyere_and_the_blast_furnace_two() {
    // The cupola is the narrow furnace. A role copied wholesale from the blast furnace onto the cupola's
    // drawing agrees with the tuyere glyph and is still wrong.
    Assert.Single(RoleCellsOf(CupolaDef(), CellRole.Tuyere));
    Assert.Equal(2, RoleCellsOf(ColdDef(), CellRole.Tuyere).Count);
  }

  [Fact]
  public void The_furnace_draws_its_blast_through_the_cells_the_role_names() {
    // The production wiring, not just the accessor: ScanForOutlets fills the field the lit tick walks,
    // and it fills it from the role.
    foreach (string furnace in new[] { "cold", "cupola" }) {
      BlockEntityFurnaceCore core = Shaft(furnace, "east");
      var tuyeres =
        (IReadOnlyList<BlockPos>)ReflectionHelpers.GetField(core, "_tuyeres")!;

      Assert.Equal(
        Render(core.CellsWithRole(CellRole.Tuyere)),
        Render(tuyeres)
      );
      Assert.NotEmpty(tuyeres);
    }
  }

  #endregion

  #region The two taps - one block code, two roles

  [Theory]
  [InlineData("cold", "north")]
  [InlineData("cold", "south")]
  [InlineData("cold", "east")]
  [InlineData("cold", "west")]
  [InlineData("cupola", "north")]
  [InlineData("cupola", "south")]
  [InlineData("cupola", "east")]
  [InlineData("cupola", "west")]
  public void Each_tap_role_is_exactly_the_cells_its_own_tap_block_may_stand_in(
    string furnace,
    string side
  ) {
    // Each role is pinned to its own tap code, so this catches both a role hung on the wrong glyph and
    // the two taps being swapped.
    BlockEntityFurnaceCore core = Shaft(furnace, side);

    Assert.Equal(
      Render(core.CellsAccepting(IronTap(furnace, side))),
      Render(core.CellsWithRole(CellRole.MetalTap))
    );
    Assert.Equal(
      Render(core.CellsAccepting(SlagTap(furnace, side))),
      Render(core.CellsWithRole(CellRole.SlagTap))
    );
    Assert.Single(core.CellsAccepting(IronTap(furnace, side)));
    Assert.Single(core.CellsAccepting(SlagTap(furnace, side)));

    // The facing is part of the demand: the other tap's facing is refused in the same cell. Without this,
    // the two asserts above pass on a layout that wildcarded the facing.
    Assert.Empty(
      core.CellsAccepting(SlagTap(furnace, side))
        .Intersect(core.CellsWithRole(CellRole.MetalTap))
    );
  }

  [Theory]
  [InlineData("cold")]
  [InlineData("cupola")]
  public void The_two_taps_are_distinct_cells_on_opposite_walls(string furnace) {
    // The two drains sit in genuinely different places, so swapping the two Role() lines is detectable.
    // They are level, on opposite walls: nothing in the sim models slag floating above the bath -
    // DrainProducts pulls each pool independently of tap height.
    BlockEntityFurnaceCore core = Shaft(furnace, "north");

    BlockPos metal = Assert.IsType<BlockPos>(core.MetalTapPos);
    BlockPos slag = Assert.IsType<BlockPos>(core.SlagTapPos);
    Assert.NotEqual(metal, slag);
    // Opposite walls: same course, and on opposite sides of the core on one axis.
    Assert.Equal(metal.Y, slag.Y);
    Assert.True(
      (metal.X - core.Pos.X) * (slag.X - core.Pos.X) < 0
        || (metal.Z - core.Pos.Z) * (slag.Z - core.Pos.Z) < 0,
      $"taps are not on opposite walls: metal {metal}, slag {slag}, core {core.Pos}"
    );
  }

  [Fact]
  public void Each_tap_is_exactly_one_cell_and_the_build_refuses_a_second() {
    // The [SingleCell] arity guard: a drain is a point, so the consumer reads `Single()` and the build has
    // to refuse a second cell. Stated on the real tap glyph, not only on exlib's synthetic fixture.
    foreach (ExBlockDef def in new[] { ColdDef(), CupolaDef() }) {
      Assert.Single(RoleCellsOf(def, CellRole.MetalTap));
      Assert.Single(RoleCellsOf(def, CellRole.SlagTap));
    }

    InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
      () =>
        ExBlockDef
          .Create("iwex", "twodrainfurnacecore")
          .MultiblockLayout(s =>
            s.Origin(0, 0)
              .Legend('C', "iwex:furnace-blastcore-*")
              .Legend('T', IronTapGlyph)
              .Role('T', CellRole.MetalTap)
              .Layer(0, "C")
              // The same glyph twice: two drains, one role.
              .Layer(1, "T T")
          )
    );

    Assert.Contains("MetalTap", ex.Message);
    Assert.Contains("2", ex.Message);
  }

  [Fact]
  public void Two_glyphs_cannot_split_one_drain_between_them() {
    // The other route into the same guard: two glyphs sharing a single-cell role rather than one glyph
    // drawn twice. The guard counts drawn cells, so both fail.
    InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
      () =>
        ExBlockDef
          .Create("iwex", "splitdrainfurnacecore")
          .MultiblockLayout(s =>
            s.Origin(0, 0)
              .Legend('C', "iwex:furnace-blastcore-*")
              .Legend('T', IronTapGlyph)
              .Legend('S', SlagTapGlyph)
              .Role('T', CellRole.SlagTap)
              .Role('S', CellRole.SlagTap)
              .Layer(0, "C")
              .Layer(1, "T S")
          )
    );

    Assert.Contains("SlagTap", ex.Message);
  }

  #endregion

  #region Gas outlets - an absence the drawing states

  [Fact]
  public void The_open_topped_furnaces_mark_no_outlet_and_have_none_to_mark() {
    // A role that answers nothing and a furnace that never declared the role are indistinguishable at the
    // accessor, so both are stated: the emitted attribute carries no GasOutlet key, and the drawing has no
    // pipe-outlet cell for one to point at.
    foreach (ExBlockDef def in new[] { ColdDef(), CupolaDef() }) {
      Assert.DoesNotContain(CellRole.GasOutlet.ToString(), RoleNamesOf(def));
      Assert.DoesNotContain(OutletGlyph, LayoutOf(def).Values);
    }

    foreach (string furnace in new[] { "cold", "cupola" }) {
      BlockEntityFurnaceCore core = Shaft(furnace, "north");
      Assert.Empty(core.CellsWithRole(CellRole.GasOutlet));
      Assert.Empty(
        (IReadOnlyList<BlockPos>)
          ReflectionHelpers.GetField(core, "_gasOutlets")!
      );
      // The same footprint answers for the roles it does mark, so the empty above is about outlets rather
      // than about a layout that failed to load.
      Assert.NotEmpty(core.CellsWithRole(CellRole.Tuyere));
    }
  }

  [Fact]
  public void No_iwex_furnace_declares_a_gas_outlet_because_none_of_them_vents() {
    // The whole-domain version: venting belongs to the hot blast furnace, which lives in smex. A future
    // iwex layout that grows a pipe outlet has to add the role deliberately.
    foreach (
      ExBlockDef def in new[]
      {
        ColdDef(),
        CupolaDef(),
        PuddlingDef(),
        HeatingDef(),
      }
    )
      Assert.Empty(RoleCellsOf(def, CellRole.GasOutlet));
  }

  #endregion

  #region The crucible - the role with no code oracle

  [Theory]
  [InlineData("cold", "north")]
  [InlineData("cold", "south")]
  [InlineData("cold", "east")]
  [InlineData("cold", "west")]
  [InlineData("cupola", "north")]
  [InlineData("cupola", "south")]
  [InlineData("cupola", "east")]
  [InlineData("cupola", "west")]
  public void The_crucible_is_the_chargeable_cells_on_the_lowest_level(
    string furnace,
    string side
  ) {
    // Stands in for the code cross-check the other roles get: the Pool glyph points at the same code as
    // the shaft glyph, since a crucible cell is burden and pool at once, so no block code can separate
    // them. What can be stated independently is the relation - the pool is the charge volume's bottom
    // course.
    BlockEntityFurnaceCore core = Shaft(furnace, side);

    int floorY = core.ChargeableCells.Min(c => c.Y);
    Assert.Equal(
      Render(core.ChargeableCells.Where(c => c.Y == floorY)),
      Render(core.PoolCells)
    );
  }

  [Theory]
  [InlineData("cold")]
  [InlineData("cupola")]
  public void The_crucible_is_burden_and_pool_at_once(string furnace) {
    // The overlap is intended: charge rests on the crucible while the furnace runs and metal freezes onto
    // it when the furnace dies, and those are the same cells today - which is why the layout DSL lets one
    // glyph carry two roles. Making the crucible pool-only changes this test; see
    // docs/design/layered-charge.md.
    BlockEntityFurnaceCore core = Shaft(furnace, "north");

    Assert.NotEmpty(core.PoolCells);
    Assert.All(core.PoolCells, c => Assert.Contains(c, core.ChargeableCells));
    Assert.True(core.PoolCells.Count < core.ChargeableCells.Count);
  }

  #endregion

  #region The reverberatory hearths

  [Fact]
  public void A_hearth_marks_no_crucible_and_the_old_declaration_pointed_at_brick() {
    // A hearth marks no crucible, so asking the drawing answers nothing, and it pools no metal:
    // SolidProductBlock is null and DrainedMetalUnits is 0 on the firebox branch, so the freeze returns
    // before it reads a cell. The two cells named below are what the inherited [(0,1,0), (1,1,0)]
    // declaration pointed at on an 8-wide hearth drawing.
    foreach (BlockEntityFireboxFurnace hearth in Hearths()) {
      Assert.Empty(hearth.PoolCells);
      Assert.Empty(hearth.CellsWithRole(CellRole.Tuyere));
      Assert.Empty(hearth.CellsWithRole(CellRole.GasOutlet));
      // The drawing did load: the role it does mark answers.
      Assert.NotEmpty(hearth.CellsWithRole(CellRole.Firebox));
    }

    // What the two inherited cells actually hold: one air cell and one brick.
    foreach (ExBlockDef def in new[] { PuddlingDef(), HeatingDef() }) {
      Dictionary<Vec3i, string> layout = LayoutOf(def);
      Assert.Equal("game:air", At(layout, new Vec3i(0, 1, 0)));
      Assert.Equal(
        "game:refractorybricks-good-tier*",
        At(layout, new Vec3i(1, 1, 0))
      );
    }
  }

  [Fact]
  public void A_hearth_has_no_drain_and_both_old_declarations_missed_the_drawing() {
    // A reverberatory hearth pours nothing - puddled iron leaves as pasty balls through the charge door
    // and the cinder is raked out - so it has no tap of either kind, its drawing carries no tap glyph, and
    // asking the drawing answers null. The cells named below are the ones the inherited MetalTapCell
    // (2,1,0) and the puddling SlagTapCell override (-3,1,1) pointed at; neither is a tap.
    foreach (BlockEntityFireboxFurnace hearth in Hearths()) {
      Assert.Empty(hearth.CellsWithRole(CellRole.MetalTap));
      Assert.Empty(hearth.CellsWithRole(CellRole.SlagTap));
      Assert.Null(hearth.MetalTapPos);
      Assert.Null(hearth.SlagTapPos);
      // The drawing did load, so the nulls above are an absence rather than a failure to read.
      Assert.NotEmpty(hearth.CellsWithRole(CellRole.Firebox));
    }

    // What those cells hold, read off the drawings. Both hearths are 8 columns wide, so a local X of 2 is
    // one past the east edge and not in the layout at all - hence no `At` lookup here.
    foreach (ExBlockDef def in new[] { PuddlingDef(), HeatingDef() }) {
      Dictionary<Vec3i, string> layout = LayoutOf(def);
      Assert.DoesNotContain(new Vec3i(2, 1, 0), layout.Keys);
      // No cell of either drawing is a tap of either kind, by code, so there is nowhere for the role to go.
      Assert.DoesNotContain(IronTapGlyph, layout.Values);
      Assert.DoesNotContain(SlagTapGlyph, layout.Values);
    }

    // (-3,1,1) is one of the fire-brick slab shoulders round the doorway: inside the footprint, so
    // ownership would not have caught it, but not a tap.
    Assert.Equal(
      "game:brickslabs-fire-south-free",
      At(LayoutOf(PuddlingDef()), new Vec3i(-3, 1, 1))
    );
    // The heating hearth's (-2,2,0) is solid refractory brick.
    Assert.Equal(
      "game:refractorybricks-good-tier*",
      At(LayoutOf(HeatingDef()), new Vec3i(-2, 2, 0))
    );
  }

  [Fact]
  public void A_hearth_declares_none_of_the_five_roles_at_all() {
    // The authoring half of the two cases above: empty because nothing was marked, not because the reader
    // dropped something. Read off the emitted attribute rather than through the production reader. The
    // five roles in the name are the shaft-furnace ones - Tuyere, GasOutlet, MetalTap, SlagTap, Pool - and
    // a hearth marks Firebox and Flue instead.
    //
    // Flue marks the centre of the chimney column, not a fixed stack: the layout states the axis and the
    // minimum height and the player builds the rest. The role has no consumer in iwex yet, so this
    // exact-set assertion is the only thing standing behind the marks; it must not be relaxed to a subset.
    foreach (ExBlockDef def in new[] { PuddlingDef(), HeatingDef() }) {
      List<string> names = RoleNamesOf(def);
      Assert.Equal(["Firebox", "Flue"], names);
      foreach (CellRole role in Migrated)
        Assert.DoesNotContain(role.ToString(), names);

      // A role name emitted over no cells would satisfy the list above while answering nothing.
      Assert.NotEmpty(RoleCellsOf(def, CellRole.Flue));
    }
  }

  [Fact]
  public void A_hearth_draws_no_blast_and_vents_through_nothing_plumbed() {
    // The branch invariant, stated against the fields the lit tick walks rather than the accessor.
    //
    // ScanForOutlets has to be driven explicitly: it runs from OnStructureCompleted and two lit-tick
    // paths, none of which fires for a raised-but-incomplete hearth, so without the call the two fields
    // still hold their `[]` initialisers and the assertions read "never assigned" rather than "the role
    // answered nothing". Completing the hearths is not an alternative - see Hearths().
    foreach (BlockEntityFireboxFurnace hearth in Hearths()) {
      ReflectionHelpers.Invoke(hearth, "ScanForOutlets");

      Assert.Empty(
        (IReadOnlyList<BlockPos>)ReflectionHelpers.GetField(hearth, "_tuyeres")!
      );
      Assert.Empty(
        (IReadOnlyList<BlockPos>)
          ReflectionHelpers.GetField(hearth, "_gasOutlets")!
      );
    }

    // The control: a ScanForOutlets that stopped reading the roles would leave the hearth assertions
    // passing for the wrong reason, so the same call on a furnace that marks tuyeres has to fill the field.
    BlockEntityFurnaceCore shaft = Cold("north");
    ReflectionHelpers.SetField(
      shaft,
      "_tuyeres",
      (IReadOnlyList<BlockPos>)new List<BlockPos>()
    );
    ReflectionHelpers.Invoke(shaft, "ScanForOutlets");
    Assert.NotEmpty(
      (IReadOnlyList<BlockPos>)ReflectionHelpers.GetField(shaft, "_tuyeres")!
    );
  }

  #endregion

  #region Rotation

  private static readonly string[] Sides = ["north", "east", "south", "west"];

  [Fact]
  public void The_four_facings_are_four_different_tuyere_and_tap_footprints() {
    // The guard a per-facing theory cannot state: a mapping that ignored the angle agrees with itself
    // everywhere and passes every theory above. Which furnace can state it differs per role - the cupola's
    // single tuyere at (0,1,-1) is off-axis and names four distinct cells, while the cold furnace's tuyere
    // pair is its own mirror image and answers only two distinct sets.
    Assert.Equal(
      4,
      Sides
        .Select(s => Render(Cupola(s).CellsWithRole(CellRole.Tuyere)))
        .Distinct()
        .Count()
    );
    // Two, not four: the cold furnace's crucible runs the full width of the hearth course,
    // (-1,1,0)..(1,1,0), so a half turn maps the row onto itself and north/south and east/west each
    // collapse to one footprint. A quarter turn still lays the row along z, so a mapping that ignored the
    // angle would answer one. The cupola's crucible is the on-axis cell (0,1,0), so no shipped crucible
    // can carry the four-way statement and the taps below carry it instead.
    Assert.Equal(
      2,
      Sides.Select(s => Render(Cold(s).PoolCells)).Distinct().Count()
    );
    Assert.NotEqual(
      Render(Cold("north").PoolCells),
      Render(Cold("east").PoolCells)
    );
    // The taps are the only four-way case: each is a single off-axis cell, so neither is its own mirror
    // image and both name four distinct world cells. A reversed rotation survives 0, 180 and the ownership
    // check, but not this.
    Assert.Equal(
      4,
      Sides.Select(s => Cold(s).MetalTapPos!.ToString()).Distinct().Count()
    );
    Assert.Equal(
      4,
      Sides.Select(s => Cold(s).SlagTapPos!.ToString()).Distinct().Count()
    );
  }

  [Fact]
  public void The_cupolas_one_cell_crucible_is_its_own_image_at_every_facing() {
    // The degenerate case, so the test above is not strengthened into something that cannot hold: (0,1,0)
    // is on the axis of rotation. Asserting the exact cell over the anchor pins this as invariance rather
    // than a set that stopped being computed.
    foreach (string side in Sides)
      Assert.Equal(
        Render([Anchor.AddCopy(0, 1, 0)]),
        Render(Cupola(side).PoolCells)
      );
  }

  #endregion
  #region The shaft box, derived from the fuel roles

  // The shaft box is the bounding box of the cells the drawing marks Chargeable (a shaft) or Firebox (a
  // hearth); nothing declares it anywhere. A bounding box is a weaker signal than a cell set - two
  // different sets share one box - so these cases lean on literal corners and asymmetric shapes.

  /// <summary>
  /// The structure-local corners each furnace's box is expected to have, held as literals so the box is
  /// pinned by something other than the layout it is derived from. The heating hearth's pair follows its
  /// layout origin, one row deeper than the puddling furnace's; the shape of that box - two cells,
  /// adjacent on z - is what its literals pin.
  /// </summary>
  private static (Vec3i Min, Vec3i Max) BoxBefore(string furnace) =>
    furnace switch {
      "cold" => (new Vec3i(-1, 1, -1), new Vec3i(1, 5, 1)),
      "cupola" => (new Vec3i(0, 1, 0), new Vec3i(0, 5, 0)),
      "puddling" => (new Vec3i(-5, 1, 0), new Vec3i(-5, 1, 0)),
      "heating" => (new Vec3i(-5, 1, -1), new Vec3i(-5, 1, 0)),
      _ => throw new KeyNotFoundException(furnace),
    };

  private static BlockEntityFurnaceCore AnyFurnace(
    string furnace,
    string side
  ) =>
    furnace switch {
      "cold" => Cold(side),
      "cupola" => Cupola(side),
      "puddling" or "heating" => Hearth(furnace, side),
      _ => throw new KeyNotFoundException(furnace),
    };

  /// <summary>One hearth at a chosen facing - <see cref="Hearths"/> raises both at north only.</summary>
  private static BlockEntityFireboxFurnace Hearth(string furnace, string side) {
    if (furnace == "puddling") {
      var puddling = new BlockEntityPuddlingFurnace();
      Stand(
        puddling,
        PuddlingDef(),
        Anchor,
        "iwex:furnace-puddlingcore-tier1",
        side,
        complete: false
      );
      return puddling;
    }

    var heating = new BlockEntityHeatingFurnace();
    Stand(
      heating,
      HeatingDef(),
      Anchor,
      "iwex:furnace-heatingcore-tier1",
      side,
      complete: false
    );
    return heating;
  }

  [Theory]
  [InlineData("cold", "north")]
  [InlineData("cold", "south")]
  [InlineData("cold", "east")]
  [InlineData("cold", "west")]
  [InlineData("cupola", "north")]
  [InlineData("cupola", "south")]
  [InlineData("cupola", "east")]
  [InlineData("cupola", "west")]
  [InlineData("puddling", "north")]
  [InlineData("puddling", "south")]
  [InlineData("puddling", "east")]
  [InlineData("puddling", "west")]
  [InlineData("heating", "north")]
  [InlineData("heating", "south")]
  [InlineData("heating", "east")]
  [InlineData("heating", "west")]
  public void The_shaft_box_is_exactly_the_corners_the_deleted_members_held(
    string furnace,
    string side
  ) {
    // The corners are structure-local, so the answer is the same at all four facings: a derivation that
    // leaked the placed rotation into a local fact is right at north and wrong at 90 and 270.
    BlockEntityFurnaceCore core = AnyFurnace(furnace, side);
    var (min, max) = BoxBefore(furnace);

    Assert.Equal(
      $"{furnace}: ({min}, {max})",
      $"{furnace}: {ShaftBoxOf(core)}"
    );
  }

  [Theory]
  [InlineData("cold")]
  [InlineData("cupola")]
  [InlineData("puddling")]
  [InlineData("heating")]
  public void The_world_box_is_the_local_corners_rotated_and_re_sorted_per_axis(
    string furnace
  ) {
    // ShaftBounds() is the one place the box meets rotation, and its re-sort is load-bearing: rotating a
    // corner pair can swap either horizontal axis, so "whatever the low corner rotated into" is not a
    // minimum. The expectation rotates both corners through ExOrientation and takes a component-wise
    // min/max - a route that shares no code with the method.
    var (localMin, localMax) = BoxBefore(furnace);

    foreach (string side in Sides) {
      int angle = AngleFromSide(side);
      Vec3i a = ExOrientation.RotateOffset(localMin, angle);
      Vec3i b = ExOrientation.RotateOffset(localMax, angle);

      var (min, max) = WorldBoxOf(AnyFurnace(furnace, side))!.Value;

      Assert.Equal(
        $"{side}: ({Anchor.X + Math.Min(a.X, b.X)},{Anchor.Y + Math.Min(a.Y, b.Y)},"
          + $"{Anchor.Z + Math.Min(a.Z, b.Z)})..({Anchor.X + Math.Max(a.X, b.X)},"
          + $"{Anchor.Y + Math.Max(a.Y, b.Y)},{Anchor.Z + Math.Max(a.Z, b.Z)})",
        $"{side}: ({min.X},{min.Y},{min.Z})..({max.X},{max.Y},{max.Z})"
      );

      // The box is never inverted on any axis, which is what a box walk needs.
      //
      // A furnace witnesses the re-sort when its two corners differ on x or on z; a quarter turn maps a z
      // difference onto x, so one is enough. The cold furnace differs on both; the heating hearth is
      // degenerate on x and still inverts, on z at 180 and on x at 270. The cupola (one column) and the
      // puddling hearth (one cell) cannot witness it - equal corners have nothing to swap.
      Assert.True(
        min.X <= max.X && min.Y <= max.Y && min.Z <= max.Z,
        $"{furnace} at {side}: the world box is inverted - ({min.X},{min.Y},{min.Z}) is not below "
          + $"({max.X},{max.Y},{max.Z})"
      );
    }
  }

  [Fact]
  public void The_box_is_the_fuel_roles_own_bounds_and_not_a_role_beside_them() {
    // The derivation, not only its result: the box is the bounds of Chargeable/Firebox and not of any
    // other role the same drawing carries. All of the cold furnace's other roles have strictly smaller
    // bounds, so a derivation reading one of them answers a different box.
    BlockEntityBlastFurnaceCold cold = Cold("north");
    Assert.Equal(AuthoredFuelBox(ColdDef()), ShaftBoxOf(cold));

    foreach (CellRole other in Migrated)
      Assert.NotEqual(
        $"{other}: {ShaftBoxOf(cold)}",
        $"{other}: {LocalBoundsOf(ColdDef(), other)}"
      );

    // The hearths are the other half of the union: their drawings mark no Chargeable cell at all, so a
    // Chargeable-only derivation gives them no box, and a hearth with no box collects no fuel and never
    // lights.
    foreach (BlockEntityFireboxFurnace hearth in Hearths()) {
      Assert.Empty(hearth.CellsWithRole(CellRole.Chargeable));
      Assert.NotEmpty(hearth.CellsWithRole(CellRole.Firebox));
      Assert.NotNull(ShaftBoxOf(hearth));
    }
  }

  /// <summary>The bounding box of one role's authored cells, read off the emitted attribute. Null when the
  /// drawing marks none.</summary>
  private static (Vec3i, Vec3i)? LocalBoundsOf(ExBlockDef def, CellRole role) {
    List<Vec3i> cells = RoleCellsOf(def, role);
    return cells.Count == 0
      ? null
      : (
        new Vec3i(
          cells.Min(c => c.X),
          cells.Min(c => c.Y),
          cells.Min(c => c.Z)
        ),
        new Vec3i(cells.Max(c => c.X), cells.Max(c => c.Y), cells.Max(c => c.Z))
      );
  }

  /// <summary>
  /// A shaft furnace drawn without a fuel role. The legend is the real shaft glyph, so the cells are
  /// genuinely chargeable-looking; only the <c>Role</c> call is missing.
  /// </summary>
  private static ExBlockDef NoFuelRoleDef() =>
    ExBlockDef
      .Create("iwex", "furnace")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "iwex:furnace-blastcore-*")
          .Legend('c', ShaftGlyph)
          .Layer(0, "C")
          .Layer(1, "c")
          .Layer(2, "c")
      );

  [Fact]
  public void A_drawing_that_marks_no_fuel_cell_has_no_box_and_nothing_throws() {
    // No pair of corners can mean "empty": ShaftBounds() and EnsureShaftColumns both re-sort per axis, so
    // an impossible corner pair normalises back into a real box at the origin - a furnace collecting fuel
    // from a cell it does not own. Emptiness is therefore carried above the corners, and every consumer
    // answers nothing.
    var core = new BlockEntityBlastFurnaceCold();
    Stand(
      core,
      NoFuelRoleDef(),
      Anchor,
      "iwex:furnace-blastcore-tier1",
      "north"
    );

    Assert.Null(ShaftBoxOf(core));
    Assert.Null(WorldBoxOf(core));
    Assert.Empty(core.ShaftColumns);
    Assert.Equal(0, core.ShaftChargeUnits);
    Assert.Null(core.ChargeColumnAt(0, 0));
    Assert.Null(core.ChargeColumnAt(Anchor.AddCopy(0, 1, 0), out int index));
    Assert.Equal(-1, index);

    // EnsureShaftColumns is reached from FromTreeAttributes, which the engine calls before Initialize, and
    // it legitimately builds nothing here; both halves of the round trip have to tolerate that.
    var tree = new TreeAttribute();
    core.ToTreeAttributes(tree);
    Assert.DoesNotContain(tree.Keys, k => k.StartsWith("chargeCol"));
    core.FromTreeAttributes(tree, core.Api.World);
    Assert.Empty(core.ShaftColumns);
  }

  [Fact]
  public void A_box_derived_before_the_layout_arrives_is_never_memoised() {
    // EnsureShaftColumns runs from FromTreeAttributes, and a furnace that cached "no box" there would own
    // no columns for the rest of its life. Vanilla assigns Block in CreateBehaviors immediately before
    // FromTreeAttributes on every load path; the cache must not depend on that ordering.
    var core = new BlockEntityBlastFurnaceCold();
    Orient(core, "iwex:furnace-blastcore-tier1-n", "north"); // a block with no attributes at all
    Assert.Null(ShaftBoxOf(core));
    Assert.Empty(core.ShaftColumns);

    // The layout arrives late, and the furnace recovers completely.
    Stand(core, ColdDef(), Anchor, "iwex:furnace-blastcore-tier1", "north");
    Assert.Equal(BoxBefore("cold"), ShaftBoxOf(core)!.Value);
    Assert.Equal(9, core.ShaftColumns.Count);
  }

  #endregion
}
