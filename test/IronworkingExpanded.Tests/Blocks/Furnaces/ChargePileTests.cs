using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.Items;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace IronworkingExpanded.Tests;

/// <summary>
/// <c>iwex:furnace-chargepile</c> - the 16-band window onto a shaft column. The block owns no charge, it
/// draws what the furnace holds, so these cases pin the correspondence: which column a pile looks at,
/// which bands of it, and that taking from it or breaking it neither creates nor destroys charge.
/// <para>
/// Geometry cases run on a skew shaft: 2 cells on x, 3 on z, off-centre on both, floored at y=2. Shipped
/// shafts are square and closed under 90 deg rotation, so a pile that rotated its world offset the wrong
/// way would still land on a valid column there. The y=2 floor separates the three possible block-index
/// readings: a pile at local y=3 reads 3 from the anchor, 19 in world coordinates and 1 from the floor.
/// </para>
/// </summary>
public class ChargePileTests {
  #region Harness

  private const string Coke = "game:coke";

  /// <summary>The shaft's second fuel. Worth half a coke band's carbon, so it has to look like something
  /// else - see <c>Coke_and_charcoal_are_told_apart_all_the_way_to_the_mesh_cache_key</c>.</summary>
  private const string Charcoal = "game:charcoal";

  private const string BurdenCode = "iwex:burden";

  private static readonly BurdenMix Fluxed = new(70f, 10f, 20f);

  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>
  /// The skew shaft: <c>x</c> in {-1, 0}, <c>z</c> in {-1, 0, 1}, <c>y</c> 2..4. Asymmetric on purpose;
  /// the shipped 3x3 box cannot distinguish a wrong-way rotation. The shaft box is the bounding box of
  /// the cells the layout marks <c>Chargeable</c>, so this is a drawing rather than a subclass overriding
  /// <c>ShaftMin</c>/<c>ShaftMax</c>. The extra brick at <c>(2,2,0)</c> is a cell the furnace owns but the
  /// shaft box excludes, which
  /// <c>A_pile_inside_the_furnace_but_outside_the_shaft_box_draws_nothing</c> needs.
  /// </summary>
  private static ExBlockDef SkewShaftDef() =>
    ShaftBoxDef(
      new Vec3i(-1, 2, -1),
      new Vec3i(0, 4, 1),
      brick: new Vec3i(2, 2, 0)
    );

  /// <summary>
  /// The shipped cold furnace with a count of <c>OnStructureLost</c> calls. A pile the layout will not
  /// accept makes the monitor tick read the furnace incomplete, and <c>OnStructureLost</c> on a lit
  /// furnace calls <c>Extinguish()</c>.
  /// </summary>
  private sealed class WatchfulFurnace : BlockEntityBlastFurnaceCold {
    public int StructureLosses;

    protected override void OnStructureLost() {
      StructureLosses++;
      base.OnStructureLost();
    }
  }

  /// <summary>The skew shaft's floor, restated rather than read off the furnace, so a block index
  /// measured against it does not agree with the machine by construction.</summary>
  private const int SkewFloorY = 2;

  private static readonly (int X, int Z)[] SkewColumns =
  [
    (-1, -1),
    (-1, 0),
    (-1, 1),
    (0, -1),
    (0, 0),
    (0, 1),
  ];

  /// <summary>A built furnace with charge piles standing in its shaft.</summary>
  private sealed class PileRig {
    public required TestWorld World { get; init; }
    public required BlockEntityBlastFurnaceCold Core { get; init; }
    public required StructureRig Structure { get; init; }
    public required BlockChargePile PileBlock { get; init; }

    private int _nextBe = 900;

    /// <summary>
    /// Places a charge pile straight into the world at a structure-local cell, at the world cell the
    /// current facing puts that offset on. Called after the structure completes; the shaft legend admits
    /// <c>iwex:furnace-chargepile</c>, so a later monitor tick still reads the furnace complete.
    /// </summary>
    public BlockEntityChargePile Pile(int localX, int localY, int localZ) {
      BlockPos pos = Structure.Cell(localX, localY, localZ);
      var be = new BlockEntityChargePile();
      World.Place(pos, PileBlock, be);
      World.Attach(be);
      be.Pos = pos.Copy();
      _nextBe++;
      return be;
    }

    public BlockPos Cell(int x, int y, int z) => Structure.Cell(x, y, z);
  }

  private static PileRig Skew(string side = "north") =>
    Build(new BlockEntityBlastFurnaceCold(), side, SkewShaftDef());

  private static PileRig Shipped(string side = "north") =>
    Build(new BlockEntityBlastFurnaceCold(), side);

  /// <summary>The shipped furnace with a loss counter on it - <see cref="WatchfulFurnace"/> in a rig.</summary>
  private static (PileRig rig, WatchfulFurnace core) Watchful() {
    var core = new WatchfulFurnace();
    return (Build(core, "north"), core);
  }

  /// <summary>
  /// Stands <paramref name="def"/>'s footprint - the 160-cell cold-furnace one unless a fixture layout is
  /// passed - around <paramref name="core"/> at <paramref name="side"/> and lets the machine complete
  /// itself. Nothing forces <c>StructureComplete</c>, so a wrong angle shows up as a furnace that never
  /// completes.
  /// </summary>
  private static PileRig Build(
    BlockEntityBlastFurnaceCold core,
    string side,
    ExBlockDef? def = null
  ) {
    var world = new TestWorld();
    world.World.Side.Returns(EnumAppSide.Server);
    world.RegisterItem(BurdenCode);
    world.RegisterItem(Coke);

    core.Pos = Anchor.Copy();
    // Theory data spells a side "west", block codes spell it "w"; SideToken owns that conversion.
    string token = SideToken(side);
    core.Block = TestBlocks.Configure(
      new Block(),
      $"iwex:furnace-blastcore-tier1-{token}",
      1,
      ("side", token)
    );
    world.Place(Anchor, core.Block, core);
    world.Attach(core);

    int angle = AngleFromSide(side);
    StructureRig structure = StructureRig.Around(
      world,
      core,
      def ?? BlockBlastFurnaceCoreCold.Definitions("iwex").Single(),
      angle
    );

    // The tall hopper is the layout's one oriented part: authored `hopper-tall-north`, and the facing
    // machinery turns that demand with the structure, so a furnace facing west wants a west-facing
    // hopper. `StructureRig.Raise` routes the wanted code through the same rotation the completion check
    // uses, so the rotated scenarios below stand a matching hopper up on their own.
    structure.Complete();

    var pileBlock = TestBlocks.Configure(
      new BlockChargePile(),
      "iwex:furnace-chargepile",
      7777,
      ("dummy", "x")
    );
    // A block that never went through the asset pipeline has no `api`, and Block.OnBlockBroken
    // dereferences it for the break particles. Assigning the test world's lets the break path run.
    ReflectionHelpers.SetField(pileBlock, "api", world.Api);

    return new PileRig {
      World = world,
      Core = core,
      Structure = structure,
      PileBlock = pileBlock,
    };
  }

