using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Storage;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Storage.BlockEntities;
using IronIndustryExpanded.BlockStructures.Storage.Blocks;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The storage rack: a row of cells filled by length, worked from any of them. The arrangement rules
/// themselves are exlib's <c>BayLayoutTests</c>; what belongs here is the rack - that its cells come off
/// its own footprint, that a click on any of them works the whole of it, and that breaking it gives the
/// contents back.
/// <para>
/// The shipped catalogue is loaded rather than a fixture one, because the numbers a player meets are the
/// ones in <c>config/bayoccupancy/storagerack.json</c> and a rack tested against invented sizes would say
/// nothing about them.
/// </para>
/// </summary>
[Collection(StorageRackCollection.Name)]
public class StorageRackTests {
  #region Harness

  private const string Rod = "iiex:stock-rod";
  private const string Slab = "iiex:stock-shingledslab";
  private const string CastSlab = "iiex:caststock-slab";

  public StorageRackTests() {
    // The real file, read off the source tree: this is the data the game loads.
    string path = Path.Combine(
      RepoPaths.Assets("iiex"),
      "config",
      "bayoccupancy",
      "storagerack.json"
    );
    BayOccupancyLoader.Load([(path, File.ReadAllText(path))]);
  }

  private static ExBlockDef Def() =>
    BlockStorageRack.Definitions("iiex").Single();

  /// <summary>A rack block wearing the shipped definition's attributes, so its footprint is the real
  /// one.</summary>
  private static BlockStorageRack BlockFor(string side) {
    var block = TestBlocks.Configure(
      new BlockStorageRack(),
      $"iiex:storage-rack-{side}",
      200,
      ("side", side)
    );
    block.Attributes = new JsonObject((JObject)Def().ToJson()["attributes"]!);
    return block;
  }

  private static (BlockEntityStorageRack Rack, TestWorld World) Rack(
    string side = "n"
  ) {
    var world = new TestWorld();
    world.World.Side.Returns(EnumAppSide.Server);
    foreach (string code in new[] { Rod, Slab, CastSlab, "game:stone-granite" })
      world.RegisterItem(code);

    var pos = new BlockPos(0, 1, 0);
    var be = new BlockEntityStorageRack { Pos = pos, Block = BlockFor(side) };
    world.Place(pos, be.Block, be);
    world.Attach(be);
    return (be, world);
  }

  private static ItemSlot Holding(
    TestWorld world,
    string code,
    int count = 1
  ) =>
    new DummySlot(
      new ItemStack(world.World.GetItem(new AssetLocation(code))!, count)
    );

  /// <summary>A player holding <paramref name="held"/>, or nothing.</summary>
  private static IPlayer Player(ItemSlot? held = null) {
    ItemSlot slot = held ?? new DummySlot();
    var player = Substitute.For<IPlayer>();
    var entity = Substitute.For<EntityPlayer>();
    entity.RightHandItemSlot.Returns(slot);
    player.Entity.Returns(entity);
    player.InventoryManager.ActiveHotbarSlot.Returns(slot);
    return player;
  }

  #endregion

  #region The footprint is the capacity

  [Fact]
  public void The_footprint_reserves_two_cells_north_of_the_principal() {
    var cells = ((JArray)Def().ToJson()["attributes"]!["fillerOffsets"]!)
      .Select(o =>
        ((int)o["x"]!, (int)o["y"]!, (int)o["z"]!, (bool)o["allowAttach"]!)
      )
      .OrderBy(c => c.Item3)
      .ToList();

    // The owner's layout is `O # #` running -Z, as the art is drawn. Attachment is on: racks stack
    // vertically, so the next rack up has to be able to land on any cell of the one below.
    Assert.Equal([(0, 0, -2, true), (0, 0, -1, true)], cells);
  }

  [Fact]
  public void The_rack_takes_its_cell_count_from_its_own_footprint() {
    // Not a constant: "how many cells" is the definition's, so a longer rack is another blocktype and
    // nothing in the code moves.
    var (rack, _) = Rack();

    Assert.Equal(3, rack.Cells);
    Assert.Equal(3, rack.FreeCells);
    Assert.Equal(
      [new Vec3i(0, 0, 0), new Vec3i(0, 0, -1), new Vec3i(0, 0, -2)],
      BlockFor("n").LocalCells
    );
  }

