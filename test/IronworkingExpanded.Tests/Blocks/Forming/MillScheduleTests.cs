using System.Linq;
using ExpandedLib.Processes;
using IronworkingExpanded.BlockStructures.Forming;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// What a fitted roll set can do to one stock family: the branch of that family's stage ladder the set's
/// roller family accepts, walked thickest first. The set carries the tooling's own limits - what it will
/// bite, how wide its barrel is, what torque it needs - and the ladder carries the states, so neither names
/// a product the other has to agree with. See docs/design/mechanics/process-extension.md.
/// </summary>
public class MillScheduleTests {
  private static JsonObject Json(string json) => new(JToken.Parse(json));

  // The narrow bar, forked: both families take the 3.0 entry, then grooved runs down to a rod and flat to a
  // beam. The half-steps carry no code, because a gap costs two passes and the piece is not claimable
  // half way through one.
  private const string BarLadder = """
    {
      "family": "shingledbar",
      "shape": "iwex:item/smithed/shingled-bar",
      "stages": [
        { "thickness": 3.0, "element": "ShingledBar1", "acceptedBy": [ "grooved", "flat" ] },
        { "thickness": 2.5, "element": "Grooved250", "acceptedBy": [ "grooved" ] },
        { "thickness": 2.0, "element": "Grooved200", "acceptedBy": [ "grooved" ], "code": "iwex:rolledrod" },
        { "thickness": 2.5, "element": "Flattened250", "acceptedBy": [ "flat" ] },
        { "thickness": 2.0, "element": "Flattened200", "acceptedBy": [ "flat" ], "code": "iwex:beam" }
      ]
    }
    """;

  private const string GroovedSet = """
    {
      "family": "grooved",
      "accepts": [ "shingledbar" ],
      "barrelWidth": 16.0,
      "minTorque": 0.3
    }
    """;

  private static StageLadderRegistry Registry(params string[] ladders) {
    var registry = new StageLadderRegistry();
    foreach (string json in ladders) {
      Assert.True(
        StageLadder.TryParse(
          Json(json),
          out StageLadder? ladder,
          out string? error
        ),
        error
      );
      Assert.Empty(registry.Contribute(ladder!));
    }
    return registry;
  }

  private static RollSetSpec Set(string json) {
    Assert.True(
      RollSetSpec.TryParse(
        Json(json),
        out RollSetSpec? spec,
        out string? error
      ),
      error
    );
    return spec!;
  }

  private static MillSchedule Schedule(
    string set = GroovedSet,
    string form = "shingledbar"
  ) {
    MillSchedule? schedule = MillSchedule.For(
      Set(set),
      form,
      Registry(BarLadder)
    );
    Assert.NotNull(schedule);
    return schedule!;
  }

  #region Which branch a set walks

  [Fact]
  public void A_fitted_set_walks_only_its_own_branch_of_the_ladder() {
    Assert.Equal([3.0f, 2.5f, 2.0f], Schedule().Gaps);
    Assert.Equal(
      ["ShingledBar1", "Grooved250", "Grooved200"],
      Schedule().Stages.Select(s => s.Element)
    );
  }

  [Fact]
  public void The_two_families_share_an_entry_and_part_after_it() {
    MillSchedule flat = Schedule(
      GroovedSet.Replace("\"grooved\"", "\"flat\""),
      "shingledbar"
    );

    Assert.Equal(
      ["ShingledBar1", "Flattened250", "Flattened200"],
      flat.Stages.Select(s => s.Element)
    );
  }

  [Fact]
  public void A_set_whose_family_the_ladder_never_names_has_no_schedule() {
    // A slitting set against a ladder with no slitting stage: the tooling exists, the route does not.
    Assert.Null(
      MillSchedule.For(
        Set(
          GroovedSet.Replace(
            "\"family\": \"grooved\"",
            "\"family\": \"slitting\""
          )
        ),
        "shingledbar",
        Registry(BarLadder)
      )
    );
  }

  [Fact]
  public void A_form_the_set_refuses_has_no_schedule() {
    // The ladder is the states the metal can take; `accepts` is the tooling's own geometry. A narrow
    // barrel refuses a slab whatever states the slab has.
    Assert.Null(
      MillSchedule.For(Set(GroovedSet), "shingledslab", Registry(BarLadder))
    );
  }

