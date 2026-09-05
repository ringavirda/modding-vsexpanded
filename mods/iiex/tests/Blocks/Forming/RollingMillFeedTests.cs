using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Processes;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Forming;
using IronIndustryExpanded.BlockStructures.Forming.BlockEntities;
using IronIndustryExpanded.BlockStructures.Forming.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Feeding the mill, and what comes out the other side. A two-high stand cannot be fed backwards: the piece
/// goes in one deck, through the rolls, and lands on the opposite deck, to be carried back round and fed
/// again while it cools. Reversing the drive swaps the two, which is what
/// <see cref="BlockEntityRollingMill.InputDeck"/> follows. See docs/design/machines/rolling-mill.md.
/// </summary>
public class RollingMillFeedTests {
  private static (
    TestWorld World,
    BlockEntityRollingMill Mill,
    BlockPos Pos
  ) Mill(string orientation = "we") {
    var world = new TestWorld();
    world.RegisterNetwork("mpenergy", sys => new MpEnergyNetwork(sys));

    var block = TestBlocks.Configure(
      new BlockRollingMill(),
      $"iiex:forming-rollingmill-{orientation}",
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
    // The harness's ModLoader does not hand back the graph manager. Without it wired in, the mill reads no
    // network and every drive-direction question answers "forward".
    ReflectionHelpers.SetProperty(mill, "NetworkSystem", world.Networks);
    return (world, mill, pos);
  }

  // Advances in the same dt steps the mill's own tick uses. A single large dt would cool the piece to a
  // standstill in one step.
  private static bool RunPass(
    BlockEntityRollingMill mill,
    float speed = 2f,
    int steps = 400
  ) {
    for (int i = 0; i < steps; i++)
      if (mill.AdvancePass(0.25f, speed))
        return true;
    return false;
  }

  private static ItemStack Piece(TestWorld world) {
    var stack = new ItemStack(world.RegisterItem("iiex:stock-shingledbar"));
    WorkPiece.Fresh(StockForm.ShingledBar).ToStack(stack);
    return stack;
  }

  #region The two decks

  [Fact]
  public void The_piece_goes_in_one_deck_and_comes_out_the_other() {
    var (_, mill, _) = Mill();
    Assert.NotEqual(mill.InputDeck, mill.OutputDeck);
  }

  [Fact]
  public void Both_decks_flank_the_roll_stand() {
    // The two footprint cells either side of the principal along the feed axis. The model carries an input
    // table on each side so the drive direction can choose between them.
    var (_, mill, pos) = Mill();
    int angle = ((BlockRollingMill)mill.Block).StructureAngle;

    var near = ExOrientation.GlobalPos(pos, 0, 0, -1, angle);
    var far = ExOrientation.GlobalPos(pos, 0, 0, 1, angle);

    Assert.Contains(mill.InputDeck, new[] { near, far });
    Assert.Contains(mill.OutputDeck, new[] { near, far });
  }

  [Fact]
  public void Reversing_the_drive_swaps_the_feed_side() {
    // Rotation direction is tracked so a reversing mill can be fed from the side the piece last landed on.
    var (world, mill, pos) = Mill();
    var net = (MpEnergyNetwork)world.NetworkAt(pos)!;
    net.RestoreState(
      new MpEnergyNetworkState {
        Inertia = 1f,
        Speed = 1f,
        Reversed = false,
      }
    );

    BlockPos forwardInput = mill.InputDeck;
    BlockPos forwardOutput = mill.OutputDeck;

    net.State!.Reversed = true;

    Assert.Equal(forwardOutput, mill.InputDeck);
    Assert.Equal(forwardInput, mill.OutputDeck);
  }

  [Fact]
  public void Only_the_current_input_deck_accepts_a_piece() {
    var (_, mill, _) = Mill();
    Assert.True(mill.IsInputDeck(mill.InputDeck));
    Assert.False(mill.IsInputDeck(mill.OutputDeck));
  }

  [Fact]
  public void The_whole_input_row_is_feedable_along_the_barrel() {
    // Which gap a click selects is read from how far along the barrel it landed, and MillFeed maps that
    // across all DeckCells of the row. Accepting only the cell beside the stand confined every click to
    // the last third - so the widest gap, the only one fresh stock can enter at, was unreachable.
    var (_, mill, pos) = Mill();
    int angle = ((BlockRollingMill)mill.Block).StructureAngle;
    int inZ = mill.InputDeck.Equals(
      ExOrientation.GlobalPos(pos, 0, 0, 1, angle)
    )
      ? 1
      : -1;

    for (int x = 0; x > -MillFeed.DeckCells; x--)
      Assert.True(
        mill.IsInputDeck(ExOrientation.GlobalPos(pos, x, 0, inZ, angle)),
        $"deck cell at local x={x} must be feedable"
      );

    // The row does not bleed past the footprint, and the far row is still refused.
    Assert.False(
      mill.IsInputDeck(
        ExOrientation.GlobalPos(pos, -MillFeed.DeckCells, 0, inZ, angle)
      )
    );
    Assert.False(
      mill.IsInputDeck(ExOrientation.GlobalPos(pos, -1, 0, -inZ, angle))
    );
  }

  [Fact]
  public void Every_gap_zone_is_reachable_from_some_cell_of_the_input_row() {
    // The end-to-end statement of B17: walking the row must select every gap, widest to narrowest. With a
    // single feedable cell this yielded only zones 2 and 3 of 4.
    var (_, mill, pos) = Mill();
    int angle = ((BlockRollingMill)mill.Block).StructureAngle;
    const int gapCount = 4;

    var zones = new HashSet<int>();
    for (int x = 0; x > -MillFeed.DeckCells; x--) {
      Assert.True(
        mill.IsInputDeck(ExOrientation.GlobalPos(pos, x, 0, 1, angle))
          || mill.IsInputDeck(ExOrientation.GlobalPos(pos, x, 0, -1, angle))
      );
      // Both ends of the cell, since a click lands anywhere across it.
      foreach (double frac in new[] { 0.0, 0.999 })
        zones.Add(MillFeed.GapZone(MillFeed.AlongBarrel(x + frac), gapCount));
    }

    Assert.Equal([0, 1, 2, 3], [.. zones.OrderBy(z => z)]);
  }

  #endregion

  #region Feeding and ejecting

  [Fact]
  public void A_finished_pass_drops_the_piece_on_the_output_deck() {
    var (world, mill, _) = Mill();
    ItemStack piece = Piece(world);

    Assert.True(mill.BeginPass(0.25f, 1f, 1100f, piece));
    Assert.Empty(world.Drops); // still in the rolls

    Assert.True(mill.AdvancePass(10f, speed: 1f)); // plenty of travel: it clears
    Assert.Single(world.Drops);
    Assert.Same(piece, world.Drops[0]);
  }

  [Fact]
  public void The_piece_is_not_duplicated_by_a_second_advance() {
    var (world, mill, _) = Mill();
    mill.BeginPass(0.25f, 1f, 1100f, Piece(world));
    mill.AdvancePass(10f, 1f);
    mill.AdvancePass(10f, 1f); // idle now - must not drop again

    Assert.Single(world.Drops);
  }

  [Fact]
  public void A_piece_still_in_the_rolls_has_not_been_dropped() {
    var (world, mill, _) = Mill();
    mill.BeginPass(0.25f, length: 100f, 1100f, Piece(world));

    mill.AdvancePass(1f, speed: 1f); // barely started
    Assert.True(mill.IsRolling);
    Assert.Empty(world.Drops);
  }

  [Fact]
  public void Pulling_the_stock_back_out_does_not_eject_it_through_the_mill() {
    // Cancelling means the piece was taken off the input side, so it must not appear on the output deck.
    var (world, mill, _) = Mill();
    mill.BeginPass(0.25f, 100f, 1100f, Piece(world));

    mill.CancelPass();

    Assert.False(mill.IsRolling);
    Assert.Empty(world.Drops);
  }

  [Fact]
  public void Breaking_the_mill_hands_back_a_piece_jammed_in_the_rolls() {
    // Breaking the machine must not swallow a stalled piece.
    var (world, mill, _) = Mill();
    mill.BeginPass(0.25f, 100f, 1100f, Piece(world));
    mill.AdvancePass(1f, speed: 0f); // jammed
    Assert.True(mill.IsStalled);

    mill.OnBlockBroken();

    Assert.Single(world.Drops);
  }

  [Fact]
  public void A_pass_can_be_run_without_a_piece_for_the_physics_alone() {
    // BeginPass takes no stack when only the load model is being driven, and must not invent a drop.
    var (world, mill, _) = Mill();
    Assert.True(mill.BeginPass(0.25f, 1f, 1100f));

    Assert.True(mill.AdvancePass(10f, 1f));
    Assert.Empty(world.Drops);
  }

  #endregion

  #region Feeding through the machine end to end

  // A mill with a flat set fitted, and a hot fresh bloom to feed it.
  private static (
    TestWorld World,
    BlockEntityRollingMill Mill,
    ItemStack Stock
  ) Fitted() {
    var (world, mill, _) = Mill();

    var setItem = world.RegisterItem("iiex:rollset-flat");
    setItem.Attributes = new Vintagestory.API.Datastructures.JsonObject(
      Newtonsoft.Json.Linq.JToken.Parse(
        """
        { "rollset": {
            "family": "flat",
            "accepts": [ "shingledbar" ],
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
  public void A_bloom_straight_off_the_grid_takes_its_first_pass() {
    // Every other test here feeds a stack whose work state was written by hand, which nothing does
    // before the first pass. A real crafted bloom carries its form on the ITEM TYPE and nothing on
    // the stack, and the mill used to read only the stack - so it refused the piece as WrongForm and
    // no player could roll anything at all. The whole machine hangs off this one read.
    var (world, mill, _) = Fitted();

    Item bloom = world.RegisterItem("iiex:stock-shingledbar-fresh");
    bloom.Attributes = new Vintagestory.API.Datastructures.JsonObject(
      Newtonsoft.Json.Linq.JToken.Parse("""{ "stockForm": "shingledbar" }""")
    );
    var stack = new ItemStack(bloom);
    stack.Collectible.SetTemperature(world.World, stack, 1100f);

    FeedDecision decision = mill.TryFeed(stack, 0, 0);

    Assert.True(decision.Accepted, $"refused as {decision.Verdict}");
    Assert.True(mill.IsRolling);
  }

  #region Claiming a finished item

  // A mill fitted with a set whose one rung names a finished item. The shipped routes name no code today
  // (every stage is a shear crop), so the claim is driven through a route authored here and handed to
  // this mill alone rather than written into the shared catalogue.
  private static (
    TestWorld World,
    BlockEntityRollingMill Mill,
    ItemStack Stock
  ) FittedWithOutput(string outputCode) {
    var (world, mill, _) = Mill();

    var setItem = world.RegisterItem("iiex:rollset-claiming");
    setItem.Attributes = new Vintagestory.API.Datastructures.JsonObject(
      Newtonsoft.Json.Linq.JToken.Parse(
        """
        { "rollset": {
            "family": "claiming",
            "accepts": [ "shingledbar" ],
            "barrelWidth": 16.0 } }
        """
      )
    );
    Assert.True(mill.TryFitRollSet(new ItemStack(setItem), out _));
    ReflectionHelpers.SetProperty(mill, "Routes", ClaimingRoute(outputCode));

    ItemStack stock = Piece(world);
    stock.Collectible.SetTemperature(world.World, stock, 1100f);
    return (world, mill, stock);
  }

  // A one-rung bloom route whose single stage is a stopping point.
  private static ProcessRouteRegistry ClaimingRoute(string outputCode) {
    var registry = new ProcessRouteRegistry();
    Assert.True(
      ProcessRoute.TryParse(
        new Vintagestory.API.Datastructures.JsonObject(
          Newtonsoft.Json.Linq.JToken.Parse(
            $$"""
            {
              "family": "shingledbar",
              "stages": [
                { "thickness": 2.5, "acceptedBy": [ "claiming" ], "code": "{{outputCode}}" }
              ]
            }
            """
          )
        ),
        out ProcessRoute? route,
        out string? error
      ),
      error
    );
    Assert.Empty(registry.Contribute(route!));
    return registry;
  }

  [Fact]
  public void A_stage_that_names_a_finished_item_ejects_that_item_not_stock() {
    var (world, mill, stock) = FittedWithOutput("iiex:nailplate");
    world.RegisterItem("iiex:nailplate");

    // Two feeds: the first lands the half-step, the second the gap the stage sits on.
    for (int i = 0; i < WorkPiece.FeedsPerSide; i++) {
      Assert.True(mill.TryFeed(stock, 0, 0).Accepted, $"feed {i} refused");
      Assert.True(RunPass(mill), $"pass {i} did not finish");
    }

    ItemStack ejected = world.Drops[^1];
    Assert.Equal("iiex:nailplate", ejected.Collectible.Code.ToString());
    Assert.Equal(1, ejected.StackSize); // one piece in, one piece out
  }

  [Fact]
  public void A_claimed_item_keeps_the_heat_the_piece_came_off_the_rolls_with() {
    var (world, mill, stock) = FittedWithOutput("iiex:nailplate");
    Item plate = world.RegisterItem("iiex:nailplate");

    for (int i = 0; i < WorkPiece.FeedsPerSide; i++) {
      mill.TryFeed(stock, 0, 0);
      RunPass(mill);
    }

    ItemStack ejected = world.Drops[^1];
    Assert.Equal(plate.Code, ejected.Collectible.Code);
    // Cold would mean the player cannot work the thing they just rolled.
    Assert.True(
      ejected.Collectible.GetTemperature(world.World, ejected) > 500f,
      "a piece claimed straight off the rolls must still be hot"
    );
  }

  [Fact]
  public void A_stage_naming_no_output_ejects_the_stock_it_rolled() {
    // Every shipped stage is like this: the piece leaves as stock, to be cropped at the shear.
    var (world, mill, _) = Fitted();
    ItemStack stock = Piece(world);
    stock.Collectible.SetTemperature(world.World, stock, 1100f);

    Assert.True(mill.TryFeed(stock, 0, 0).Accepted);
    Assert.True(RunPass(mill));

    Assert.Same(stock, world.Drops[^1]);
  }

  [Fact]
  public void An_output_code_naming_no_item_leaves_the_piece_as_stock() {
    // The four codes that used to ship named no item that existed. A dangling code must not destroy the
    // player's piece.
    var (world, mill, stock) = FittedWithOutput("iiex:nosuchproduct");

    for (int i = 0; i < WorkPiece.FeedsPerSide; i++) {
      mill.TryFeed(stock, 0, 0);
      RunPass(mill);
    }

    Assert.Same(stock, world.Drops[^1]);
  }

  #endregion

  [Fact]
  public void A_bare_stand_cannot_roll_anything() {
    var (world, mill, _) = Mill();
    Assert.False(mill.HasRollSet);
    Assert.Equal(
      FeedVerdict.NoRollSet,
      mill.TryFeed(Piece(world), 0, 0).Verdict
    );
  }

  [Fact]
  public void Fitting_a_set_hands_back_the_one_it_replaces() {
    var (world, mill, _) = Fitted();
    Assert.True(mill.HasRollSet);

    Assert.True(mill.TryFitRollSet(null, out ItemStack? previous));
    Assert.NotNull(previous);
    Assert.False(mill.HasRollSet);
  }

  [Fact]
  public void The_tooling_cannot_be_swapped_mid_pass() {
    var (_, mill, stock) = Fitted();
    mill.TryFeed(stock, gapIndex: 0, side: 0);
    Assert.True(mill.IsRolling);

    Assert.False(mill.TryFitRollSet(null, out _));
    Assert.True(mill.HasRollSet);
  }

  [Fact]
  public void An_accepted_feed_reduces_the_piece_and_sends_it_through() {
    var (world, mill, stock) = Fitted();

    // Nothing is committed on the way in: the gauge moves only once the piece has been through.
    Assert.True(mill.TryFeed(stock, gapIndex: 0, side: 0).Accepted);
    Assert.True(mill.IsRolling);
    Assert.Equal(3f, WorkPiece.FromStack(stock)!.Thickness, 3);

    // The first trip lands the half-step of the 2.5 gap; the second lands the gap.
    Assert.True(RunPass(mill));
    WorkPiece once = WorkPiece.FromStack(world.Drops[0])!;
    Assert.Equal(2.75f, once.Thickness, 3);
    Assert.Equal(2.5f, once.Gap, 3);

    Assert.True(mill.TryFeed(world.Drops[0], gapIndex: 0, side: 0).Accepted);
    Assert.True(RunPass(mill));
    WorkPiece landed = WorkPiece.FromStack(world.Drops[^1])!;
    Assert.Equal(2.5f, landed.Thickness, 3);
    Assert.Equal(0f, landed.Gap, 3);
  }

  [Fact]
  public void A_refused_feed_leaves_the_piece_and_the_mill_alone() {
    var (world, mill, stock) = Fitted();

    // Straight to the narrowest gap the bar's flat branch has - a whole rung past the one it could take,
    // so the round's half-draft is far beyond delta_max. (The branch ends at 2.0: a bar taken that far IS
    // a beam, and rolling it on is the beam's own route.)
    Assert.Equal(
      FeedVerdict.WontBite,
      mill.TryFeed(stock, gapIndex: 1, side: 0).Verdict
    );

    Assert.False(mill.IsRolling);
    Assert.Empty(world.Drops);
    Assert.Equal(3f, WorkPiece.FromStack(stock)!.Thickness, 3); // unchanged
  }

  [Fact]
  public void Cold_stock_is_turned_away_at_the_deck() {
    var (_, mill, stock) = Fitted();
    stock.Collectible.SetTemperature(mill.Api.World, stock, 400f);

    Assert.Equal(FeedVerdict.TooCold, mill.TryFeed(stock, 0, 0).Verdict);
    Assert.False(mill.IsRolling);
  }

  [Fact]
  public void A_gap_costs_two_trips_through_the_mill() {
    // One pass, carry back, pass again: only after the second is the piece at the gap.
    var (world, mill, stock) = Fitted();

    mill.TryFeed(stock, gapIndex: 0, side: 0);
    RunPass(mill);
    Assert.Equal(2.75f, WorkPiece.FromStack(stock)!.Thickness, 3); // half way

    mill.TryFeed(stock, gapIndex: 0, side: 0);
    RunPass(mill);
    Assert.Equal(2.5f, WorkPiece.FromStack(stock)!.Thickness, 3); // and there
  }

  [Fact]
  public void A_piece_wrenched_out_mid_pass_comes_back_exactly_as_it_went_in() {
    // An interrupted pass commits nothing, so the gap is redone from the top rather than yielding a
    // half-rolled piece.
    var (_, mill, stock) = Fitted();
    mill.TryFeed(stock, gapIndex: 0, side: 0);
    mill.AdvancePass(1f, speed: 0f); // drive stopped
    Assert.True(mill.IsStalled);

    ItemStack? freed = mill.ReleaseStuckPiece();

    Assert.Same(stock, freed);
    Assert.False(mill.IsRolling);
    WorkPiece back = WorkPiece.FromStack(freed)!;
    Assert.Equal(3f, back.Thickness, 3);
    Assert.False(back.IsFed(0)); // the feed is not credited either
    Assert.Equal(0f, back.Gap);
  }

  [Fact]
  public void Nothing_to_release_when_the_rolls_are_empty() {
    var (_, mill, _) = Fitted();
    Assert.Null(mill.ReleaseStuckPiece());
  }

  #endregion
}
