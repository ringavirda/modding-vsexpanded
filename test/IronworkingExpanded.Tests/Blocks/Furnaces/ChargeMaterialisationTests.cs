using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Materialisation: the furnace making the world match its columns by placing and removing
/// <c>iwex:furnace-chargepile</c>, so each column stands as many blocks high as the units in it.
/// </summary>
/// <remarks>
/// A column's floor is its own, not <see cref="BlockEntityFurnaceCore.ShaftBox"/>'s
/// (<see cref="A_column_fills_from_its_own_floor_not_the_shaft_boxs"/>). The shipped cold blast furnace has a
/// three-cell crucible well in its middle row at y=1 while the outer six columns start at y=2, so indexing
/// or placing from the box floor is right for a uniform shaft and wrong for that one.
/// </remarks>
public class ChargeMaterialisationTests {
  #region Harness

  private const string Coke = "game:coke";
  private static readonly BlockPos Anchor = new(0, 16, 0);
  private static readonly BurdenMix Fluxed = new(70f, 10f, 20f);

  /// <summary>One ore block of charge: 16 bands x <c>ChargeItemsPerBand</c> = 32 items.</summary>
  private static int PerBlock =>
    ChargeColumn.BandsPerBlock * IwexValues.ChargeItemsPerBand;

  /// <summary>
  /// A standing cold blast furnace whose world knows the charge-pile block and its entity class, so
  /// <c>SetBlock</c> spawns a real <see cref="BlockEntityChargePile"/> as the engine would. Without the
  /// factory the placement assertions still pass while the redraw assertions test nothing.
  /// </summary>
  private static (BlockEntityBlastFurnaceCold Core, StructureRig Rig) Cold(
    string side = "north"
  ) => Stood(BlockBlastFurnaceCoreCold.Definitions("iwex").Single(), side);

  /// <summary>The same, wearing <paramref name="def"/> rather than the shipped drawing, so a fixture can vary
  /// the layout materialisation reads.</summary>
  private static (BlockEntityBlastFurnaceCold Core, StructureRig Rig) Stood(
    ExBlockDef def,
    string side = "north"
  ) {
    var be = new BlockEntityBlastFurnaceCold();
    StructureRig rig = Stand(
      be,
      def,
      Anchor,
      "iwex:furnace-blastcore-tier1",
      side
    );

    rig.World.RegisterBlockEntityFactory(
      "iwex.BlockEntityChargePile",
      () => new BlockEntityChargePile()
    );
    Block pile = TestBlocks.Configure(
      new Block(),
      BlockChargePile.PileCode.ToShortString(),
      950,
      ("type", "chargepile")
    );
    pile.EntityClass = "iwex.BlockEntityChargePile";
    rig.World.Register(pile);

    return (be, rig);
  }

  private static void Sync(BlockEntityFurnaceCore be) =>
    ReflectionHelpers.Invoke(be, "SyncChargeBlocks");

  /// <summary>Every world cell of <paramref name="be"/>'s footprint that currently holds a charge pile,
  /// as structure-local offsets.</summary>
  private static List<Vec3i> PlacedCells(
    BlockEntityFurnaceCore be,
    StructureRig rig
  ) =>
    [
      .. be
        .ChargeableCells.Where(c =>
          rig.World.Accessor.GetBlock(c)?.Code?.ToShortString()
          == BlockChargePile.PileCode.ToShortString()
        )
        .Select(be.LocalOf)
        .OrderBy(l => l.X)
        .ThenBy(l => l.Z)
        .ThenBy(l => l.Y),
    ];

  private static void Charge(
    BlockEntityFurnaceCore be,
    int x,
    int z,
    int units
  ) => be.ChargeColumnAt(x, z)!.Push(Coke, units, 20f, Fluxed);

  #endregion

  #region Height follows the column

  [Fact]
  public void An_empty_furnace_places_nothing() {
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();

    Sync(be);

    Assert.Empty(PlacedCells(be, rig));
  }

  [Theory]
  [InlineData(1, 1)]
  [InlineData(31, 1)]
  [InlineData(32, 1)]
  [InlineData(33, 2)]
  [InlineData(64, 2)]
  [InlineData(65, 3)]
  public void A_columns_block_count_is_its_units_rounded_up_to_a_block(
    int units,
    int blocks
  ) {
    // 32 items per ore block: 16 bands x 2 items. One item makes a whole block appear, because a
    // part-filled block draws part-filled.
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();
    Assert.Equal(32, PerBlock);

    Charge(be, 0, 0, units);
    Sync(be);

    Assert.Equal(blocks, PlacedCells(be, rig).Count);
  }

