using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace IronworkingExpanded.Tests;

/// <summary>
/// <c>BlockEntityFurnaceCore.ChargeableCells</c> - which world cells of a furnace's own footprint hold the
/// burden column. Phase 3 places and removes <c>iwex:furnace-chargepile</c> against this set as a column's height
/// changes; nothing reads it yet.
/// <para>
/// <b>The set now comes off the layout's own <see cref="CellRole.Chargeable"/> marks</b>, not off "which
/// cells would accept an <c>iwex:furnace-chargepile</c>". Both answer the same 38 cells today and these pin that
/// they do, element-wise and at all four facings - but only the role keeps answering it once the charge
/// block is retyped, which is the point of the migration.
/// </para>
/// <para>
/// <b>The shaft box is not the charge volume.</b> The cold furnace's <c>ShaftBox</c> is a 3x3x5
/// <em>bounding</em> box of 45 cells and the layout marks 38 of them chargeable - the hearth floor is
/// mostly the tuyere pair and brick. Filling the box would put charge inside a wall, so the answer has to
/// come off the layout, cell by cell, and that is what these pin.
/// </para>
/// <para>
/// <b>Where the rotation risk actually lives.</b> The suite's odd shafts
/// (<c>ShaftColumnsTests.AsymmetricDef</c>, <c>FurnaceOrientationMatrixTests.LopsidedDef</c>,
/// <c>ChargePileTests.SkewShaftDef</c>) used to vary <c>ShaftMin</c>/<c>ShaftMax</c> on a subclass, and this
/// set is derived from the <b>layout</b>, which none of them touched - so none of them could see a rotation
/// bug here. All three are asymmetric <em>drawings</em> now, so they can.
/// The asymmetry that bites is in the drawing: the shipped hearth-floor pair <c>(0,1,0)/(1,1,0)</c> is
/// asymmetric on x and lands on a different world cell at each facing, and
/// <see cref="SkewChargeDef"/> below adds a fully chiral one so a mapping that turns the wrong way cannot
/// hide in a shape that happens to survive it.
/// </para>
/// </summary>
public class ChargeableCellsTests
{
  #region Harness

  // Note: plain concrete helpers only - a generic constraint naming a game type is resolved by xUnit's
  // discovery reflection before the module initializer registers VsAssemblyResolver, which fails the
  // whole assembly. Same rule as FurnaceLayoutRig.

  private static readonly BlockPos Anchor = new(0, 16, 0);

  private static readonly AssetLocation ChargePile = new("iwex:furnace-chargepile");

  /// <summary>Vanilla's pile - still inside the <b>shaft</b> glyph's alternation, which is why it is still
  /// here. It left the hearths with the firebox cutover.</summary>
  private static readonly AssetLocation CoalPile = new("game:coalpile");

  /// <summary>The block a fuel bed takes. This was <see cref="CoalPile"/> until the firebox became a
  /// block of its own, and the swap is what makes the positive controls below mean something
  /// they could not mean before: a coal pile was accepted by a hearth's fuel cell <em>and</em> by every
  /// shaft cell, so "the fuel bed is exactly the cells that take this" was a weaker statement than it read
  /// as. A firebox stands only in a firebox cell.</summary>
  private static readonly AssetLocation Firebox = new(
    "iwex:furnace-firebox-tier1-n"
  );

  private static ExBlockDef ColdDef() =>
    BlockBlastFurnaceCoreCold.Definitions("iwex").Single();

  private static ExBlockDef CupolaDef() =>
    BlockCupolaFurnaceCore.Definitions("iwex").Single();

  private static ExBlockDef PuddlingDef() =>
    BlockPuddlingFurnaceCore.Definitions("iwex").Single();

  private static ExBlockDef HeatingDef() =>
    BlockHeatingFurnaceCore.Definitions("iwex").Single();

