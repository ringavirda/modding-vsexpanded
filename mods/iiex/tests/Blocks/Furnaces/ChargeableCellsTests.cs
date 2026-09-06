using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// <c>BlockEntityFurnaceCore.ChargeableCells</c> - which world cells of a furnace's own footprint hold the
/// burden column. The set comes off the layout's <see cref="FurnaceCellRoles.Chargeable"/> marks rather than off
/// which cells would accept an <c>iiex:furnace-chargepile</c>; both routes answer the same cells today and
/// these pin that they do, element-wise and at all four facings.
/// <para>
/// The shaft box is not the charge volume: the cold furnace's <c>ShaftBox</c> is a 3x3x5 bounding box of
/// 45 cells and the layout marks 39 of them chargeable, the rest being the tuyere pair, the taps and
/// brick. The rotation risk lives in the drawing, so <see cref="SkewChargeDef"/> below supplies a chiral
/// footprint congruent to none of its own rotations.
/// </para>
/// </summary>
public class ChargeableCellsTests {
  #region Harness

  // Plain concrete helpers only: a generic constraint naming a game type is resolved by xUnit's discovery
  // reflection before the module initializer registers VsAssemblyResolver, which fails the whole
  // assembly. Same rule as FurnaceLayoutRig.

  private static readonly BlockPos Anchor = new(0, 16, 0);

  private static readonly AssetLocation ChargePile = new(
    "iiex:furnace-chargepile"
  );

  /// <summary>Vanilla's pile, still inside the shaft glyph's alternation and no longer accepted by a
  /// hearth.</summary>
  private static readonly AssetLocation CoalPile = new("game:coalpile");

  /// <summary>The block a fuel bed takes. A firebox stands only in a firebox cell, unlike
  /// <see cref="CoalPile"/>, which every shaft cell also accepts - which is what makes the positive
  /// controls below discriminate.</summary>
  private static readonly AssetLocation Firebox = new(
    "iiex:furnace-firebox-tier1-n"
  );

  private static ExBlockDef ColdDef() =>
    BlockBlastFurnaceCoreCold.Definitions("iiex").Single();

  private static ExBlockDef CupolaDef() =>
    BlockCupolaFurnaceCore.Definitions("iiex").Single();

  private static ExBlockDef PuddlingDef() =>
    BlockPuddlingFurnaceCore.Definitions("iiex").Single();

  private static ExBlockDef HeatingDef() =>
    BlockHeatingFurnaceCore.Definitions("iiex").Single();

  /// <summary>
  /// Chargeable cells of a hand-drawn cold-furnace footprint that is chiral: an L in <c>(x, z)</c> plus
  /// one cell a level up, congruent to none of its own rotations. The shaft box is untouched; the layout
  /// is the only input <c>ChargeableCells</c> has.
  /// </summary>
  private static readonly Vec3i[] SkewCharge =
  [
    new(0, 1, 0),
    new(1, 1, 0),
    new(0, 1, 1),
    new(1, 2, 0),
  ];

