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
/// The five cell sets that stopped being hand-written C# and became things a furnace asks its own
/// drawing: <see cref="CellRole.Tuyere"/> (was <c>TuyereCells</c>), <see cref="CellRole.GasOutlet"/> (was
/// <c>GasOutletCells</c>), <see cref="CellRole.Pool"/> (was <c>SolidifyCells</c>),
/// <see cref="CellRole.MetalTap"/> (was <c>MetalTapCell</c>) and <see cref="CellRole.SlagTap"/> (was
/// <c>SlagTapCell</c>).
/// <para>
/// <b>What these tests are for is the migration being exact.</b> Each set's pre-migration value is
/// restated here as a literal - see <see cref="Before"/> - and the role has to answer it cell for cell, at
/// all four facings, on every furnace that had one. A count would not do: the cold furnace's two tuyeres are
/// a mirror pair, so swapping them is invisible to anything that counts.
/// </para>
/// <para>
/// <b>The code cross-check is uneven, and each role's gap is filled differently.</b> A <c>Tuyere</c> cell
/// holds <c>iwex:furnace-tuyere-*</c> and a <c>GasOutlet</c> cell holds <c>lpex:pipe-outlet*</c>, so for those the
/// role can be played off <c>CellsAccepting</c> - an independent oracle that catches a <c>Role()</c> hung on
/// the wrong glyph, which nothing in the DSL checks. <b><c>Pool</c> has none, and cannot have one</b>: its
/// glyph is a deliberate duplicate of the shaft glyph, pointing at the identical code, because a crucible
/// cell is burden column and metal pool at once; what stands in is the relation the deleted test asserted -
/// the pool is exactly the chargeable cells on the lowest level. <b>The two taps had half an oracle until
/// they were typed</b>: both were <c>iwex:furnace-tap-*</c>, so <c>CellsAccepting</c> could say the pair sat
/// on tap cells but not which was which, and the missing half was left to <see cref="Before"/> and to the
/// physical relation - the slag tap is the higher of the two, because slag floats. Now each
/// tap carries its own code (<c>furnace-irontap</c> / <c>furnace-slagtap</c>) and the oracle is whole; the
/// two relations stay because they are independent of it.
/// </para>
/// <para>
/// The hot blast furnace runs its own copy of all of this from the smex suite, because the iwex test host
/// never loads that assembly.
/// </para>
/// </summary>
public class FurnaceRoleCellsTests
{
  #region Harness

  // Note: plain concrete helpers only, and no generic constrained on a game type - xUnit's discovery
  // reflection runs before VsAssemblyResolver is registered. Same rule as FurnaceLayoutRig.

  private static readonly BlockPos Anchor = new(0, 16, 0);

