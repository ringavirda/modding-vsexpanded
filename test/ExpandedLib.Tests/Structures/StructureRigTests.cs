using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>A concrete mega-block for driving the multiblock lifecycle headlessly.</summary>
internal sealed class TestMegablock : BlockEntityMultiblockMachine {
  /// <summary>The rotation the machine reports, standing in for reading a <c>side</c> variant.</summary>
  public int Angle;
  public int ProductionTicks;
  public int CompletedCount;
  public int LostCount;

  protected override void UpdateStructureRotation() => SetStructureAngle(Angle);

  protected override void OnProductionTick(float dt) => ProductionTicks++;

  protected override void OnStructureCompleted() => CompletedCount++;

  protected override void OnStructureLost() => LostCount++;

  protected override string GetIncompleteMessage(int missingCount) =>
    $"missing {missingCount}";

  protected override string GetCompleteMessage() => "complete";
}

/// <summary>
/// Covers <see cref="StructureRig"/>, the harness primitive that stands up a machine's authored footprint
/// so the machine completes itself rather than having <c>StructureComplete</c> assigned. Completion is
/// only ever reached by satisfying vanilla's <c>InCompleteBlockCount</c>, and is lost again as soon as the
/// world stops matching the layout.
/// </summary>
public class StructureRigTests {
  #region Nested alternations

  /// <summary>
  /// A layout glyph may be an alternation with a nested group inside one of its branches, because vanilla
  /// treats the inside of <c>@( )</c> as a regex. Picking the first branch by scanning to the first
  /// <c>)</c> stops inside that nested group and leaves a stray bracket on the stand-in's code, which then
  /// matches nothing: the stand-ins are placed, no error is raised, and the machine never completes.
  /// </summary>
  [Fact]
  public void A_glyph_with_a_nested_alternation_is_still_satisfied() {
    var world = new TestWorld();
    var machine = new TestMegablock { Angle = 0 };
    world.Place(
      new BlockPos(0, 10, 0),
      TestBlocks.Configure(new Block(), "exlib:testmega-n", 1),
      machine
    );
    world.Attach(machine);

    ExBlockDef def = ExBlockDef
      .Create("exlib", "testnested")
      .Multiblock(m =>
        m.Number("exlib:testmega*", 1)
          .Number("@(brickcourse-.*-(black|red|tan)|claybricks-good-fire)", 2)
          .At(0, 0, 0, 1)
          .At(1, 0, 0, 2)
      );

    var rig = StructureRig.Around(world, machine, def);
    rig.Raise();

    Assert.Equal(0, rig.Missing);
  }

  #endregion

  #region Fixture

  // A principal, a ring of solid cells around it, and a shaft cell satisfied by air: the "@(air|coalpile)"
  // fuel slot the blast furnaces use, which a filler must not try to place a block into.
  private static ExBlockDef Def() =>
    ExBlockDef
      .Create("exlib", "testmega")
      .Multiblock(m =>
        m.Number("exlib:testmega*", 1)
          .Number("exlib:testbrick*", 2)
          .Number("@(air|coalpile)", 3)
          .At(0, 0, 0, 1)
          .At(1, 0, 0, 2)
          .At(-1, 0, 0, 2)
          .At(0, 0, 1, 2)
          .At(0, 0, -1, 2)
          .At(0, 1, 0, 3)
      );

