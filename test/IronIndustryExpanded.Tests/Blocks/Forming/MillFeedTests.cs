using ExpandedLib.Processes;
using IronIndustryExpanded.BlockStructures.Forming;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Offering stock to the rolls. The player picks where along the barrel to feed (the gap) and which side of
/// the piece to put through; the rest follows from geometry. There is no explicit next-gap-only check: the
/// bite limit <c>δ_max = μ²R</c> makes a gap far narrower than the stock skid, and a deeper bite inside that
/// limit is legal.
/// <para>
/// What the rolls are asked to bite is one round's draft, not the gap's: a gap is taken in two rounds, so
/// the first lands half way and the second the rest. See docs/design/processes/rolling.md.
/// </para>
/// </summary>
public class MillFeedTests {
  private const float Radius = 4f; // IiexValues.RollingRollRadius
  private const float RollingTemp = 900f;
  private const float Hot = 1100f;

  private static RollSetSpec FlatSet =>
    RollSetSpec.TryParse(
      new JsonObject(
        JToken.Parse(
          """
          {
            "family": "flat",
            "accepts": [ "shingledbar" ],
            "barrelWidth": 4.0
          }
          """
        )
      ),
      out RollSetSpec? spec,
      out _
    )
      ? spec!
      : throw new System.InvalidOperationException("fixture failed to parse");

  // The bloom's flat branch, the four gaps this set walks. Declared here rather than taken from the shipped
  // route so the arithmetic below stays readable against the numbers it asserts.
  private static ProcessRouteRegistry BloomRoute() {
    var registry = new ProcessRouteRegistry();
    if (
      !ProcessRoute.TryParse(
        new JsonObject(
          JToken.Parse(
            """
            {
              "family": "shingledbar",
              "stages": [
                { "thickness": 2.5, "acceptedBy": [ "flat" ] },
                { "thickness": 2.0, "acceptedBy": [ "flat" ] },
                { "thickness": 1.5, "acceptedBy": [ "flat" ] },
                { "thickness": 1.0, "acceptedBy": [ "flat" ], "code": "iiex:rolledplate-iron" }
              ]
            }
            """
          )
        ),
        out ProcessRoute? route,
        out string? error
      )
    )
      throw new System.InvalidOperationException(
        "fixture route failed to parse: " + error
      );
    registry.Contribute(route!);
    return registry;
  }

  private static MillSchedule FlatSchedule =>
    MillSchedule.For(FlatSet, "shingledbar", BloomRoute())
    ?? throw new System.InvalidOperationException("fixture has no schedule");

  private static FeedDecision Feed(
    WorkPiece piece,
    int gapIndex,
    int side = 0,
    float tempC = Hot,
    RollSetSpec? set = null
  ) =>
    MillFeed.Decide(
      set ?? FlatSet,
      MillSchedule.For(set ?? FlatSet, piece.Form.Name, BloomRoute()),
      piece,
      gapIndex,
      side,
      tempC,
      Radius,
      RollingTemp
    );

  #region Gap zones along the barrel

  [Theory]
  [InlineData(0.0f, 0)]
  [InlineData(0.24f, 0)]
  [InlineData(0.26f, 1)]
  [InlineData(0.5f, 2)]
  [InlineData(0.99f, 3)]
  [InlineData(1.0f, 3)] // the far edge still lands in the last band, not off the end
  public void The_deck_divides_into_one_band_per_gap(
    float alongBarrel,
    int expected
  ) {
    Assert.Equal(expected, MillFeed.GapZone(alongBarrel, 4));
  }

  [Fact]
  public void A_single_gap_set_is_all_one_band() {
    // A wide set has one gap across the whole barrel, so every feed point is the same pass.
    Assert.Equal(0, MillFeed.GapZone(0f, 1));
    Assert.Equal(0, MillFeed.GapZone(0.9f, 1));
  }

  [Fact]
  public void Out_of_range_hits_are_clamped_rather_than_thrown() {
    Assert.Equal(0, MillFeed.GapZone(-1f, 4));
    Assert.Equal(3, MillFeed.GapZone(2f, 4));
  }