  [Theory]
  [InlineData("n")]
  [InlineData("e")]
  [InlineData("s")]
  [InlineData("w")]
  public void The_mesh_and_the_footprint_turn_together(string side) {
    int shapeAngle = (int)
      Def().ToJson()["shape"]!["rotateYByType"]![$"*-{side}"]!;
    int footprintAngle = ((BlockFor(side).StructureAngle % 360) + 360) % 360;

    // Declared separately - one on the shape, one on the block - so a rack whose model ran one way while
    // its reserved cells lay the other would put two solid invisible cells in the open and leave two
    // drawn bays hanging in buildable space.
    Assert.Equal(shapeAngle, footprintAngle);
  }

  #endregion

  #region The three arrangements, on a real rack

  [Fact]
  public void Three_one_cell_stacks_fill_it() {
    var (rack, world) = Rack();

    for (int i = 0; i < 3; i++)
      Assert.True(rack.TryLay(Holding(world, Rod)));

    Assert.Equal(3, rack.Loads.Count);
    Assert.Equal(0, rack.FreeCells);
    Assert.False(rack.TryLay(Holding(world, Rod)));
  }

  [Fact]
  public void A_two_cell_stack_and_a_one_cell_stack_fill_it() {
    var (rack, world) = Rack();

    Assert.True(rack.TryLay(Holding(world, Slab)));
    Assert.True(rack.TryLay(Holding(world, Rod)));

    Assert.Equal([2, 1], rack.Loads.Select(l => l.Run.Length));
    Assert.Equal(0, rack.FreeCells);
    // And no room for another of either.
    Assert.False(rack.TryLay(Holding(world, Rod)));
  }

  [Fact]
  public void One_three_cell_stack_fills_it_on_its_own() {
    var (rack, world) = Rack();

    Assert.True(rack.TryLay(Holding(world, CastSlab)));

    Assert.Equal(3, Assert.Single(rack.Loads).Run.Length);
    Assert.Equal(0, rack.FreeCells);
  }

  [Fact]
  public void A_three_cell_stack_needs_the_whole_rack_free() {
    var (rack, world) = Rack();
    Assert.True(rack.TryLay(Holding(world, Rod)));

    Assert.False(rack.TryLay(Holding(world, CastSlab)));
    Assert.Equal(2, rack.FreeCells);
  }

  #endregion

  #region Working it from any cell

  [Fact]
  public void Any_cell_of_a_run_takes_that_run() {
    // The whole point of every cell being interactive: a three-cell slab comes off whichever bay the
    // player happens to be standing at.
    for (int cell = 0; cell < 3; cell++) {
      var (rack, world) = Rack();
      Assert.True(rack.TryLay(Holding(world, CastSlab)));

      ItemStack? taken = rack.TryTake(cell);

      Assert.NotNull(taken);
      Assert.Equal(CastSlab, taken!.Collectible.Code.ToString());
      Assert.Empty(rack.Loads);
    }
  }

  [Fact]
  public void An_empty_cell_gives_nothing_back() {
    var (rack, world) = Rack();
    Assert.True(rack.TryLay(Holding(world, Rod))); // lands on cell 0

    Assert.Null(rack.TryTake(1));
    Assert.Null(rack.TryTake(2));
    Assert.NotNull(rack.TryTake(0));
  }

  [Theory]
  [InlineData("n", 0, 0, -1)]
  [InlineData("s", 0, 0, 1)]
  [InlineData("e", 1, 0, 0)]
  [InlineData("w", -1, 0, 0)]
  public void A_clicked_world_cell_reads_as_a_cell_of_the_turned_rack(
    string side,
    int dx,
    int dy,
    int dz
  ) {
    // The footprint turns with the block, so the offset is rotated back into the rack's own frame before
    // it is read; a rack facing west would otherwise have its near and far ends swapped.
    BlockStorageRack block = BlockFor(side);
    var principal = new BlockPos(0, 1, 0);

    Assert.Equal(0, block.CellAt(principal, principal));
    Assert.Equal(1, block.CellAt(principal, principal.AddCopy(dx, dy, dz)));
    Assert.Null(
      block.CellAt(principal, principal.AddCopy(-dx * 2, dy, -dz * 2))
    );
  }

  [Fact]
  public void A_click_with_a_held_stack_lays_it_and_an_empty_hand_takes_it_back() {
    // Through the block's own interaction, not through TryLay: the gesture is what a player has, and a
    // rack that only worked when called directly would be a rack nobody can use.
    var (rack, world) = Rack();
    var principal = new BlockPos(0, 1, 0);
    var selection = new BlockSelection { Position = principal };
    ItemSlot held = Holding(world, Rod);

    Assert.True(
      ((BlockStorageRack)rack.Block).OnBlockInteractStart(
        world.World,
        Player(held),
        selection
      )
    );

    Assert.Single(rack.Loads);
    Assert.True(held.Empty); // the piece left the player's hand

    Assert.True(
      ((BlockStorageRack)rack.Block).OnBlockInteractStart(
        world.World,
        Player(),
        selection
      )
    );
    Assert.Empty(rack.Loads);
  }