  /// <summary>A pile standing on its own, with no furnace anywhere near it.</summary>
  private static BlockEntityChargePile Orphan(
    out TestWorld world,
    out BlockPos pos
  ) {
    world = new TestWorld();
    world.World.Side.Returns(EnumAppSide.Server);
    pos = new BlockPos(0, 16, 0);
    var block = TestBlocks.Configure(
      new BlockChargePile(),
      "iwex:furnace-chargepile",
      7777,
      ("dummy", "x")
    );
    ReflectionHelpers.SetField(block, "api", world.Api);
    var be = new BlockEntityChargePile { Pos = pos.Copy() };
    world.Place(pos, block, be);
    world.Attach(be);
    return be;
  }

  private static IPlayer EmptyHandedPlayer() {
    var player = Substitute.For<IPlayer>();
    var entity = Substitute.For<EntityPlayer>();
    entity.RightHandItemSlot.Returns(new DummySlot());
    player.Entity.Returns(entity);
    return player;
  }

  private static string[] Materials(IReadOnlyList<ChargeBandRun> runs) =>
    [.. runs.Select(r => r.Material)];

  private static int[] BandCounts(IReadOnlyList<ChargeBandRun> runs) =>
    [.. runs.Select(r => r.Bands)];

  #endregion

  #region Column resolution

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void A_pile_resolves_the_structure_local_column_it_stands_over_at_any_facing(
    string side
  ) {
    PileRig rig = Skew(side);

    // Two corners of the skew box at two different heights. Reference equality against the column the
    // core hands out for a named key is the assertion; "resolved to some column" would hold at any facing.
    BlockEntityChargePile far = rig.Pile(-1, 3, 1);
    BlockEntityChargePile near = rig.Pile(0, 2, -1);

    Assert.Same(rig.Core.ChargeColumnAt(-1, 1), far.Window.column);
    Assert.Same(rig.Core.ChargeColumnAt(0, -1), near.Window.column);
    // Distinct columns, so a mapping that handed every key one shared object cannot pass.
    Assert.NotSame(far.Window.column, near.Window.column);

    // The block index is measured from the shaft floor: 3-2 and 2-2. From the anchor it would read 3 and
    // 2; in world coordinates, 19 and 18.
    Assert.Equal(1, far.Window.blockIndex);
    Assert.Equal(0, near.Window.blockIndex);
  }

  [Fact]
  public void The_same_local_column_is_a_different_world_cell_at_every_facing() {
    // The companion to the theory above: its assertions hold at all four facings, so they mean something
    // only if the four facings are genuinely different world cells.
    var cells = new List<BlockPos>();
    foreach (string side in new[] { "north", "east", "south", "west" }) {
      PileRig rig = Skew(side);
      BlockPos pos = rig.Cell(-1, 3, 1);
      Vec3i rotated = ExOrientation.RotateOffset(
        new Vec3i(-1, 3, 1),
        AngleFromSide(side)
      );
      Assert.Equal(
        new BlockPos(
          Anchor.X + rotated.X,
          Anchor.Y + rotated.Y,
          Anchor.Z + rotated.Z,
          Anchor.dimension
        ),
        pos
      );
      cells.Add(pos);
    }

    Assert.Equal(4, cells.Distinct().Count());
  }

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Every_column_of_the_skew_shaft_is_reachable_from_the_cell_it_stands_on(
    string side
  ) {
    PileRig rig = Skew(side);

    // Round trip, one pile per column: place at the world cell the facing puts the key on, and the pile
    // must name that key back. A mapping that rotated forward instead of back lands on x = 1 at east and
    // west, which is not a column of this shaft, so the pile resolves nothing and the case fails.
    foreach (var (x, z) in SkewColumns) {
      BlockEntityChargePile pile = rig.Pile(x, SkewFloorY, z);

      // Assert.Same is ReferenceEquals, and for a key this shaft does not have both sides are null, so
      // without this guard the comparison below is Assert.Same(null, null). On a 2-by-3 shaft a
      // transposed derivation resolves four of the six keys and nothing for the other two.
      Assert.NotNull(pile.Window.column);
      Assert.Same(rig.Core.ChargeColumnAt(x, z), pile.Window.column);
    }

    Assert.Equal(6, SkewColumns.Length); // the loop cannot pass by running zero times
  }

  [Fact]
  public void A_pile_with_no_furnace_draws_nothing_and_refuses_to_be_read_as_a_window() {
    BlockEntityChargePile pile = Orphan(out TestWorld world, out BlockPos pos);

    Assert.Null(pile.Window.column);
    Assert.Equal(-1, pile.Window.blockIndex);
    Assert.False(pile.BelongsToFurnace);
    Assert.Empty(pile.Bands);
    Assert.Equal(0f, pile.FillHeight);
    Assert.Null(pile.TryTakeTop());
    Assert.Empty(pile.Block.GetCollisionBoxes(world.Accessor, pos));
  }

  [Fact]
  public void A_pile_inside_the_furnace_but_outside_the_shaft_box_draws_nothing() {
    PileRig rig = Skew();
    // A brick cell of the layout: the core owns it, so the anchor scan succeeds and only the shaft-box
    // test can reject it.
    BlockEntityChargePile pile = rig.Pile(2, 2, 0);

    Assert.NotNull(pile.ResolveOwningAnchor());
    Assert.Null(pile.Window.column);
    Assert.False(pile.BelongsToFurnace);
    Assert.Empty(pile.Bands);
    Assert.Equal(0f, pile.FillHeight);
  }

