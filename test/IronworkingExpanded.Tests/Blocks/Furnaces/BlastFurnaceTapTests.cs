using System;
using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkMolten;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The blast-furnace tap: an open/closed spout that hands the furnace's molten metal down into the
/// canal start beneath it. Covers the open/closed toggle, persistence, and the <c>TryPourMetal</c>
/// handoff (gated on being open and on a receiving canal start below).
/// </summary>
[Collection(FurnaceConfigCollection.Name)]
public class BlastFurnaceTapTests
{
  private const string Iron = "game:ingot-iron";

  private static TestWorld NewWorld()
  {
    var world = new TestWorld();
    world.RegisterItem(Iron, 1500f);
    return world;
  }

  private static BlockEntityFurnaceTap Tap(
    TestWorld world,
    string side = "north",
    BlockPos? pos = null
  )
  {
    var be = new BlockEntityFurnaceTap
    {
      Pos = pos ?? new BlockPos(0, 12, 0),
      // This used to read `iwex:blastfurnacetap-{side}` - a code no definition has produced since the
      // tap moved out of smex. A stand-in wearing a dead code proves nothing about the live block, which
      // is the same trap SmexToIwexMigrationTests was built on.
      Block = TestBlocks.Configure(
        new Block(),
        $"iwex:furnace-{BlockFurnaceTap.IronType}-{side}",
        1,
        ("type", BlockFurnaceTap.IronType),
        ("side", side)
      ),
    };
    world.Place(be.Pos, be.Block, be);
    world.Attach(be);
    return be;
  }

  /// <summary>Places a canal start in the cell the tap pours into (Pos + side.Opposite, one down).</summary>
  private static BlockEntityMoltenCanalStart CanalBelow(
    TestWorld world,
    BlockEntityFurnaceTap tap,
    string side = "north"
  )
  {
    var facing = BlockFacing.FromCode(side);
    var pos = tap.Pos.AddCopy(facing.Opposite).DownCopy();
    var start = new BlockEntityMoltenCanalStart
    {
      Block = TestBlocks.Configure(
        new Block(),
        "smex:moltencanalstart-ns",
        2,
        ("type", "start"),
        ("orientation", "ns")
      ),
    };
    world.Place(pos, start.Block, start);
    world.Attach(start);
    return start;
  }

  private static ItemStack IronStack(TestWorld world, int units, float temp) =>
    new(world.World.GetItem(new AssetLocation(Iron))!, units)
    {
      // Temperature carrier so the canal start's pour path works.
    };

  #region Toggle / persistence

  [Fact]
  public void Defaults_closed_and_toggles_open()
  {
    var be = Tap(NewWorld());
    Assert.False(be.IsPouring);
    be.TogglePouring();
    Assert.True(be.IsPouring);
    be.TogglePouring();
    Assert.False(be.IsPouring);
  }

  [Fact]
  public void Pour_state_round_trips_through_the_tree()
  {
    var world = NewWorld();
    var src = Tap(world);
    src.TogglePouring();

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var dst = Tap(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.True(dst.IsPouring);
  }

  #endregion

  #region TryPourMetal

  [Fact]
  public void A_closed_tap_pours_nothing()
  {
    var world = NewWorld();
    var tap = Tap(world);
    CanalBelow(world, tap); // present, but tap is closed

    int accepted = tap.TryPourMetal(IronStack(world, 20, 1400f), 1400f);

    Assert.Equal(0, accepted);
  }

  [Fact]
  public void An_open_tap_hands_metal_to_the_canal_start_below()
  {
    var world = NewWorld();
    var tap = Tap(world);
    var canal = CanalBelow(world, tap);
    tap.TogglePouring();

    int accepted = tap.TryPourMetal(IronStack(world, 20, 1400f), 1400f);

    Assert.True(accepted > 0);
    Assert.True(canal.CellAmount > 0);
    Assert.Equal(Iron, canal.CellMetalType);
  }

  [Fact]
  public void An_open_tap_over_nothing_pours_nothing()
  {
    var world = NewWorld();
    var tap = Tap(world);
    tap.TogglePouring(); // open, but no canal beneath

    Assert.Equal(0, tap.TryPourMetal(IronStack(world, 20, 1400f), 1400f));
  }

  #endregion

  #region Orientation (pour target tracks the side)

  /// <summary>
  /// The tap is a plain oriented block, not a multiblock, so it carries no structure offsets - its one
  /// rotating piece is the pour target, <c>Pos + FromCode(side).Opposite</c> one down. The north case
  /// above pins the <c>Opposite</c>; this stands the tap up facing each side and confirms the pour lands
  /// in that side's target cell (four distinct cells: N->+z, S->-z, E->-x, W->+x). A tap that ignored its
  /// side would keep pouring into north's cell and find no canal at the other three, so every side but
  /// north would fail.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void An_open_tap_pours_into_the_cell_its_side_faces_away_from(
    string side
  )
  {
    var world = NewWorld();
    var tap = Tap(world, side);
    var canal = CanalBelow(world, tap, side);
    tap.TogglePouring();

    int accepted = tap.TryPourMetal(IronStack(world, 20, 1400f), 1400f);

    Assert.True(accepted > 0);
    Assert.True(canal.CellAmount > 0);
  }

