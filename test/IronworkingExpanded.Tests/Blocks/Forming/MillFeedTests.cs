using IronworkingExpanded.BlockStructures.Forming;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Offering stock to the rolls. The player picks where along the barrel to feed (the gap) and which strip of
/// the piece to put through; the rest follows from geometry. There is no explicit next-gap-only check: the
/// bite limit <c>δ_max = μ²R</c> makes a gap far narrower than the stock skid, and a deeper bite inside that
/// limit is legal. See docs/design/processes/rolling.md.
/// </summary>
public class MillFeedTests {
  private const float Radius = 4f; // IwexValues.RollingRollRadius
  private const float RollingTemp = 900f;
  private const float Hot = 1100f;

  private static RollSetSpec FlatSet =>
    RollSetSpec.TryParse(
      new JsonObject(
        JToken.Parse(
          """
          {
            "family": "flat",
            "accepts": [ "bloom" ],
            "gaps": [ 2.0, 1.5, 1.0, 0.5 ],
            "outputs": [ { "gap": 1.0, "code": "iwex:rolledplate-iron" } ],
            "barrelWidth": 6.0
          }
          """
        )
      ),
      out RollSetSpec? spec,
      out _
    )
      ? spec!
      : throw new System.InvalidOperationException("fixture failed to parse");

  private static FeedDecision Feed(
    WorkPiece piece,
    int gapIndex,
    int strip = 0,
    float tempC = Hot,
    RollSetSpec? set = null
  ) =>
    MillFeed.Decide(
      set ?? FlatSet,
      piece,
      gapIndex,
      strip,
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

  #region Which strip

  [Fact]
  public void Sneak_takes_the_far_strip_and_a_plain_click_the_near_one() {
    // With two strips the choice matters; with one there is only the whole piece to feed.
    Assert.NotEqual(
      MillFeed.StripIndex(true, 2),
      MillFeed.StripIndex(false, 2)
    );
    Assert.Equal(0, MillFeed.StripIndex(true, 1));
    Assert.Equal(0, MillFeed.StripIndex(false, 1));
  }

  #endregion

  #region The decision

  [Fact]
  public void A_fresh_bloom_is_taken_at_the_widest_gap() {
    FeedDecision d = Feed(WorkPiece.Fresh(StockForm.Bloom), gapIndex: 0);

    Assert.True(d.Accepted);
    Assert.Equal(1f, d.Draft, 3); // 3.0 -> 2.0
  }

  [Fact]
  public void Skipping_far_down_the_barrel_skids_instead_of_being_forbidden() {
    // 3.0 straight to 0.5 is a 2.5 draft against delta_max of 1.0, so the rolls cannot pull it in. No
    // wrong-gap rule is involved; the friction limit refuses it.
    FeedDecision d = Feed(WorkPiece.Fresh(StockForm.Bloom), gapIndex: 3);

    Assert.Equal(FeedVerdict.WontBite, d.Verdict);
  }

  [Fact]
  public void A_legal_deeper_bite_is_allowed_if_the_geometry_permits_it() {
    // From 2.0 the 1.0 gap is a 1.0 draft, exactly delta_max, so it is legal even though it skips 1.5.
    var piece = WorkPiece.Fresh(StockForm.Bloom).WithStrip(0, 2f);

    Assert.True(Feed(piece, gapIndex: 2).Accepted);
  }

  [Fact]
  public void A_gap_wider_than_the_stock_wastes_the_trip_rather_than_erroring() {
    // The piece passes through untouched.
    var piece = WorkPiece.Fresh(StockForm.Bloom).WithStrip(0, 1f);
    FeedDecision d = Feed(piece, gapIndex: 0); // 2.0 gap on 1.0 stock

    Assert.Equal(FeedVerdict.NoReduction, d.Verdict);
    Assert.Equal(0f, d.Draft);
  }

  [Fact]
  public void Cold_stock_is_refused_at_the_bite() {
    // Reported as cold rather than as a bad gap.
    FeedDecision d = Feed(
      WorkPiece.Fresh(StockForm.Bloom),
      gapIndex: 0,
      tempC: 500f
    );
    Assert.Equal(FeedVerdict.TooCold, d.Verdict);
  }

  [Fact]
  public void Each_strip_is_judged_on_its_own_thickness() {
    // Half-rolled: strip 0 down to 2.0, strip 1 untouched at 3.0. The same gap is a reduction for one and a
    // skid for the other, so each strip is judged on its own.
    var half = new WorkPiece(StockForm.Bloom, [2f, 3f], new bool[2]);

    Assert.True(Feed(half, gapIndex: 1, strip: 0).Accepted); // 2.0 -> 1.5
    Assert.Equal(
      FeedVerdict.WontBite,
      Feed(half, gapIndex: 1, strip: 1).Verdict
    ); // 3.0 -> 1.5 is too deep
  }

  [Fact]
  public void A_stand_with_no_roll_set_rolls_nothing() {
    Assert.Equal(
      FeedVerdict.NoRollSet,
      MillFeed
        .Decide(
          null,
          WorkPiece.Fresh(StockForm.Bloom),
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
      Feed(WorkPiece.Fresh(StockForm.Slab), 0).Verdict
    );
    Assert.Equal(
      FeedVerdict.WrongForm,
      MillFeed.Decide(FlatSet, null, 0, 0, Hot, Radius, RollingTemp).Verdict
    );
  }

  [Fact]
  public void An_out_of_range_gap_or_strip_is_a_no_op_not_a_crash() {
    var piece = WorkPiece.Fresh(StockForm.Bloom);
    Assert.Equal(FeedVerdict.NoReduction, Feed(piece, gapIndex: 99).Verdict);
    Assert.Equal(
      FeedVerdict.NoReduction,
      Feed(piece, gapIndex: 0, strip: 99).Verdict
    );
  }

  #endregion
}