  /// <summary>
  /// The shipped shaft has no single floor: its drawing marks three crucible cells <c>Chargeable</c> at
  /// y=1 (the middle row only) and nine per course at y=2..5, so <c>ShaftBox</c>'s floor is 1 while six of
  /// the nine columns cannot hold charge below 2.
  /// <para>
  /// <c>blockIndex</c> is measured from each column's own lowest chargeable cell rather than from the box,
  /// because <c>ChargeColumn.BlocksTall</c> counts from the column's base. Indexed off the box, an outer
  /// column's first pile would ask for bands 16-31 of a column holding only 0-15 and render empty.
  /// </para>
  /// </summary>
  [Fact]
  public void A_shipped_furnace_indexes_each_column_from_its_own_floor() {
    PileRig rig = Shipped();

    // The crucible row: floor y=1, so the hearth cell is block 0.
    BlockEntityChargePile crucible = rig.Pile(0, 1, 0);
    // An outer column: floor y=2, so y=5 is its block 3 - not 4.
    BlockEntityChargePile outer = rig.Pile(-1, 5, 1);

    Assert.Same(rig.Core.ChargeColumnAt(0, 0), crucible.Window.column);
    Assert.Equal(0, crucible.Window.blockIndex);
    Assert.Same(rig.Core.ChargeColumnAt(-1, 1), outer.Window.column);
    Assert.Equal(3, outer.Window.blockIndex);

    // The outer column's own floor is its block 0.
    Assert.Equal(0, rig.Pile(-1, 2, 1).Window.blockIndex);
  }

  #endregion

  #region The pile's own code

  [Fact]
  public void PileCode_is_the_code_the_definition_actually_renders() {
    // `PileCode` is the code materialisation calls SetBlock with, and the def renders it as code
    // `furnace` plus variant `type=chargepile`. A mismatch resolves to null and skips every SetBlock
    // silently, leaving a charged furnace that draws no piles.
    ExBlockDef def = BlockChargePile.Definitions("iwex").Single();

    string rendered =
      $"{def.Domain}:{def.Code}-"
      + string.Join(
        "-",
        ((JArray)def.ToJson()["variantgroups"]!).Select(g =>
          (string)((JArray)g["states"]!)[0]!
        )
      );

    Assert.Equal(rendered, BlockChargePile.PileCode.ToShortString());
    // Cross-checked against the generated table, which is produced by running the definitions.
    Assert.Equal(
      IwexBlocks.FurnaceChargepile.Any,
      BlockChargePile.PileCode.ToShortString()
    );
  }

  #endregion

  #region The shaft legend

  /// <summary>Every chargeable cell of the shipped cold shaft - the 3x3 box on levels 2..5 plus the two
  /// hearth-floor cells the layout marks <c>c</c> at level 1. Restated rather than read off the shaft box,
  /// because a set derived from the block entity's box would agree with the layout by construction.
  /// </summary>
  private static IEnumerable<Vec3i> ShippedShaftCells() {
    yield return new Vec3i(0, 1, 0);
    yield return new Vec3i(1, 1, 0);
    for (int y = 2; y <= 5; y++)
      for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
          yield return new Vec3i(x, y, z);
  }

  [Fact]
  public void A_charged_shaft_does_not_take_the_furnace_out_of_completion() {
    var (rig, core) = Watchful();
    Assert.True(
      core.StructureComplete,
      "the furnace must be built before it is charged"
    );

    // The whole shaft materialised at once, the way charging does it.
    var cells = ShippedShaftCells().ToList();
    Assert.Equal(38, cells.Count); // the loop cannot pass by running zero times
    foreach (Vec3i cell in cells)
      rig.Pile(cell.X, cell.Y, cell.Z);

    // The monitor tick is the production one; nothing here forces the completion flag either way.
    rig.World.AdvanceBlockEntityTime(3000);
    rig.World.AdvanceBlockEntityTime(3000);

    Assert.True(
      core.StructureComplete,
      "a charged furnace went incomplete: the shaft legend does not accept iwex:furnace-chargepile."
        + rig.Structure.MissingReport
    );
    // OnStructureLost on a lit furnace calls Extinguish(), so a legend that refuses the pile is a furnace
    // that puts itself out when the player charges it.
    Assert.Equal(0, core.StructureLosses);
    Assert.Equal(0, rig.Structure.Missing);
  }

  [Fact]
  public void The_shaft_legend_still_accepts_an_open_shaft_and_a_vanilla_coal_pile() {
    // Air is an uncharged shaft; game:coalpile is what the tall hopper materialises and what the furnace
    // scenes pile into the shaft. Both stay admissible alongside the charge pile.
    var wanted = new AssetLocation(FurnaceLayoutRig.ShaftGlyph);
    Assert.True(WildcardUtil.Match(wanted, new AssetLocation("game:air")));
    Assert.True(WildcardUtil.Match(wanted, new AssetLocation("game:coalpile")));
    Assert.True(
      WildcardUtil.Match(wanted, new AssetLocation("iwex:furnace-chargepile"))
    );
    // And it is still a shaft legend, not a wildcard that accepts the wall it is cut into.
    Assert.False(
      WildcardUtil.Match(
        wanted,
        new AssetLocation("game:refractorybricks-good-tier1")
      )
    );
  }

  #endregion

  #region Bands

  // Expected band counts below are literals at the shipped 8 units a band, hand-derived from each test's
  // unit figures rather than computed from IwexValues.ChargeUnitsPerBand: an expectation computed from
  // that constant would agree with any value of it. The constant itself is pinned in ChargeColumnTests.

  [Fact]
  public void A_column_of_known_segments_draws_the_expected_runs_in_each_of_its_blocks() {
    PileRig rig = Skew();
    ChargeColumn column = rig.Core.ChargeColumnAt(-1, 1)!;
    column.Push(Coke, 25, 1180f, default);
    column.Push(BurdenCode, 25, 1140f, Fluxed);

    BlockEntityChargePile lower = rig.Pile(-1, 2, 1);
    BlockEntityChargePile upper = rig.Pile(-1, 3, 1);

    // 200 u at 8 u a band is 25 bands. Coke fills units 0..100, so bands 0..11 are wholly coke; band 12
    // spans [96, 104) - 4 u of each, a tie, which the lower segment keeps - and bands 13..24 are burden.
    Assert.Equal([Coke, BurdenCode], Materials(lower.Bands));
    Assert.Equal([13, 3], BandCounts(lower.Bands));
    Assert.Equal([BurdenCode], Materials(upper.Bands));
    Assert.Equal([9], BandCounts(upper.Bands));

    // The mix rides the run, not just the material - a burden stripe has to know which grade it is.
    Assert.Equal(Fluxed, upper.Bands[0].Mix);
    Assert.Equal(default, lower.Bands[0].Mix);
  }

