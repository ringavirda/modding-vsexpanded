using ExpandedLib.Processes;
using IronworkingExpanded.BlockStructures.Forming;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// What a piece of stock looks like in the hand. A stage that names no <c>code</c> is the same item at a
/// different gauge, so it cannot be an item def - it is rendered by swapping the model reference from the
/// stack's own thickness, as vanilla's <c>ItemWorkItem</c> does for its voxel state. This suite covers the
/// decision; the upload itself needs a client and is not reachable headlessly.
/// See docs/design/mechanics/process-extension.md.
/// </summary>
public class StockRenderStateTests {
  private static StageLadder Ladder(string json) {
    Assert.True(
      StageLadder.TryParse(
        new JsonObject(JToken.Parse(json)),
        out StageLadder? ladder,
        out string? error
      ),
      error
    );
    return ladder!;
  }

  // Both branches draw a 2.0 state, and they are different geometry - which is why thickness alone cannot
  // choose between them.
  private static StageLadder Forked() =>
    Ladder(
      """
      {
        "family": "bloom",
        "shape": "iwex:item/smithed/shingled-bar",
        "stages": [
          { "thickness": 3.0, "element": "ShingledBar1", "acceptedBy": [ "grooved", "flat" ] },
          { "thickness": 2.0, "element": "Grooved200", "acceptedBy": [ "grooved" ] },
          { "thickness": 2.0, "element": "Flattened200", "acceptedBy": [ "flat" ] }
        ]
      }
      """
    );

  #region Which pieces need a mesh of their own

  [Fact]
  public void A_piece_straight_off_the_helve_is_its_base_shape() {
    // The authored shape already is the unworked stage, so composing one would be work for nothing.
    Assert.True(StockMesh.IsBaseState(WorkPiece.Fresh(StockForm.Bloom)));
  }

  [Fact]
  public void A_piece_rolled_down_a_gauge_is_not_its_base_shape() {
    // The bug this replaces: every single-sided piece read as "base state" because one side is always
    // even, so a bloom taken from 3.0 to 2.0 looked exactly like one straight off the helve.
    WorkPiece rolled = WorkPiece.Fresh(StockForm.Bloom).WithStrip(0, 2.0f);

    Assert.False(StockMesh.IsBaseState(rolled));
  }

  [Fact]
  public void A_piece_split_across_the_barrel_is_not_its_base_shape() {
    Assert.False(
      StockMesh.IsBaseState(WorkPiece.Fresh(StockForm.Bloom).Resplit(2))
    );
  }

  #endregion

  #region Walking the ladder by thickness

  [Fact]
  public void The_drawn_stage_is_the_one_at_the_piece_s_gauge_on_its_own_branch() {
    WorkPiece rolled = WorkPiece.Fresh(StockForm.Bloom).WithStrip(0, 2.0f) with {
      Family = "grooved",
    };

    Assert.Equal("Grooved200", StockMesh.ElementFor(Forked(), rolled));
  }

  [Fact]
  public void The_other_branch_draws_the_same_gauge_differently() {
    WorkPiece rolled = WorkPiece.Fresh(StockForm.Bloom).WithStrip(0, 2.0f) with {
      Family = "flat",
    };

    Assert.Equal("Flattened200", StockMesh.ElementFor(Forked(), rolled));
  }

  [Fact]
  public void A_piece_that_names_no_branch_picks_no_element() {
    // A piece rolled before the family was recorded, or one never rolled at all. It falls back to the
    // composed mesh rather than guessing a branch, because at a fork the guess is visibly wrong half the
    // time.
    WorkPiece rolled = WorkPiece.Fresh(StockForm.Bloom).WithStrip(0, 2.0f);

    Assert.Null(StockMesh.ElementFor(Forked(), rolled));
  }

  [Fact]
  public void A_gauge_the_ladder_does_not_draw_picks_no_element() {
    WorkPiece rolled = WorkPiece.Fresh(StockForm.Bloom).WithStrip(0, 1.75f) with {
      Family = "grooved",
    };

    Assert.Null(StockMesh.ElementFor(Forked(), rolled));
  }

  [Fact]
  public void A_ladder_with_no_shape_file_draws_no_element() {
    // An element name means nothing without the file holding it, and half a reference would render as
    // nothing at all rather than as the fallback.
    WorkPiece rolled = WorkPiece.Fresh(StockForm.Bloom).WithStrip(0, 2.0f) with {
      Family = "grooved",
    };

    Assert.Null(
      StockMesh.ElementFor(
        Ladder(
          """
          {
            "family": "bloom",
            "stages": [ { "thickness": 2.0, "element": "Grooved200", "acceptedBy": [ "grooved" ] } ]
          }
          """
        ),
        rolled
      )
    );
  }

  [Fact]
  public void No_ladder_at_all_draws_no_element() {
    Assert.Null(StockMesh.ElementFor(null, WorkPiece.Fresh(StockForm.Bloom)));
  }

  #endregion

  #region The handbook trap

  [Fact]
  public void The_cache_key_is_the_geometry_and_nothing_about_the_stack() {
    // The handbook clones the stack every frame, so a key carrying anything per-stack would upload a new
    // mesh per frame and leak every one of them. Two pieces that look alike must share a key however
    // differently they got there.
    var turned = new WorkPiece(StockForm.Bloom, [2.0f], [true]);
    var untouched = new WorkPiece(StockForm.Bloom, [2.0f], [false]);

    Assert.Equal(StockMesh.CacheKey(turned), StockMesh.CacheKey(untouched));
  }

  [Fact]
  public void Two_branches_at_one_gauge_do_not_share_a_cached_mesh() {
    // They are different geometry, so a key blind to the branch would show one piece as the other.
    WorkPiece grooved = new WorkPiece(StockForm.Bloom, [2.0f], [false]) with {
      Family = "grooved",
    };
    WorkPiece flat = grooved with { Family = "flat" };

    Assert.NotEqual(StockMesh.CacheKey(grooved), StockMesh.CacheKey(flat));
  }

  #endregion
}
