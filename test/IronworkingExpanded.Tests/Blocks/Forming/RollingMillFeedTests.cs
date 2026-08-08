using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Forming;
using IronworkingExpanded.BlockStructures.Forming.BlockEntities;
using IronworkingExpanded.BlockStructures.Forming.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Feeding the mill, and what comes out the other side.
/// <para>
/// A two-high stand cannot be fed backwards, so the piece goes in one deck, through the rolls, and lands on the
/// <b>opposite</b> deck - the player then walks round, picks it up and feeds it again. That carry-back is the
/// mechanic, not a nuisance: the piece keeps cooling the whole time, so a long round trip costs heat and the
/// next pass loads harder. It is also why a reversing mill was worth building, which is what
/// <see cref="BlockEntityRollingMill.InputDeck"/> following the drive direction models.
/// </para>
/// </summary>
public class RollingMillFeedTests
{
  private static (TestWorld World, BlockEntityRollingMill Mill, BlockPos Pos) Mill(
    string orientation = "we"
  )
  {
    var world = new TestWorld();
    world.RegisterNetwork("mpenergy", sys => new MpEnergyNetwork(sys));

    var block = TestBlocks.Configure(
      new BlockRollingMill(),
      $"iwex:forming-rollingmill-{orientation}",
      1,
      ("type", "rollingmill"),
      ("orientation", orientation)
    );
    ReflectionHelpers.SetProperty(block, "Type", "rollingmill");
    ReflectionHelpers.SetProperty(block, "Orientation", orientation);

    var pos = new BlockPos(0, 0, 0);
    var mill = new BlockEntityRollingMill();
    world.Place(pos, block, mill);
    world.Attach(mill);
    world.AddNode(pos, "mpenergy");
    // The harness's ModLoader does not hand back the graph manager, so wire it as the transmission tests do -
    // without it the mill reads no network at all and every drive-direction question answers "forward".
    ReflectionHelpers.SetProperty(mill, "NetworkSystem", world.Networks);
    return (world, mill, pos);
  }

  // Advance the way the mill's own tick does. A single huge dt would cool the piece to a standstill in one
  // step, which is an artefact of the test rather than the machine.
  private static bool RunPass(BlockEntityRollingMill mill, float speed = 2f, int steps = 400)
  {
    for (int i = 0; i < steps; i++)
      if (mill.AdvancePass(0.25f, speed))
        return true;
    return false;
  }

  private static ItemStack Piece(TestWorld world)
  {
    var stack = new ItemStack(world.RegisterItem("iwex:stock-bloom"));
    WorkPiece.Fresh(StockForm.Bloom).ToStack(stack);
    return stack;
  }

  #region The two decks

  [Fact]
  public void The_piece_goes_in_one_deck_and_comes_out_the_other()
  {
    var (_, mill, _) = Mill();
    Assert.NotEqual(mill.InputDeck, mill.OutputDeck);
  }

  [Fact]
  public void Both_decks_flank_the_roll_stand()
  {
    // They are the two footprint cells either side of the principal along the feed axis - the mill's model has
    // an input table on each side precisely so the drive can choose.
    var (_, mill, pos) = Mill();
    int angle = ((BlockRollingMill)mill.Block).StructureAngle;

    var near = ExOrientation.GlobalPos(pos, 0, 0, -1, angle);
    var far = ExOrientation.GlobalPos(pos, 0, 0, 1, angle);

    Assert.Contains(mill.InputDeck, new[] { near, far });
    Assert.Contains(mill.OutputDeck, new[] { near, far });
  }

  [Fact]
  public void Reversing_the_drive_swaps_the_feed_side()
  {
    // The whole point of tracking rotation direction: a reversing mill spares the crew the walk back around,
    // which is exactly why they were built.
    var (world, mill, pos) = Mill();
    var net = (MpEnergyNetwork)world.NetworkAt(pos)!;
    net.RestoreState(new MpEnergyNetworkState { Inertia = 1f, Speed = 1f, Reversed = false });

    BlockPos forwardInput = mill.InputDeck;
    BlockPos forwardOutput = mill.OutputDeck;

    net.State!.Reversed = true;

    Assert.Equal(forwardOutput, mill.InputDeck);
    Assert.Equal(forwardInput, mill.OutputDeck);
  }