  [Fact]
  public void A_form_with_no_ladder_at_all_has_no_schedule() {
    Assert.Null(
      MillSchedule.For(
        Set(GroovedSet.Replace("\"bloom\"", "\"billet\"")),
        "billet",
        Registry(BarLadder)
      )
    );
  }

  [Fact]
  public void A_bare_stand_has_no_schedule() {
    Assert.Null(MillSchedule.For(null, "shingledbar", Registry(BarLadder)));
  }

  #endregion

  #region Walking the branch

  [Fact]
  public void The_next_gap_is_the_first_rung_thinner_than_the_stock() {
    MillSchedule schedule = Schedule();

    Assert.Equal(3.0f, schedule.NextGap(3.5f));
    Assert.Equal(2.5f, schedule.NextGap(3.0f));
    Assert.Equal(2.0f, schedule.NextGap(2.5f));
  }

  [Fact]
  public void Stock_that_has_walked_the_whole_branch_has_no_next_pass() {
    Assert.Null(Schedule().NextGap(2.0f));
    Assert.Null(Schedule().NextGap(1.5f));
  }

  [Fact]
  public void You_cannot_skip_a_rung_to_get_a_deeper_bite() {
    // From 3.0 the only next step is 2.5. The rung spacing is what keeps every pass inside delta_max
    // without the mill checking it.
    Assert.Equal(0.5f, Schedule().NextDraft(3.0f), 4);
    Assert.Equal(0f, Schedule().NextDraft(2.0f), 4);
  }

  [Fact]
  public void A_single_rung_branch_is_a_wide_set_that_needs_a_train() {
    // One rung fills the whole barrel, so there is no sequence to walk along it and the reduction takes a
    // train of stands instead.
    MillSchedule wide = MillSchedule.For(
      Set(GroovedSet.Replace("\"grooved\"", "\"wide\"")),
      "shingledbar",
      Registry(
        """
        {
          "family": "shingledbar",
          "stages": [ { "thickness": 2.0, "acceptedBy": [ "wide" ], "code": "lpex:heavyplate" } ]
        }
        """
      )
    )!;

    Assert.True(wide.IsWide);
    Assert.False(Schedule().IsWide);
  }

  #endregion

  #region Stopping points

  [Fact]
  public void The_product_is_the_stage_you_stop_at() {
    Assert.Equal("iwex:rolledrod", Schedule().OutputAt(2.0f));
  }

  [Fact]
  public void A_stage_naming_no_code_leaves_the_piece_as_stock() {
    // A half-step is a real rung and not a product: the piece has another pass to take before it is even.
    Assert.Null(Schedule().OutputAt(2.5f));
    Assert.Null(Schedule().OutputAt(3.0f));
  }

  [Fact]
  public void A_thickness_off_the_branch_names_no_product() {
    Assert.Null(Schedule().OutputAt(2.25f));
  }

  [Fact]
  public void The_other_branch_s_product_is_not_reachable_from_this_one() {
    // Same gauge, same stock, different set: 2.0 is a rod on grooved rolls and a beam on flat ones. Keyed
    // on gap alone the two could not both exist.
    Assert.Equal(
      "iwex:beam",
      Schedule(GroovedSet.Replace("\"grooved\"", "\"flat\"")).OutputAt(2.0f)
    );
  }

  [Fact]
  public void A_gauge_a_hair_off_the_rung_still_finds_its_stopping_point() {
    // The gauge a piece carries is arrived at by arithmetic, not by re-reading the literal, so an exact
    // float match would miss the product the player just rolled. The old gap-keyed lookup compared with
    // `==` and is why this is pinned.
    Assert.Equal("iwex:rolledrod", Schedule().OutputAt(2.0f + 1e-5f));
    Assert.Equal("iwex:rolledrod", Schedule().OutputAt(2.0f - 1e-5f));
  }

  [Fact]
  public void A_gauge_between_two_rungs_is_not_rounded_onto_either() {
    // The tolerance closes a float gap, not a real one: a piece genuinely part way between rungs is still
    // stock, or a player could claim a product one pass early.
    Assert.Null(Schedule().OutputAt(2.01f));
  }

  #endregion
}