  [Fact]
  public void A_course_straddling_a_block_boundary_draws_as_one_continuous_stripe() {
    PileRig rig = Skew();
    ChargeColumn column = rig.Core.ChargeColumnAt(-1, 1)!;
    column.Push(Coke, 25, 1180f, default);
    column.Push(BurdenCode, 25, 1140f, Fluxed);

    IReadOnlyList<ChargeBandRun> lower = rig.Pile(-1, 2, 1).Bands;
    IReadOnlyList<ChargeBandRun> upper = rig.Pile(-1, 3, 1).Bands;

    // The burden course does not restart at the boundary: it runs off the top of one block and straight
    // on across the bottom of the next.
    Assert.Equal(lower[^1].Material, upper[0].Material);
    Assert.Equal(lower[^1].Mix, upper[0].Mix);

    // Guard: on a single-material column the equality above holds for any implementation, including one
    // that snapped every course to a block boundary. The lower block must change material inside itself.
    Assert.NotEqual(lower[0].Material, lower[^1].Material);
    Assert.Equal(2, lower.Count);

    // Continuous in geometry too: the last slab of the lower block ends at its ceiling and the first slab
    // of the upper starts at its floor, so there is no visible seam.
    List<BlockChargePile.ChargeBandSlab> lowerSlabs = BlockChargePile.SlabsOf(
      lower
    );
    List<BlockChargePile.ChargeBandSlab> upperSlabs = BlockChargePile.SlabsOf(
      upper
    );
    Assert.Equal(1f, lowerSlabs[^1].ToY);
    Assert.Equal(0f, upperSlabs[0].FromY);
  }

  [Fact]
  public void A_course_that_snapped_to_the_block_boundary_would_look_different() {
    // The negative of the case above: a course laid to land exactly on the boundary does restart there,
    // which is what makes "same material across the seam" evidence of continuity when a course straddles.
    PileRig rig = Skew();
    ChargeColumn column = rig.Core.ChargeColumnAt(-1, 1)!;
    column.Push(Coke, 32, 1180f, default); // exactly 16 bands = one ore block
    column.Push(BurdenCode, 16, 1140f, Fluxed);

    IReadOnlyList<ChargeBandRun> lower = rig.Pile(-1, 2, 1).Bands;
    IReadOnlyList<ChargeBandRun> upper = rig.Pile(-1, 3, 1).Bands;

    Assert.Equal([Coke], Materials(lower));
    Assert.Equal([16], BandCounts(lower));
    Assert.Equal([BurdenCode], Materials(upper));
    Assert.Equal([8], BandCounts(upper));
    Assert.NotEqual(lower[^1].Material, upper[0].Material);
  }

  [Fact]
  public void An_empty_column_draws_nothing_and_a_full_block_draws_sixteen_bands() {
    PileRig rig = Skew();
    BlockEntityChargePile pile = rig.Pile(-1, 2, 1);

    Assert.Empty(pile.Bands);
    Assert.Equal(0f, pile.FillHeight);
    Assert.Empty(BlockChargePile.SlabsOf(pile.Bands));

    // 32 items at 2 items a band is exactly one block's 16 bands.
    rig.Core.ChargeColumnAt(-1, 1)!.Push(Coke, 32, 1180f, default);

    Assert.Equal(16, pile.Bands.Sum(r => r.Bands));
    Assert.Equal(1f, pile.FillHeight);
  }

  [Fact]
  public void A_part_filled_block_stands_only_as_high_as_its_bands() {
    PileRig rig = Skew();
    BlockEntityChargePile pile = rig.Pile(-1, 2, 1);
    // 8 items is 4 bands - a quarter of the block.
    rig.Core.ChargeColumnAt(-1, 1)!.Push(Coke, 8, 1180f, default);

    Assert.Equal(4, pile.Bands.Sum(r => r.Bands));
    Assert.Equal(0.25f, pile.FillHeight);

    Cuboidf[] collision = pile.Block.GetCollisionBoxes(
      rig.World.Accessor,
      pile.Pos
    );
    Assert.Single(collision);
    Assert.Equal(0.25f, collision[0].Y2);
  }

  [Fact]
  public void Slabs_stack_without_gaps_and_end_at_the_fill_height() {
    // The mesh contract: three runs tile, in order, from the block floor to the fill height.
    var runs = new List<ChargeBandRun>
    {
      new(Coke, default, 3),
      new(BurdenCode, Fluxed, 9),
      new(Coke, default, 4),
    };

    List<BlockChargePile.ChargeBandSlab> slabs = BlockChargePile.SlabsOf(runs);

    Assert.Equal(3, slabs.Count);
    Assert.Equal(0f, slabs[0].FromY);
    for (int i = 1; i < slabs.Count; i++)
      Assert.Equal(slabs[i - 1].ToY, slabs[i].FromY);
    Assert.Equal(BlockChargePile.HeightOf(runs), slabs[^1].ToY);
    Assert.Equal(1f, slabs[^1].ToY); // 3 + 9 + 4 = 16 bands

    // Each run keeps its own material and mix, so the mesher can pick a texture per stripe.
    Assert.Equal(
      BlockChargePile.CokeElement,
      BlockChargePile.ElementOf(slabs[0].Material)
    );
    Assert.Equal(
      BlockChargePile.BurdenElement,
      BlockChargePile.ElementOf(slabs[1].Material)
    );
    Assert.Equal(Fluxed, slabs[1].Mix);
  }

  [Fact]
  public void A_run_list_taller_than_the_block_is_clipped_at_its_ceiling() {
    // BandsAt cannot produce this, but the mesh must not draw outside its own cell if anything ever does.
    var runs = new List<ChargeBandRun>
    {
      new(Coke, default, 12),
      new(BurdenCode, Fluxed, 12),
    };

    Assert.Equal(1f, BlockChargePile.HeightOf(runs));
    Assert.Equal(1f, BlockChargePile.SlabsOf(runs)[^1].ToY);
    Assert.Equal(2, BlockChargePile.SlabsOf(runs).Count);
  }

