using ExpandedLib.Processes;
using IronworkingExpanded.BlockStructures.Forming;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The shear's feed decision: whether a piece offered to the blades is cut, and what the stroke asks of the
/// run. The rule that shapes all of it is that temperature does not gate a cut - shearing needs force, not
/// friction - so a cold cut is not refused, it is only dearer.
/// See docs/design/machines/shear.md.
/// </summary>
public class ShearFeedTests {
  private const float RollingTemp = 900f;
  private const float ColdMultiplier = 3f; // ShearColdTorqueMultiplier, proposed
  private const int AnyTemper = 0;

  private static ProcessJob Crop(
    float minTorque = 0.2f,
    int count = 4,
    int minTier = 0
  ) =>
    new(
      "iwex:stock-shingledbar",
      "iwex:rolledplate",
      count,
      Stage: 1.0f,
      Family: "flat",
      MinTorque: minTorque,
      MinTier: minTier
    );

  private static ProcessJob Convert() =>
    new(
      "iwex:nailplate",
      "game:metalnailsandstrips",
      4,
      Stage: null,
      Family: null,
      MinTorque: 0.2f
    );

  private static WorkPiece Piece(int cropped = 0) =>
    new(StockForm.ShingledBar, 1.0f, 0f, [false], "flat", cropped);

  private static ShearDecision Offer(
    ProcessJob? job,
    WorkPiece? piece,
    bool hasBladeSet = true,
    int bladeTier = AnyTemper,
    float tempC = 1100f,
    float availableTorque = 1f,
    float speed = 1f
  ) =>
    ShearFeed.Decide(
      hasBladeSet,
      bladeTier,
      job,
      piece,
      tempC,
      RollingTemp,
      ColdMultiplier,
      availableTorque,
      speed
    );

  #region What the machine is missing

  [Fact]
  public void A_bare_machine_cuts_nothing() {
    Assert.Equal(
      ShearVerdict.NoBladeSet,
      Offer(Crop(), Piece(), hasBladeSet: false).Verdict
    );
  }

  [Fact]
  public void A_stage_that_names_no_job_is_not_a_stopping_point() {
    // The ladder stays open: a gauge nobody has declared a crop for is stock and leaves the machine as it
    // came, rather than being refused as a mistake.
    ShearDecision d = Offer(job: null, piece: Piece());

    Assert.Equal(ShearVerdict.NoJob, d.Verdict);
    Assert.Null(d.Job);
    Assert.Equal(0f, d.RequiredTorque);
  }

  #endregion

  #region What is wrong with the piece

  [Fact]
  public void A_whole_piece_is_cut_and_reports_the_job_that_took_it() {
    ProcessJob job = Crop();
    ShearDecision d = Offer(job, Piece());

    Assert.True(d.Accepted);
    Assert.Same(job, d.Job);
  }

  [Fact]
  public void A_worked_out_piece_is_spent_rather_than_jobless() {
    // The job is right and the piece is finished, so the player is told about the piece.
    ShearDecision d = Offer(Crop(count: 4), Piece(cropped: 4));

    Assert.Equal(ShearVerdict.Spent, d.Verdict);
    Assert.NotNull(d.Job);
  }

  [Fact]
  public void The_piece_is_cut_until_the_last_crop_and_refused_after_it() {
    // Four crops means four strokes: the fourth is taken and the fifth has nothing left to take.
    for (int taken = 0; taken < 4; taken++)
      Assert.True(
        Offer(Crop(count: 4), Piece(cropped: taken)).Accepted,
        $"the stroke after {taken} crops should still be legal"
      );

    Assert.Equal(
      ShearVerdict.Spent,
      Offer(Crop(count: 4), Piece(cropped: 4)).Verdict
    );
  }

  [Fact]
  public void A_whole_item_job_needs_no_piece_and_can_never_be_spent() {
    // It converts rather than crops, so there is no remainder to run out and no work piece to read.
    Assert.True(Offer(Convert(), piece: null).Accepted);
  }

  [Fact]
  public void A_staged_job_offered_something_that_is_not_stock_has_no_job_at_all() {
    // A staged job is addressed by gauge. A stack carrying no piece is not what it was declared for, which
    // is a job the machine does not have rather than a piece that is finished.
    ShearDecision d = Offer(Crop(), piece: null);

    Assert.Equal(ShearVerdict.NoJob, d.Verdict);
    Assert.Null(d.Job);
  }

  [Fact]
  public void A_blade_below_the_jobs_temper_floor_will_not_take_it() {
    Assert.Equal(
      ShearVerdict.BladeTooSoft,
      Offer(Crop(minTier: 3), Piece(), bladeTier: 2).Verdict
    );
    Assert.True(Offer(Crop(minTier: 3), Piece(), bladeTier: 3).Accepted);
  }

  #endregion

  #region What the run has to supply

  [Fact]
  public void A_stopped_run_cannot_start_a_stroke_at_any_torque() {
    ShearDecision d = Offer(Crop(), Piece(), availableTorque: 1000f, speed: 0f);

    Assert.Equal(ShearVerdict.NotTurning, d.Verdict);
    Assert.True(d.RequiredTorque > 0f, "the cost is still reported");
  }

  [Fact]
  public void A_hot_cut_asks_the_jobs_own_torque_and_nothing_more() {
    Assert.Equal(
      0.2f,
      ShearFeed.RequiredTorque(Crop(0.2f), 1100f, RollingTemp, ColdMultiplier),
      4
    );
    // At exactly rolling heat the stock still counts as hot, matching the mill's own boundary.
    Assert.Equal(
      0.2f,
      ShearFeed.RequiredTorque(Crop(0.2f), 900f, RollingTemp, ColdMultiplier),
      4
    );
  }

  [Fact]
  public void A_cold_cut_is_dearer_but_never_refused_for_being_cold() {
    // The whole cold-shear ruling: force, not friction. A cold piece is cut by a run that can drive it.
    ShearDecision cold = Offer(
      Crop(0.2f),
      Piece(),
      tempC: 20f,
      availableTorque: 1f
    );

    Assert.True(cold.Accepted);
    Assert.Equal(0.6f, cold.RequiredTorque, 4); // 0.2 x 3
  }

  [Fact]
  public void A_run_that_carries_the_hot_cut_may_still_fail_the_cold_one() {
    // Cold shearing is a power achievement rather than a tier unlock: the same blades on a stronger run.
    Assert.True(
      Offer(Crop(0.2f), Piece(), tempC: 1100f, availableTorque: 0.3f).Accepted
    );
    Assert.Equal(
      ShearVerdict.NotEnoughDrive,
      Offer(Crop(0.2f), Piece(), tempC: 20f, availableTorque: 0.3f).Verdict
    );
  }

  [Fact]
  public void A_cold_multiplier_below_one_cannot_make_a_cut_cheaper() {
    Assert.Equal(
      0.2f,
      ShearFeed.RequiredTorque(
        Crop(0.2f),
        20f,
        RollingTemp,
        coldMultiplier: 0.5f
      ),
      4
    );
  }

  #endregion
}
