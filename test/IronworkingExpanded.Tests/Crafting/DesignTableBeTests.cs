using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Crafting.BlockEntities;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The design table's block entity: its typed 3-slot inventory (medium / parchment / take-only output), the
/// end-to-end draft (charcoal + parchment -> the drafted diagram), and the client/server packet handshake
/// its window relies on (open/close/draft, guarded by land claims). The material-identity predicates live in
/// <see cref="DesignTableDraftTests"/>; this pins the wiring around them.
/// </summary>
public class DesignTableBeTests
{
  private const string Charcoal = "game:charcoal";
  private const string Paper = "game:paper";
  private const string Tuyere = "iwex:diagram-tuyere";

  private static BlockEntityDesignTable Table(TestWorld world, BlockPos pos)
  {
    var be = new BlockEntityDesignTable
    {
      Pos = pos,
      Block = TestBlocks.Configure(new Block(), "iwex:designtable", 100),
    };
    world.Place(pos, be.Block, be);
    world.Initialize(be); // runs the real Initialize so the inventory captures the API
    return be;
  }

  private static IPlayer AccessPlayer(TestWorld world, bool granted)
  {
    var player = Substitute.For<IPlayer>();
    player.PlayerName.Returns("tester");
    world
      .World.Claims.TryAccess(
        player,
        Arg.Any<BlockPos>(),
        Arg.Any<EnumBlockAccessFlags>()
      )
      .Returns(granted);
    return player;
  }

  private static ItemSlot Source(ItemStack stack) => new DummySlot(stack);

  #region Slot layout & typing

  [Fact]
  public void The_inventory_is_medium_parchment_and_a_take_only_output()
  {
    var table = Table(new TestWorld(), new BlockPos(0, 1, 0));

    Assert.Equal(3, table.Inventory.Count);
    Assert.IsType<ItemSlotDesignInput>(
      table.Inventory[BlockEntityDesignTable.MediumSlot]
    );
    Assert.IsType<ItemSlotDesignInput>(
      table.Inventory[BlockEntityDesignTable.ParchmentSlot]
    );
    Assert.IsType<ItemSlotDesignOutput>(
      table.Inventory[BlockEntityDesignTable.OutputSlot]
    );
  }

  [Fact]
  public void The_medium_slot_rejects_parchment_and_the_parchment_slot_rejects_charcoal()
  {
    var world = new TestWorld();
    var charcoal = world.RegisterItem(Charcoal);
    var paper = world.RegisterItem(Paper);
    var table = Table(world, new BlockPos(0, 1, 0));

    Assert.False(
      table
        .Inventory[BlockEntityDesignTable.MediumSlot]
        .CanHold(Source(new ItemStack(paper)))
    );
    Assert.False(
      table
        .Inventory[BlockEntityDesignTable.ParchmentSlot]
        .CanHold(Source(new ItemStack(charcoal)))
    );
  }

  [Fact]
  public void The_output_slot_never_accepts_a_hand_placed_stack()
  {
    var world = new TestWorld();
    var paper = world.RegisterItem(Paper);
    var table = Table(world, new BlockPos(0, 1, 0));
    var output = table.Inventory[BlockEntityDesignTable.OutputSlot];

    Assert.False(output.CanHold(Source(new ItemStack(paper))));
    Assert.False(output.CanTakeFrom(Source(new ItemStack(paper))));
  }

  #endregion

  #region Draft (end to end)

  [Fact]
  public void Drafting_consumes_one_medium_and_one_parchment_and_outputs_the_diagram()
  {
    var world = new TestWorld();
    var charcoal = world.RegisterItem(Charcoal);
    var paper = world.RegisterItem(Paper);
    world.RegisterItem(Tuyere);
    var table = Table(world, new BlockPos(0, 1, 0));
    table.Inventory[BlockEntityDesignTable.MediumSlot].Itemstack = new ItemStack(
      charcoal,
      3
    );
    table.Inventory[BlockEntityDesignTable.ParchmentSlot].Itemstack =
      new ItemStack(paper, 3);

    Assert.True(table.TryDraft(Tuyere));

    Assert.Equal(
      "diagram-tuyere",
      table.Inventory[BlockEntityDesignTable.OutputSlot].Itemstack!.Collectible.Code.Path
    );
    Assert.Equal(2, table.Inventory[BlockEntityDesignTable.MediumSlot].StackSize);
    Assert.Equal(
      2,
      table.Inventory[BlockEntityDesignTable.ParchmentSlot].StackSize
    );
  }