  [Theory]
  [InlineData("iwex:burden", "Burden")]
  [InlineData("iwex:remeltburden", "Burden")]
  [InlineData("game:coke", "Coke")]
  // A charcoal band is half a coke band's carbon, so the two are different amounts of fuel and must be
  // different stripes; see Coke_and_charcoal_are_told_apart_all_the_way_to_the_mesh_cache_key below.
  [InlineData("game:charcoal", "Charcoal")]
  // Substring, not equality: any `*charcoal*` code from a later mod draws as charcoal, the same rule
  // BlockFirebox.TextureKeyOf applies to its four fuels.
  [InlineData("othermod:finecharcoal", "Charcoal")]
  [InlineData("burden", "Burden")]
  // An unknown material still draws - as coke, the metallurgical default.
  [InlineData("game:peat", "Coke")]
  [InlineData("", "Coke")]
  [InlineData(null, "Coke")]
  public void Every_material_draws_some_stripe(string? material, string element) {
    // Total by construction: a column can hold any code, and one that drew nothing would leave a hole in
    // the shaft wall indistinguishable from an empty band.
    Assert.Equal(element, BlockChargePile.ElementOf(material));
  }

  [Fact]
  public void Coke_and_charcoal_are_told_apart_all_the_way_to_the_mesh_cache_key() {
    // The table above pins the element each code maps to; this pins that the two fuels differ.
    Assert.NotEqual(
      BlockChargePile.ElementOf(Coke),
      BlockChargePile.ElementOf(Charcoal)
    );
    Assert.NotEqual(
      BlockChargePile.ElementOf(Charcoal),
      BlockChargePile.ElementOf(BurdenCode)
    );

    // BlockEntityChargePile keys its tesselation cache on the element alone ("chargepile-" + ElementOf),
    // so drawing charcoal by retexturing the Coke element would land both stripes on one cache entry and
    // render whichever tesselated first. A distinct element per material is therefore load-bearing.
    // Read off the shipped asset staged next to the test binary; chargepile.json has no editable source.
    string file = Path.Combine(
      AppContext.BaseDirectory,
      "assets",
      "iwex",
      "shapes",
      "furnace",
      "chargepile.json"
    );
    Assert.True(File.Exists(file), $"shape asset not found: {file}");
    var shape = JObject.Parse(File.ReadAllText(file));
    string[] elements = [.. shape["elements"]!.Select(e => (string)e["name"]!)];
    Assert.Contains(BlockChargePile.CokeElement, elements);
    Assert.Contains(BlockChargePile.CharcoalElement, elements);
    Assert.Contains(BlockChargePile.BurdenElement, elements);
    // Every element the classifier can name draws from its own texture key.
    Assert.Equal(
      3,
      shape["elements"]!
        .Select(e => (string)e["faces"]!["north"]!["texture"]!)
        .Distinct()
        .Count()
    );
  }

  #endregion

  #region Selection and collision

  // A pile that draws nothing still keeps a one-band selection box, so the player can aim at it and break
  // it out. Without that floor such a pile is invisible, non-colliding and unremovable.

  [Fact]
  public void An_orphan_keeps_a_selection_box_to_be_clicked_out_with() {
    BlockEntityChargePile pile = Orphan(out TestWorld world, out BlockPos pos);

    Cuboidf[] selection = pile.Block.GetSelectionBoxes(world.Accessor, pos);

    // One band tall - enough to aim at, and visibly not the full cube, so it does not read as solid charge.
    Assert.Single(selection);
    Assert.Equal(BlockChargePile.BandHeight, selection[0].Y2);
    // It holds nothing up: an orphan draws nothing, so there is no collision box.
    Assert.Empty(pile.Block.GetCollisionBoxes(world.Accessor, pos));
  }

  [Fact]
  public void An_empty_furnace_owned_pile_is_still_clickable() {
    // The other pile that draws nothing: a real but empty column. It stays clickable, or a shaft emptied
    // by hand becomes a wall of unselectable blocks.
    PileRig rig = Skew();
    BlockEntityChargePile pile = rig.Pile(-1, 2, 1);

    Assert.Empty(pile.Bands);
    Assert.Single(pile.Block.GetSelectionBoxes(rig.World.Accessor, pile.Pos));
    Assert.Empty(pile.Block.GetCollisionBoxes(rig.World.Accessor, pile.Pos));
  }

  [Fact]
  public void A_furnace_owned_piles_selection_box_follows_its_fill_height() {
    PileRig rig = Skew();
    BlockEntityChargePile pile = rig.Pile(-1, 2, 1);
    // 96 u at 8 u a band is 12 bands - three quarters of the block, well clear of the one-band floor.
    rig.Core.ChargeColumnAt(-1, 1)!.Push(Coke, 24, 1180f, default);

    Cuboidf[] selection = pile.Block.GetSelectionBoxes(
      rig.World.Accessor,
      pile.Pos
    );

    Assert.Single(selection);
    Assert.Equal(0.75f, selection[0].Y2);
    // Selection, collision and fill height are the same number.
    Assert.Equal(pile.FillHeight, selection[0].Y2);
    Assert.Equal(
      selection[0].Y2,
      pile.Block.GetCollisionBoxes(rig.World.Accessor, pile.Pos)[0].Y2
    );
  }

  #endregion

  #region The render snapshot

  // OnTesselation runs on the tesselation thread and may read the snapshot and nothing else: resolving
  // the core from there writes the anchor link's cache and can drive the core into rewriting its own
  // structure, and walking the column enumerates a List<T> the client clears and refills on every sync.

  [Fact]
  public void The_mesh_reads_a_snapshot_that_a_column_change_alone_does_not_move() {
    PileRig rig = Skew();
    BlockEntityChargePile pile = rig.Pile(-1, 2, 1);
    rig.World.Initialize(pile);
    Assert.Empty(pile.RenderSlabs);

    // The column changes behind the pile's back, the way descent and charging do.
    rig.Core.ChargeColumnAt(-1, 1)!.Push(Coke, 24, 1180f, default);

    // The live read moved...
    Assert.Equal(12, pile.Bands.Sum(r => r.Bands));
    // ...and the snapshot did not, which is what makes it a snapshot rather than a second live read.
    Assert.Empty(pile.RenderSlabs);
  }