  // Wildcard-matching codes for the roles that have a distinguishing block behind them.
  // Two codes since the tuyeres were orientation-pinned. A tuyere is walled in on three
  // sides, so its cell admits exactly one connector face - `n` out of the north wall, `s` out of the south
  // - and the drawings now say so. The oracle is correspondingly sharper: it is no longer "these cells hold
  // a tuyere" but "this cell holds this tuyere".
  /// <summary>
  /// The tuyere a cell wants once the furnace faces <paramref name="side"/>: the authored letter
  /// (<c>n</c> for the north wall's inlet, <c>s</c> for the south's) rotated by the structure angle.
  /// <para>
  /// <b>The rotation is the whole reason this is a method and not a constant.</b> The tuyere legends are
  /// orientation-pinned, so <c>MultiblockFacings</c> swaps the letter for the structure-rotated one at
  /// check time - a west-facing furnace wants <c>iwex:furnace-tuyere-e</c> in the cell that was drawn
  /// <c>n</c>. Asking with the authored letter at a rotated facing finds nothing, which is a passing
  /// oracle turned into a vacuous one.
  /// </para>
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
  /// One code per tap. It used to be one code for both, which is why every
  /// assertion below could only be played off the pair; each is now checkable on its own.
  /// <para>
  /// And the facing is part of the demand now, so these cannot be constants. The letter reads
  /// backwards: <c>TryPourMetal</c> pours at <c>facing.Opposite</c>, so a tap in the east wall faces
  /// west and spouts east. `MultiblockFacings` rotates that letter with the structure, so the code a
  /// cell will accept at side <c>east</c> is not the one it accepts at <c>north</c>. Hard-coding
  /// `-north` made every one of these read empty at three facings out of four.
  /// </para>
  /// <para>
  /// And the two shipped drawings are mirrored, so the drawn side is per-furnace too. The blast
  /// furnaces drain iron out east (<c>-west</c>) and skim cinder off west (<c>-east</c>); the cupola is
  /// the other hand round. Both are internally correct - each notch has a free runout cell - they just
  /// do not agree with each other, and assuming they did reported the cupola's correct tap as wrong.
  /// </para>
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
          // A `side` group renders a single letter since the side respelling. Spelling it out
          // here produced a code no block carries, so every one of these asked for nothing and the
          // set-equality assertions compared two empties - vacuously green, not correct.
          asLetter: true
        )
    );

  /// <summary>The roles the migration moved that every shaft furnace answers non-empty for.</summary>
  private static readonly CellRole[] Migrated =
  [
    CellRole.Tuyere,
    CellRole.GasOutlet,
    CellRole.MetalTap,
    CellRole.SlagTap,
    CellRole.Pool,
  ];

  // `Before(furnace, role)` lived here: the structure-local cells each deleted C# member held,
  // transcribed from the source before the role migration removed it -- the migration's ground truth.
  //
  // Retired along with the half of the test that consumed it. Those literals described the
  // furnace as it was drawn when the migration happened (tuyeres at y=1, the slag tap at (-2,2,0), a
  // two-cell crucible). The cold furnace and the cupola have since been redrawn on purpose, so the
  // literals now describe a machine that no longer exists. Re-pointing them at the new drawing would
  // have turned the comparison into "the layout equals itself"; leaving them would have made every
  // deliberate redraw look like a regression.
  //
  // What replaced them is nothing, deliberately: the migration is years done, and the properties
  // still worth asserting -- arity, rotation, role-vs-accessor wiring, cells landing on the right
  // blocks -- are each pinned by their own test elsewhere in this file, none of which depend on the
  // pre-migration shape.

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

  private static BlockEntityBlastFurnaceCold Cold(string side)
  {
    var core = new BlockEntityBlastFurnaceCold();
    Stand(core, ColdDef(), Anchor, "iwex:furnace-blastcore-tier1", side);
    return core;
  }

  private static BlockEntityCupolaFurnace Cupola(string side)
  {
    var core = new BlockEntityCupolaFurnace();
    Stand(core, CupolaDef(), Anchor, "iwex:furnace-cupolacore-tier1", side);
    return core;
  }

  /// <summary>
  /// The two reverberatory hearths, raised but deliberately <b>not</b> completed - their footprints need
  /// blocks the iwex test host does not register. Raised is enough: the layout loads, so every role
  /// answers off a real drawing.
  /// </summary>
  private static IEnumerable<BlockEntityFireboxFurnace> Hearths()
  {
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
  /// rotation helper. The furnace's own answer comes off vanilla's <c>InitForUse</c>-rotated offset table,
  /// so this is a genuinely independent second route to the same world cells.
  /// </summary>
  private static string ExpectedAt(IEnumerable<Vec3i> local, int angle) =>
    Render(
      local.Select(c =>
      {
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
  )
  {
    // This was `Every_migrated_set_answers_exactly_what_the_deleted_array_held`, and its first half
    // compared every role's cells element-wise against literals transcribed from the hand-written
    // arrays the role migration deleted. That half is retired.
    //
    // It did its job: it proved the migration was faithful, at all four facings, at the moment it
    // happened. What it cannot do is survive the furnace being redrawn - and the cold furnace and the
    // cupola were both redrawn deliberately (taps onto the hearth course, the slag runout, a shorter
    // cupola shaft). Re-pointing those literals at the new drawing would have made the test assert
    // that the layout equals itself, which is not a test; keeping them would have frozen the shape of
    // a furnace still being designed.
    //
    // What survives is the half that was never about the migration: that each accessor is wired to
    // its own role. `Before()` and the per-role literals went with the retired half.
    BlockEntityFurnaceCore core = Shaft(furnace, side);

    // The furnace's own accessor is that role, not something beside it. Reference identity rather
    // than equal contents: on a shipped layout the pool happens to be a subset of the burden column, so a
    // PoolCells that answered Chargeable instead would still be a plausible set of cells.
    Assert.Same(core.CellsWithRole(CellRole.Pool), core.PoolCells);

    // The two point accessors read the same roles. `Single()` is legal here only because the layout build
    // refuses a second cell - see Each_tap_is_exactly_one_cell_and_the_build_refuses_a_second.
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
  )
  {
    // This test does not catch A wrong-way rotation, and an earlier version of this comment claimed it
    // did. Ownership of a role cell is a structural tautology: CellsWithRole emits
    // `Pos.AddCopy(transformed[i])` drawn from MultiblockStructure.TransformedOffsets, and OwnsCell scans
    // that same list for `Pos + o == cell`. No wrong role, wrong index or reversed rotation can make it
    // false. Verified by mutation: scrambling the authored->transformed index mapping fails 61 iwex tests
    // and not one of them is this one.
    //
    // What actually catches a reversed rotation is the element-wise
    // `Assert.Equal(ExpectedAt(...), Render(...))` comparisons above, which recompute each cell through
    // ExOrientation.RotateOffset and check it against vanilla's own InitForUse table. Do not delete
    // those as "redundant with the ownership invariant" - they are the only real coverage of the mapping,
    // and this test would go on passing without them.
    //
    // What ownership does pin, and the only reason it is still here: that OwnsCell discriminates (the
    // negative control below - without it `OwnsCell => true` passes all 2296 tests), and that it walks the
    // transformed footprint rather than the authored one (point it at Offsets instead and this fails at
    // three facings of four). Both are facts about OwnsCell, not about the role mapping.
    BlockEntityFurnaceCore core = Shaft(furnace, side);

    // The cell one step under the anchor. Every furnace layout starts at layer 0, so this is directly
    // below an owned cell and outside the footprint at every facing - the tightest negative control
    // available, and the one that makes the loop below a statement rather than a tautology twice over.
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
    )
    {
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
  )
  {
    // The independent oracle, and the only thing that would catch a Role() hung on the wrong glyph:
    // nothing in the layout DSL relates a role to the code its glyph carries, so `Role('#', Tuyere)` would
    // build cleanly and answer a set of plausible-looking cells. The tuyere codes are worn by exactly the
    // cells the drawing means, so the two routes have to agree - and they rotate differently
    // (CellsAccepting resolves each transformed offset's wanted code; CellsWithRole matches authored
    // offsets and reads the transformed one at that index), which is why all four facings are here.
    //
    // Since the tuyeres were pinned this says more than it used to. The union still has to be the role's
    // cells, but each code also has to answer its own cell: a drawing that used the north letter for both
    // inlets - so the south tuyere faced into the hearth - would pass the union and fail the per-code count.
    BlockEntityFurnaceCore core = Shaft(furnace, side);

    bool blownBothWalls = furnace != "cupola";
    AssetLocation northWall = Tuyere("n", side);
    AssetLocation southWall = Tuyere("s", side);

    IEnumerable<BlockPos> accepted = core.CellsAccepting(northWall);
    if (blownBothWalls)
      accepted = accepted.Concat(core.CellsAccepting(southWall));

    Assert.NotEmpty(core.CellsWithRole(CellRole.Tuyere));
    Assert.Equal(
      Render(accepted),
      Render(core.CellsWithRole(CellRole.Tuyere))
    );

    Assert.Single(core.CellsAccepting(northWall));
    Assert.Equal(blownBothWalls ? 1 : 0, core.CellsAccepting(southWall).Count);
  }

  [Fact]
  public void The_cupola_takes_one_tuyere_and_the_blast_furnace_two()
  {
    // The counts, stated once and named - the cupola is the narrow furnace. A role copied wholesale from
    // the blast furnace onto the cupola's drawing agrees with the tuyere glyph and is still wrong.
    Assert.Single(RoleCellsOf(CupolaDef(), CellRole.Tuyere));
    Assert.Equal(2, RoleCellsOf(ColdDef(), CellRole.Tuyere).Count);
  }

  [Fact]
  public void The_furnace_draws_its_blast_through_the_cells_the_role_names()
  {
    // The production wiring, not just the accessor: ScanForOutlets fills the field the lit tick walks,
    // and it now fills it from the role. Without this the migration could be correct in CellsWithRole and
    // never reach the tick that consumes it.
    foreach (string furnace in new[] { "cold", "cupola" })
    {
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
  )
  {
    // This used to be half an oracle. Both taps were `iwex:furnace-tap-*` - one block placed twice -
    // so CellsAccepting could only be played off the union of the two roles: it caught `Role('#',
    // MetalTap)`, a role hung on brick, but could not see the two being swapped. Typing the taps closed
    // that half: each role is now pinned to its own code, so a drawing that puts the iron notch on the
    // cinder notch's cell fails right here rather than being caught downstream by "slag floats" - if at
    // all.
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

    // And the facing is genuinely part of the demand: the other tap's facing is refused in the same
    // cell. Without this the two asserts above would still pass on a layout that wildcarded the
    // facing, which is what it did before the redraw.
    Assert.Empty(core.CellsAccepting(SlagTap(furnace, side)).Intersect(
      core.CellsWithRole(CellRole.MetalTap)));
  }

  [Theory]
  [InlineData("cold")]
  [InlineData("cupola")]
  public void The_two_taps_are_distinct_cells_on_opposite_walls(string furnace)
  {
    // The statement the code cross-check above cannot make: that the drawing puts the two drains in
    // genuinely different places, so swapping the two Role() lines is detectable here.
    //
    // This asserted `slag.Y == metal.Y + 1` - "slag floats, so it is skimmed off above the metal
    // drain" - until the furnace redraw brought both taps down onto the hearth course. They are now
    // level, on opposite walls. That is the drawing's call, not this test's, so the test follows it.
    //
    // But the metallurgy the old assertion encoded has not gone away: on a real furnace the cinder
    // notch sits above the iron notch precisely because slag floats on the bath, and draining both
    // from the same depth is not how a blast furnace separates them. Nothing in the sim models that
    // yet - DrainProducts pulls each pool independently of tap height - so the drawing is currently
    // free to do this. If pool depth ever starts mattering, this is the first test to revisit.
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
  public void Each_tap_is_exactly_one_cell_and_the_build_refuses_a_second()
  {
    // The [SingleCell] arity guard, added for this migration - a furnace's
    // drain is a point, so the consumer reads `Single()`, and without a build-time promise behind it that
    // is an exception waiting for the first layout that draws the glyph twice. Stated here on the real tap
    // glyph and the real block code rather than only on exlib's synthetic fixture.
    foreach (ExBlockDef def in new[] { ColdDef(), CupolaDef() })
    {
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
  public void Two_glyphs_cannot_split_one_drain_between_them()
  {
    // The other route into the same guard, and the one a layout author is likelier to take: not one glyph
    // drawn twice but two glyphs sharing a single-cell role. Counted over drawn cells, so both fail.
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
  public void The_open_topped_furnaces_mark_no_outlet_and_have_none_to_mark()
  {
    // "Empty" has to be empty for the right reason. A role that answers nothing and a furnace that never
    // declared the role are indistinguishable at the accessor, so both are stated: the emitted attribute
    // carries no GasOutlet key at all, and there is no pipe-outlet cell in the drawing for one to point at.
    // Before the migration this was a C# override reading `=> []`, which said neither.
    foreach (ExBlockDef def in new[] { ColdDef(), CupolaDef() })
    {
      Assert.DoesNotContain(CellRole.GasOutlet.ToString(), RoleNamesOf(def));
      Assert.DoesNotContain(OutletGlyph, LayoutOf(def).Values);
    }

    foreach (string furnace in new[] { "cold", "cupola" })
    {
      BlockEntityFurnaceCore core = Shaft(furnace, "north");
      Assert.Empty(core.CellsWithRole(CellRole.GasOutlet));
      Assert.Empty(
        (IReadOnlyList<BlockPos>)
          ReflectionHelpers.GetField(core, "_gasOutlets")!
      );
      // ...and the same footprint answers something for the roles it does mark, so the empty above is a
      // statement about outlets rather than about a layout that failed to load.
      Assert.NotEmpty(core.CellsWithRole(CellRole.Tuyere));
    }
  }

  [Fact]
  public void No_iwex_furnace_declares_a_gas_outlet_because_none_of_them_vents()
  {
    // The whole-domain version: venting is the hot blast furnace's, and it lives in smex. Stated so a
    // future iwex layout that grows a pipe outlet has to add the role deliberately.
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
  )
  {
    // This is what stands in for the code cross-check the other two roles get. The Pool glyph points at
    // the same code as the shaft glyph - deliberately, because a crucible cell is burden and pool at once -
    // so `CellsAccepting` cannot separate them and no block code can. What can be stated independently is
    // the relation: the pool is exactly the charge volume's bottom course. That is the assertion
    // FurnaceLayoutRig.AssertShaftMatchesLayout used to make about the deleted SolidifyCells array, moved
    // onto the role rather than dropped with it.
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
  public void The_crucible_is_burden_and_pool_at_once(string furnace)
  {
    // The overlap is the fact, not a bug: charge rests on the crucible while the furnace runs and metal
    // freezes onto it when the furnace dies, and those are the same cells today. It is also the reason the
    // layout DSL had to let one glyph carry two roles - a cell holds one glyph, so the two sets cannot be
    // split across two of them. When the layered-charge layout change makes the crucible pool-only, this
    // test is the one that has to change, and it says so by existing.
    BlockEntityFurnaceCore core = Shaft(furnace, "north");

    Assert.NotEmpty(core.PoolCells);
    Assert.All(core.PoolCells, c => Assert.Contains(c, core.ChargeableCells));
    Assert.True(core.PoolCells.Count < core.ChargeableCells.Count);
  }

  #endregion

  #region The reverberatory hearths

  [Fact]
  public void A_hearth_marks_no_crucible_and_the_old_declaration_pointed_at_brick()
  {
    // The one place the migration deliberately does not preserve the pre-migration value, and it is a
    // fix rather than a regression. Neither hearth overrode SolidifyCells, so both inherited the blast
    // furnace's [(0,1,0), (1,1,0)] - and on their own 8-wide drawings (0,1,0) is the air cell beside the
    // core and (1,1,0) is solid refractory brick. It was never noticed because a hearth pools no metal:
    // SolidProductBlock is null and DrainedMetalUnits is 0 on the firebox branch, so the freeze returns
    // before it reads a single cell. Asking the drawing answers nothing, which is the truth.
    foreach (BlockEntityFireboxFurnace hearth in Hearths())
    {
      Assert.Empty(hearth.PoolCells);
      Assert.Empty(hearth.CellsWithRole(CellRole.Tuyere));
      Assert.Empty(hearth.CellsWithRole(CellRole.GasOutlet));
      // ...and the drawing did load: the role it does mark answers.
      Assert.NotEmpty(hearth.CellsWithRole(CellRole.Firebox));
    }

    // The old declaration, re-derived: one air cell and one brick. Stated so the paragraph above is
    // evidence rather than assertion.
    foreach (ExBlockDef def in new[] { PuddlingDef(), HeatingDef() })
    {
      Dictionary<Vec3i, string> layout = LayoutOf(def);
      Assert.Equal("game:air", At(layout, new Vec3i(0, 1, 0)));
      Assert.Equal(
        "game:refractorybricks-good-tier*",
        At(layout, new Vec3i(1, 1, 0))
      );
    }
  }

  [Fact]
  public void A_hearth_has_no_drain_and_both_old_declarations_missed_the_drawing()
  {
    // The second place the migration deliberately does not preserve the pre-migration value, and the
    // worst of the two. A reverberatory hearth pours nothing - puddled iron leaves as pasty balls through
    // the charge door and the cinder is raked out - so it has no tap of either kind, and its drawing has
    // never contained a tap glyph. Yet both hearths inherited MetalTapCell = (2,1,0) from the blast
    // furnace, and the puddling furnace overrode SlagTapCell to (-3,1,1) with a doc-comment claiming a
    // layout `T` that does not exist. Asking the drawing answers null, which is the truth.
    foreach (BlockEntityFireboxFurnace hearth in Hearths())
    {
      Assert.Empty(hearth.CellsWithRole(CellRole.MetalTap));
      Assert.Empty(hearth.CellsWithRole(CellRole.SlagTap));
      Assert.Null(hearth.MetalTapPos);
      Assert.Null(hearth.SlagTapPos);
      // ...and the drawing did load, so the nulls above are an absence rather than a failure to read.
      Assert.NotEmpty(hearth.CellsWithRole(CellRole.Firebox));
    }

    // The old declarations, re-derived from the drawings rather than asserted. Both hearths are 8 columns
    // wide with Origin(-6,-1), so a local X of 2 is grid column 8 - one past the east edge, a cell that is
    // not in the layout at all. That is why `At` is not used here: there is nothing to look up.
    foreach (ExBlockDef def in new[] { PuddlingDef(), HeatingDef() })
    {
      Dictionary<Vec3i, string> layout = LayoutOf(def);
      Assert.DoesNotContain(new Vec3i(2, 1, 0), layout.Keys);
      // ...and no cell of either drawing is a tap of either kind, by code, so there was nowhere for the
      // role to go.
      Assert.DoesNotContain(IronTapGlyph, layout.Values);
      Assert.DoesNotContain(SlagTapGlyph, layout.Values);
    }

    // The puddling furnace's own override pointed at one of the fire-brick slab shoulders round its
    // doorway - inside the footprint, so ownership would never have caught it, but not a tap.
    Assert.Equal(
      "game:brickslabs-fire-south-free",
      At(LayoutOf(PuddlingDef()), new Vec3i(-3, 1, 1))
    );
    // The heating furnace never overrode it, so it inherited (-2,2,0): solid refractory brick.
    Assert.Equal(
      "game:refractorybricks-good-tier*",
      At(LayoutOf(HeatingDef()), new Vec3i(-2, 2, 0))
    );
  }

  [Fact]
  public void A_hearth_declares_none_of_the_five_roles_at_all()
  {
    // The authoring half of the two cases above: empty because nothing was marked, not because the reader
    // dropped something. Read off the emitted attribute rather than through the production reader, so a
    // role the builder failed to emit cannot pass by the reader inventing it.
    //
    // The set is Firebox + Flue, and the five the name refers to are the shaft-furnace roles this test
    // is about - Tuyere, GasOutlet, MetalTap, SlagTap, Pool - none of which a hearth has any business
    // declaring.
    //
    // Flue marks the centre of the chimney column, not a fixed stack. The hearth chimneys are meant to
    // be built to whatever height the player wants, which is why the drawn stack is short: the layout
    // states the column's axis and the required minimum, and the rest is the player's. That is also why
    // the role has no consumer in iwex yet - the dynamic-height reader is not written. Until it is, this
    // exact-set assertion is the only thing standing behind the marks, so do not relax it to a subset.
    foreach (ExBlockDef def in new[] { PuddlingDef(), HeatingDef() })
    {
      List<string> names = RoleNamesOf(def);
      Assert.Equal(["Firebox", "Flue"], names);
      foreach (CellRole role in Migrated)
        Assert.DoesNotContain(role.ToString(), names);

      // ...and the marks are not empty. A role name emitted over no cells would satisfy the list above
      // while answering nothing, which is the vacuity every other role loop here guards against.
      Assert.NotEmpty(RoleCellsOf(def, CellRole.Flue));
    }
  }

  [Fact]
  public void A_hearth_draws_no_blast_and_vents_through_nothing_plumbed()
  {
    // The branch invariant that used to be two `=> []` overrides on BlockEntityFireboxFurnace. Nothing
    // replaced them, so this is the only place it is stated against the fields the lit tick walks rather
    // than against the accessor.
    //
    // ScanForOutlets is driven here, and that is the whole point of the test. It runs from
    // OnStructureCompleted and from two lit-tick paths, none of which fires for a raised-but-incomplete
    // hearth - so without this call the two fields are still their `[]` field initialisers and the
    // assertions below would be reading "never assigned" while claiming to read "the role answered
    // nothing". They passed for that wrong reason once. Completing the hearths is not the
    // fix: their footprints need blocks the iwex test host does not register (see Hearths()).
    foreach (BlockEntityFireboxFurnace hearth in Hearths())
    {
      ReflectionHelpers.Invoke(hearth, "ScanForOutlets");

      Assert.Empty(
        (IReadOnlyList<BlockPos>)ReflectionHelpers.GetField(hearth, "_tuyeres")!
      );
      Assert.Empty(
        (IReadOnlyList<BlockPos>)
          ReflectionHelpers.GetField(hearth, "_gasOutlets")!
      );
    }

    // ...and the drive is not itself a no-op. A ScanForOutlets that silently stopped reading the roles
    // would leave the hearth assertions above passing for a brand-new wrong reason; the same reflective
    // call on a furnace that does mark tuyeres has to fill the field.
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
  public void The_four_facings_are_four_different_tuyere_and_tap_footprints()
  {
    // The guard a per-facing theory cannot state: a mapping that ignored the angle would agree with itself
    // everywhere, and every one of the theories above would pass.
    //
    // Which furnace can state it differs per role, and picking the wrong one makes the test vacuous.
    // The cupola's single tuyere at (0,1,-1) is off-axis, so it names four distinct cells; the cold
    // furnace's tuyere pair is a mirror image of itself and answers only two distinct sets.
    Assert.Equal(
      4,
      Sides
        .Select(s => Render(Cupola(s).CellsWithRole(CellRole.Tuyere)))
        .Distinct()
        .Count()
    );
    // Two, not four, and this line used to say four. The cold furnace's crucible was the pair
    // (0,1,0)/(1,1,0) - off-centre on x, so it named four distinct world cells. The furnace redraw
    // ran it the full width of the hearth course, (-1,1,0)..(1,1,0), which centres it: a half turn maps
    // the row onto itself, so north/south and east/west each collapse to one footprint. It still moves
    // (a quarter turn lays the row along z), which is what keeps this non-vacuous - a mapping that
    // ignored the angle would answer one - but the crucible can no longer carry the four-way statement,
    // and no shipped furnace's can: the cupola's is the single on-axis cell (0,1,0). The taps below
    // carry it instead. Do not "restore" this to four; the drawing is what changed.
    Assert.Equal(
      2,
      Sides.Select(s => Render(Cold(s).PoolCells)).Distinct().Count()
    );
    Assert.NotEqual(
      Render(Cold("north").PoolCells),
      Render(Cold("east").PoolCells)
    );
    // The taps are now the only four-way case, and the cleanest of the three: each is a single off-axis
    // cell, so neither is its own mirror image and both name four distinct world cells. That is why a
    // reversed rotation - the near-miss that survives 0 and 180, survives a "four distinct footprints"
    // guard on a mirror-symmetric pair, and survives ownership - cannot hide in either of them.
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
  public void The_cupolas_one_cell_crucible_is_its_own_image_at_every_facing()
  {
    // The degenerate case, named so nobody "strengthens" the test above into something that cannot hold.
    // (0,1,0) is on the axis of rotation, so the cupola's crucible is genuinely the same world cell all
    // four ways round - and it must still be the cell directly over the anchor, which is what pins that
    // this is invariance rather than a set that stopped being computed.
    foreach (string side in Sides)
      Assert.Equal(
        Render([Anchor.AddCopy(0, 1, 0)]),
        Render(Cupola(side).PoolCells)
      );
  }

  #endregion
  #region The shaft box, derived from the fuel roles

  // The eighth and last hand-declared cell member. `ShaftMin`/`ShaftMax` were two structure-local corners
  // with four overrides between the cupola and the two hearths; they are now the bounding box of the cells
  // the drawing marks Chargeable (a shaft) or Firebox (a hearth), and nothing declares them anywhere.
  //
  // A bounding box is a weaker signal than a cell set - two different sets share one box - so these lean
  // on the pre-deletion literals and on asymmetric shapes rather than on the box alone.

  /// <summary>
  /// The corners each deleted member held, transcribed before the deletion. Only the first two are
  /// recoverable from git: <c>BlockEntityPuddlingFurnace</c> and <c>BlockEntityHeatingFurnace</c> are
  /// untracked files created earlier in this same phase, so their <c>_fireboxMin</c>/<c>_fireboxMax</c>
  /// pairs come from the working tree instead.
  /// <list type="bullet">
  /// <item><c>BlockEntityFurnaceCore</c> - <c>(-1,1,-1)</c>..<c>(1,5,1)</c>. Inherited by the cold blast
  /// furnace and by smex's hot one.</item>
  /// <item><c>BlockEntityCupolaFurnace</c> - <c>(0,1,0)</c>..<c>(0,5,0)</c>, one column.</item>
  /// <item><c>BlockEntityPuddlingFurnace</c> - <c>(-5,1,0)</c>..<c>(-5,1,0)</c>, one firebox cell.</item>
  /// <item><c>BlockEntityHeatingFurnace</c> - <c>(-5,1,-1)</c>..<c>(-5,1,0)</c>, two.</item>
  /// </list>
  /// <para>
  /// <b>The heating hearth's pair was transcribed against a broken origin.</b> Its layout carried
  /// <c>Origin(-6,-1)</c> while its core glyph sat at col 6 / <b>row 2</b> - the puddling furnace's origin
  /// copied onto a drawing one row deeper - so every cell it reported, these corners included, was one
  /// north of where the furnace actually builds. Corrected along with the origin; the
  /// <em>shape</em> of the box (two cells, adjacent on z) is unchanged, which is what these literals are
  /// really pinning.
  /// </para>
  /// </summary>
  private static (Vec3i Min, Vec3i Max) BoxBefore(string furnace) =>
    furnace switch
    {
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
    furnace switch
    {
      "cold" => Cold(side),
      "cupola" => Cupola(side),
      "puddling" or "heating" => Hearth(furnace, side),
      _ => throw new KeyNotFoundException(furnace),
    };

  /// <summary>One hearth at a chosen facing - <see cref="Hearths"/> raises both at north only.</summary>
  private static BlockEntityFireboxFurnace Hearth(string furnace, string side)
  {
    if (furnace == "puddling")
    {
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
  )
  {
    // The migration proof for the box. The corners are structure-local, so the answer must be the same
    // at all four facings - which is itself half of what is being stated: a derivation that leaked the
    // placed rotation into a local fact would be right at north and wrong at 90 and 270.
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
  )
  {
    // ShaftBounds() is the one place the box meets rotation, and its re-sort is load-bearing: rotating a
    // corner pair can swap either horizontal axis, so "whatever the low corner rotated into" is not a
    // minimum. The expectation is built here by rotating both corners through ExOrientation and taking a
    // component-wise min/max - the same statement, by a route that shares no code with the method.
    var (localMin, localMax) = BoxBefore(furnace);

    foreach (string side in Sides)
    {
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

      // ...and it is never inverted on any axis, which is what a box walk needs and what the re-sort is
      // there for.
      //
      // The criterion is that the two corners differ on x or on z - not that the box is asymmetric on
      // both, which is what this comment used to claim. A quarter turn maps a z difference onto x, so one
      // difference is enough. The cold furnace differs on both; the heating hearth's box is degenerate on
      // x (both corners x=-5) and differs only on z, and still inverts - on z at 180, on x at 270. The
      // cupola (one column) and the puddling hearth (one cell) cannot witness it at all: two equal corners
      // have nothing to swap, so for them a dropped re-sort is merely a relabelling.
      //
      // A third shipped furnace could witness it and long did not: smex's hot blast furnace, whose box
      // is (-1,1,-1)..(1,5,1) - the cold furnace's exactly. It was unwitnessed not because it is degenerate
      // but because smex only ever read the facing-invariant local box; see FurnaceLayoutRig.WorldBoxOf.
      Assert.True(
        min.X <= max.X && min.Y <= max.Y && min.Z <= max.Z,
        $"{furnace} at {side}: the world box is inverted - ({min.X},{min.Y},{min.Z}) is not below "
          + $"({max.X},{max.Y},{max.Z})"
      );
    }
  }

  [Fact]
  public void The_box_is_the_fuel_roles_own_bounds_and_not_a_role_beside_them()
  {
    // A bounding box is a weak signal on its own - two different cell sets share one - so this states
    // the derivation rather than only its result: the box is the bounds of Chargeable/Firebox, and it is
    // not the bounds of any other role the same drawing carries. All of the cold furnace's other roles
    // have strictly smaller bounds, so a derivation that read one of them answers a different box.
    BlockEntityBlastFurnaceCold cold = Cold("north");
    Assert.Equal(AuthoredFuelBox(ColdDef()), ShaftBoxOf(cold));

    foreach (CellRole other in Migrated)
      Assert.NotEqual(
        $"{other}: {ShaftBoxOf(cold)}",
        $"{other}: {LocalBoundsOf(ColdDef(), other)}"
      );

    // The hearths are the other half of the union, and the sharper one: their drawings mark no Chargeable
    // cell at all, so a Chargeable-only derivation gives them no box - which is a machine that collects no
    // fuel and never lights, not a machine with a slightly wrong box.
    foreach (BlockEntityFireboxFurnace hearth in Hearths())
    {
      Assert.Empty(hearth.CellsWithRole(CellRole.Chargeable));
      Assert.NotEmpty(hearth.CellsWithRole(CellRole.Firebox));
      Assert.NotNull(ShaftBoxOf(hearth));
    }
  }

  /// <summary>The bounding box of one role's authored cells, read off the emitted attribute. Null when the
  /// drawing marks none.</summary>
  private static (Vec3i, Vec3i)? LocalBoundsOf(ExBlockDef def, CellRole role)
  {
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
  /// A shaft furnace drawn without a single fuel role - the "what is the bounding box of nothing" case.
  /// The legend is the real shaft glyph, so the cells are genuinely chargeable-looking; what is missing is
  /// the <c>Role</c> call.
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
  public void A_drawing_that_marks_no_fuel_cell_has_no_box_and_nothing_throws()
  {
    // There is no pair of corners that means "empty": ShaftBounds() and EnsureShaftColumns both re-sort
    // per axis, so any "impossible" corner pair they were handed would be normalised straight back into a
    // real box - a box at the origin, which is a furnace quietly collecting fuel from a cell it does not
    // own. So the emptiness is carried above the corners, exactly as the [SingleCell] accessors carry
    // theirs, and every consumer answers nothing.
    var core = new BlockEntityBlastFurnaceCold();
    Stand(core, NoFuelRoleDef(), Anchor, "iwex:furnace-blastcore-tier1", "north");

    Assert.Null(ShaftBoxOf(core));
    Assert.Null(WorldBoxOf(core));
    Assert.Empty(core.ShaftColumns);
    Assert.Equal(0, core.ShaftChargeUnits);
    Assert.Null(core.ChargeColumnAt(0, 0));
    Assert.Null(core.ChargeColumnAt(Anchor.AddCopy(0, 1, 0), out int index));
    Assert.Equal(-1, index);

    // ...and the save path survives it. EnsureShaftColumns is reached from FromTreeAttributes, which the
    // engine calls before Initialize; it now legitimately builds nothing, and both halves of the round
    // trip used to dereference `_shaftColumns!` unconditionally.
    var tree = new TreeAttribute();
    core.ToTreeAttributes(tree);
    Assert.DoesNotContain(tree.Keys, k => k.StartsWith("chargeCol"));
    core.FromTreeAttributes(tree, core.Api.World);
    Assert.Empty(core.ShaftColumns);
  }

  [Fact]
  public void A_box_derived_before_the_layout_arrives_is_never_memoised()
  {
    // The failure this guards is silent and permanent: EnsureShaftColumns runs from FromTreeAttributes,
    // and a furnace that cached "no box" there would own no columns for the rest of its life and come back
    // from every save empty. Vanilla assigns Block in CreateBehaviors immediately before
    // FromTreeAttributes on every load path, so this cannot happen in game - but the cache must not be the
    // thing that depends on that.
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
