using System.Linq;
using ExpandedLib.Blocks.Machines;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Forming.BlockEntities;
using IronworkingExpanded.BlockStructures.Forming.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The rolling mill as a machine station (A3). Its base slot went to the container it is, and the two
/// things it has - a mechanical-energy membership and a pass clock - became behaviours. What that
/// touches, and so what is guarded here: the graph join now runs through the hosted membership rather
/// than a base class, the roll set and the piece live in inventory slots, and a mill saved before the
/// change still finds both.
/// </summary>
public class RollingMillStationTests {
  private static (
    TestWorld World,
    BlockEntityRollingMill Mill,
    BlockPos Pos
  ) Mill(bool initialize = true) {
    var world = new TestWorld();
    world.RegisterNetwork("mpenergy", n => new MpEnergyNetwork(n));

    Block block = TestBlocks.Configure(
      new BlockRollingMill(),
      "iwex:forming-rollingmill-we",
      1,
      ("type", "rollingmill"),
      ("orientation", "we")
    );
    var pos = new BlockPos(0, 1, 0);
    var mill = new BlockEntityRollingMill();
    world.Place(pos, block, mill);
    if (initialize)
      world.Initialize(mill);
    else
      world.Attach(mill);
    return (world, mill, pos);
  }

  #region Membership is a behaviour, not a base class

  [Fact]
  public void The_mill_joins_the_mpenergy_graph_through_a_hosted_membership() {
    (TestWorld world, BlockEntityRollingMill mill, BlockPos pos) = Mill();

    // The premise this rests on: the mill is no longer a BlockEntityNetworkNode, so nothing but the
    // hosted behaviour could have registered it. Asserted rather than assumed - if the base ever came
    // back, the join below would pass for the wrong reason.
    Assert.IsNotAssignableFrom<BlockEntityNetworkNode>(mill);
    Assert.Single(mill.Behaviors.OfType<BEBehaviorNetworkMember>());

    Assert.NotNull(world.NetworkAt(pos));
    Assert.Equal("mpenergy", world.NetworkAt(pos)!.NetworkType);
    Assert.NotNull(mill.NetworkSystem);
  }

  [Fact]
  public void Breaking_the_mill_drops_its_cell_out_of_the_graph() {
    (TestWorld world, BlockEntityRollingMill mill, BlockPos pos) = Mill();
    Assert.NotNull(world.NetworkAt(pos));

    mill.OnBlockRemoved();

    Assert.Null(world.NetworkAt(pos));
  }

  #endregion

  #region The two stacks are inventory slots

  [Fact]
  public void The_station_offers_a_roll_set_slot_and_a_piece_slot() {
    (_, BlockEntityRollingMill mill, _) = Mill();

    Assert.Equal(2, mill.Inventory.Count);
    Assert.IsType<ItemSlotMachineInput>(
      mill.Inventory[BlockEntityRollingMill.RollSetSlot]
    );
    Assert.IsType<ItemSlotMachineInput>(
      mill.Inventory[BlockEntityRollingMill.PieceSlot]
    );
  }

  [Fact]
  public void The_roll_set_slot_refuses_a_stack_that_is_not_a_roll_set() {
    (TestWorld world, BlockEntityRollingMill mill, _) = Mill();
    Item bloom = world.RegisterItem("iwex:stock-bloom");

    ItemSlot source = new DummySlot(new ItemStack(bloom));

    Assert.False(
      mill.Inventory[BlockEntityRollingMill.RollSetSlot].CanHold(source)
    );
  }

  [Fact]
  public void A_fitted_set_and_a_piece_under_the_rolls_land_in_their_slots() {
    (TestWorld world, BlockEntityRollingMill mill, _) = Mill();
    ItemStack set = RollSetStack(world);
    Item bloom = world.RegisterItem("iwex:stock-bloom");
    var piece = new ItemStack(bloom);

    Assert.True(mill.TryFitRollSet(set, out _));
    Assert.True(
      mill.BeginPass(
        draft: 0.5f,
        width: 4f,
        length: 40f,
        tempC: 1100f,
        piece: piece
      )
    );

    Assert.Same(
      set,
      mill.Inventory[BlockEntityRollingMill.RollSetSlot].Itemstack
    );
    Assert.Same(
      piece,
      mill.Inventory[BlockEntityRollingMill.PieceSlot].Itemstack
    );
  }