  [Fact]
  public void Only_the_current_input_deck_accepts_a_piece()
  {
    var (_, mill, _) = Mill();
    Assert.True(mill.IsInputDeck(mill.InputDeck));
    Assert.False(mill.IsInputDeck(mill.OutputDeck));
  }

  #endregion

  #region Feeding and ejecting

  [Fact]
  public void A_finished_pass_drops_the_piece_on_the_output_deck()
  {
    var (world, mill, _) = Mill();
    ItemStack piece = Piece(world);

    Assert.True(mill.BeginPass(0.5f, 4f, 1f, 1100f, piece));
    Assert.Empty(world.Drops); // still in the rolls

    Assert.True(mill.AdvancePass(10f, speed: 1f)); // plenty of travel: it clears
    Assert.Single(world.Drops);
    Assert.Same(piece, world.Drops[0]);
  }

  [Fact]
  public void The_piece_is_not_duplicated_by_a_second_advance()
  {
    var (world, mill, _) = Mill();
    mill.BeginPass(0.5f, 4f, 1f, 1100f, Piece(world));
    mill.AdvancePass(10f, 1f);
    mill.AdvancePass(10f, 1f); // idle now - must not drop again

    Assert.Single(world.Drops);
  }

  [Fact]
  public void A_piece_still_in_the_rolls_has_not_been_dropped()
  {
    var (world, mill, _) = Mill();
    mill.BeginPass(0.5f, 4f, length: 100f, 1100f, Piece(world));

    mill.AdvancePass(1f, speed: 1f); // barely started
    Assert.True(mill.IsRolling);
    Assert.Empty(world.Drops);
  }

  [Fact]
  public void Pulling_the_stock_back_out_does_not_eject_it_through_the_mill()
  {
    // Cancelling means the player took it off the input side; it should not appear on the output deck.
    var (world, mill, _) = Mill();
    mill.BeginPass(0.5f, 4f, 100f, 1100f, Piece(world));

    mill.CancelPass();

    Assert.False(mill.IsRolling);
    Assert.Empty(world.Drops);
  }

  [Fact]
  public void Breaking_the_mill_hands_back_a_piece_jammed_in_the_rolls()
  {
    // A stalled piece must not be swallowed by breaking the machine.
    var (world, mill, _) = Mill();
    mill.BeginPass(0.5f, 4f, 100f, 1100f, Piece(world));
    mill.AdvancePass(1f, speed: 0f); // jammed
    Assert.True(mill.IsStalled);

    mill.OnBlockBroken();

    Assert.Single(world.Drops);
  }

  [Fact]
  public void A_pass_can_be_run_without_a_piece_for_the_physics_alone()
  {
    // The load model is exercised on its own in RollingMillLoadTests, so a pass with no stack must not throw
    // or invent a drop.
    var (world, mill, _) = Mill();
    Assert.True(mill.BeginPass(0.5f, 4f, 1f, 1100f));

    Assert.True(mill.AdvancePass(10f, 1f));
    Assert.Empty(world.Drops);
  }

  #endregion

  #region Feeding through the machine end to end

  // A mill with a flat set fitted, and a hot fresh bloom to feed it.
  private static (TestWorld World, BlockEntityRollingMill Mill, ItemStack Stock) Fitted()
  {
    var (world, mill, _) = Mill();

    var setItem = world.RegisterItem("iwex:rollset-flat");
    setItem.Attributes = new Vintagestory.API.Datastructures.JsonObject(
      Newtonsoft.Json.Linq.JToken.Parse(
        """
        { "rollset": {
            "family": "flat",
            "accepts": [ "bloom" ],
            "gaps": [ 2.0, 1.5, 1.0, 0.5 ],
            "outputs": [ { "gap": 1.0, "code": "iwex:rolledplate-iron" } ],
            "barrelWidth": 6.0 } }
        """
      )
    );
    Assert.True(mill.TryFitRollSet(new ItemStack(setItem), out _));

    ItemStack stock = Piece(world);
    stock.Collectible.SetTemperature(world.World, stock, 1100f);
    return (world, mill, stock);
  }

  [Fact]
  public void A_bare_stand_cannot_roll_anything()
  {
    var (world, mill, _) = Mill();
    Assert.False(mill.HasRollSet);
    Assert.Equal(FeedVerdict.NoRollSet, mill.TryFeed(Piece(world), 0, 0).Verdict);
  }

