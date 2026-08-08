using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="BlockEntityMultiblockStructure.CellsAccepting"/> - "which cells of my own footprint would
/// take this block?", asked of the layout rather than of a second list somebody wrote out beside it.
/// <para>
/// The footprint here is deliberately <b>chiral</b>: an L of charge cells sitting off to one side, which
/// is congruent to none of its own rotations. Every mega-block shipped in this repo has a square or
/// single-cell charge volume, and a square set is closed under 90 deg rotation - so "the same cells at all
/// four facings" holds just as well for a mapping that turns the wrong way, or not at all. This repo has
/// shipped that bug twice. The L is what makes the rotation assertions bite.
/// </para>
/// <para>
/// The other half of what is pinned here is the <b>discrimination</b>: the same footprint answers a
/// different set for <c>game:coalpile</c> than for <c>iwex:furnace-chargepile</c>, because one of its slots is a
/// domainless <c>@(air|coalpile)</c> fuel slot that vanilla's matcher can never let a modded block into.
/// That is not an edge case - it is exactly how a reverberatory firebox differs from a shaft.
/// </para>
/// </summary>
public class MultiblockCellsAcceptingTests
{
  #region Fixture

  private static readonly BlockPos Anchor = new(0, 10, 0);

  /// <summary>The domain-wildcarded shaft glyph the furnaces ship. <c>*:</c> is load-bearing: vanilla's
  /// matcher compares domain and path separately, so a bare alternation is implicitly <c>game:</c>.</summary>
  private const string ShaftGlyph = "*:@(air|coalpile|furnace-chargepile)";

  /// <summary>The domainless fuel glyph a firebox layout ships - the one that cannot admit a modded block
  /// however the alternation is written.</summary>
  private const string FireboxGlyph = "@(air|coalpile)";

  private static readonly AssetLocation ChargePile = new("iwex:furnace-chargepile");
  private static readonly AssetLocation CoalPile = new("game:coalpile");
  private static readonly AssetLocation Unrelated = new("iwex:furnace-tuyere-n");

  /// <summary>
  /// The chiral charge volume - see the class remarks. Three cells make an L in <c>(x, z)</c> and the
  /// fourth sits a level up, so a rotation that leaked into <c>y</c> shows as well.
  /// </summary>
  private static readonly Vec3i[] Chargeable =
  [
    new(2, 0, 0),
    new(2, 0, 1),
    new(3, 0, 0),
    new(2, 1, 0),
  ];

  /// <summary>The anchor, one brick, one firebox fuel slot, and the chiral charge volume. The brick and
  /// the fuel slot are what keep the walk from being "every cell of the footprint".</summary>
  private static ExBlockDef Def() =>
    ExBlockDef
      .Create("exlib", "testmega")
      .Multiblock(m =>
      {
        m.Number("exlib:testmega*", 1)
          .Number("exlib:testbrick*", 2)
          .Number(ShaftGlyph, 3)
          .Number(FireboxGlyph, 4)
          .At(0, 0, 0, 1)
          .At(1, 0, 0, 2)
          .At(-1, 0, 0, 4);
        foreach (Vec3i cell in Chargeable)
          m.At(cell.X, cell.Y, cell.Z, 3);
      });