  [Fact]
  public void Blocks_stack_upward_from_the_bottom_with_no_gaps() {
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();

    Charge(be, 0, 0, 2 * PerBlock);
    Sync(be);

    List<Vec3i> placed = PlacedCells(be, rig);
    Assert.Equal(2, placed.Count);
    Assert.Equal(placed[0].Y + 1, placed[1].Y);
  }

  [Fact]
  public void Draining_a_column_removes_every_block_it_had() {
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();

    Charge(be, 0, 0, 3 * PerBlock);
    Sync(be);
    Assert.Equal(3, PlacedCells(be, rig).Count);

    be.ChargeColumnAt(0, 0)!.Take(3 * PerBlock);
    Sync(be);

    Assert.Empty(PlacedCells(be, rig));
  }

  [Fact]
  public void Syncing_twice_changes_nothing() {
    // Sync is idempotent, because every caller syncs unconditionally rather than tracking what it last
    // placed. A version that toggled, or that re-spawned entities on every call, would pass every test
    // above and thrash the world once wired to a tick.
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();

    Charge(be, 0, 0, PerBlock + 5);
    Sync(be);
    List<Vec3i> first = PlacedCells(be, rig);
    BlockEntity? entity = rig.World.Accessor.GetBlockEntity(
      be.ChargeableCells.First(c => be.LocalOf(c).Equals(first[0]))
    );

    Sync(be);

    Assert.Equal(first, PlacedCells(be, rig));
    // The same entity object, not merely an equal cell set: a re-spawn loses any state a pile held and
    // resets the snapshot the client draws from.
    Assert.Same(
      entity,
      rig.World.Accessor.GetBlockEntity(
        be.ChargeableCells.First(c => be.LocalOf(c).Equals(first[0]))
      )
    );
  }

  [Fact]
  public void Every_column_materialises_independently() {
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();

    Charge(be, 0, 0, PerBlock);
    Charge(be, 1, 1, 2 * PerBlock);
    Sync(be);

    List<Vec3i> placed = PlacedCells(be, rig);
    Assert.Equal(1, placed.Count(l => l.X == 0 && l.Z == 0));
    Assert.Equal(2, placed.Count(l => l.X == 1 && l.Z == 1));
    Assert.Equal(3, placed.Count);
  }

  #endregion

  #region The uneven floor

  /// <summary>
  /// The shipped cold blast furnace has no flat shaft floor: its drawing marks three crucible cells
  /// <c>Chargeable</c> at y=1 (the middle row) and nine per course at y=2..5, so <c>ShaftBox</c>'s floor is 1
  /// while six of the nine columns cannot hold charge below 2. Placing from the box floor would write a
  /// charge block into the tuyere and brick cells either side of the crucible.
  /// </summary>
  [Fact]
  public void A_column_fills_from_its_own_floor_not_the_shaft_boxs() {
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();

    // The premise, asserted rather than assumed: a flattened layout makes the rest of this test vacuous.
    int crucibleFloor = ReadFloor(be, 0, 0);
    int outerFloor = ReadFloor(be, -1, -1);
    Assert.Equal(1, crucibleFloor);
    Assert.Equal(2, outerFloor);

    Charge(be, -1, -1, 1);
    Sync(be);

    Vec3i only = Assert.Single(PlacedCells(be, rig));
    Assert.Equal(new Vec3i(-1, outerFloor, -1), only);
  }

  [Fact]
  public void The_first_block_of_every_column_draws_the_bottom_of_that_column() {
    // `BlocksTall` counts from the column's own base, so the render window must too. Indexed off the shaft
    // box, an outer column's first pile would ask for bands 16-31 of a column holding only 0-15 and render
    // as an empty block.
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();

    Charge(be, -1, -1, 1);
    Charge(be, 0, 0, 1);
    Sync(be);

    foreach (Vec3i local in PlacedCells(be, rig)) {
      BlockPos world = be.ChargeableCells.First(c =>
        be.LocalOf(c).Equals(local)
      );
      Assert.NotNull(be.ChargeColumnAt(world, out int index));
      Assert.Equal(0, index);
    }
  }

  [Fact]
  public void A_cell_below_a_columns_floor_belongs_to_no_block_of_it() {
    // The other side of the same rule: the tuyere course under an outer column is not that column's block
    // -1, it is not part of the column at all.
    (BlockEntityBlastFurnaceCold be, _) = Cold();

    // The cell one below an outer column's floor, found by walking down from a cell of that column rather
    // than by arithmetic on the anchor, because the local-to-world map turns with the facing.
    BlockPos floorCell = be.ChargeableCells.First(c =>
      be.LocalOf(c).Equals(new Vec3i(-1, 2, -1))
    );
    BlockPos below = floorCell.DownCopy();

    Assert.Null(be.ChargeColumnAt(below, out int index));
    Assert.Equal(-1, index);
  }