  [Fact]
  public void Fitting_a_set_hands_back_the_one_it_replaces()
  {
    var (world, mill, _) = Fitted();
    Assert.True(mill.HasRollSet);

    Assert.True(mill.TryFitRollSet(null, out ItemStack? previous));
    Assert.NotNull(previous);
    Assert.False(mill.HasRollSet);
  }

  [Fact]
  public void The_tooling_cannot_be_swapped_mid_pass()
  {
    var (_, mill, stock) = Fitted();
    mill.TryFeed(stock, gapIndex: 0, strip: 0);
    Assert.True(mill.IsRolling);

    Assert.False(mill.TryFitRollSet(null, out _));
    Assert.True(mill.HasRollSet);
  }

  [Fact]
  public void An_accepted_feed_reduces_the_strip_and_sends_it_through()
  {
    var (world, mill, stock) = Fitted();

    // Nothing is committed on the way in - the metal only reaches the gap once it has actually been through.
    Assert.True(mill.TryFeed(stock, gapIndex: 0, strip: 0).Accepted);
    Assert.True(mill.IsRolling);
    Assert.Equal(3f, WorkPiece.FromStack(stock)!.Strips[0], 3);

    // First trip marks it turned; the thickness only moves on the second.
    Assert.True(RunPass(mill));
    WorkPiece once = WorkPiece.FromStack(world.Drops[0])!;
    Assert.Equal(3f, once.Strips[0], 3);
    Assert.True(once.IsTurned(0));

    Assert.True(mill.TryFeed(world.Drops[0], gapIndex: 0, strip: 0).Accepted);
    Assert.True(RunPass(mill));
    Assert.Equal(2f, WorkPiece.FromStack(world.Drops[^1])!.Strips[0], 3);
  }

  [Fact]
  public void A_refused_feed_leaves_the_piece_and_the_mill_alone()
  {
    var (world, mill, stock) = Fitted();

    // Straight to the narrowest gap: too deep to bite.
    Assert.Equal(FeedVerdict.WontBite, mill.TryFeed(stock, gapIndex: 3, strip: 0).Verdict);

    Assert.False(mill.IsRolling);
    Assert.Empty(world.Drops);
    Assert.Equal(3f, WorkPiece.FromStack(stock)!.Strips[0], 3); // unchanged
  }

  [Fact]
  public void Cold_stock_is_turned_away_at_the_deck()
  {
    var (_, mill, stock) = Fitted();
    stock.Collectible.SetTemperature(mill.Api.World, stock, 400f);

    Assert.Equal(FeedVerdict.TooCold, mill.TryFeed(stock, 0, 0).Verdict);
    Assert.False(mill.IsRolling);
  }

  [Fact]
  public void A_gap_costs_two_trips_through_the_mill()
  {
    // The turn-over, made concrete: one pass, carry it back, pass it again - and only then is it at the gap.
    var (world, mill, stock) = Fitted();

    mill.TryFeed(stock, gapIndex: 0, strip: 0);
    RunPass(mill);
    Assert.Equal(3f, WorkPiece.FromStack(stock)!.Strips[0], 3); // still as it went in

    mill.TryFeed(stock, gapIndex: 0, strip: 0);
    RunPass(mill);
    Assert.Equal(2f, WorkPiece.FromStack(stock)!.Strips[0], 3); // now it has moved
  }

  [Fact]
  public void A_piece_wrenched_out_mid_pass_comes_back_exactly_as_it_went_in()
  {
    // Whatever stopped the pass, the reduction never happened - so the player redoes that gap from the top
    // rather than getting a half-rolled piece back.
    var (_, mill, stock) = Fitted();
    mill.TryFeed(stock, gapIndex: 0, strip: 0);
    mill.AdvancePass(1f, speed: 0f); // the drive died
    Assert.True(mill.IsStalled);

    ItemStack? freed = mill.ReleaseStuckPiece();

    Assert.Same(stock, freed);
    Assert.False(mill.IsRolling);
    WorkPiece back = WorkPiece.FromStack(freed)!;
    Assert.Equal(3f, back.Strips[0], 3);
    Assert.False(back.IsTurned(0)); // not even the turn is credited
  }

  [Fact]
  public void Nothing_to_release_when_the_rolls_are_empty()
  {
    var (_, mill, _) = Fitted();
    Assert.Null(mill.ReleaseStuckPiece());
  }

  #endregion
}