  [Fact]
  public void OnColumnChanged_republishes_the_snapshot_and_syncs_the_block_entity() {
    PileRig rig = Skew();
    BlockEntityChargePile pile = rig.Pile(-1, 2, 1);
    rig.World.Initialize(pile);
    rig.Core.ChargeColumnAt(-1, 1)!.Push(Coke, 24, 1180f, default);
    rig.Core.ChargeColumnAt(-1, 1)!.Push(BurdenCode, 8, 1140f, Fluxed);

    pile.OnColumnChanged();

    // Republished, and equal to what the geometry says the block holds now: both stripes, in order.
    Assert.Equal(BlockChargePile.SlabsOf(pile.Bands), pile.RenderSlabs);
    Assert.Equal(
      [BlockChargePile.CokeElement, BlockChargePile.BurdenElement],
      pile.RenderSlabs.Select(s => BlockChargePile.ElementOf(s.Material))
    );
    // Without the sync the client never learns the column moved, so it never re-tesselates and the
    // republished snapshot is never read.
    rig.World.Accessor.Received().MarkBlockEntityDirty(pile.Pos);
  }

  [Fact]
  public void A_take_republishes_the_snapshot_of_the_window_it_was_clicked_through() {
    PileRig rig = Skew();
    BlockEntityChargePile pile = rig.Pile(-1, 2, 1);
    rig.World.Initialize(pile);
    rig.Core.ChargeColumnAt(-1, 1)!.Push(Coke, 24, 1180f, default);
    pile.OnColumnChanged();
    Assert.Equal(12, pile.RenderSlabs[^1].ToY * ChargeColumn.BandsPerBlock);

    pile.TryTakeTop();

    // One band lighter: the take route goes through the same republish, or the block just clicked keeps
    // drawing the charge the player is now holding.
    Assert.Equal(11, pile.RenderSlabs[^1].ToY * ChargeColumn.BandsPerBlock);
  }

  #endregion

  #region Take

  [Fact]
  public void Taking_lifts_one_band_off_the_top_and_the_shaft_total_drops_by_exactly_that() {
    PileRig rig = Skew();
    ChargeColumn column = rig.Core.ChargeColumnAt(-1, 1)!;
    column.Push(Coke, 25, 1180f, default);
    column.Push(BurdenCode, 25, 1140f, Fluxed);
    int before = rig.Core.ShaftChargeUnits;

    ItemStack? taken = rig.Pile(-1, 2, 1).TryTakeTop();

    Assert.NotNull(taken);
    Assert.Equal(BurdenCode, taken!.Collectible.Code.ToShortString());
    Assert.Equal(2, taken.StackSize); // one band = ChargeItemsPerBand
    Assert.Equal(before - taken.StackSize, rig.Core.ShaftChargeUnits);

    // From the top. A take running the raceway's Take() would remove the same 2 items and pass every
    // assertion above, so name which end moved: the coke laid first is untouched, the burden on it is
    // 2 lighter.
    Assert.Equal(Coke, column.Segments[0].Material);
    Assert.Equal(25, column.Segments[0].Units);
    Assert.Equal(BurdenCode, column.Segments[^1].Material);
    Assert.Equal(23, column.Segments[^1].Units);
  }

  [Fact]
  public void The_stack_a_take_hands_back_carries_the_grade_the_column_was_holding() {
    PileRig rig = Skew();
    rig.Core.ChargeColumnAt(0, -1)!.Push(BurdenCode, 10, 1140f, Fluxed);

    ItemStack? taken = rig.Pile(0, 2, -1).TryTakeTop();

    // Burden's one surviving quality is its flux ratio; a take that dropped the stamp would hand back
    // ungraded burden.
    Assert.NotNull(taken);
    Assert.Equal(Fluxed, Burden.Read(taken));
  }

  [Fact]
  public void A_take_never_spans_two_courses_and_never_over_takes_a_short_one() {
    PileRig rig = Skew();
    ChargeColumn column = rig.Core.ChargeColumnAt(-1, 0)!;
    column.Push(Coke, 10, 1180f, default);
    column.Push(BurdenCode, 1, 1140f, Fluxed); // a course shorter than one band (2 items)
    BlockEntityChargePile pile = rig.Pile(-1, 2, 0);

    ItemStack? taken = pile.TryTakeTop();

    // Clamped to the top segment as well as to one band: taking a full band of 2 would owe the player
    // 1 burden and 1 coke for a single click, and would silently merge two materials into one stack.
    Assert.Equal(1, taken!.StackSize);
    Assert.Equal(BurdenCode, taken.Collectible.Code.ToShortString());
    Assert.Equal(10, rig.Core.ShaftChargeUnits);
    Assert.Equal(Coke, column.TopMaterial);
  }

  [Fact]
  public void Taking_from_an_empty_column_hands_back_nothing_and_changes_nothing() {
    PileRig rig = Skew();
    BlockEntityChargePile pile = rig.Pile(-1, 2, 1);

    Assert.Null(pile.TryTakeTop());
    Assert.Equal(0, rig.Core.ShaftChargeUnits);
  }

  [Fact]
  public void Taking_a_material_no_item_answers_to_leaves_the_column_alone() {
    PileRig rig = Skew();
    rig.Core.ChargeColumnAt(-1, 1)!
      .Push("someothermod:cinders", 40, 900f, default);

    // The material is resolved before anything is removed. Otherwise a column holding the code of a
    // removed mod would be emptied into a null stack, destroying the charge on a right-click.
    Assert.Null(rig.Pile(-1, 2, 1).TryTakeTop());
    Assert.Equal(40, rig.Core.ShaftChargeUnits);
  }

  [Fact]
  public void The_right_click_route_reaches_the_take() {
    PileRig rig = Skew();
    rig.Core.ChargeColumnAt(-1, 1)!.Push(BurdenCode, 10, 1140f, Fluxed);
    BlockEntityChargePile pile = rig.Pile(-1, 2, 1);

    bool handled = pile.Block.OnBlockInteractStart(
      rig.World.World,
      EmptyHandedPlayer(),
      new BlockSelection { Position = pile.Pos.Copy() }
    );

    Assert.True(handled);
    Assert.Equal(8, rig.Core.ShaftChargeUnits);
  }

  #endregion

  #region Break

  // Breaking a pile digs that block's charge out and everything above falls. A chill sits at the bottom
  // of the shaft, so recovery has to reach below the stockline; `ChargeColumn.TakeSpan` is that splice.
  // See docs/design/layered-charge.md.

