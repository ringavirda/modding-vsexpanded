using System.Linq;
using ExpandedLib.Structures;
using ExpandedLib.Industry.Metals;
using ExpandedLib.Industry.Molten;
using ExpandedLib.Testing;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockNetworkMolten.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.BlockStructures.Products.BlockEntities;
using IronIndustryExpanded.Items;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The hearth as live molten cells. The crucible floor is no longer two floats on the furnace that a
/// shutdown stamps into a block: melting puts <c>iiex:hearthmetal-{metal}</c> into the layout's
/// <see cref="ExpandedLib.Structures.CellRole"/> pool cells the moment it starts, the metal lives
/// in that block's own molten cells for the campaign, and freezing is the cell's own thermal latch rather
/// than an extinguish-time stamp. See docs/internal/plans/2026-08-04-iwex-u2-u10-expansion.md § U4.4.
/// <para>
/// None of these raise the structure: completion is not what is under test, and forcing it would couple
/// every case to the layout rig's build rules.
/// </para>
/// </summary>
public class HearthMetalCellTests {
  private const string IronCellKey = "hm_iron_";
  private const string SlagCellKey = "hm_slag_";
  private const string HearthCode = "iiex:hearthmetal-pigiron";

  #region Rig

  /// <summary>
  /// A blast furnace carrying its shipped layout, so <c>PoolCells</c> comes from the drawing rather than
  /// a literal offset, plus the hearth block registered with a factory that hosts the two molten cells.
  /// The headless world builds block entities from registered factories rather than from a block def, so
  /// the cells are configured here the way the def's <c>behaviors</c> array configures them in game.
  /// </summary>
  private static (TestWorld World, BlockEntityBlastFurnaceCold Furnace) Rig() {
    var world = new TestWorld();
    world.RegisterItem(
      ExpandedLib.Industry.Metals.MetalRegistry.MoltenItemOf("pigiron").ToString(),
      1500f
    );
    world.RegisterItem("iiex:slag", 1500f);
    world.RegisterItem("game:ingot-slag", 1500f);
    // The chisel recovery resolves through MetalRegistry.SolidDropOf; with no bit item registered it
    // yields null and the chisel case reads as "nothing was frozen" rather than "nothing resolved".
    world.RegisterItem(
      ExpandedLib.Industry.Metals.MetalRegistry.SolidDropOf(
          ExpandedLib.Industry.Metals.MetalRegistry.MoltenItemOf("pigiron")
        )
        .ToString()
    );

    HearthRig.Register(world, HearthCode, 951);

    var furnace = new BlockEntityBlastFurnaceCold {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "iiex:furnace-blastcore-tier1-n",
        1,
        ("side", "north")
      ),
    };
    world.Attach(furnace);
    StructureRig.Around(
      world,
      furnace,
      BlockBlastFurnaceCoreCold.Definitions("iiex").Single()
    );
    furnace.ApplyStructureRotation();
    ReflectionHelpers.Invoke(furnace, "CacheAttributes");
    return (world, furnace);
  }

  /// <summary>
  /// Drives one melt directly, the way the smex lifecycle suite does - the furnace's own columns, no tick
  /// loop and no completion. The burden is laid in first and laid in hot: the melt condition is per band
  /// and reads the heat the band carried down the shaft, not the furnace's own, so cold burden renders
  /// nothing and the crucible stays empty.
  /// </summary>
  private static void Melt(BlockEntityBlastFurnaceCold furnace, int units) {
    ChargeColumn column = furnace.ChargeColumnAt(0, 0)!;
    if (column.TotalUnits < units)
      column.Push(
        "iiex:burden",
        units * 2,
        IiexValues.BfIronMeltingPoint + 100f,
        new BurdenMix(65f, 5f, 30f)
      );

    ReflectionHelpers.Invoke(
      furnace,
      "ConsumeForMelting",
      furnace.ShaftColumns,
      units
    );
  }

  private static BEBehaviorMoltenCell? IronCell(
    TestWorld world,
    BlockPos pos
  ) => world.Accessor.GetBlockEntity(pos).MoltenCell(IronCellKey);

  #endregion

  #region The pool becomes blocks

  [Fact]
  public void Melting_puts_hearth_metal_into_every_pool_cell() {
    var (world, furnace) = Rig();

    Melt(furnace, 200);

    // The drawing decides where the crucible floor is; the test follows it rather than naming cells.
    Assert.NotEmpty(furnace.PoolCells);
    Assert.All(
      furnace.PoolCells,
      pos =>
        Assert.Equal(
          HearthCode,
          world.Accessor.GetBlock(pos)?.Code?.ToShortString()
        )
    );
    Assert.True(
      furnace.PoolCells.Sum(p => IronCell(world, p)?.CellAmount ?? 0) > 0
    );
  }

  [Fact]
  public void A_second_melt_raises_the_same_block_rather_than_replacing_it() {
    var (world, furnace) = Rig();

    Melt(furnace, 200);
    int after1 = furnace.PoolCells.Sum(p =>
      IronCell(world, p)?.CellAmount ?? 0
    );
    Melt(furnace, 200);
    int after2 = furnace.PoolCells.Sum(p =>
      IronCell(world, p)?.CellAmount ?? 0
    );

    // A campaign is one hearth block filling, not a block replaced per cycle - replacing it would reset
    // the cell and silently discard the previous cycle's metal.
    Assert.True(after1 > 0);
    Assert.True(after2 > after1);
  }

  #endregion

  #region Draining and freezing

  [Fact]
  public void Opening_the_iron_tap_drains_the_cells() {
    var (world, furnace) = Rig();
    Melt(furnace, 400);
    int before = furnace.PoolCells.Sum(p =>
      IronCell(world, p)?.CellAmount ?? 0
    );

    BlockEntityFurnaceTap tap = Tap(world, furnace.MetalTapPos!);
    BlockEntityMoltenCanalStart canal = CanalBelow(world, tap);
    tap.SetPlugged(false);
    ReflectionHelpers.Invoke(furnace, "DrainProducts", false);

    int after = furnace.PoolCells.Sum(p => IronCell(world, p)?.CellAmount ?? 0);
    Assert.True(after < before);
    Assert.True(canal.CellAmount > 0);
  }

  /// <summary>The iron tap at the layout's own metal-tap cell, coded as a shipped definition emits it.</summary>
  private static BlockEntityFurnaceTap Tap(TestWorld world, BlockPos pos) {
    var be = new BlockEntityFurnaceTap {
      Pos = pos,
      Block = TestBlocks.Configure(
        new Block(),
        $"iiex:furnace-{BlockFurnaceTap.IronType}-north",
        2,
        ("type", BlockFurnaceTap.IronType),
        ("side", "north")
      ),
    };
    world.Place(be.Pos, be.Block, be);
    world.Attach(be);
    return be;
  }

  /// <summary>A canal start in the cell the tap pours into, so the drain has somewhere to go.</summary>
  private static BlockEntityMoltenCanalStart CanalBelow(
    TestWorld world,
    BlockEntityFurnaceTap tap
  ) {
    BlockPos pos = tap.Pos.AddCopy(BlockFacing.NORTH.Opposite).DownCopy();
    var start = new BlockEntityMoltenCanalStart {
      Block = TestBlocks.Configure(
        new Block(),
        "iiex:moltencanalstart-ns",
        3,
        ("type", "start"),
        ("orientation", "ns")
      ),
    };
    world.Place(pos, start.Block, start);
    world.Attach(start);
    return start;
  }

  [Fact]
  public void Freezing_keeps_every_unit_that_was_liquid() {
    var (world, furnace) = Rig();
    Melt(furnace, 400);
    int liquid = furnace.PoolCells.Sum(p =>
      IronCell(world, p)?.CellAmount ?? 0
    );

    foreach (BlockPos pos in furnace.PoolCells)
      Freeze(IronCell(world, pos)!, world.World);

    // Nothing is lost at the freeze: the latch is a state change on the same cell, not a re-stamp into a
    // block whose amount is recomputed.
    Assert.All(
      furnace.PoolCells,
      p => Assert.True(IronCell(world, p)!.Solidified)
    );
    Assert.Equal(
      liquid,
      furnace.PoolCells.Sum(p => IronCell(world, p)?.CellAmount ?? 0)
    );
  }

  [Fact]
  public void A_hearth_block_with_no_furnace_above_it_still_cools_and_latches() {
    var world = new TestWorld();
    world.RegisterItem(
      ExpandedLib.Industry.Metals.MetalRegistry.MoltenItemOf("pigiron").ToString(),
      1500f
    );
    Block hearth = TestBlocks.Configure(new Block(), HearthCode, 951);
    var be = (BlockEntityHearthMetal)HearthRig.NewHearth();
    world.Place(new BlockPos(0, 1, 0), hearth, be);
    world.Attach(be);

    BEBehaviorMoltenCell cell = be.MoltenCell(IronCellKey)!;
    cell.PushMetalRaw(
      100,
      ExpandedLib.Industry.Metals.MetalRegistry.MoltenItemOf("pigiron").ToString(),
      1500f,
      world.World
    );

    Freeze(cell, world.World);

    // A hearth block outlives the furnace that made it - a player can break the shaft off around it - so
    // its thermal tick has to be its own rather than the furnace's.
    Assert.True(cell.Solidified);
    Assert.Equal(100, cell.CellAmount);
  }

  #endregion

  #region Chiselling it back out

  [Fact]
  public void A_hardened_hearth_block_chisels_out_what_it_froze() {
    var (world, furnace) = Rig();
    Melt(furnace, 400);

    BlockPos pos = furnace.PoolCells[0];
    var be = (BlockEntityHearthMetal)world.Accessor.GetBlockEntity(pos);
    BEBehaviorMoltenCell cell = be.MoltenCell(IronCellKey)!;
    int frozen = cell.CellAmount;
    Freeze(cell, world.World);

    var chiselable = (IChiselableMolten)be;
    Assert.True(chiselable.HasChiselableContent);
    Assert.True(chiselable.CanChiselOut);

    ItemStack? recovered = chiselable.ChiselOut();
    Assert.NotNull(recovered);
    // The recovery is metal bits at the cell's own units-per-bit; what matters here is that the frozen
    // amount comes back rather than a fixed drop count.
    Assert.True(recovered!.StackSize > 0);
    Assert.Equal(0, cell.CellAmount);
    Assert.True(frozen > 0);
  }

  #endregion

  #region Cell helpers

  /// <summary>
  /// Cools a cell to ambient and runs the thermal update, which is what latches <c>Solidified</c>. The
  /// temperature lives on the cell's carrier stack rather than in <c>_cellTemperature</c> - the field is
  /// a mirror the update overwrites - so the stack is what a test has to cool.
  /// </summary>
  private static void Freeze(BEBehaviorMoltenCell cell, IWorldAccessor world) {
    ReflectionHelpers.Invoke(cell, "SetStackTemperature", world, 20f);
    cell.UpdateThermal(world);
  }

  #endregion
}
