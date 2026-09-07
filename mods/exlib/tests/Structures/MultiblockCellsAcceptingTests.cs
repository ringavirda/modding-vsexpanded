using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Covers <see cref="BlockEntityMultiblockStructure.CellsAccepting"/>: which footprint cells admit a given
/// block code, answered from the layout. The fixture footprint is chiral (an L of charge cells) so the
/// rotation assertions bite - a square charge volume is closed under 90 deg rotation and would pass for a
/// mapping that turns the wrong way as well. Its domainless <c>@(air|coalpile)</c> fuel slot, which
/// vanilla's matcher never admits a modded block into, is what makes the answer differ per block code.
/// </summary>
public class MultiblockCellsAcceptingTests {
  #region Fixture

  private static readonly BlockPos Anchor = new(0, 10, 0);

  /// <summary>The domain-wildcarded shaft glyph the furnaces ship. The <c>*:</c> is required: vanilla's
  /// matcher compares domain and path separately, so a bare alternation is implicitly <c>game:</c>.</summary>
  private const string ShaftGlyph = "*:@(air|coalpile|furnace-chargepile)";

  /// <summary>The domainless fuel glyph a firebox layout ships. It cannot admit a modded block however the
  /// alternation is written.</summary>
  private const string FireboxGlyph = "@(air|coalpile)";

  private static readonly AssetLocation ChargePile = new(
    "iiex:furnace-chargepile"
  );
  private static readonly AssetLocation CoalPile = new("game:coalpile");
  private static readonly AssetLocation Unrelated = new(
    "iiex:furnace-tuyere-n"
  );

  /// <summary>
  /// The chiral charge volume. Three cells make an L in <c>(x, z)</c> and the fourth sits a level up, so a
  /// rotation that leaked into <c>y</c> shows as well.
  /// </summary>
  private static readonly Vec3i[] Chargeable =
  [
    new(2, 0, 0),
    new(2, 0, 1),
    new(3, 0, 0),
    new(2, 1, 0),
  ];

  /// <summary>The anchor, one brick, one firebox fuel slot, and the chiral charge volume. The brick and
  /// the fuel slot keep the walk from being every cell of the footprint.</summary>
  private static ExBlockDef Def() =>
    ExBlockDef
      .Create("exlib", "testmega")
      .Multiblock(m => {
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

  /// <summary>A layout with one oriented part in it. Authored through the layout builder because only the
  /// builder emits the <c>multiblockFacings</c> table the rotation reads.</summary>
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

  private static (TestWorld world, TestMegablock machine) Stand(int angle = 0) {
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
  /// through the shared rotation math rather than through the machine. The machine's own answer comes off
  /// vanilla's <c>InitForUse</c>, so the two are independent routes to the same cells.</summary>
  private static string ExpectedAt(int angle) =>
    Render(
      Chargeable.Select(c => {
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
  public void Only_the_slots_whose_glyph_admits_the_code_come_back() {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, Def()).Complete();

    // Not the anchor, not the brick, not the firebox fuel slot - and all four charge cells.
    Assert.Equal(ExpectedAt(0), Render(machine.CellsAccepting(ChargePile)));
  }

  [Fact]
  public void The_same_footprint_answers_a_different_set_for_a_different_block() {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, Def()).Complete();

    // `@(air|coalpile)` is implicitly `game:`, so the firebox slot takes a coal pile and never takes
    // `iiex:furnace-chargepile`; the shaft glyph takes both. One cell of difference.
    Assert.Equal(Chargeable.Length + 1, machine.CellsAccepting(CoalPile).Count);
    Assert.Equal(Chargeable.Length, machine.CellsAccepting(ChargePile).Count);
    Assert.Contains(Anchor.AddCopy(-1, 0, 0), machine.CellsAccepting(CoalPile));
    Assert.DoesNotContain(
      Anchor.AddCopy(-1, 0, 0),
      machine.CellsAccepting(ChargePile)
    );
  }

  [Fact]
  public void A_code_no_slot_admits_comes_back_empty() {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, Def()).Complete();

    // Empty is a valid answer; the non-empty assertion beside it rules out an empty footprint.
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
  public void Every_facing_puts_the_cells_where_that_rotation_says(int angle) {
    var (world, machine) = Stand(angle);
    StructureRig.Around(world, machine, Def(), angle).Complete();

    // On a square charge volume this passes for a mapping that turns the wrong way; on the L it does not,
    // because turning -90 instead of +90 lands the whole set in the opposite quadrant.
    Assert.Equal(ExpectedAt(angle), Render(machine.CellsAccepting(ChargePile)));
  }

  [Fact]
  public void The_four_facings_are_four_different_footprints() {
    var sets = new List<string>();
    foreach (int angle in new[] { 0, 90, 180, 270 }) {
      var (world, machine) = Stand(angle);
      StructureRig.Around(world, machine, Def(), angle).Complete();
      sets.Add(Render(machine.CellsAccepting(ChargePile)));
    }

    // Guards against a mapping that ignores the angle: four distinct sets is what the per-facing theory
    // cannot state on its own.
    Assert.Equal(4, sets.Distinct().Count());
  }

  [Fact]
  public void Turning_the_structure_moves_the_cells_rather_than_answering_out_of_the_old_facing() {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, Def()).Complete();
    Assert.Equal(ExpectedAt(0), Render(machine.CellsAccepting(ChargePile)));

    // A wrench turn: the machine re-derives its angle and reloads its structure on the next monitor tick.
    // The cells are cached in world space, so a cache surviving the reload would keep answering north.
    machine.Angle = 90;
    world.AdvanceBlockEntityTime(3000);

    Assert.Equal(ExpectedAt(90), Render(machine.CellsAccepting(ChargePile)));
  }

  #endregion

  #region Caching

  [Fact]
  public void The_answer_is_computed_once_and_handed_back() {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, Def()).Complete();

    // Read on every change of a charge column's height, so it must not re-walk the offsets per read.
    // Same instance, not merely equal contents.
    Assert.Same(
      machine.CellsAccepting(ChargePile),
      machine.CellsAccepting(ChargePile)
    );
  }

  [Fact]
  public void A_structure_asked_before_its_layout_arrives_answers_properly_afterwards() {
    var (world, machine) = Stand();

    // The block carries no attributes yet, the state a client-side block entity is in until the layout
    // loads. Empty is the answer in that window.
    Assert.Empty(machine.CellsAccepting(ChargePile));

    StructureRig.Around(world, machine, Def()).Complete();

    // The early read leaves no residue: the late-arriving layout is answered in full.
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
  ) {
    var (world, machine) = Stand(angle);
    StructureRig.Around(world, machine, OrientedDef(), angle).Complete();

    // The layout authors a north door; a structure turned a quarter turn wants a west one. The answer goes
    // through the same WantedCodeAt the completion walk uses; reading the raw blockNumbers code instead
    // would report north at every facing.
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