  private static int ReadFloor(BlockEntityFurnaceCore be, int x, int z) =>
    (int)ReflectionHelpers.Invoke(be, "ColumnFloorY", x, z)!;

  #endregion

  #region A hole in the column

  /// <summary>
  /// A single-column shaft with a cell missing from the middle: chargeable at y=1, y=2 and y=4, brick at
  /// y=3. No shipped furnace draws one, so nothing else distinguishes <c>y - floor</c> from position in the
  /// cell list; the two functions agree on every contiguous column.
  /// </summary>
  private static ExBlockDef HoleyDef() =>
    ExBlockDef
      .Create("iwex", "furnace")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "iwex:furnace-blastcore-*")
          .Legend('#', "game:refractorybricks-good-tier*")
          .Legend('c', ShaftGlyph)
          .Role('c', CellRole.Chargeable)
          .Layer(0, "C")
          .Layer(1, "c")
          .Layer(2, "c")
          .Layer(3, "#")
          .Layer(4, "c")
      );

  [Fact]
  public void A_column_with_a_hole_stacks_around_it_rather_than_stopping_at_it() {
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Stood(HoleyDef());

    // The premise: three chargeable cells, and the third is two levels above the second.
    Assert.Equal(
      [1, 2, 4],
      be.ChargeableCells.Select(c => be.LocalOf(c).Y).OrderBy(y => y)
    );

    Charge(be, 0, 0, 3 * PerBlock);
    Sync(be);

    Assert.Equal(
      [new Vec3i(0, 1, 0), new Vec3i(0, 2, 0), new Vec3i(0, 4, 0)],
      PlacedCells(be, rig)
    );
  }

  [Fact]
  public void A_blocks_index_is_its_position_in_the_column_not_its_height_above_the_floor() {
    // Placement fills the cell list from index 0, so the pile at y=4 is the column's third block. `y - floor`
    // calls it the fourth, and a pile drawing bands 48-63 of a column holding only 0-47 renders empty. The
    // two functions differ by the size of the hole.
    (BlockEntityBlastFurnaceCold be, _) = Stood(HoleyDef());

    foreach ((int y, int expected) in new[] { (1, 0), (2, 1), (4, 2) }) {
      BlockPos cell = be.ChargeableCells.First(c => be.LocalOf(c).Y == y);
      Assert.NotNull(be.ChargeColumnAt(cell, out int index));
      Assert.Equal(expected, index);
    }
  }

  [Fact]
  public void A_shaft_has_a_column_for_every_chargeable_footprint_and_no_others() {
    // Columns are minted per chargeable footprint, not per (x,z) of the shaft bounding box. A shaft that is
    // not a solid rectangle in plan would otherwise get pushable columns for footprints the drawing marks
    // nothing chargeable in: charge pushed there counts toward ShaftChargeUnits and rides the save, while
    // SyncChargeBlocks skips it for want of cells to place into.
    //
    // The shipped shafts are solid 3x3 above the hearth, so their two key sets are identical and this passes
    // either way on them. The L-shaped fixture is what distinguishes them.
    (BlockEntityBlastFurnaceCold be, _) = Stood(LShapedDef());

    var footprints = be
      .ChargeableCells.Select(c => (be.LocalOf(c).X, be.LocalOf(c).Z))
      .ToHashSet();

    Assert.Equal(3, footprints.Count);
    Assert.Equal(
      footprints.OrderBy(f => f.Item1).ThenBy(f => f.Item2),
      be.ShaftColumns.Keys.OrderBy(k => k.X).ThenBy(k => k.Z)
    );
    // The hole in the L is inside the box, so the case is not vacuous: a box walk would mint a fourth
    // column here.
    Assert.DoesNotContain((1, 1), footprints);
    Assert.Null(be.ChargeColumnAt(1, 1));
  }

  /// <summary>An L in plan: three of the box's four footprints are chargeable, the fourth is brick. The
  /// shipped shafts are solid rectangles, so nothing else distinguishes a column per chargeable footprint
  /// from a column per box cell.</summary>
  private static ExBlockDef LShapedDef() =>
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
            c #
            """
          )
      );

  #endregion

  #region Cells the furnace does not own

  /// <summary>
  /// The occupant used to test the guard that stops the shaft and the furnace writing over one cell. The
  /// shipped drawings mark the crucible both <c>Chargeable</c> and <c>Pool</c>, so the furnace puts a block
  /// there and an unguarded sync would read the cell as empty and take it back.
  /// </summary>
  /// <remarks>
  /// <c>iwex:hearthmetal-pigiron</c> is the real occupant: <c>ConsumeForMelting</c> places hearth metal in a
  /// free pool cell the moment melting starts and it stands for the campaign, so the two writers contend
  /// every tick rather than once at shutdown. The guard itself is about a block this walk did not place,
  /// not about any particular code, and still applies to a block a player put in an open shaft cell.
  /// </remarks>
  private const string Foreign = "iwex:hearthmetal-pigiron";

  private static void PlaceForeign(StructureRig rig, BlockPos pos) {
    Block block = TestBlocks.Configure(new Block(), Foreign, 951);
    rig.World.Register(block);
    rig.World.Accessor.SetBlock(block.BlockId, pos);
  }

  private static bool HoldsForeign(StructureRig rig, BlockPos pos) =>
    rig.World.Accessor.GetBlock(pos)?.Code?.ToShortString() == Foreign;

  [Fact]
  public void A_cell_holding_someone_elses_block_is_skipped_not_overwritten() {
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();

    // The crucible floor of the middle column: the cell that is Chargeable and Pool at once.
    BlockPos crucible = be.ChargeableCells.First(c =>
      be.LocalOf(c).Equals(new Vec3i(0, 1, 0))
    );
    PlaceForeign(rig, crucible);

    Charge(be, 0, 0, 2 * PerBlock);
    Sync(be);

    Assert.True(
      HoldsForeign(rig, crucible),
      "the solidified iron was overwritten"
    );
    // The column draws the one block it can, above the obstruction rather than not at all.
    Assert.Equal([new Vec3i(0, 2, 0)], PlacedCells(be, rig));
  }

  [Fact]
  public void An_obstructed_column_still_holds_all_of_its_units() {
    // Drawing short does not lose charge. The units stay in the column, which is what the hopper readout
    // reports and what descent consumes, so an obstruction costs a rendered block and not the burden.
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();

    PlaceForeign(
      rig,
      be.ChargeableCells.First(c => be.LocalOf(c).Equals(new Vec3i(0, 1, 0)))
    );
    Charge(be, 0, 0, 2 * PerBlock);
    Sync(be);

    Assert.Equal(2 * PerBlock, be.ChargeColumnAt(0, 0)!.TotalUnits);
    Assert.Equal(2 * PerBlock, be.ShaftChargeUnits);
  }

  [Fact]
  public void Clearing_the_obstruction_lets_the_column_heal_itself() {
    // Nothing records that a cell was skipped, so the heal falls out of reconciling the world to the columns
    // rather than out of a retry list.
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();

    BlockPos crucible = be.ChargeableCells.First(c =>
      be.LocalOf(c).Equals(new Vec3i(0, 1, 0))
    );
    PlaceForeign(rig, crucible);
    Charge(be, 0, 0, 2 * PerBlock);
    Sync(be);
    Assert.Single(PlacedCells(be, rig));

    rig.World.Accessor.SetBlock(0, crucible);
    Sync(be);

    Assert.Equal(
      [new Vec3i(0, 1, 0), new Vec3i(0, 2, 0)],
      PlacedCells(be, rig)
    );
  }

  #endregion

  #region Redraw

  [Fact]
  public void A_surviving_pile_is_told_its_column_moved() {
    // `OnColumnChanged` is the only route that republishes the snapshot the tesselation thread reads. A sync
    // that placed blocks correctly but skipped it leaves a permanently stale client, which no placement
    // assertion can see because the world is right and only the mesh is wrong.
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();

    Charge(be, 0, 0, IwexValues.ChargeItemsPerBand);
    Sync(be);

    BlockPos cell = be.ChargeableCells.First(c =>
      rig.World.Accessor.GetBlockEntity(c) is BlockEntityChargePile
    );
    var pile = (BlockEntityChargePile)rig.World.Accessor.GetBlockEntity(cell)!;
    Assert.Single(pile.RenderSlabs);

    // A band of burden on top of the band of coke: still one block tall, so nothing is placed or removed and
    // only the stripes change. A sync that redrew on height change alone would leave this pile drawing a
    // single coke band.
    be.ChargeColumnAt(0, 0)!
      .Push("iwex:burden", IwexValues.ChargeItemsPerBand, 20f, Fluxed);
    Sync(be);

    Assert.Single(PlacedCells(be, rig));
    Assert.Equal(2, pile.RenderSlabs.Count);
  }

  [Fact]
  public void A_pile_the_furnace_removed_leaves_no_block_entity_behind() {
    (BlockEntityBlastFurnaceCold be, StructureRig rig) = Cold();

    Charge(be, 0, 0, PerBlock);
    Sync(be);
    BlockPos cell = be.ChargeableCells.First(c =>
      rig.World.Accessor.GetBlockEntity(c) is BlockEntityChargePile
    );

    be.ChargeColumnAt(0, 0)!.Take(PerBlock);
    Sync(be);

    Assert.Null(rig.World.Accessor.GetBlockEntity(cell));
    Assert.Null(rig.World.Accessor.GetBlock(cell)?.EntityClass);
  }

  #endregion
}