  #endregion

  #region The pre-A3 save

  /// <summary>Builds the tree a mill saved before A3 wrote: both stacks loose at the root, under the
  /// keys the block entity used to serialise by hand.</summary>
  private static TreeAttribute LegacyTree(ItemStack? rollSet, ItemStack? piece) {
    var tree = new TreeAttribute();
    if (rollSet != null)
      tree.SetItemstack("rmRollSet", rollSet);
    if (piece != null)
      tree.SetItemstack("rmPiece", piece);
    return tree;
  }

  [Fact]
  public void A_mill_saved_before_A3_finds_both_of_its_loose_stacks() {
    (TestWorld world, BlockEntityRollingMill mill, _) = Mill(initialize: false);
    ItemStack set = RollSetStack(world);
    Item bloom = world.RegisterItem("iwex:stock-bloom");
    var piece = new ItemStack(bloom);

    mill.FromTreeAttributes(LegacyTree(set, piece), world.World);

    // Without the migration both slots stay empty and the player's fitted set is silently gone - the
    // mill comes back bare, and a piece mid-pass is destroyed.
    Assert.Equal(
      set.Collectible.Code,
      mill.Inventory[BlockEntityRollingMill.RollSetSlot]
        .Itemstack
        ?.Collectible
        ?.Code
    );
    Assert.Equal(
      bloom.Code,
      mill.Inventory[BlockEntityRollingMill.PieceSlot]
        .Itemstack
        ?.Collectible
        ?.Code
    );
  }

  [Fact]
  public void The_migration_leaves_a_stack_the_inventory_already_holds_alone() {
    (TestWorld world, BlockEntityRollingMill mill, _) = Mill(initialize: false);
    ItemStack fitted = RollSetStack(world);
    ItemStack stale = RollSetStack(world);

    mill.Inventory[BlockEntityRollingMill.RollSetSlot].Itemstack = fitted;
    mill.FromTreeAttributes(LegacyTree(stale, null), world.World);

    // A tree carrying both forms means the inventory is the authority; restoring over it would put
    // back a set the player had already swapped out.
    Assert.Same(
      fitted,
      mill.Inventory[BlockEntityRollingMill.RollSetSlot].Itemstack
    );
  }

  [Fact]
  public void The_migrated_keys_are_not_written_back() {
    (TestWorld world, BlockEntityRollingMill mill, _) = Mill(initialize: false);
    ItemStack set = RollSetStack(world);

    mill.FromTreeAttributes(LegacyTree(set, null), world.World);

    var saved = new TreeAttribute();
    mill.ToTreeAttributes(saved);

    // One-way: the next save carries only the container's form, so the migration stops running.
    Assert.Null(saved["rmRollSet"]);
    Assert.Null(saved["rmPiece"]);
  }

  #endregion

  // Distinct codes per call, so two sets in one test are told apart by more than reference identity.
  private static int _setCounter;

  /// <summary>A stack carrying a parseable roll-set spec, so the tooling slot accepts it.</summary>
  private static ItemStack RollSetStack(TestWorld world) {
    Item set = world.RegisterItem($"iwex:rollset-flat{++_setCounter}");
    set.Attributes = new JsonObject(
      Newtonsoft.Json.Linq.JToken.Parse(
        """
        { "rollset": {
            "family": "flat",
            "accepts": [ "bloom" ],
            "gaps": [ 2.0, 1.0 ],
            "outputs": [ { "gap": 1.0, "code": "iwex:rolledplate-iron" } ],
            "barrelWidth": 6.0 } }
        """
      )
    );
    return new ItemStack(set);
  }
}
