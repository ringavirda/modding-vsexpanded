using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Machines;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Crafting.BlockEntities;
using IronIndustryExpanded.BlockStructures.Crafting.Blocks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The workbench as a placed block: the two cells it reserves, the frame its art is drawn in, and the
/// grid-over-output inventory the window works through. The match itself is vanilla's
/// <c>GridRecipe</c> matcher, which this bench only supplies a width to; what belongs here is the
/// distribution of a result too big for one slot, which is the reason the output is an inventory.
/// See docs/design/machines/workbench.md.
/// </summary>
public class WorkbenchTests {
  private static ExBlockDef Def() =>
    BlockWorkbench.Definitions("iiex").Single();

  private static BlockWorkbench BlockFor(string side) =>
    TestBlocks.Configure(
      new BlockWorkbench(),
      $"iiex:crafting-workbench-{side}",
      100,
      ("side", side)
    );

  private static BlockEntityWorkbench Bench(TestWorld world) {
    var pos = new BlockPos(0, 1, 0);
    var be = new BlockEntityWorkbench {
      Pos = pos,
      Block = TestBlocks.Configure(
        new Block(),
        "iiex:crafting-workbench-n",
        100
      ),
    };
    world.Place(pos, be.Block, be);
    world.Initialize(be); // the real Initialize, so the inventory captures the API
    return be;
  }

  #region Footprint and frame

  [Fact]
  public void The_footprint_reserves_one_cell_east_of_the_principal() {
    var cells = ((JArray)Def().ToJson()["attributes"]!["fillerOffsets"]!)
      .Select(o => ((int)o["x"]!, (int)o["y"]!, (int)o["z"]!))
      .ToList();

    // The owner's layout is `O #`: the principal plus the far half of the bench top, and nothing else.
    Assert.Equal([(1, 0, 0)], cells);
  }

  [Theory]
  [InlineData("n")]
  [InlineData("e")]
  [InlineData("s")]
  [InlineData("w")]
  public void The_mesh_and_the_footprint_turn_together(string side) {
    int shapeAngle = (int)
      Def().ToJson()["shape"]!["rotateYByType"]![$"*-{side}"]!;
    int footprintAngle = Normalise(BlockFor(side).StructureAngle);

    // The two rotations are declared separately - one on the shape, one on the block - and a bench whose
    // model faced one way while its reserved cell lay the other would place a solid invisible cell in
    // the open and leave half the drawn bench hanging in a buildable space.
    Assert.Equal(shapeAngle, footprintAngle);
  }

  [Fact]
  public void The_drawn_frame_is_the_south_variant() {
    // The vices mount on the +Z edge of the art, which is the edge the player works from, so the bench a
    // player places while standing south of it is the one drawn unrotated.
    Assert.Equal(0, (int)Def().ToJson()["shape"]!["rotateYByType"]!["*-s"]!);
    Assert.Equal(0, Normalise(BlockFor("s").StructureAngle));
  }

  private static int Normalise(int angle) => ((angle % 360) + 360) % 360;

  #endregion

  #region Inventory

  [Fact]
  public void The_inventory_is_a_five_by_five_grid_over_an_output_row() {
    BlockEntityWorkbench bench = Bench(new TestWorld());

    Assert.Equal(25, BlockEntityWorkbench.GridSlots);
    Assert.Equal(
      BlockEntityWorkbench.GridSlots + BlockEntityWorkbench.OutputSlots,
      bench.Inventory.Count
    );

    // Grid first and contiguous, because the matcher reads the array row-major off the grid width.
    Assert.All(
      bench.GridSlotArray,
      s => Assert.IsType<ItemSlotMachineInput>(s)
    );
    Assert.All(
      bench.OutputSlotArray,
      s => Assert.IsType<ItemSlotMachineOutput>(s)
    );
  }

  [Fact]
  public void The_output_row_refuses_what_a_player_tries_to_put_in_it() {
    var world = new TestWorld();
    BlockEntityWorkbench bench = Bench(world);
    var source = new DummySlot(
      new ItemStack(world.RegisterItem("game:rod-iron"))
    );

    Assert.All(bench.OutputSlotArray, s => Assert.False(s.CanTakeFrom(source)));
    Assert.All(bench.OutputSlotArray, s => Assert.False(s.CanHold(source)));
  }

  #endregion

  #region Distributing a result

  private static (TestWorld World, ItemSlot[] Outputs, Item Item) Outputs(
    int maxStackSize = 16
  ) {
    var world = new TestWorld();
    BlockEntityWorkbench bench = Bench(world);
    Item item = world.RegisterItem("game:rod-iron");
    item.MaxStackSize = maxStackSize;
    return (world, bench.OutputSlotArray, item);
  }

  [Fact]
  public void A_result_larger_than_one_stack_spreads_across_the_row() {
    var (world, outputs, item) = Outputs(maxStackSize: 16);

    // 48 against a 16 cap is the design's own worked case, and the whole reason the output is an
    // inventory rather than a slot.
    bool fits = BlockEntityWorkbench.Distribute(
      outputs,
      new ItemStack(item, 48),
      world.World,
      simulate: false
    );

    Assert.True(fits);
    Assert.Equal([16, 16, 16, 0, 0], outputs.Select(s => s.StackSize));
  }

  [Fact]
  public void A_partial_stack_is_topped_up_before_a_new_slot_is_opened() {
    var (world, outputs, item) = Outputs(maxStackSize: 16);
    outputs[0].Itemstack = new ItemStack(item, 10);

    BlockEntityWorkbench.Distribute(
      outputs,
      new ItemStack(item, 12),
      world.World,
      simulate: false
    );

    // Repeat crafting fills the row rather than opening a slot per craft, which is what keeps a held
    // Craft button from running out of room while stacks sit half empty.
    Assert.Equal([16, 6, 0, 0, 0], outputs.Select(s => s.StackSize));
  }

  [Fact]
  public void A_result_that_does_not_fit_is_refused_without_writing_anything() {
    var (world, outputs, item) = Outputs(maxStackSize: 16);
    foreach (ItemSlot slot in outputs)
      slot.Itemstack = new ItemStack(item, 16);

    bool fits = BlockEntityWorkbench.Distribute(
      outputs,
      new ItemStack(item, 1),
      world.World,
      simulate: true
    );

    // The simulate pass is what lets a craft be refused before the grid is consumed; a full bench that
    // still ate its inputs would destroy them.
    Assert.False(fits);
    Assert.All(outputs, s => Assert.Equal(16, s.StackSize));
  }

  [Fact]
  public void A_simulated_fit_writes_nothing_either() {
    var (world, outputs, item) = Outputs(maxStackSize: 16);

    bool fits = BlockEntityWorkbench.Distribute(
      outputs,
      new ItemStack(item, 48),
      world.World,
      simulate: true
    );

    Assert.True(fits);
    Assert.All(outputs, s => Assert.True(s.Empty));
  }

  #endregion
}