  [Theory]
  [InlineData(-2.0, 0f)] // the far end of the deck
  [InlineData(-0.5, 0.5f)] // the middle
  [InlineData(1.0, 1f)] // past the near end, clamped
  [InlineData(-5.0, 0f)] // behind the deck, clamped
  public void The_hit_point_maps_onto_the_length_of_the_deck(
    double localX,
    float expected
  ) {
    // The caller rotates the hit into the mill's own frame; this only has to place it along the barrel.
    Assert.Equal(expected, MillFeed.AlongBarrel(localX), 3);
  }

  [Fact]
  public void The_far_end_of_the_deck_is_the_widest_gap_and_the_near_end_the_narrowest() {
    // The barrel reads the same way the schedule does, so walking along it walks down the gaps.
    int gapCount = 4;
    Assert.Equal(0, MillFeed.GapZone(MillFeed.AlongBarrel(-2.0), gapCount));
    Assert.Equal(
      gapCount - 1,
      MillFeed.GapZone(MillFeed.AlongBarrel(0.9), gapCount)
    );
  }

  #endregion

  #region Which side

  [Fact]
  public void Sneak_takes_the_far_side_and_a_plain_click_the_near_one() {
    // With two sides the choice matters; with one there is only the whole piece to feed.
    Assert.NotEqual(MillFeed.SideIndex(true, 2), MillFeed.SideIndex(false, 2));
    Assert.Equal(0, MillFeed.SideIndex(true, 1));
    Assert.Equal(0, MillFeed.SideIndex(false, 1));
  }

  #endregion

  #region The decision

  [Fact]
  public void A_fresh_bar_is_taken_at_the_widest_gap_half_way() {
    FeedDecision d = Feed(WorkPiece.Fresh(StockForm.ShingledBar), gapIndex: 0);

    Assert.True(d.Accepted);
    Assert.Equal(0.25f, d.Draft, 3); // 3.0 -> 2.75, the half-step of the 2.5 gap
  }

  [Fact]
  public void The_second_round_takes_the_rest_of_the_gap() {
    // Same gap, same click; the piece knows it is half way through and the rolls take what is left.
    var half = new WorkPiece(StockForm.ShingledBar, 2.75f, 2.5f, [false]);

    FeedDecision d = Feed(half, gapIndex: 0);

    Assert.True(d.Accepted);
    Assert.Equal(0.25f, d.Draft, 3); // 2.75 -> 2.5
  }

  [Fact]
  public void Skipping_far_down_the_barrel_skids_instead_of_being_forbidden() {
    // 3.0 offered the narrowest gap is a 1.0 draft even taken half way, against delta_max of 0.36, so the
    // rolls cannot pull it in. No wrong-gap rule is involved; the friction limit refuses it.
    FeedDecision d = Feed(WorkPiece.Fresh(StockForm.ShingledBar), gapIndex: 3);

    Assert.Equal(FeedVerdict.WontBite, d.Verdict);
  }

  [Fact]
  public void Even_a_single_skipped_gap_skids() {
    // delta_max is calibrated between one round's draft and one gap's, so the next rung is the only rung
    // that bites and the barrel has to be walked. This is the whole of the ordering rule the mill does not
    // otherwise have (settled 2026-08-12).
    var piece = new WorkPiece(StockForm.ShingledBar, 2.5f, 0f, [false]);

    Assert.True(Feed(piece, gapIndex: 1).Accepted); // 2.5 -> 2.25, the next rung's half-step
    Assert.Equal(FeedVerdict.WontBite, Feed(piece, gapIndex: 2).Verdict); // straight at 1.5: 0.5 deep
  }

  [Fact]
  public void A_part_cropped_piece_is_refused_at_every_gap() {
    // What a stage yields is declared per stage, so a part piece carried to the next one would be worth that
    // stage's whole count again however much of it had already gone. The stand refuses it instead.
    var piece = WorkPiece.Fresh(StockForm.ShingledBar).Crop(4);

    for (int gap = 0; gap < 4; gap++)
      Assert.Equal(FeedVerdict.PartCropped, Feed(piece, gap).Verdict);
  }