  [Fact]
  public void A_click_on_a_filler_cell_works_the_same_rack() {
    var (rack, world) = Rack();
    var principal = new BlockPos(0, 1, 0);
    var block = (BlockStorageRack)rack.Block;

    Assert.True(
      block.OnFillerInteractStart(
        world.World,
        Player(Holding(world, Rod)),
        new BlockSelection { Position = principal },
        principal.AddCopy(0, 0, -2)
      )
    );

    Assert.Single(rack.Loads);
  }

  #endregion

  #region What it will not hold

  [Fact]
  public void An_item_the_catalogue_does_not_list_is_refused() {
    // A rack is not a chest: it holds what it is told it can hold, because anything it holds it also has
    // to draw.
    var (rack, world) = Rack();

    Assert.Null(rack.CellsFor(Holding(world, "game:stone-granite").Itemstack));
    Assert.False(rack.TryLay(Holding(world, "game:stone-granite")));
    Assert.Empty(rack.Loads);
  }

  [Fact]
  public void The_shipped_catalogue_offers_all_three_lengths() {
    // Otherwise one of the owner's three arrangements would be unreachable in play, and the capacity
    // model would be tested by nothing a player can pick up.
    var (rack, world) = Rack();

    Assert.Equal(1, rack.CellsFor(Holding(world, Rod).Itemstack));
    Assert.Equal(2, rack.CellsFor(Holding(world, Slab).Itemstack));
    Assert.Equal(3, rack.CellsFor(Holding(world, CastSlab).Itemstack));
  }

  #endregion

  #region Persistence and drops

  [Fact]
  public void The_loads_round_trip_through_the_tree() {
    var (rack, world) = Rack();
    Assert.True(rack.TryLay(Holding(world, Slab)));
    Assert.True(rack.TryLay(Holding(world, Rod)));

    var tree = new TreeAttribute();
    rack.ToTreeAttributes(tree);

    var (reloaded, _) = Rack();
    reloaded.FromTreeAttributes(tree, world.World);

    Assert.Equal(
      rack.Loads.Select(l => (l.Run.Start, l.Run.Length)),
      reloaded.Loads.Select(l => (l.Run.Start, l.Run.Length))
    );
    Assert.Equal(
      [Slab, Rod],
      reloaded.Loads.Select(l => l.Stack.Collectible.Code.ToString())
    );
    Assert.Equal(0, reloaded.FreeCells);
  }

  [Fact]
  public void Breaking_the_rack_gives_everything_back() {
    // The worst bug this block can have is eating 15 000 u of slabs on a misclick, and it is one missing
    // call: a filler reroutes being broken to the principal, so any cell breaks the whole rack.
    var (rack, world) = Rack();
    Assert.True(rack.TryLay(Holding(world, Slab)));
    Assert.True(rack.TryLay(Holding(world, Rod)));

    IReadOnlyList<ItemStack> spawned = rack.TakeAll();

    Assert.Equal(
      [Slab, Rod],
      spawned.Select(s => s.Collectible.Code.ToString())
    );
    Assert.Empty(rack.Loads);
    Assert.Equal(3, rack.FreeCells);
  }

  #endregion

  #region Where a piece is drawn

  /// <summary>
  /// The one part of the renderer that can be checked without a client: a piece is centred on the cells
  /// it covers, so a three-cell slab lies down the middle of the rack rather than at one end and a
  /// two-cell one straddles the pair it occupies.
  /// </summary>
  [Theory]
  [InlineData(0, 1, 0f)]
  [InlineData(1, 1, 1f)]
  [InlineData(2, 1, 2f)]
  [InlineData(0, 2, 0.5f)]
  [InlineData(1, 2, 1.5f)]
  [InlineData(0, 3, 1f)]
  public void A_run_is_drawn_over_the_middle_of_the_cells_it_covers(
    int start,
    int length,
    float centre
  ) =>
    Assert.Equal(
      centre,
      BlockEntityStorageRack.CentreOf(new BayRun(start, length)),
      3
    );

  #endregion
}

/// <summary>
/// Serialises the rack cases: they repopulate the process-wide <c>BayOccupancyRegistry.Shared</c>, which
/// xUnit would otherwise let another class read mid-rewrite.
/// </summary>
[CollectionDefinition(Name)]
public class StorageRackCollection {
  public const string Name = "storage-rack";
}