  /// <summary>
  /// The window's units leave the column and come back as stacks, one per material. Two materials in the
  /// window on purpose: a single-material window cannot tell "one stack per material" from "one stack,
  /// whatever was there".
  /// </summary>
  [Fact]
  public void Breaking_a_pile_the_furnace_owns_digs_its_window_out_and_drops_it() {
    PileRig rig = Skew();
    int perBlock = rig.Core.ChargeUnitsPerBlock;
    ChargeColumn column = rig.Core.ChargeColumnAt(-1, 1)!;

    // Block 0 is [0, perBlock): all the coke, then burden up to the boundary. Block 1 is burden alone.
    column.Push(Coke, 20, 1180f, default);
    column.Push(BurdenCode, (2 * perBlock) - 20, 1140f, Fluxed);
    rig.Core.SyncChargeBlocks();

    BlockEntityChargePile pile = rig.Pile(-1, SkewFloorY, 1);
    Assert.Equal(0, pile.Window.blockIndex); // the premise, not an assumption
    rig.World.Drops.Clear();

    // The block still has no drop table - the charge is not the block's, it is the column's.
    Assert.Empty(
      pile.Block.GetDrops(rig.World.World, pile.Pos, EmptyHandedPlayer())
    );

    pile.Block.OnBlockBroken(rig.World.World, pile.Pos, EmptyHandedPlayer());

    // One stack of coke, one of burden, raceway-first, with the burden still carrying its grade. Compared
    // as AssetLocation rather than as strings: ToShortString() drops the `game:` domain, so a string
    // compare would assert a spelling rather than an identity.
    Assert.Equal(2, rig.World.Drops.Count);
    Assert.Equal(new AssetLocation(Coke), rig.World.Drops[0].Collectible.Code);
    Assert.Equal(20, rig.World.Drops[0].StackSize);
    Assert.Equal(
      new AssetLocation(BurdenCode),
      rig.World.Drops[1].Collectible.Code
    );
    Assert.Equal(perBlock - 20, rig.World.Drops[1].StackSize);
    Assert.Equal(Fluxed, Burden.Read(rig.World.Drops[1]));

    // Exactly the window's units left the column.
    Assert.Equal(perBlock, column.TotalUnits);
  }

  /// <summary>
  /// The window is this block's, not the column's bottom. Every other break case digs the lowest block,
  /// where <c>blockIndex * perBlock</c> and <c>0</c> are the same number, so all of them also pass on a
  /// splice that always takes from the raceway. Digging high up the shaft on such a splice would hand the
  /// player the hot charge off the tuyeres and drop the cold charge at the top into the raceway.
  /// </summary>
  [Fact]
  public void Breaking_a_pile_ABOVE_the_bottom_digs_out_its_own_window_and_not_the_raceways() {
    PileRig rig = Skew();
    int perBlock = rig.Core.ChargeUnitsPerBlock;
    ChargeColumn column = rig.Core.ChargeColumnAt(-1, 1)!;

    // One block each of three distinguishable materials, so which window came out is readable from the drop.
    column.Push(Coke, perBlock, 1180f, default);
    column.Push(BurdenCode, perBlock, 1140f, Fluxed);
    column.Push(Charcoal, perBlock, 980f, default);
    rig.Core.SyncChargeBlocks();

    BlockEntityChargePile middle = rig.Pile(-1, SkewFloorY + 1, 1);
    Assert.Equal(1, middle.Window.blockIndex); // the premise, stated
    rig.World.Drops.Clear();

    middle.Block.OnBlockBroken(
      rig.World.World,
      middle.Pos,
      EmptyHandedPlayer()
    );

    // Burden is block 1's material; coke would mean the splice read the raceway instead.
    Assert.Single(rig.World.Drops);
    Assert.Equal(
      new AssetLocation(BurdenCode),
      rig.World.Drops[0].Collectible.Code
    );
    Assert.Equal(perBlock, rig.World.Drops[0].StackSize);

    // And the coke is still standing on the tuyeres, with the charcoal fallen onto it.
    Assert.Equal(
      [Coke, Charcoal],
      column.Segments.Select(s => s.Material).ToArray()
    );
  }

  /// <summary>
  /// Breaking a block does not punch a hole: the column got shorter, so the sync re-materialises the wall
  /// one block down. The settling is one list rebuild with no block writes.
  /// </summary>
  [Fact]
  public void Everything_above_the_broken_window_falls_and_the_wall_repairs_itself() {
    PileRig rig = Skew();
    int perBlock = rig.Core.ChargeUnitsPerBlock;
    ChargeColumn column = rig.Core.ChargeColumnAt(-1, 1)!;

    // Three blocks' worth, each a distinguishable band so the fall is visible in the order.
    column.Push(Coke, perBlock, 1180f, default);
    column.Push(BurdenCode, perBlock, 1140f, Fluxed);
    column.Push(Charcoal, perBlock, 980f, default);
    rig.Core.SyncChargeBlocks();

    BlockPos top = rig.Pile(-1, SkewFloorY + 2, 1).Pos;
    BlockEntityChargePile bottom = rig.Pile(-1, SkewFloorY, 1);

    bottom.Block.OnBlockBroken(
      rig.World.World,
      bottom.Pos,
      EmptyHandedPlayer()
    );

    // The bottom block is back, because the charge above fell into it, and the top one is gone, because
    // the column no longer reaches that high.
    Assert.Equal(rig.PileBlock.BlockId, rig.World.GetBlock(bottom.Pos).BlockId);
    Assert.Equal(0, rig.World.GetBlock(top).BlockId);

    // The order survived the fall: burden is now at the raceway, charcoal above it.
    Assert.Equal(
      [BurdenCode, Charcoal],
      column.Segments.Select(s => s.Material).ToArray()
    );
  }

  /// <summary>
  /// Drops are grouped by material and grade. "One stack per material" means "not one stack per segment",
  /// but a stack carries one burden mix, so two grades in one window cannot become one stack: merging
  /// would invent a grade and averaging two discrete grades lands between both. Same rule the hoppers
  /// enforce when they refuse two grades into one tank.
  /// </summary>
  [Fact]
  public void Two_grades_of_burden_in_one_window_come_back_as_two_stacks() {
    PileRig rig = Skew();
    int perBlock = rig.Core.ChargeUnitsPerBlock;
    ChargeColumn column = rig.Core.ChargeColumnAt(-1, 1)!;
    var lean = new BurdenMix(60f, 20f, 20f);

    column.Push(BurdenCode, perBlock / 2, 1140f, Fluxed);
    column.Push(BurdenCode, perBlock / 2, 1140f, lean);
    rig.Core.SyncChargeBlocks();

    BlockEntityChargePile pile = rig.Pile(-1, SkewFloorY, 1);
    rig.World.Drops.Clear();
    pile.Block.OnBlockBroken(rig.World.World, pile.Pos, EmptyHandedPlayer());

    Assert.Equal(2, rig.World.Drops.Count);
    Assert.Equal(Fluxed, Burden.Read(rig.World.Drops[0]));
    Assert.Equal(lean, Burden.Read(rig.World.Drops[1]));
    Assert.Equal(0, column.TotalUnits);
  }

