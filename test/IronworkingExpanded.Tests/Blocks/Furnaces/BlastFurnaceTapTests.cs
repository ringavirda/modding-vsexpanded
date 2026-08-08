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
public class BlastFurnaceTapTests {
  private const string Iron = "game:ingot-iron";

  private static TestWorld NewWorld() {
    var world = new TestWorld();
    world.RegisterItem(Iron, 1500f);
    return world;
  }

  private static BlockEntityFurnaceTap Tap(
    TestWorld world,
    string side = "north",
    BlockPos? pos = null
  ) {
    var be = new BlockEntityFurnaceTap {
      Pos = pos ?? new BlockPos(0, 12, 0),
      // The code has to be one the shipped definitions actually produce; a stand-in wearing a code no def
      // emits says nothing about the live block.
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
  ) {
    var facing = BlockFacing.FromCode(side);
    var pos = tap.Pos.AddCopy(facing.Opposite).DownCopy();
    var start = new BlockEntityMoltenCanalStart {
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
    new(world.World.GetItem(new AssetLocation(Iron))!, units) {
      // Temperature carrier so the canal start's pour path works.
    };

  #region Toggle / persistence

  [Fact]
  public void Defaults_closed_and_toggles_open() {
    var be = Tap(NewWorld());
    Assert.False(be.IsPouring);
    be.TogglePouring();
    Assert.True(be.IsPouring);
    be.TogglePouring();
    Assert.False(be.IsPouring);
  }

  [Fact]
  public void Pour_state_round_trips_through_the_tree() {
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
  public void A_closed_tap_pours_nothing() {
    var world = NewWorld();
    var tap = Tap(world);
    CanalBelow(world, tap); // present, but tap is closed

    int accepted = tap.TryPourMetal(IronStack(world, 20, 1400f), 1400f);

    Assert.Equal(0, accepted);
  }

  [Fact]
  public void An_open_tap_hands_metal_to_the_canal_start_below() {
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
  public void An_open_tap_over_nothing_pours_nothing() {
    var world = NewWorld();
    var tap = Tap(world);
    tap.TogglePouring(); // open, but no canal beneath

    Assert.Equal(0, tap.TryPourMetal(IronStack(world, 20, 1400f), 1400f));
  }

  #endregion

  #region Orientation (pour target tracks the side)

  /// <summary>
  /// The tap is a plain oriented block, not a multiblock, so it carries no structure offsets: its one
  /// rotating piece is the pour target at <c>Pos + FromCode(side).Opposite</c>, one down. The four sides
  /// resolve to four distinct cells (N to +z, S to -z, E to -x, W to +x), so a tap ignoring its side would
  /// find no canal at three of them.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void An_open_tap_pours_into_the_cell_its_side_faces_away_from(
    string side
  ) {
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
  /// <c>DrainIronTap</c>'s per-tick pool draw follows <see cref="IwexValues.TapDrainPerTick"/> rather than
  /// a fixed clamp. The config is retuned to 45, not the shipped default of 50, because a test that sets a
  /// value to its own default would pass against a hard-coded clamp as well. With 500 units banked the
  /// tap accepts <c>Ceiling(45 * TapIronStackFactor)</c> = 28: <c>0.6f</c> is fractionally above 0.6 in
  /// IEEE-754, so the product lands at 27.000002 and the ceiling carries it to 28 rather than 27.
  /// </summary>
  [Fact]
  public void Retuning_the_tap_rate_moves_the_drain_with_it() {
    var world = NewWorld();
    // With no MetalDef registered, MetalRegistry.MoltenItemOf("pigiron") falls back to the
    // "game:ingot-<code>" convention, as Iron above does for "iron".
    world.RegisterItem("game:ingot-pigiron", 1500f);

    var furnace = new BlockEntityBlastFurnaceCold {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "iwex:furnace-blastcore-tier1-n",
        1,
        ("side", "north")
      ),
    };
    world.Attach(furnace);
    // The furnace must carry its shipped layout: the drain point comes from the layout (CellRole.MetalTap)
    // rather than a literal offset, so a block with no attributes has no tap and DrainIronTap returns
    // before it looks. StructureRig.Around attaches the attributes; nothing is raised, because completion
    // is not under test here.
    StructureRig.Around(
      world,
      furnace,
      BlockBlastFurnaceCoreCold.Definitions("iwex").Single()
    );
    ReflectionHelpers.Invoke(furnace, "UpdateStructureRotation");
    ReflectionHelpers.Invoke(furnace, "CacheAttributes");

    // The lower (metal) tap's world cell, read exactly as DrainIronTap reads it, so the rig follows the
    // layout if the tap cell moves.
    BlockPos metalTapPos = furnace.MetalTapPos!;

    var tap = Tap(world, pos: metalTapPos);
    var canal = CanalBelow(world, tap);
    tap.TogglePouring();

    ReflectionHelpers.SetField(furnace, "_moltenIron", 500f);

    int original = IwexValues.TapDrainPerTick;
    IwexValues.Edit(c => c.TapDrainPerTick = 45);
    try {
      ReflectionHelpers.Invoke(furnace, "DrainProducts", false);
    } finally {
      IwexValues.Edit(c => c.TapDrainPerTick = original);
    }

    // 45 capped units x 0.6f (TapIronStackFactor default), ceiling. See the doc comment above for why this
    // is 28 and not 27.
    Assert.Equal(28, canal.CellAmount);
    Assert.Equal(
      472f,
      (float)ReflectionHelpers.GetField(furnace, "_moltenIron")!,
      3
    );
  }

  /// <summary>
  /// Mirrors <see cref="Retuning_the_tap_rate_moves_the_drain_with_it"/> for <c>DrainSlagTap</c>,
  /// <c>_moltenSlag</c> and <see cref="IwexValues.TapSlagStackFactor"/>. No other test exercises the slag
  /// tap, so <c>DrainSlagTap</c> reading <c>TapIronStackFactor</c> (0.6f) in place of its own
  /// <c>TapSlagStackFactor</c> (0.8f default) would go uncaught. With 500 units banked and the tap at
  /// 45/tick, <c>Ceiling(45 * 0.8f)</c> is exactly 36: <c>0.8f</c> is also fractionally above 0.8 in
  /// IEEE-754, but at 45 units the product rounds back to a clean 36.0, unlike the iron case.
  /// </summary>
  [Fact]
  public void The_slag_tap_drains_by_its_own_stack_factor_not_the_irons() {
    var world = NewWorld();
    // MetalRegistry.MoltenItemOf("slag") answers iwex:slag when a MetalDef is registered (process-wide
    // state) and game:ingot-slag otherwise. Both spellings are registered so the test does not depend on
    // registry state left by another one.
    world.RegisterItem("iwex:slag", 1500f);
    world.RegisterItem("game:ingot-slag", 1500f);

    var furnace = new BlockEntityBlastFurnaceCold {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "iwex:furnace-blastcore-tier1-n",
        1,
        ("side", "north")
      ),
    };
    world.Attach(furnace);
    // The shipped layout, for the reason given on the iron test above.
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
    try {
      ReflectionHelpers.Invoke(furnace, "DrainProducts", false);
    } finally {
      IwexValues.Edit(c => c.TapDrainPerTick = original);
    }

    // 45 capped units x 0.8f (TapSlagStackFactor default), ceiling = 36 exactly. Against
    // TapIronStackFactor (0.6f) this would read 28.
    Assert.Equal(36, canal.CellAmount);
    Assert.Equal(
      464f,
      (float)ReflectionHelpers.GetField(furnace, "_moltenSlag")!,
      3
    );
  }

  #endregion

  #region Drops / pick

  /// <summary>The four facings of one tap type, registered so <c>world.GetBlock(code)</c> can resolve the
  /// normalised one. Returns the <c>-w</c> block: any non-canonical facing serves, and west is the one the
  /// cold blast furnace's layout places.</summary>
  private static BlockFurnaceTap FourFacings(
    TestWorld world,
    string type,
    int baseId
  ) {
    BlockFurnaceTap? west = null;
    int id = baseId;
    foreach (string side in new[] { "n", "e", "s", "w" }) {
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
  /// A mined or picked tap always comes back at the canonical facing. <c>NormalisedStack</c> resolves the
  /// canonical code through <c>GetBlock</c> and falls back to the block itself when that resolves to null,
  /// so a code the <c>side</c> variant group does not render - a whole word where it emits single letters -
  /// silently returns the block's own facing and splits the inventory four ways.
  /// </summary>
  [Theory]
  [InlineData(BlockFurnaceTap.IronType)]
  [InlineData(BlockFurnaceTap.SlagType)]
  public void A_mined_tap_comes_back_at_the_canonical_facing(string type) {
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

  /// <summary>The <c>type</c> variant rides through the normalisation: an iron tap never comes back as a
  /// slag tap. Both are the same class at the same facings, so a normaliser rebuilding the code from a
  /// literal rather than from <c>CodeWithVariant</c> would swap them and still look right.</summary>
  [Fact]
  public void Normalising_the_facing_never_changes_the_tap_type() {
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
  /// The tap's canonical facing is <c>s</c>, which disagrees with <c>BlockBehaviorExOrientable</c>, whose
  /// canonical is the scheme's first token, <c>n</c>. The tap keeps its own <c>OnPickBlock</c> and
  /// <c>GetDrops</c> overrides and never calls base, so the behaviour's version never runs here. Pinned as
  /// a three-way agreement between the drop, the creative entry and the grid recipes: if they drift apart
  /// a crafted tap and a mined one stop stacking, with nothing else to report it.
  /// </summary>
  [Fact]
  public void The_creative_entry_the_grid_recipe_and_the_drop_all_name_the_south_facing() {
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
      BlockFurnaceTap.Definitions("iwex").First().ToJson()[
        "creativeinventory"
      ]!["general"]!.First()!;
    Assert.EndsWith("-s", creative);

    // 3. every grid recipe in the mod that outputs a furnace tap, read from the recipe defs rather than
    // one file, so a second tap recipe added elsewhere is covered without an edit here.
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