  [Fact]
  public void Drafting_again_stacks_the_output()
  {
    var world = new TestWorld();
    var charcoal = world.RegisterItem(Charcoal);
    var paper = world.RegisterItem(Paper);
    world.RegisterItem(Tuyere);
    var table = Table(world, new BlockPos(0, 1, 0));
    table.Inventory[BlockEntityDesignTable.MediumSlot].Itemstack = new ItemStack(
      charcoal,
      3
    );
    table.Inventory[BlockEntityDesignTable.ParchmentSlot].Itemstack =
      new ItemStack(paper, 3);

    Assert.True(table.TryDraft(Tuyere));
    Assert.True(table.TryDraft(Tuyere));

    Assert.Equal(
      2,
      table.Inventory[BlockEntityDesignTable.OutputSlot].StackSize
    );
    Assert.Equal(1, table.Inventory[BlockEntityDesignTable.MediumSlot].StackSize);
  }

  [Fact]
  public void Drafting_without_inputs_fails_and_leaves_the_output_empty()
  {
    var world = new TestWorld();
    world.RegisterItem(Tuyere);
    var table = Table(world, new BlockPos(0, 1, 0));

    Assert.False(table.TryDraft(Tuyere));
    Assert.True(table.Inventory[BlockEntityDesignTable.OutputSlot].Empty);
  }

  #endregion

  #region Packet handshake (server-side)

  // The window lives on the client and forwards slot moves and the Draw request as block-entity packets.
  // Without OnReceivedClientPacket routing them, the base container drops them and the two inventories
  // silently diverge (the hopper's "item reappears" scar). These pin the open/close/draft + claim guard.

  [Fact]
  public void Open_packet_opens_the_inventory_on_the_server()
  {
    var world = new TestWorld();
    var table = Table(world, new BlockPos(0, 1, 0));
    var player = AccessPlayer(world, granted: true);

    table.OnReceivedClientPacket(player, 1000, null!);

    player.InventoryManager.Received().OpenInventory(table.Inventory);
  }

  [Fact]
  public void Close_packet_closes_the_inventory_on_the_server()
  {
    var world = new TestWorld();
    var table = Table(world, new BlockPos(0, 1, 0));
    var player = AccessPlayer(world, granted: true);

    table.OnReceivedClientPacket(player, 1001, null!);

    player.InventoryManager.Received().CloseInventory(table.Inventory);
  }

  [Fact]
  public void Open_packet_is_rejected_without_claim_access()
  {
    var world = new TestWorld();
    var table = Table(world, new BlockPos(0, 1, 0));
    var player = AccessPlayer(world, granted: false);

    table.OnReceivedClientPacket(player, 1000, null!);

    player.InventoryManager.DidNotReceive().OpenInventory(Arg.Any<IInventory>());
  }

  [Fact]
  public void Draft_packet_drafts_the_requested_diagram()
  {
    var world = new TestWorld();
    var charcoal = world.RegisterItem(Charcoal);
    var paper = world.RegisterItem(Paper);
    world.RegisterItem(Tuyere);
    var table = Table(world, new BlockPos(0, 1, 0));
    table.Inventory[BlockEntityDesignTable.MediumSlot].Itemstack = new ItemStack(
      charcoal,
      3
    );
    table.Inventory[BlockEntityDesignTable.ParchmentSlot].Itemstack =
      new ItemStack(paper, 3);
    var player = AccessPlayer(world, granted: true);

    table.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(Tuyere));

    Assert.Equal(Tuyere, table.SelectedType);
    Assert.Equal(
      "diagram-tuyere",
      table.Inventory[BlockEntityDesignTable.OutputSlot].Itemstack!.Collectible.Code.Path
    );
  }

  [Fact]
  public void Draft_packet_is_rejected_without_claim_access()
  {
    var world = new TestWorld();
    world.RegisterItem(Tuyere);
    var table = Table(world, new BlockPos(0, 1, 0));
    var player = AccessPlayer(world, granted: false);

    table.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(Tuyere));

    Assert.Null(table.SelectedType);
    Assert.True(table.Inventory[BlockEntityDesignTable.OutputSlot].Empty);
  }

  #endregion
}
