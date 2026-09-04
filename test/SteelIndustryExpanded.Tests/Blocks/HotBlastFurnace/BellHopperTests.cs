using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using IronIndustryExpanded.Tests;
using SteelIndustryExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelIndustryExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The bell hopper pulls ready-made burden from the reinforced tank above into its magazine and drips
/// that down the furnace shaft. Covers the magazine and dropping persistence, the furnace-full check,
/// the pull-from-tank feed, and the drip into a shaft pile with the grade preserved.
/// </summary>
public class BellHopperTests {
  private static readonly BlockPos BellPos = new(0, 16, 0);

  private static BlockEntityHopperBell Bell(TestWorld world) {
    var be = new BlockEntityHopperBell {
      Pos = BellPos,
      Block = TestBlocks.Configure(new Block(), "iiex:hopperbell", 90),
    };
    world.Place(BellPos, be.Block, be);
    world.Attach(be);
    return be;
  }

  /// <summary>
  /// The reinforced tank in the cell directly above the bell - the furnace's own <c>R</c> cell. It only
  /// loads over a standing furnace: the tank keeps no list of what is chargeable and asks the anchored
  /// core, so a hopper over nothing accepts nothing and loads zero.
  /// </summary>
  private static BlockEntityHopperReinforced HopperAbove(
    TestWorld world,
    BlockPos bellPos,
    Item burden,
    int units
  ) {
    var be = new BlockEntityHopperReinforced {
      Pos = bellPos.UpCopy(),
      Block = TestBlocks.Configure(new Block(), "siex:hopperreinforced", 91),
    };
    world.Place(be.Pos, be.Block, be);
    world.Attach(be);
    if (units > 0)
      Assert.True(
        be.TryDeposit(
          new DummySlot(new ItemStack(burden, units)),
          wholeStack: true
        ),
        "the tank must actually take the load, or the case that follows tests nothing"
      );
    return be;
  }

  /// <summary>
  /// A standing hot blast furnace with a real bell hopper in the cell its drawing puts one in (local
  /// <c>(0,7,0)</c>) and the reinforced tank above it. The bell has no geometry of its own: it resolves
  /// the core through the multiblock anchor and asks it which column to lay on, so a bare bell over a
  /// hand-placed pile expresses none of its behaviour.
  /// </summary>
  private static (
    BlockEntityBlastFurnaceHot core,
    StructureRig rig,
    BlockEntityHopperBell bell
  ) Standing() {
    var core = new BlockEntityBlastFurnaceHot();
    StructureRig rig = FurnaceLayoutRig.Stand(
      core,
      BlockBlastFurnaceCoreHot.Definitions("siex").Single(),
      new BlockPos(0, 16, 0),
      "siex:blastfurnacecore",
      "north"
    );

    rig.World.RegisterItem("iiex:burden");
    rig.World.RegisterItem("game:coke");
    rig.World.RegisterBlockEntityFactory(
      "iiex.BlockEntityChargePile",
      () => new BlockEntityChargePile()
    );
    Block pile = TestBlocks.Configure(
      new Block(),
      BlockChargePile.PileCode.ToShortString(),
      950,
      [("type", "chargepile")]
    );
    pile.EntityClass = "iiex.BlockEntityChargePile";
    rig.World.Register(pile);

    // The cell and the code it wants come off the raised structure rather than being hand-written, so a
    // redrawn layout moves the fixture with it. A structure that never completes resolves no anchor.
    var (bellPos, wanted) = rig.Cells.Single(c =>
      c.Wanted.Contains("hopperbell", System.StringComparison.Ordinal)
    );
    var bell = new BlockEntityHopperBell {
      Pos = bellPos.Copy(),
      Block = TestBlocks.Configure(new Block(), wanted, 90),
    };
    rig.World.Place(bellPos, bell.Block, bell);
    rig.World.Attach(bell);

    return (core, rig, bell);
  }

  private static Item BurdenItem(StructureRig rig) =>
    rig.World.World.GetItem(new AssetLocation("iiex:burden"))!;