  /// <summary>
  /// The chiral fixture, drawn through <c>MultiblockLayout</c> rather than the hand-numbered
  /// <c>Multiblock</c> builder, because a role attaches to a glyph and only the ASCII DSL has glyphs. The
  /// shipped shaft glyph appears verbatim so the fixture carries the same legend the real furnaces do;
  /// <see cref="SkewCharge"/> restates the cells independently of the drawing.
  /// </summary>
  private static ExBlockDef SkewChargeDef() =>
    ExBlockDef
      .Create("iiex", "furnace")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "iiex:furnace-blastcore-*")
          .Legend('#', "game:refractorybricks-good-tier*")
          .Legend('c', ShaftGlyph)
          .Role('c', FurnaceCellRoles.Chargeable)
          .Layer(0, "C #")
          .Layer(
            1,
            """
            c c
            c .
            """
          )
          .Layer(2, ". c")
      );

  private static BlockEntityBlastFurnaceCold Cold(string side = "north") {
    var core = new BlockEntityBlastFurnaceCold();
    Stand(core, ColdDef(), Anchor, "iiex:furnace-blastcore-tier1", side);
    return core;
  }

  private static BlockEntityCupolaFurnace Cupola(string side = "north") {
    var core = new BlockEntityCupolaFurnace();
    Stand(core, CupolaDef(), Anchor, "iiex:furnace-cupolacore-tier1", side);
    return core;
  }

  private static BlockEntityBlastFurnaceCold Skew(string side = "north") {
    var core = new BlockEntityBlastFurnaceCold();
    Stand(core, SkewChargeDef(), Anchor, "iiex:furnace-blastcore-tier1", side);
    return core;
  }

  /// <summary>Structure-local cells as one ordered, printable string - the same shape as
  /// <see cref="Render"/> for a set that has not been placed in the world yet.</summary>
  private static string RenderLocal(IEnumerable<Vec3i> cells) =>
    string.Join(
      ", ",
      cells
        .Select(c => $"({c.X},{c.Y},{c.Z})")
        .OrderBy(s => s, System.StringComparer.Ordinal)
    );

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
  /// rotation math. The furnace's own answer comes off vanilla's <c>InitForUse</c>-rotated offset table,
  /// so this is an independent second route to the same cells - the same pairing
  /// <see cref="FurnaceLayoutRig.AssertRotatedCell"/> uses.
  /// </summary>
  private static string ExpectedAt(IEnumerable<Vec3i> local, int angle) =>
    Render(
      local.Select(c => {
        Vec3i r = ExOrientation.RotateOffset(c, angle);
        return Anchor.AddCopy(r.X, r.Y, r.Z);
      })
    );

  /// <summary>The layout's chargeable cells, read off the shipped definition by its authored glyph. This
  /// literal-string route is not how production finds them. The shaft glyph alone on both iwex furnaces:
  /// their crucible course is pool, not burden.</summary>
  private static List<Vec3i> AuthoredChargeCells(ExBlockDef def) =>
    ChargeCells(LayoutOf(def), ShaftGlyph);

  #endregion

  #region The shipped furnaces

  [Fact]
  public void The_cold_furnace_offers_the_thirty_six_cells_its_layout_marks() {
    BlockEntityBlastFurnaceCold core = Cold();
    List<Vec3i> authored = AuthoredChargeCells(ColdDef());

    // Four full 3x3 levels, y=2 up. The crucible course below them is pool only, so the burden rests on
    // the hearthmetal the furnace stands there rather than sharing its cells. Counted so the set equality
    // below cannot pass with both sides empty.
    Assert.Equal(36, authored.Count);
    Assert.Equal(ExpectedAt(authored, 0), Render(core.ChargeableCells));
  }

  [Fact]
  public void The_cold_furnaces_burden_starts_a_course_above_its_crucible() {
    BlockEntityBlastFurnaceCold core = Cold();

    // Nothing at y=1: that course is the crucible, and a burden pile resting there would share a cell
    // with the live bath the furnace pools into. It is also where the tuyere pair and the two taps sit.
    Assert.DoesNotContain(core.ChargeableCells, c => c.Y == Anchor.Y + 1);
    // From y=2 up the full 3x3 opens, four levels of it, and the box is exactly those.
    for (int y = 2; y <= 5; y++)
      Assert.Equal(9, core.ChargeableCells.Count(c => c.Y == Anchor.Y + y));
    Assert.Equal(36, core.ChargeableCells.Count);
  }

  [Fact]
  public void The_cupola_offers_the_four_cells_of_its_single_column() {
    BlockEntityCupolaFurnace core = Cupola();
    List<Vec3i> authored = AuthoredChargeCells(CupolaDef());

    // Four shaft cells, the crucible below them being pool only. The fifth is not owed back: a cupola
    // charges remelt, which carries far more metal per cell than the blast furnace's ore burden.
    Assert.Equal(4, authored.Count);
    Assert.Equal(ExpectedAt(authored, 0), Render(core.ChargeableCells));
    // One column, however many levels of it. On this furnace the ShaftBox is exactly the charge volume
    // rather than merely containing it; the height is whatever the drawing says.
    Assert.All(
      core.ChargeableCells,
      c => Assert.Equal((Anchor.X, Anchor.Z), (c.X, c.Z))
    );
  }

  [Fact]
  public void Every_chargeable_cell_lies_inside_the_shaft_box_and_the_footprint() {
    // Two invariants, checked against the machine the furnace completed rather than against the drawing:
    // the box contains the charge volume, so a box walk bounds a column, and every chargeable cell is one
    // this furnace owns, so placing there cannot tread on a neighbouring structure.
    foreach (
      BlockEntityFurnaceCore core in new BlockEntityFurnaceCore[]
      {
        Cold(),
        Cupola(),
      }
    ) {
      var (min, max) = ShaftBoxOf(core)!.Value;
      Assert.NotEmpty(core.ChargeableCells);
      // Containment is a tautology: the box is the bounding box of this set. What is worth stating is
      // that the box is tight - each of its six faces is touched by at least one chargeable cell, which a
      // padded box, or one taking its bounds from a different role, would fail.
      List<Vec3i> local = core.ChargeableCells.Select(core.LocalOf).ToList();
      Assert.Equal(
        $"{min}..{max}",
        $"{new Vec3i(local.Min(c => c.X), local.Min(c => c.Y), local.Min(c => c.Z))}"
          + $"..{new Vec3i(local.Max(c => c.X), local.Max(c => c.Y), local.Max(c => c.Z))}"
      );
      foreach (BlockPos cell in core.ChargeableCells)
        Assert.True(
          core.OwnsCell(cell),
          $"{core.GetType().Name}: chargeable cell {core.LocalOf(cell)} is outside the footprint it completed"
        );
    }
  }

  #endregion

  #region Rotation

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void The_shipped_charge_volume_turns_with_the_furnace(string side) {
    BlockEntityBlastFurnaceCold core = Cold(side);

    Assert.Equal(
      ExpectedAt(AuthoredChargeCells(ColdDef()), AngleFromSide(side)),
      Render(core.ChargeableCells)
    );
  }

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void A_chiral_charge_volume_turns_with_the_furnace(string side) {
    // The shipped 3x3 levels are closed under 90 deg rotation, so all but the hearth-floor row of the
    // case above passes for a mapping that turned the wrong way. This footprint is congruent to none of
    // its rotations, so nothing survives a wrong one.
    BlockEntityBlastFurnaceCold core = Skew(side);

    Assert.Equal(
      ExpectedAt(SkewCharge, AngleFromSide(side)),
      Render(core.ChargeableCells)
    );
  }

  [Fact]
  public void The_four_facings_of_a_chiral_volume_are_four_different_footprints() {
    // The guard a per-facing theory cannot state: a mapping that ignored the angle would agree with
    // itself at every facing.
    var sets = new[] { "north", "east", "south", "west" }
      .Select(side => Render(Skew(side).ChargeableCells))
      .ToList();

    Assert.Equal(4, sets.Distinct().Count());
  }

  [Fact]
  public void One_named_cell_of_the_shipped_shaft_moves_at_every_facing() {
    // The shipped layout's own asymmetry, named rather than counted: (1,2,0) sits off-centre on x, so it
    // is a different world cell at each of the four facings and must be chargeable at all of them. The
    // volume as a set cannot say this - four square courses are closed under a quarter turn.
    var cells = new List<BlockPos>();
    foreach (string side in new[] { "north", "east", "south", "west" }) {
      BlockEntityBlastFurnaceCold core = Cold(side);
      Vec3i r = ExOrientation.RotateOffset(
        new Vec3i(1, 2, 0),
        AngleFromSide(side)
      );
      BlockPos world = Anchor.AddCopy(r.X, r.Y, r.Z);

      Assert.Contains(world, core.ChargeableCells);
      cells.Add(world);
    }

    Assert.Equal(4, cells.Distinct().Count());
  }

  #endregion

  #region The firebox branch

  [Fact]
  public void A_reverberatory_hearth_offers_no_chargeable_cell_at_all() {
    // With no branch in the accessor: a hearth's drawing marks its fuel cells Firebox, a different role,
    // and the builder refuses a layout claiming both. The legend says so too - `@(air|coalpile)` is
    // domainless, hence implicitly `game:`, hence unable to admit `iiex:furnace-chargepile` - and that is
    // what stops a pile physically standing there. Both are pinned below.
    foreach (BlockEntityFireboxFurnace hearth in Hearths()) {
      Assert.Empty(hearth.ChargeableCells);
      Assert.Empty(hearth.CellsAccepting(ChargePile));

      // The walk is not merely finding nothing: the same footprint answers a non-empty set for the block
      // its firebox does take. Without this the case above passes on a layout that failed to load.
      Assert.NotEmpty(hearth.CellsAccepting(Firebox));
    }
  }

  [Fact]
  public void A_reverberatory_hearth_marks_a_fuel_bed_instead_of_a_burden_column() {
    // A hearth answering empty for Chargeable means something only if the same structure answers
    // something for Firebox; otherwise it says only that this footprint failed to load.
    foreach (BlockEntityFireboxFurnace hearth in Hearths()) {
      Assert.Empty(hearth.CellsWithRole(FurnaceCellRoles.Chargeable));
      Assert.NotEmpty(hearth.CellsWithRole(FurnaceCellRoles.Firebox));

      // The role landed on the right glyph: the fuel bed is exactly the cells that take a firebox.
      // Nothing in the DSL relates a role to the code its glyph carries, so this comparison is the only
      // check between a mistyped Role() call and a silently wrong cell set.
      Assert.Equal(
        Render(hearth.CellsAccepting(Firebox)),
        Render(hearth.CellsWithRole(FurnaceCellRoles.Firebox))
      );
    }
  }

  [Fact]
  public void A_shaft_furnace_offers_the_same_cells_to_a_coal_pile_and_a_charge_pile() {
    // The converse of the hearth case, so "empty for chargepile" is about the hearth's layout rather than
    // about the code being unresolvable anywhere. The shaft glyph is domain-wildcarded so both codes land
    // in the same cells.
    BlockEntityBlastFurnaceCold core = Cold();

    // Both renders are "" if the layout never attached, so the comparison alone would pass in the one
    // case this exists to rule out. The two sibling hearth cases carry the same guard.
    Assert.NotEmpty(core.CellsAccepting(CoalPile));

    Assert.Equal(
      Render(core.CellsAccepting(CoalPile)),
      Render(core.CellsAccepting(ChargePile))
    );
  }

  private static IEnumerable<BlockEntityFireboxFurnace> Hearths() {
    var puddling = new BlockEntityPuddlingFurnace();
    Stand(
      puddling,
      BlockPuddlingFurnaceCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-puddlingcore-tier1",
      "north",
      complete: false
    );
    yield return puddling;

    var heating = new BlockEntityHeatingFurnace();
    Stand(
      heating,
      BlockHeatingFurnaceCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-heatingcore-tier1",
      "north",
      complete: false
    );
    yield return heating;
  }

  #endregion

  #region The role migration

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void The_role_answers_exactly_the_cells_the_charge_pile_code_does(
    string side
  ) {
    // The only cross-check that exists: nothing in the DSL relates a role to the code its glyph carries,
    // so a Role() hung on the brick glyph would build cleanly and answer a plausible-looking set.
    // Element-wise rather than by count, because the same count can come out of a wrong mapping, and at
    // all four facings, because the two routes rotate differently - CellsAccepting resolves each
    // transformed offset's wanted code, CellsWithRole matches authored offsets and reads the transformed
    // one at that index.
    foreach (
      BlockEntityFurnaceCore core in new BlockEntityFurnaceCore[]
      {
        Cold(side),
        Cupola(side),
      }
    ) {
      Assert.NotEmpty(core.ChargeableCells);
      // Less the crucible course. The hearth glyph still admits `furnace-chargepile` on purpose: a world
      // saved before that course stopped being charged has piles standing there, and a code that refused
      // them would read the furnace as broken on load. The next melt claims the cell for hearth metal.
      Assert.Equal(
        Render(core.CellsAccepting(ChargePile).Except(core.PoolCells)),
        Render(core.CellsWithRole(FurnaceCellRoles.Chargeable))
      );
      // And the difference is exactly that course rather than some third set, which the subtraction above
      // would hide.
      Assert.Equal(
        Render(core.PoolCells),
        Render(core.CellsAccepting(ChargePile).Except(core.ChargeableCells))
      );
      // The furnace's own accessor is that role, not something beside it.
      Assert.Same(
        core.CellsWithRole(FurnaceCellRoles.Chargeable),
        core.ChargeableCells
      );
    }
  }

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Every_role_cell_is_a_cell_the_furnace_owns(string side) {
    // Ownership of a role cell is a structural tautology, so this does not catch a wrong-way rotation:
    // CellsWithRole emits `Pos.AddCopy(transformed[i])` drawn from MultiblockStructure.TransformedOffsets
    // and OwnsCell scans that same list, so no reversed rotation can put a cell outside the footprint it
    // was read from. A wrong-way rotation is caught by
    // `The_role_answers_exactly_the_cells_the_charge_pile_code_does` above, which rotates by a different
    // route. What this case pins is that OwnsCell discriminates - the negative control below - and that
    // it walks the transformed footprint rather than the authored one.
    foreach (
      BlockEntityFurnaceCore core in new BlockEntityFurnaceCore[]
      {
        Cold(side),
        Cupola(side),
        Skew(side),
      }
    ) {
      Assert.NotEmpty(core.ChargeableCells);

      // The cell one step under the anchor: below layer 0, so outside the footprint at every facing while
      // adjacent to an owned cell. Without it the loop below can only pass.
      Assert.False(
        core.OwnsCell(core.Pos.AddCopy(0, -1, 0)),
        $"{core.GetType().Name} at {side}: the cell under the anchor is not part of any layer, so it "
          + "must not be owned"
      );

      foreach (BlockPos cell in core.ChargeableCells)
        Assert.True(
          core.OwnsCell(cell),
          $"{core.GetType().Name} at {side}: role cell {core.LocalOf(cell)} is outside the "
            + "footprint it completed"
        );
    }
  }

  [Fact]
  public void Every_shipped_furnace_marks_its_fuel_cells_one_way_and_not_the_other() {
    // The declared roles are what make the builder's Chargeable-XOR-Firebox guard non-vacuous. Read off
    // the emitted attribute rather than through MultiblockCellRoles, so a role the builder failed to emit
    // cannot pass by the reader inventing it, and compared against the literal-glyph route (ChargeCells /
    // FireboxCells read the offsets table and filter by block code), an independent second source for the
    // same cells.
    (
      ExBlockDef Def,
      CellRole Role,
      CellRole Other,
      int Count
    )[] furnaces =
    [
      (ColdDef(), FurnaceCellRoles.Chargeable, FurnaceCellRoles.Firebox, 36),
      (CupolaDef(), FurnaceCellRoles.Chargeable, FurnaceCellRoles.Firebox, 4),
      (PuddlingDef(), FurnaceCellRoles.Firebox, FurnaceCellRoles.Chargeable, 1),
      (HeatingDef(), FurnaceCellRoles.Firebox, FurnaceCellRoles.Chargeable, 2),
    ];

    foreach (var (def, role, other, count) in furnaces) {
      Assert.Contains(role.ToString(), RoleNamesOf(def));
      Assert.DoesNotContain(other.ToString(), RoleNamesOf(def));

      List<Vec3i> declared = RoleCellsOf(def, role);
      List<Vec3i> byGlyph =
        role == FurnaceCellRoles.Chargeable
          ? ChargeCells(LayoutOf(def), ShaftGlyph)
          : FireboxCells(LayoutOf(def));

      Assert.Equal(count, declared.Count);
      Assert.Equal(RenderLocal(byGlyph), RenderLocal(declared));
    }
  }

  // A_furnace_layout_claiming_both_fuel_roles_does_not_build used to pin exlib's own guard against a
  // layout drawing both Chargeable and Firebox. exlib's MultiblockLayoutBuilder no longer knows what
  // either role means, so it cannot enforce this - CellRole is an open string key now, and the
  // shaft/firebox exclusivity is a fact about iiex's furnace class tree, not about multiblock layouts in
  // general. No shipped drawing marks both; nothing in iiex currently re-enforces it at build time.

  #endregion

  #region Caching

  [Fact]
  public void The_answer_is_computed_once_and_handed_back() {
    // Read every time a column's height changes. Same instance, not merely equal contents.
    BlockEntityBlastFurnaceCold core = Cold();

    Assert.Same(core.ChargeableCells, core.ChargeableCells);
  }

  #endregion
}