  /// <summary>
  /// A cold-furnace core wearing a hand-drawn footprint whose chargeable cells are <b>chiral</b>: an L in
  /// <c>(x, z)</c> plus one cell a level up, congruent to none of its own rotations.
  /// <para>
  /// Note what is varied and what is not. The shaft <em>box</em> is untouched - the existing odd-shaft
  /// fixtures already cover that, and it has no say in this answer at all. What is varied is the
  /// <b>layout</b>, which is the only input <c>ChargeableCells</c> has.
  /// </para>
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
  /// <c>Multiblock</c> builder, because a role attaches to a <b>glyph</b> and only the ASCII DSL has
  /// glyphs. The shipped shaft glyph appears verbatim so the fixture carries the same legend the real
  /// furnaces do; <see cref="SkewCharge"/> restates the cells independently of the drawing.
  /// </summary>
  private static ExBlockDef SkewChargeDef() =>
    ExBlockDef
      .Create("iwex", "furnace")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "iwex:furnace-blastcore-*")
          .Legend('#', "game:refractorybricks-good-tier*")
          .Legend('c', ShaftGlyph)
          .Role('c', CellRole.Chargeable)
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

  private static BlockEntityBlastFurnaceCold Cold(string side = "north")
  {
    var core = new BlockEntityBlastFurnaceCold();
    Stand(core, ColdDef(), Anchor, "iwex:furnace-blastcore-tier1", side);
    return core;
  }

  private static BlockEntityCupolaFurnace Cupola(string side = "north")
  {
    var core = new BlockEntityCupolaFurnace();
    Stand(core, CupolaDef(), Anchor, "iwex:furnace-cupolacore-tier1", side);
    return core;
  }

  private static BlockEntityBlastFurnaceCold Skew(string side = "north")
  {
    var core = new BlockEntityBlastFurnaceCold();
    Stand(core, SkewChargeDef(), Anchor, "iwex:furnace-blastcore-tier1", side);
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
      local.Select(c =>
      {
        Vec3i r = ExOrientation.RotateOffset(c, angle);
        return Anchor.AddCopy(r.X, r.Y, r.Z);
      })
    );

  /// <summary>The layout's own chargeable cells, read off the shipped definition by its authored glyph -
  /// the literal-string route, which is deliberately <b>not</b> how production finds them.</summary>
  private static List<Vec3i> AuthoredChargeCells(ExBlockDef def) =>
    ChargeCells(LayoutOf(def));

  #endregion

  #region The shipped furnaces

  [Fact]
  public void The_cold_furnace_offers_the_thirty_nine_cells_its_layout_marks()
  {
    BlockEntityBlastFurnaceCold core = Cold();
    List<Vec3i> authored = AuthoredChargeCells(ColdDef());

    // The hearth-floor row plus the full levels above it. Stated so the set equality below cannot
    // pass by both sides having quietly become empty.
    // The hearth course is a full three-cell row since the furnace redraw, which is where the
    // thirty-ninth cell comes from.
    Assert.Equal(39, authored.Count);
    Assert.Equal(ExpectedAt(authored, 0), Render(core.ChargeableCells));
  }

  [Fact]
  public void The_cold_furnaces_hearth_floor_is_three_cells_of_nine_not_the_whole_box()
  {
    BlockEntityBlastFurnaceCold core = Cold();

    // The fact the whole accessor exists for. The shaft box is 3x3 at y=1; the layout opens only
    // the middle row of those nine, and the rest are the tuyere pair, the two taps and brick. A
    // Phase 3 that filled the box would place charge into a wall on the very first course.
    // The hearth course is the full `h h h` row.
    int floorY = Anchor.Y + 1;
    Assert.Equal(
      Render(
        [
          Anchor.AddCopy(-1, 1, 0),
          Anchor.AddCopy(0, 1, 0),
          Anchor.AddCopy(1, 1, 0),
        ]
      ),
      Render(core.ChargeableCells.Where(c => c.Y == floorY))
    );
    // ...and above the tuyeres the full 3x3 opens, four levels of it.
    for (int y = 2; y <= 5; y++)
      Assert.Equal(9, core.ChargeableCells.Count(c => c.Y == Anchor.Y + y));
  }

  [Fact]
  public void The_cupola_offers_the_five_cells_of_its_single_column()
  {
    BlockEntityCupolaFurnace core = Cupola();
    List<Vec3i> authored = AuthoredChargeCells(CupolaDef());

    // Five: four shaft cells plus the crucible floor. It briefly
    // read as four only because this oracle was not matching the hearth glyph - see HearthGlyph.
    Assert.Equal(5, authored.Count);
    Assert.Equal(ExpectedAt(authored, 0), Render(core.ChargeableCells));
    // One column, however many levels of it - the shape the cupola's ShaftBox also spans, which on
    // this furnace happens to be exactly the charge volume rather than merely containing it. The
    // column-ness is the claim worth keeping; the height is just what the drawing currently says.
    Assert.All(
      core.ChargeableCells,
      c => Assert.Equal((Anchor.X, Anchor.Z), (c.X, c.Z))
    );
  }

  [Fact]
  public void Every_chargeable_cell_lies_inside_the_shaft_box_and_the_footprint()
  {
    // Two invariants Phase 3 leans on, checked against the machine the furnace actually completed rather
    // than against the drawing: the box really does contain the charge volume (so a box walk can be used
    // to bound a column), and every chargeable cell is one this furnace owns (so placing there cannot
    // tread on a neighbouring structure).
    foreach (BlockEntityFurnaceCore core in new BlockEntityFurnaceCore[]
    {
      Cold(),
      Cupola(),
    })
    {
      var (min, max) = ShaftBoxOf(core)!.Value;
      Assert.NotEmpty(core.ChargeableCells);
      // Containment is now a tautology - the box is the bounding box of this set, so a cell
      // outside it is not representable. What is still worth stating is that the box is tight: each of the
      // six faces is touched by at least one chargeable cell, which a derivation that padded the box, or
      // that took its bounds from a different role, would fail.
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
  public void The_shipped_charge_volume_turns_with_the_furnace(string side)
  {
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
  public void A_chiral_charge_volume_turns_with_the_furnace(string side)
  {
    // The one that actually bites. The shipped 3x3 levels are closed under 90 deg rotation, so all but
    // the hearth-floor pair of the case above would pass for a mapping that turned the wrong way. This
    // footprint is congruent to none of its rotations, so nothing survives a wrong one.
    BlockEntityBlastFurnaceCold core = Skew(side);

    Assert.Equal(
      ExpectedAt(SkewCharge, AngleFromSide(side)),
      Render(core.ChargeableCells)
    );
  }

  [Fact]
  public void The_four_facings_of_a_chiral_volume_are_four_different_footprints()
  {
    // The guard a per-facing theory cannot state: a mapping that ignored the angle would agree with
    // itself at every facing. Four distinct sets is the fact.
    var sets = new[] { "north", "east", "south", "west" }
      .Select(side => Render(Skew(side).ChargeableCells))
      .ToList();

    Assert.Equal(4, sets.Distinct().Count());
  }

  [Fact]
  public void One_named_cell_of_the_shipped_hearth_floor_moves_at_every_facing()
  {
    // The shipped layout's own asymmetry, named rather than counted: (1,1,0) sits off-centre on x, so it
    // is a different world cell at each of the four facings - and it must be chargeable at all of them.
    var cells = new List<BlockPos>();
    foreach (string side in new[] { "north", "east", "south", "west" })
    {
      BlockEntityBlastFurnaceCold core = Cold(side);
      Vec3i r = ExOrientation.RotateOffset(new Vec3i(1, 1, 0), AngleFromSide(side));
      BlockPos world = Anchor.AddCopy(r.X, r.Y, r.Z);

      Assert.Contains(world, core.ChargeableCells);
      cells.Add(world);
    }

    Assert.Equal(4, cells.Distinct().Count());
  }

  #endregion

  #region The firebox branch

  [Fact]
  public void A_reverberatory_hearth_offers_no_chargeable_cell_at_all()
  {
    // And with no branch in the accessor to make it so - a hearth's drawing marks its fuel cells
    // Firebox, which is a different role, and the builder refuses a layout claiming both. Before the
    // roles it was the legend that said so: `@(air|coalpile)` is domainless, hence implicitly `game:`,
    // hence unable to admit `iwex:furnace-chargepile`. Both statements are pinned below, because the second is
    // still what stops a pile physically standing there.
    foreach (BlockEntityFireboxFurnace hearth in Hearths())
    {
      Assert.Empty(hearth.ChargeableCells);
      Assert.Empty(hearth.CellsAccepting(ChargePile));

      // ...and the walk is not merely finding nothing: the same footprint answers a non-empty set for
      // the block its firebox does take. Without this the case above would pass on a layout that failed
      // to load at all.
      Assert.NotEmpty(hearth.CellsAccepting(Firebox));
    }
  }

  [Fact]
  public void A_reverberatory_hearth_marks_a_fuel_bed_instead_of_a_burden_column()
  {
    // The positive half. A hearth answering empty for Chargeable is only meaningful if the same
    // structure answers something for Firebox - otherwise it says nothing about roles at all, only that
    // this footprint failed to load.
    foreach (BlockEntityFireboxFurnace hearth in Hearths())
    {
      Assert.Empty(hearth.CellsWithRole(CellRole.Chargeable));
      Assert.NotEmpty(hearth.CellsWithRole(CellRole.Firebox));

      // And the role landed on the right glyph: the fuel bed is exactly the cells that take a coal
      // pile. Nothing in the DSL relates a role to the code its glyph carries, so this comparison is the
      // only thing standing between a mistyped Role() call and a silently wrong cell set.
      Assert.Equal(
        Render(hearth.CellsAccepting(Firebox)),
        Render(hearth.CellsWithRole(CellRole.Firebox))
      );
    }
  }

  [Fact]
  public void A_shaft_furnace_offers_the_same_cells_to_a_coal_pile_and_a_charge_pile()
  {
    // The converse of the hearth case, so "empty for chargepile" is a statement about the hearth's
    // layout rather than about the code being unresolvable anywhere. The shaft glyph is domain-wildcarded
    // precisely so both codes land in the same cells.
    BlockEntityBlastFurnaceCold core = Cold();

    // Both renders are "" if the layout never attached - which is exactly the failure this test exists
    // to exclude, so the comparison alone would pass in the one case it is here to rule out. Its two
    // sibling hearth cases already carry the same guard.
    Assert.NotEmpty(core.CellsAccepting(CoalPile));

    Assert.Equal(
      Render(core.CellsAccepting(CoalPile)),
      Render(core.CellsAccepting(ChargePile))
    );
  }

  private static IEnumerable<BlockEntityFireboxFurnace> Hearths()
  {
    var puddling = new BlockEntityPuddlingFurnace();
    Stand(
      puddling,
      BlockPuddlingFurnaceCore.Definitions("iwex").Single(),
      Anchor,
      "iwex:furnace-puddlingcore-tier1",
      "north",
      complete: false
    );
    yield return puddling;

    var heating = new BlockEntityHeatingFurnace();
    Stand(
      heating,
      BlockHeatingFurnaceCore.Definitions("iwex").Single(),
      Anchor,
      "iwex:furnace-heatingcore-tier1",
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
  )
  {
    // The migration's proof, and the only cross-check that exists at all: nothing in the DSL relates a
    // role to the code its glyph carries, so a Role() hung on the brick glyph would build cleanly and
    // answer a plausible-looking set. Element-wise rather than by count, because the same 38 could come
    // out of a wrong mapping - and at all four facings, because the two routes rotate differently
    // (CellsAccepting resolves each transformed offset's wanted code; CellsWithRole matches authored
    // offsets and reads the transformed one at that index).
    foreach (
      BlockEntityFurnaceCore core in new BlockEntityFurnaceCore[]
      {
        Cold(side),
        Cupola(side),
      }
    )
    {
      Assert.NotEmpty(core.ChargeableCells);
      Assert.Equal(
        Render(core.CellsAccepting(ChargePile)),
        Render(core.CellsWithRole(CellRole.Chargeable))
      );
      // ...and the furnace's own accessor is that role, not something beside it.
      Assert.Same(
        core.CellsWithRole(CellRole.Chargeable),
        core.ChargeableCells
      );
    }
  }

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Every_role_cell_is_a_cell_the_furnace_owns(string side)
  {
    // This test does not catch A wrong-way rotation, and an earlier version of this comment claimed it
    // did. Ownership of a role cell is a structural tautology: CellsWithRole emits
    // `Pos.AddCopy(transformed[i])` drawn from MultiblockStructure.TransformedOffsets, and OwnsCell scans
    // that same list. No reversed rotation can put a cell it produced outside the footprint, because the
    // footprint is where it read the cell from. Verified by mutation: scrambling the authored->transformed
    // index mapping fails 61 iwex tests, and this is not one of them.
    //
    // What actually catches it is `The_role_answers_exactly_the_cells_the_charge_pile_code_does` above -
    // the element-wise comparison against CellsAccepting, which resolves each transformed offset's wanted
    // code and so rotates by a genuinely different route. Do not delete that as "redundant with the
    // ownership invariant"; it is the real coverage and this test cannot replace it.
    //
    // What ownership does pin, and the only reason it is still here: that OwnsCell discriminates (the
    // negative control below - without it `OwnsCell => true` passes all 2296 tests), and that it walks the
    // transformed footprint rather than the authored one. Both are facts about OwnsCell itself.
    foreach (
      BlockEntityFurnaceCore core in new BlockEntityFurnaceCore[]
      {
        Cold(side),
        Cupola(side),
        Skew(side),
      }
    )
    {
      Assert.NotEmpty(core.ChargeableCells);

      // The cell one step under the anchor - below layer 0, so outside the footprint at every facing while
      // being adjacent to an owned cell. Without this the loop below can only ever pass.
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
  public void Every_shipped_furnace_marks_its_fuel_cells_one_way_and_not_the_other()
  {
    // This is what makes the builder's Chargeable-XOR-Firebox guard non-vacuous. Until these five
    // layouts declared a role, its first operand was false for every layout in the repo and the second
    // was never evaluated at all.
    // Read off the emitted attribute rather than through MultiblockCellRoles, so a role the builder
    // failed to emit cannot pass by the reader inventing it; and compared against the literal-glyph
    // route (ChargeCells / FireboxCells read the offsets table and filter by block code), which is a
    // genuinely independent second source for the same cells.
    (
      ExBlockDef Def,
      CellRole Role,
      CellRole Other,
      int Count
    )[] furnaces =
    [
      (ColdDef(), CellRole.Chargeable, CellRole.Firebox, 39),
      (CupolaDef(), CellRole.Chargeable, CellRole.Firebox, 5),
      (PuddlingDef(), CellRole.Firebox, CellRole.Chargeable, 1),
      (HeatingDef(), CellRole.Firebox, CellRole.Chargeable, 2),
    ];

    foreach (var (def, role, other, count) in furnaces)
    {
      Assert.Contains(role.ToString(), RoleNamesOf(def));
      Assert.DoesNotContain(other.ToString(), RoleNamesOf(def));

      List<Vec3i> declared = RoleCellsOf(def, role);
      List<Vec3i> byGlyph =
        role == CellRole.Chargeable
          ? ChargeCells(LayoutOf(def))
          : FireboxCells(LayoutOf(def));

      Assert.Equal(count, declared.Count);
      Assert.Equal(RenderLocal(byGlyph), RenderLocal(declared));
    }
  }

  [Fact]
  public void A_furnace_layout_claiming_both_fuel_roles_does_not_build()
  {
    // The other direction of the same guard, stated on the real glyphs rather than on exlib's synthetic
    // fixture: a drawing with a shaft and a firebox is what the shaft/firebox class split says cannot
    // exist, so it must be unrepresentable rather than merely unusual.
    InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
      () =>
        ExBlockDef
          .Create("iwex", "impossiblefurnacecore")
          .MultiblockLayout(s =>
            s.Origin(0, 0)
              .Legend('C', "iwex:furnace-blastcore-*")
              .Legend('c', ShaftGlyph)
              .Legend('k', FireboxGlyph)
              .Role('c', CellRole.Chargeable)
              .Role('k', CellRole.Firebox)
              .Layer(0, "C")
              .Layer(1, "c k")
          )
    );

    Assert.Contains("Chargeable", ex.Message);
    Assert.Contains("Firebox", ex.Message);
  }

  #endregion

  #region Caching

  [Fact]
  public void The_answer_is_computed_once_and_handed_back()
  {
    // Phase 3 reads this every time a column's height changes. Same instance, not merely equal contents.
    BlockEntityBlastFurnaceCold core = Cold();

    Assert.Same(core.ChargeableCells, core.ChargeableCells);
  }

  #endregion
}