  /// <summary>A layout with one <b>oriented</b> part in it. Authored through the layout builder because
  /// only that emits the <c>multiblockFacings</c> table the rotation reads.</summary>
  private static ExBlockDef OrientedDef() =>
    ExBlockDef
      .Create("exlib", "testmega")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "exlib:testmega*")
          .Legend('D', "exlib:testdoor-n")
          .Layer(
            0,
            """
            C D
            """
          )
      );

  private static (TestWorld world, TestMegablock machine) Stand(int angle = 0)
  {
    var world = new TestWorld();
    var machine = new TestMegablock { Angle = angle };
    world.Place(
      Anchor,
      TestBlocks.Configure(new Block(), "exlib:testmega-n", 1),
      machine
    );
    world.Attach(machine);
    return (world, machine);
  }

  /// <summary>Where <see cref="Chargeable"/> lands in the world at <paramref name="angle"/>, computed
  /// through the shared rotation math rather than through the machine - the machine's own answer comes off
  /// vanilla's <c>InitForUse</c>, so these are two independent routes to the same cells.</summary>
  private static string ExpectedAt(int angle) =>
    Render(
      Chargeable.Select(c =>
      {
        Vec3i r = ExOrientation.RotateOffset(c, angle);
        return Anchor.AddCopy(r.X, r.Y, r.Z);
      })
    );

  /// <summary>Cells as one ordered, printable string, so a failure names the whole set rather than a
  /// count.</summary>
  private static string Render(IEnumerable<BlockPos> cells) =>
    string.Join(
      ", ",
      cells
        .Select(p => $"({p.X},{p.Y},{p.Z})")
        .OrderBy(s => s, System.StringComparer.Ordinal)
    );

  #endregion

  #region Which cells come back

  [Fact]
  public void Only_the_slots_whose_glyph_admits_the_code_come_back()
  {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, Def()).Complete();

    // Not the anchor, not the brick, not the firebox fuel slot - and all four charge cells.
    Assert.Equal(ExpectedAt(0), Render(machine.CellsAccepting(ChargePile)));
  }

  [Fact]
  public void The_same_footprint_answers_a_different_set_for_a_different_block()
  {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, Def()).Complete();

    // The discrimination, and the whole reason this is a question about a block rather than a match
    // against a legend string. `@(air|coalpile)` is implicitly `game:`, so the firebox slot takes a coal
    // pile and can never take `iwex:furnace-chargepile`; the shaft glyph takes both. One extra cell, and it is
    // exactly the cell a hearth's fire sits in.
    Assert.Equal(
      Chargeable.Length + 1,
      machine.CellsAccepting(CoalPile).Count
    );
    Assert.Equal(Chargeable.Length, machine.CellsAccepting(ChargePile).Count);
    Assert.Contains(
      Anchor.AddCopy(-1, 0, 0),
      machine.CellsAccepting(CoalPile)
    );
    Assert.DoesNotContain(
      Anchor.AddCopy(-1, 0, 0),
      machine.CellsAccepting(ChargePile)
    );
  }

  [Fact]
  public void A_code_no_slot_admits_comes_back_empty()
  {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, Def()).Complete();

    // Empty is a real answer, not a failure - and the footprint is demonstrably non-empty beside it, so
    // this cannot pass by the walk having nothing to walk.
    Assert.Empty(machine.CellsAccepting(Unrelated));
    Assert.NotEmpty(machine.CellsAccepting(ChargePile));
  }

  #endregion

  #region Rotation

  [Theory]
  [InlineData(0)]
  [InlineData(90)]
  [InlineData(180)]
  [InlineData(270)]
  public void Every_facing_puts_the_cells_where_that_rotation_says(int angle)
  {
    var (world, machine) = Stand(angle);
    StructureRig.Around(world, machine, Def(), angle).Complete();

    // On a square charge volume this passes for a mapping that turns the wrong way; on the L it does
    // not, because turning -90 instead of +90 lands the whole set in the opposite quadrant.
    Assert.Equal(ExpectedAt(angle), Render(machine.CellsAccepting(ChargePile)));
  }

  [Fact]
  public void The_four_facings_are_four_different_footprints()
  {
    var sets = new List<string>();
    foreach (int angle in new[] { 0, 90, 180, 270 })
    {
      var (world, machine) = Stand(angle);
      StructureRig.Around(world, machine, Def(), angle).Complete();
      sets.Add(Render(machine.CellsAccepting(ChargePile)));
    }

    // The guard the per-facing theory cannot state: a mapping that ignored the angle entirely would
    // satisfy every one of those four cases against ExpectedAt only if ExpectedAt were also broken, but
    // it would trivially satisfy "each facing agrees with itself". Four distinct sets is the fact.
    Assert.Equal(4, sets.Distinct().Count());
  }

  [Fact]
  public void Turning_the_structure_moves_the_cells_rather_than_answering_out_of_the_old_facing()
  {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, Def()).Complete();
    Assert.Equal(ExpectedAt(0), Render(machine.CellsAccepting(ChargePile)));

    // A wrench turn: the machine re-derives its angle and reloads its structure on the next monitor tick.
    // The cells are cached in world space, so a cache that survived that would keep answering north.
    machine.Angle = 90;
    world.AdvanceBlockEntityTime(3000);

    Assert.Equal(ExpectedAt(90), Render(machine.CellsAccepting(ChargePile)));
  }

  #endregion

  #region Caching

  [Fact]
  public void The_answer_is_computed_once_and_handed_back()
  {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, Def()).Complete();

    // Phase 3 reads this every time a column's height changes, so it must not re-walk 157 offsets per
    // read. Same instance, not merely equal contents.
    Assert.Same(
      machine.CellsAccepting(ChargePile),
      machine.CellsAccepting(ChargePile)
    );
  }

  [Fact]
  public void A_structure_asked_before_its_layout_arrives_answers_properly_afterwards()
  {
    var (world, machine) = Stand();

    // The block carries no attributes yet - the state a client-side block entity is in until the layout
    // loads, and the read a functional component can genuinely make in that window. Empty is right.
    Assert.Empty(machine.CellsAccepting(ChargePile));

    StructureRig.Around(world, machine, Def()).Complete();

    // ...and the early read did not poison anything: the late-arriving layout is answered in full.
    // What this does not pin is the "never memoise an empty" branch in CellsAccepting. Mutation says
    // so: writing that empty into the cache fails no test here, because EnsureStructureLoaded re-enters
    // the reload path while the layout is missing and that path drops the cache anyway. The branch is
    // deliberate belt-and-braces against the invalidation moving; this case pins the behaviour it
    // protects, not the branch.
    Assert.Equal(ExpectedAt(0), Render(machine.CellsAccepting(ChargePile)));
  }

  #endregion

  #region Oriented slots

  [Theory]
  [InlineData(0, "n")]
  [InlineData(90, "w")]
  [InlineData(180, "s")]
  [InlineData(270, "e")]
  public void An_oriented_slot_admits_the_variant_the_placed_structure_wants(
    int angle,
    string facing
  )
  {
    var (world, machine) = Stand(angle);
    StructureRig.Around(world, machine, OrientedDef(), angle).Complete();

    // The layout authors a north door; a structure turned a quarter turn wants a west one. Going through
    // the same WantedCodeAt the completion walk uses is what makes this hold - a walk that read the raw
    // blockNumbers code would answer "north" at every facing, and Phase 3 would place its blocks into
    // slots the furnace then reports missing.
    Assert.Single(
      machine.CellsAccepting(new AssetLocation($"exlib:testdoor-{facing}"))
    );
    if (angle != 0)
      Assert.Empty(
        machine.CellsAccepting(new AssetLocation("exlib:testdoor-n"))
      );
  }

  #endregion
}