  /// <summary>
  /// A footprint with one oriented part in it. The door is authored facing north, and
  /// <see cref="MultiblockLayoutBuilder.Legend"/> marks any code carrying a whole side segment oriented,
  /// so the completion check rotates that facing with the structure. Authored through the layout builder
  /// rather than <c>Multiblock</c> because only the builder emits the <c>multiblockFacings</c> table.
  /// </summary>
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
      new BlockPos(0, 10, 0),
      TestBlocks.Configure(new Block(), "exlib:testmega-n", 1),
      machine
    );
    world.Attach(machine);
    return (world, machine);
  }

  #endregion

  #region Raising the footprint

  [Fact]
  public void Raise_satisfies_every_cell_of_the_layout() {
    var (world, machine) = Stand();
    var rig = StructureRig.Around(world, machine, Def());

    // Four brick cells. The anchor's own cell is satisfied by the anchor, and the shaft cell by the air
    // already there: vanilla's wildcard matcher resolves "@(air|coalpile)" against "game:air", so an
    // unbuilt shaft is not counted as missing in game either.
    Assert.Equal(4, rig.Missing);
    rig.Raise();
    Assert.Equal(0, rig.Missing);
  }

  [Fact]
  public void An_air_satisfied_cell_is_left_empty_rather_than_filled() {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, Def()).Raise();

    // The shaft wants "@(air|coalpile)". Filling it with a block satisfies the code check while plugging
    // the cell the machine expects to be open.
    Assert.Equal(
      "game:air",
      world.GetBlock(new BlockPos(0, 11, 0)).Code.ToString()
    );
  }

  [Fact]
  public void A_cell_the_test_already_occupied_is_left_alone() {
    var (world, machine) = Stand();
    var rig = StructureRig.Around(world, machine, Def());

    // A functional block standing in one of the footprint cells: a tuyere, a tap, a gas outlet.
    var functional = TestBlocks.Configure(
      new Block(),
      "exlib:testbrick-real",
      77
    );
    rig.Occupy(new BlockPos(1, 10, 0), functional);
    rig.Raise();

    Assert.Same(functional, world.GetBlock(new BlockPos(1, 10, 0)));
    Assert.Equal(0, rig.Missing);
  }

  [Fact]
  public void Raise_is_idempotent() {
    var (world, machine) = Stand();
    var rig = StructureRig.Around(world, machine, Def()).Raise();
    Block first = world.GetBlock(new BlockPos(1, 10, 0));

    rig.Raise();

    Assert.Same(first, world.GetBlock(new BlockPos(1, 10, 0)));
    Assert.Equal(0, rig.Missing);
  }

  #endregion

  #region Completion is an outcome, not an assignment

  [Fact]
  public void The_machine_completes_itself_once_the_footprint_stands() {
    var (world, machine) = Stand();
    var rig = StructureRig.Around(world, machine, Def()).Raise();
    Assert.False(machine.StructureComplete);

    world.Initialize(machine);
    Assert.True(rig.AwaitCompletion());

    Assert.True(machine.StructureComplete);
    Assert.Equal(1, machine.CompletedCount);
  }

  [Fact]
  public void An_unbuilt_footprint_never_completes() {
    var (world, machine) = Stand();
    var rig = StructureRig.Around(world, machine, Def()); // deliberately not raised

    world.Initialize(machine);

    Assert.False(rig.AwaitCompletion());
    Assert.False(machine.StructureComplete);
    Assert.Equal(0, machine.CompletedCount);
  }

  [Fact]
  public void Breaking_one_cell_takes_the_machine_back_out_of_completion() {
    var (world, machine) = Stand();
    var rig = StructureRig.Around(world, machine, Def()).Complete();

    world.Accessor.SetBlock(0, new BlockPos(0, 10, -1));
    Assert.Equal(1, rig.Missing);
    world.AdvanceBlockEntityTime(3000);

    Assert.False(machine.StructureComplete);
    Assert.Equal(1, machine.LostCount);
  }

  [Fact]
  public void Production_does_not_run_until_the_structure_is_complete() {
    var (world, machine) = Stand();
    var rig = StructureRig.Around(world, machine, Def());

    // Live, initialized, ticking - but unbuilt.
    world.Initialize(machine);
    world.AdvanceBlockEntityTime(6000);
    Assert.Equal(0, machine.ProductionTicks);

    rig.Raise();
    Assert.True(rig.AwaitCompletion());
    world.AdvanceBlockEntityTime(3000);

    Assert.True(machine.ProductionTicks > 0);
  }

  #endregion

  #region Rotation

  [Theory]
  [InlineData(0)]
  [InlineData(90)]
  [InlineData(180)]
  [InlineData(270)]
  public void A_structure_raised_at_any_angle_completes(int angle) {
    var (world, machine) = Stand(angle);

    // Throws with a per-cell breakdown if the rotated cells land where the machine cannot see them.
    StructureRig.Around(world, machine, Def(), angle).Complete();

    Assert.True(machine.StructureComplete);
  }

  [Fact]
  public void Cell_maps_a_local_offset_through_the_rigs_rotation() {
    var (world, machine) = Stand(90);
    var rig = StructureRig.Around(world, machine, Def(), 90);

    // The rig's cell table is vanilla's rotated layout, so both must agree on where the structure-local
    // (+1, 0, 0) cell ended up. That agreement is what lets a fixture address a tuyere by its authored
    // coordinates instead of hand-rotating them per orientation.
    BlockPos mapped = rig.Cell(1, 0, 0);
    Assert.Contains(rig.Cells, c => c.Pos.Equals(mapped));
    Assert.NotEqual(new BlockPos(1, 10, 0), mapped); // it genuinely moved
  }

  #endregion

  #region Oriented parts

  // The rig's half of MultiblockFacings. The production check turns an oriented part's facing with the
  // structure, so a rig that fills and counts by the authored code disagrees with the machine at every
  // non-north angle: it places a north-facing part, reports no unsatisfied cells, and the machine never
  // completes.

  [Theory]
  [InlineData(0)]
  [InlineData(90)]
  [InlineData(180)]
  [InlineData(270)]
  public void A_structure_with_an_oriented_part_completes_at_any_angle(
    int angle
  ) {
    var (world, machine) = Stand(angle);

    // Throws with a per-cell breakdown if the rig satisfied its own idea of the cell but not the machine's.
    StructureRig.Around(world, machine, OrientedDef(), angle).Complete();

    Assert.True(machine.StructureComplete);
  }

  [Fact]
  public void A_rotated_oriented_cell_wants_the_rotated_facing_not_the_authored_one() {
    var (world, machine) = Stand(90);
    var rig = StructureRig.Around(world, machine, OrientedDef(), 90);

    // The layout authors a north-facing door; a structure turned to 90 deg wants a west-facing one, so
    // placing the authored variant leaves the cell unsatisfied.
    rig.Occupy(
      rig.Cell(1, 0, 0),
      TestBlocks.Configure(new Block(), "exlib:testdoor-n", 91)
    );
    rig.Raise();

    // The count alone is not enough: Missing read against the authored code answers 0 and Complete()
    // then reports no unsatisfied cells while the machine stays incomplete.
    Assert.Equal(1, rig.Missing);
    // Single letters: the legend authors `exlib:testdoor-n` and MultiblockFacings rotates the token in the
    // spelling it was given, so a quarter turn asks for `-w`.
    Assert.Contains("wants 'exlib:testdoor-w'", rig.MissingReport);
    Assert.Contains("has 'exlib:testdoor-n'", rig.MissingReport);
  }

  [Fact]
  public void A_layout_with_no_oriented_part_keeps_its_authored_glyphs() {
    var (world, machine) = Stand(90);
    var rig = StructureRig.Around(world, machine, Def(), 90);

    // The other direction, so the rotation above is not a rewrite of every code. A shaft glyph is
    // domainless: an AssetLocation round trip would re-domain it to "game:@(air|coalpile)", and the
    // authored form is what the rig's filler and report read.
    Assert.Contains(rig.Cells, c => c.Wanted == "@(air|coalpile)");
    Assert.Contains(rig.Cells, c => c.Wanted == "exlib:testbrick*");
  }

  #endregion

  #region Rotation, continued

  [Fact]
  public void A_structure_raised_at_the_wrong_angle_does_not_complete() {
    // The machine faces north; the rig lays the footprint a quarter-turn out, so an asymmetric cell lands
    // where the machine is not looking.
    var world = new TestWorld();
    var machine = new TestMegablock { Angle = 0 };
    world.Place(
      new BlockPos(0, 10, 0),
      TestBlocks.Configure(new Block(), "exlib:testmega-n", 1),
      machine
    );
    world.Attach(machine);

    ExBlockDef asymmetric = ExBlockDef
      .Create("exlib", "testmega")
      .Multiblock(m =>
        m.Number("exlib:testmega*", 1)
          .Number("exlib:testbrick*", 2)
          .At(0, 0, 0, 1)
          .At(2, 0, 0, 2)
      );

    StructureRig.Around(world, machine, asymmetric, 90).Raise();
    world.Initialize(machine);

    Assert.False(machine.StructureComplete);
  }

  #endregion

  #region Guards

  [Fact]
  public void A_definition_without_a_multiblock_layout_is_rejected() {
    var (world, machine) = Stand();
    var ex = Assert.Throws<System.InvalidOperationException>(() =>
      StructureRig.Around(world, machine, ExBlockDef.Create("exlib", "plain"))
    );
    Assert.Contains("multiblockStructure", ex.Message);
  }

  [Fact]
  public void Complete_reports_which_cells_are_unsatisfied() {
    var (world, machine) = Stand();
    var rig = StructureRig.Around(world, machine, Def());

    // Block one cell with the wrong block so Raise leaves it unsatisfied.
    rig.Occupy(
      new BlockPos(1, 10, 0),
      TestBlocks.Configure(new Block(), "exlib:wrongblock", 88)
    );

    var ex = Assert.Throws<System.InvalidOperationException>(() =>
      rig.Complete()
    );
    Assert.Contains("exlib:testbrick*", ex.Message);
    Assert.Contains("exlib:wrongblock", ex.Message);
  }

  #endregion
}