  #endregion

  #region Throughput

  /// <summary>
  /// D5b: <c>DrainIronTap</c> used to clamp its per-tick pool draw at a hard-coded 20
  /// (<c>Math.Min(20, _moltenIron)</c>), invisible to config and capping the iron tap at 12 u/s no
  /// matter how the furnace was tuned - the molten network's own settled rate is far higher, and the
  /// casting bed already honoured it. This proves <see cref="IwexValues.TapDrainPerTick"/> now drives
  /// that draw.
  /// <para>
  /// F4: retuned to <b>45</b>, not the shipped default (50) - a test that sets config to its own default
  /// value proves nothing about config-liveness (it would pass identically against a hard-coded
  /// <c>Math.Min(50, ...)</c>). With 500 units banked and the tap retuned to 45/tick, the iron tap
  /// accepts <c>Ceiling(45 * TapIronStackFactor)</c> units. <c>45 * 0.6f</c> is <b>not</b> the
  /// mathematically-clean 27: <c>0.6f</c> is fractionally above 0.6 in IEEE-754
  /// (<c>0.60000002384185791015625</c>), so the float product lands at <c>27.000002</c> and
  /// <c>Math.Ceiling</c> carries it to <b>28</b> - the same float-representation trap D5b's own comment
  /// flags for 50 (<c>Ceiling(50 * 0.6f) = 31</c>, not 30). Verified directly with a temporary
  /// <c>TapDrainPerTick = 20</c> run (the pre-fix hard-coded ceiling): it produces <c>Ceiling(20 * 0.6f)
  /// = 12</c>, a different number from both 28 and 31, confirming the assertion below tracks the config
  /// value and not a coincidence of arithmetic.
  /// </para>
  /// </summary>
  [Fact]
  public void Retuning_the_tap_rate_moves_the_drain_with_it()
  {
    var world = NewWorld();
    // MetalRegistry.MoltenItemOf("pigiron") unregistered -> convention "game:ingot-<code>", matching
    // how Iron above stands in for MetalRegistry.MoltenItemOf("iron").
    world.RegisterItem("game:ingot-pigiron", 1500f);

    var furnace = new BlockEntityBlastFurnaceCold
    {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "iwex:furnace-blastcore-tier1-n",
        1,
        ("side", "north")
      ),
    };
    world.Attach(furnace);
    // The furnace has to carry its shipped layout now: since the tap-cell migration the drain point is
    // the drawing's answer (CellRole.MetalTap), not a Vec3i literal on the class, so a block with no
    // attributes has no tap and DrainIronTap returns before it looks. StructureRig.Around attaches them;
    // nothing is raised, because completion is not what this test is about.
    StructureRig.Around(
      world,
      furnace,
      BlockBlastFurnaceCoreCold.Definitions("iwex").Single()
    );
    ReflectionHelpers.Invoke(furnace, "UpdateStructureRotation");
    ReflectionHelpers.Invoke(furnace, "CacheAttributes");

    // The lower (metal) tap's world cell, read exactly as DrainIronTap reads it - not hard-coded, so this
    // rig tracks the metal tap if the drawing ever moves it.
    BlockPos metalTapPos = furnace.MetalTapPos!;

    var tap = Tap(world, pos: metalTapPos);
    var canal = CanalBelow(world, tap);
    tap.TogglePouring();

    ReflectionHelpers.SetField(furnace, "_moltenIron", 500f);

    int original = IwexValues.TapDrainPerTick;
    IwexValues.Edit(c => c.TapDrainPerTick = 45);
    try
    {
      ReflectionHelpers.Invoke(furnace, "DrainProducts", false);
    }
    finally
    {
      IwexValues.Edit(c => c.TapDrainPerTick = original);
    }