  /// <summary>
  /// One stack per material in the ordinary case. Two coke bands at different temperatures are one
  /// substance and come back as one stack; temperature is not a grade.
  /// </summary>
  [Fact]
  public void Two_bands_of_one_material_at_different_temperatures_come_back_as_ONE_stack() {
    PileRig rig = Skew();
    int perBlock = rig.Core.ChargeUnitsPerBlock;
    ChargeColumn column = rig.Core.ChargeColumnAt(-1, 1)!;

    column.Push(Coke, perBlock / 2, 1180f, default);
    column.Push(Coke, perBlock / 2, 400f, default); // 780 C apart - far past the merge epsilon
    Assert.Equal(2, column.Segments.Count); // the premise: they really are two bands
    rig.Core.SyncChargeBlocks();

    BlockEntityChargePile pile = rig.Pile(-1, SkewFloorY, 1);
    rig.World.Drops.Clear();
    pile.Block.OnBlockBroken(rig.World.World, pile.Pos, EmptyHandedPlayer());

    Assert.Single(rig.World.Drops);
    Assert.Equal(perBlock, rig.World.Drops[0].StackSize);
  }

  [Fact]
  public void An_orphan_pile_breaks_out_and_leaves_nothing_behind() {
    BlockEntityChargePile pile = Orphan(out TestWorld world, out BlockPos pos);

    // A half-broken furnace leaves piles standing with no column behind them, so a block that renders
    // nothing, collides with nothing and cannot be removed would be permanent.
    Assert.Empty(pile.Block.GetDrops(world.World, pos, EmptyHandedPlayer()));
    pile.Block.OnBlockBroken(world.World, pos, EmptyHandedPlayer());

    Assert.Equal(0, world.GetBlock(pos).BlockId);
  }

  [Fact]
  public void A_pile_outside_the_shaft_box_breaks_out_like_any_other_orphan() {
    PileRig rig = Skew();
    BlockEntityChargePile pile = rig.Pile(2, 2, 0);

    pile.Block.OnBlockBroken(rig.World.World, pile.Pos, EmptyHandedPlayer());

    // It has a furnace but no column, so there is nothing for it to be a window onto.
    Assert.Equal(0, rig.World.GetBlock(pile.Pos).BlockId);
  }

  [Fact]
  public void A_pile_may_not_be_hand_placed_outside_a_shaft() {
    BlockEntityChargePile pile = Orphan(out TestWorld world, out BlockPos pos);
    string failureCode = "";

    bool allowed = pile.Block.CanPlaceBlock(
      world.World,
      EmptyHandedPlayer(),
      new BlockSelection { Position = pos.Copy() },
      ref failureCode
    );

    // It is furnace-materialised, not hand-placed: a pile anywhere else is an orphan that renders nothing.
    Assert.False(allowed);
    Assert.Equal("iwex-chargepile-notinshaft", failureCode);
  }

  [Fact]
  public void The_shaft_gate_is_not_what_refuses_a_cell_inside_a_real_shaft() {
    PileRig rig = Skew();
    string failureCode = "";

    rig.PileBlock.CanPlaceBlock(
      rig.World.World,
      EmptyHandedPlayer(),
      new BlockSelection { Position = rig.Cell(-1, 3, 1) },
      ref failureCode
    );

    // The other direction, so the refusal above is not simply "never". Placement success is not asserted:
    // vanilla's base check then asks whether the cell's current block is replaceable, and the headless
    // world's empty cell is a bare Block with Replaceable 0 rather than game:air's 9999. Only the shaft
    // gate is under test.
    Assert.NotEqual("iwex-chargepile-notinshaft", failureCode);
    Assert.NotNull(rig.Core.ChargeColumnAt(rig.Cell(-1, 3, 1), out _));
  }

  #endregion

  #region Lang

  [Fact]
  public void The_new_block_has_a_name_in_every_shipped_locale() {
    string langDir = DefinitionGoldens.SolutionRelative("assets/iwex/lang");
    string[] locales =
    [
      .. Directory
        .EnumerateFiles(langDir, "*.json")
        .Select(Path.GetFileName)
        .Order()!,
    ];

    // Counted, so a renamed or moved lang file cannot make the loop below run zero times.
    Assert.Equal(["en.json", "ru.json", "uk.json"], locales);

    var missing = new List<string>();
    foreach (string locale in locales) {
      var lang = JObject.Parse(File.ReadAllText(Path.Combine(langDir, locale)));
      foreach (
        string key in new[]
        {
          "block-furnace-chargepile",
          "blockdesc-furnace-chargepile",
          "chargepile-help-take",
          // The retired `chargepile-furnaceowns` key is not listed here; its absence is asserted below.
          "game:ingameerror-iwex-chargepile-notinshaft",
        }
      )
        if (
          lang[key] is not JValue { Type: JTokenType.String } value
          || string.IsNullOrWhiteSpace((string?)value)
        )
          missing.Add($"{locale}: {key}");
    }

    Assert.True(missing.Count == 0, string.Join("\n", missing));

    // The retired refusal is gone from every locale, not merely unreferenced in one: a dead ingameerror
    // row is text no code can print.
    foreach (string locale in locales) {
      var lang = JObject.Parse(File.ReadAllText(Path.Combine(langDir, locale)));
      Assert.Null(lang["game:ingameerror-iwex-chargepile-furnaceowns"]);
    }
  }

  [Fact]
  public void The_name_key_is_exact_because_the_piles_only_variant_group_has_one_state() {
    // One variant group with one state renders exactly one code (`furnace-chargepile`), so the lang key
    // is exact and there is no suffix for a wildcard to cover. A second state or a second group makes it
    // `block-furnace-chargepile*`, which is what LangCoverage would then demand.
    var groups = (JArray)
      ((JObject)BlockChargePile.Definitions("iwex").Single().ToJson())[
        "variantgroups"
      ]!;

    Assert.Single(groups);
    Assert.Equal("type", (string?)groups[0]["code"]);
    Assert.Equal(
      ["chargepile"],
      ((JArray)groups[0]["states"]!).Select(s => (string?)s)
    );
  }

  #endregion
}