  [Fact]
  public void A_worked_out_piece_is_refused_for_the_same_reason() {
    // Nothing distinguishes it at the stand: any piece with metal already taken out of it is a part piece.
    WorkPiece piece = WorkPiece.Fresh(StockForm.ShingledBar);
    for (int i = 0; i < 4; i++)
      piece = piece.Crop(4);

    Assert.Equal(FeedVerdict.PartCropped, Feed(piece, gapIndex: 0).Verdict);
  }

  [Fact]
  public void The_piece_being_a_part_piece_is_reported_before_the_gap_is_judged() {
    // A part piece offered a gap that would also have been wrong must still say so: sending the player along
    // the barrel to look for a gap that does not exist is the wrong fix.
    var piece = new WorkPiece(
      StockForm.ShingledBar,
      3f,
      0f,
      [false],
      null,
      Cropped: 1
    );

    Assert.Equal(FeedVerdict.PartCropped, Feed(piece, gapIndex: 3).Verdict);
  }

  [Fact]
  public void A_gap_wider_than_the_stock_wastes_the_trip_rather_than_erroring() {
    // The piece passes through untouched.
    var piece = new WorkPiece(StockForm.ShingledBar, 1f, 0f, [false]);
    FeedDecision d = Feed(piece, gapIndex: 0); // 2.5 gap on 1.0 stock

    Assert.Equal(FeedVerdict.NoReduction, d.Verdict);
    Assert.Equal(0f, d.Draft);
  }

  [Fact]
  public void Cold_stock_is_refused_at_the_bite() {
    // Reported as cold rather than as a bad gap.
    FeedDecision d = Feed(
      WorkPiece.Fresh(StockForm.ShingledBar),
      gapIndex: 0,
      tempC: 500f
    );
    Assert.Equal(FeedVerdict.TooCold, d.Verdict);
  }

  [Fact]
  public void A_side_already_through_this_round_would_pass_untouched() {
    // It is already at this round's gauge and the rest of the piece has yet to catch up with it, so the
    // refusal is the same one a too-wide gap gets.
    var midRound = new WorkPiece(StockForm.ShingledBar, 3f, 0f, [true, false]);

    Assert.Equal(FeedVerdict.NoReduction, Feed(midRound, 0, side: 0).Verdict);
    Assert.True(Feed(midRound, 0, side: 1).Accepted); // the side still owed is taken
  }

  [Fact]
  public void A_stand_with_no_roll_set_rolls_nothing() {
    Assert.Equal(
      FeedVerdict.NoRollSet,
      MillFeed
        .Decide(
          null,
          null,
          WorkPiece.Fresh(StockForm.ShingledBar),
          0,
          0,
          Hot,
          Radius,
          RollingTemp
        )
        .Verdict
    );
  }

  [Fact]
  public void A_set_refuses_stock_it_does_not_accept() {
    // The flat set here takes bloom only; a slab needs wide rolls.
    Assert.Equal(
      FeedVerdict.WrongForm,
      Feed(WorkPiece.Fresh(StockForm.ShingledSlab), 0).Verdict
    );
    Assert.Equal(
      FeedVerdict.WrongForm,
      MillFeed
        .Decide(FlatSet, FlatSchedule, null, 0, 0, Hot, Radius, RollingTemp)
        .Verdict
    );
  }

  [Fact]
  public void A_fitted_set_with_no_route_for_this_stock_refuses_it() {
    // The tooling is there and it accepts the form, but nothing has declared a stage its family works. A
    // set that cannot reach the metal is the same refusal as one that will not bite it.
    Assert.Equal(
      FeedVerdict.WrongForm,
      MillFeed
        .Decide(
          FlatSet,
          null,
          WorkPiece.Fresh(StockForm.ShingledBar),
          0,
          0,
          Hot,
          Radius,
          RollingTemp
        )
        .Verdict
    );
  }

  [Fact]
  public void An_out_of_range_gap_or_side_is_a_no_op_not_a_crash() {
    var piece = WorkPiece.Fresh(StockForm.ShingledBar);
    Assert.Equal(FeedVerdict.NoReduction, Feed(piece, gapIndex: 99).Verdict);
    Assert.Equal(
      FeedVerdict.NoReduction,
      Feed(piece, gapIndex: 0, side: 99).Verdict
    );
  }

  #endregion
}