  /// <summary>Every column of the furnace's shaft, in the ascending-<c>(x, z)</c> order the selection rule
  /// breaks its ties on.</summary>
  private static List<ChargeColumn> Columns(BlockEntityBlastFurnaceHot core) =>
    [
      .. core
        .ShaftColumns.OrderBy(kv => kv.Key.X)
        .ThenBy(kv => kv.Key.Z)
        .Select(kv => kv.Value),
    ];

  #region Default state

  [Fact]
  public void A_freshly_placed_bell_hopper_is_dropping_by_default() {
    var bell = Bell(new TestWorld());
    Assert.True(bell.IsDropping);
  }

  [Fact]
  public void Dropping_defaults_on_when_a_saved_tree_omits_the_flag() {
    var world = new TestWorld();
    var bell = Bell(world);
    ReflectionHelpers.SetField(bell, "_isDropping", false);

    bell.FromTreeAttributes(new TreeAttribute(), world.World); // legacy tree, no key

    Assert.True(bell.IsDropping);
  }

  [Fact]
  public void An_explicitly_stopped_bell_stays_stopped_across_a_reload() {
    var world = new TestWorld();
    var src = Bell(world);
    src.IsDropping = false;

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var dst = Bell(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.False(dst.IsDropping);
  }

  #endregion

  #region Persistence

  [Fact]
  public void Magazine_and_dropping_round_trip_through_the_tree() {
    var world = new TestWorld();
    var burden = world.RegisterItem("iiex:burden");
    var src = Bell(world);
    ReflectionHelpers.SetField(src, "_magazine", new ItemStack(burden, 24));
    src.IsDropping = true;

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var dst = Bell(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.Equal(24, dst.BlastMixMagazine);
    Assert.True(dst.IsDropping);
  }

  #endregion

  #region Furnace-full check

  [Fact]
  public void A_bell_over_no_furnace_reads_the_shaft_as_not_full() {
    // With no shaft, "is the shaft full" reads false. The drip refuses separately, on the same missing
    // anchor.
    Assert.False(Bell(new TestWorld()).IsFurnaceFull());
  }

  [Fact]
  public void The_shaft_reads_full_only_when_EVERY_column_is_at_its_own_capacity() {
    var (core, _, bell) = Standing();

    Assert.False(bell.IsFurnaceFull(), "an empty shaft is not full");

    // Capacity is per column. The hot furnace's crucible course makes two of its columns a cell taller
    // than the rest, so filling every column to the shallowest one's ceiling leaves real room and must
    // still read as not full.
    var keys = new List<(int X, int Z)>(core.ShaftColumns.Keys);
    keys.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
    int shallowest = int.MaxValue;
    foreach (var (x, z) in keys)
      shallowest = System.Math.Min(shallowest, core.ColumnCapacity(x, z));

    foreach (var (x, z) in keys)
      core.ChargeColumnAt(x, z)!.Push("iiex:burden", shallowest, 20f, default);
    Assert.False(
      bell.IsFurnaceFull(),
      "a column with room left means the shaft is not full, however many others are topped out"
    );

    foreach (var (x, z) in keys) {
      ChargeColumn column = core.ChargeColumnAt(x, z)!;
      int room = core.ColumnCapacity(x, z) - column.TotalUnits;
      if (room > 0)
        column.Push("iiex:burden", room, 20f, default);
    }

    Assert.True(bell.IsFurnaceFull());
  }

  #endregion

  #region Feed + drip

  [Fact]
  public void OnServerTick_pulls_burden_from_the_tank_above_into_the_magazine() {
    var (core, rig, bell) = Standing();
    Item burden = BurdenItem(rig);
    var hopper = HopperAbove(rig.World, bell.Pos, burden, 30);

    ReflectionHelpers.Invoke(bell, "OnServerTick", 1f);

    // The whole tank moves up in one pull...
    Assert.Equal(0, hopper.TankCount);
    // ...and the same tick drips one drop onward, so the 30 splits between the magazine and the shaft.
    int drop = SiexValues.HopperDropAmount;
    Assert.Equal(30 - drop, bell.BlastMixMagazine);
    Assert.Equal(drop, core.ShaftChargeUnits);
  }

  [Fact]
  public void OnServerTick_does_nothing_with_an_empty_tank_above() {
    var (core, rig, bell) = Standing();
    HopperAbove(rig.World, bell.Pos, BurdenItem(rig), 0);

    ReflectionHelpers.Invoke(bell, "OnServerTick", 1f);

    Assert.Equal(0, bell.BlastMixMagazine);
    Assert.Equal(0, core.ShaftChargeUnits);
  }

  [Fact]
  public void OnServerTick_drips_the_pulled_burden_onto_the_shafts_lowest_column() {
    var (core, rig, bell) = Standing();
    Item burden = BurdenItem(rig);
    HopperAbove(rig.World, bell.Pos, burden, 30);

    ReflectionHelpers.Invoke(bell, "OnServerTick", 1f);

    int drop = SiexValues.HopperDropAmount;
    Assert.Equal(drop, core.ShaftChargeUnits); // one drop landed in the shaft
    Assert.Equal(30 - drop, bell.BlastMixMagazine); // the magazine fell by exactly the same

    // On an empty shaft every column is equally low, so the tie breaks on ascending (x, z): the first
    // key and nothing else. A bell that dropped straight down would charge its own column.
    List<ChargeColumn> columns = Columns(core);
    Assert.Equal(drop, columns[0].TotalUnits);
    Assert.All(columns.Skip(1), c => Assert.Equal(0, c.TotalUnits));

    // The grade rides along: the column holds the burden the tank was loaded with, not a count-only
    // charge.
    Assert.Equal("iiex:burden", columns[0].Segments[0].Material);
  }

  [Fact]
  public void The_bell_and_the_tall_hopper_fill_the_shaft_by_the_SAME_rule() {
    // Both hoppers delegate to `BlockEntityFurnaceCore.NextChargeColumn`, so the shaft fills in level
    // courses whichever one loads it. A divergence between them is visible from neither side alone.
    var (core, rig, bell) = Standing();
    Item burden = BurdenItem(rig);
    int columnCount = core.ShaftColumns.Count;
    int drop = SiexValues.HopperDropAmount;
    HopperAbove(rig.World, bell.Pos, burden, drop * columnCount);

    for (int i = 0; i < columnCount; i++)
      ReflectionHelpers.Invoke(bell, "OnServerTick", 1f);

    Assert.All(Columns(core), c => Assert.Equal(drop, c.TotalUnits));
  }

  [Fact]
  public void A_full_shaft_holds_the_magazine_rather_than_eating_it() {
    var (core, rig, bell) = Standing();
    Item burden = BurdenItem(rig);
    HopperAbove(rig.World, bell.Pos, burden, 30);

    foreach (var (x, z) in core.ShaftColumns.Keys)
      core.ChargeColumnAt(x, z)!
        .Push("iiex:burden", core.ColumnCapacity(x, z), 20f, default);
    int full = core.ShaftChargeUnits;

    ReflectionHelpers.Invoke(bell, "OnServerTick", 1f);

    // Both halves: a bell that held by pushing above a column's roof would leave the magazine untouched
    // here too, while inventing charge no block can draw.
    Assert.Equal(30, bell.BlastMixMagazine);
    Assert.Equal(full, core.ShaftChargeUnits);
  }

  #endregion

  #region Break drops

  [Fact]
  public void Breaking_a_loaded_bell_returns_the_burden_it_is_actually_holding() {
    // The drop is the magazine's own stack, so the item and the grade both come back as loaded.
    var (_, rig, bell) = Standing();
    Item burden = BurdenItem(rig);
    var loaded = new ItemStack(burden, 24);
    Burden.Write(loaded, new BurdenMix(75f, 5f, 20f));
    ReflectionHelpers.SetField(bell, "_magazine", loaded);

    var block =
      new SteelIndustryExpanded.BlockStructures.HotBlastFurnace.Blocks.BlockHopperBell();
    ItemStack[] drops = block.GetDrops(rig.World.World, bell.Pos, null);

    ItemStack magazine = Assert.Single(
      drops,
      d => d.Collectible?.Code?.ToShortString() == "iiex:burden"
    );
    Assert.Equal(24, magazine.StackSize);
    // The grade comes back with it.
    Assert.Equal(0.20f, Burden.Read(magazine).FuelFrac, 3);
  }

  #endregion
}