    // 45 (config, not the shipped default of 50) capped units, x 0.6f (TapIronStackFactor default) stack
    // factor, ceiling; see the doc comment above for why this is 28, not the mathematically-clean 27.
    Assert.Equal(28, canal.CellAmount);
    Assert.Equal(
      472f,
      (float)ReflectionHelpers.GetField(furnace, "_moltenIron")!,
      3
    );
  }

  /// <summary>
  /// F5: mirrors <see cref="Retuning_the_tap_rate_moves_the_drain_with_it"/> exactly, but for
  /// <c>DrainSlagTap</c> / <c>_moltenSlag</c> / <see cref="IwexValues.TapSlagStackFactor"/>. Nothing in
  /// the tree pinned an exact value for the slag tap before this - a copy-paste slip in
  /// <c>DrainSlagTap</c> that read <c>TapIronStackFactor</c> (0.6f) instead of its own
  /// <c>TapSlagStackFactor</c> (0.8f default) would have gone uncaught by every existing test, iron's
  /// included (they only ever exercise the metal tap). With 500 units banked and the tap retuned to
  /// 45/tick (same non-default value as the iron test, so this is not re-proving F4's config-liveness
  /// point, only pinning the slag-specific factor): <c>Ceiling(45 * 0.8f) = 36</c> exactly - <c>0.8f</c>
  /// is also fractionally above 0.8 in IEEE-754 (<c>0.80000001192092896</c>), but at 45 units the float
  /// product rounds back to a clean 36.0, unlike the iron factor's 45 * 0.6f case. Verified against the
  /// wrong-factor mutation directly (see the fix report).
  /// </summary>
  [Fact]
  public void The_slag_tap_drains_by_its_own_stack_factor_not_the_irons()
  {
    var world = NewWorld();
    // MetalRegistry.MoltenItemOf("slag") -> iwex:slag when a MetalDef is registered (process-wide state),
    // else the game:ingot-slag convention - register both spellings so this does not depend on registry
    // state left by another test, same reasoning as the pigiron double-registration above.
    world.RegisterItem("iwex:slag", 1500f);
    world.RegisterItem("game:ingot-slag", 1500f);

    var furnace = new BlockEntityBlastFurnaceCold
    {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "iwex:furnace-blastcore-tier1-n",
        1,
        ("side", "north")
      ),
    };
    world.Attach(furnace);
    // The shipped layout, for the reason spelled out on the iron test above.
    StructureRig.Around(
      world,
      furnace,
      BlockBlastFurnaceCoreCold.Definitions("iwex").Single()
    );
    ReflectionHelpers.Invoke(furnace, "UpdateStructureRotation");
    ReflectionHelpers.Invoke(furnace, "CacheAttributes");

    // The higher (slag) tap's world cell, read exactly as DrainSlagTap reads it.
    BlockPos slagTapPos = furnace.SlagTapPos!;

    var tap = Tap(world, pos: slagTapPos);
    var canal = CanalBelow(world, tap);
    tap.TogglePouring();

    ReflectionHelpers.SetField(furnace, "_moltenSlag", 500f);

    int original = IwexValues.TapDrainPerTick;
    IwexValues.Edit(c => c.TapDrainPerTick = 45);
    try
    {
      ReflectionHelpers.Invoke(furnace, "DrainProducts", false);
    }
    finally
    {
      IwexValues.Edit(c => c.TapDrainPerTick = original);
    }

    // 45 capped units x 0.8f (TapSlagStackFactor default) stack factor, ceiling = 36 exactly (see the
    // doc comment above). If DrainSlagTap ever regresses to TapIronStackFactor (0.6f) this reads 28,
    // not 36.
    Assert.Equal(36, canal.CellAmount);
    Assert.Equal(
      464f,
      (float)ReflectionHelpers.GetField(furnace, "_moltenSlag")!,
      3
    );
  }

  #endregion

  #region Drops / pick

  /// <summary>The four real facings of one tap type, registered so <c>world.GetBlock(code)</c> can
  /// resolve the normalised one. Returns the <c>-w</c> block: any non-canonical facing will do, and west
  /// is the one the cold blast furnace's own layout places.</summary>
  private static BlockFurnaceTap FourFacings(TestWorld world, string type, int baseId)
  {
    BlockFurnaceTap? west = null;
    int id = baseId;
    foreach (string side in new[] { "n", "e", "s", "w" })
    {
      var block = TestBlocks.Configure(
        new BlockFurnaceTap(),
        $"iwex:furnace-{type}-{side}",
        id++,
        ("type", type),
        ("side", side)
      );
      world.Register(block);
      if (side == "w")
        west = block;
    }
    return west!;
  }

  /// <summary>
  /// <b>The normalisation named a code no definition produces, and the <c>?? this</c> fallback ate
  /// it.</b> <c>NormalisedStack</c> asked for <c>CodeWithVariant("side", "south")</c> while the
  /// <c>side</c> group renders single letters, so <c>GetBlock</c> answered null, the fallback handed back
  /// the block's own facing, and a mined tap split the inventory four ways - with nothing failing. The
  /// method's own doc comment claimed the derive-from-my-own-code shape had made exactly this
  /// unrepeatable; it had not, because nothing asserted the result.
  /// </summary>
  [Theory]
  [InlineData(BlockFurnaceTap.IronType)]
  [InlineData(BlockFurnaceTap.SlagType)]
  public void A_mined_tap_comes_back_at_the_canonical_facing(string type)
  {
    var world = NewWorld();
    BlockFurnaceTap west = FourFacings(world, type, 500);
    var pos = new BlockPos(0, 12, 0);

    Assert.Equal(
      $"iwex:furnace-{type}-s",
      west.OnPickBlock(world.World, pos).Collectible.Code.ToString()
    );
    Assert.Equal(
      $"iwex:furnace-{type}-s",
      west.GetDrops(world.World, pos, null!)[0].Collectible.Code.ToString()
    );
  }

  /// <summary>The <b>type</b> rides through the normalisation: an iron tap must never come back as a
  /// slag tap. Both are the same class at the same facings, so a normaliser that rebuilt the code from a
  /// literal instead of from <c>CodeWithVariant</c> would swap them and look right.</summary>
  [Fact]
  public void Normalising_the_facing_never_changes_the_tap_type()
  {
    var world = NewWorld();
    BlockFurnaceTap iron = FourFacings(world, BlockFurnaceTap.IronType, 500);
    BlockFurnaceTap slag = FourFacings(world, BlockFurnaceTap.SlagType, 600);
    var pos = new BlockPos(0, 12, 0);

    Assert.Equal(
      BlockFurnaceTap.IronType,
      iron.OnPickBlock(world.World, pos).Collectible.Variant["type"]
    );
    Assert.Equal(
      BlockFurnaceTap.SlagType,
      slag.OnPickBlock(world.World, pos).Collectible.Variant["type"]
    );
  }

  /// <summary>
  /// <b>The tap's canonical facing is <c>s</c>, and it deliberately disagrees with
  /// <c>BlockBehaviorExOrientable</c>, whose canonical is the scheme's first token - <c>n</c>.</b> The
  /// tap keeps its own <c>OnPickBlock</c>/<c>GetDrops</c> overrides and never calls base, so the
  /// behaviour's version never runs on this block.
  /// <para>
  /// Pinned as a three-way agreement rather than described in a comment, because the cost of the two
  /// answers drifting apart is invisible: a crafted tap and a mined one would simply stop stacking. If a
  /// later change moves the tap onto the behaviour's canonical, this test is the one that says which
  /// other two facts have to move with it.
  /// </para>
  /// </summary>
  [Fact]
  public void The_creative_entry_the_grid_recipe_and_the_drop_all_name_the_south_facing()
  {
    var world = NewWorld();
    BlockFurnaceTap west = FourFacings(world, BlockFurnaceTap.IronType, 500);

    // 1. what a mined/picked tap becomes
    Assert.Equal(
      "iwex:furnace-irontap-s",
      west.OnPickBlock(world.World, new BlockPos(0, 12, 0))
        .Collectible.Code.ToString()
    );

    // 2. the creative-tab entry
    string creative = (string)
      BlockFurnaceTap.Definitions("iwex")
        .First()
        .ToJson()["creativeinventory"]!["general"]!
        .First()!;
    Assert.EndsWith("-s", creative);

    // 3. every grid recipe in the whole mod that outputs a furnace tap - read from the recipe defs
    // rather than from one file, so a second tap recipe added elsewhere is covered without an edit.
    string[] tapOutputs =
    [
      .. RecipeCodes
        .OutputBlockCodes("iwex", typeof(BlockFurnaceTap).Assembly)
        .Where(c =>
          c.StartsWith("iwex:furnace-irontap", StringComparison.Ordinal)
          || c.StartsWith("iwex:furnace-slagtap", StringComparison.Ordinal)
        ),
    ];
    Assert.Equal(2, tapOutputs.Length);
    Assert.All(tapOutputs, c => Assert.EndsWith("-s", c));
  }

  #endregion
}
