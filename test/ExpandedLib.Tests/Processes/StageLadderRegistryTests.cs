using System.Linq;
using ExpandedLib.Processes;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The merged stage catalogue. Every process registry is contributed to rather than owned: a mod adding a
/// machine family declares the stages that family accepts, a mod adding a stock family declares its whole
/// ladder, and both land in the same place. That is what makes the two extension directions cost the same.
/// See docs/design/mechanics/process-extension.md.
/// </summary>
public class StageLadderRegistryTests {
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

  // Ours: the narrow bar's grooved branch.
  private static StageLadder Ours() =>
    Ladder(
      """
      {
        "family": "shingledbar",
        "shape": "iiex:item/smithed/shingled-bar",
        "stages": [
          { "thickness": 3.00, "element": "ShingledBar1", "acceptedBy": [ "grooved" ] },
          { "thickness": 2.50, "element": "Grooved250", "acceptedBy": [ "grooved" ], "code": "iiex:rolledrod" }
        ]
      }
      """
    );

  private static StageLadderRegistry Fresh() => new();

  #region Contributing

  [Fact]
  public void A_contributed_ladder_is_looked_up_by_its_family() {
    StageLadderRegistry reg = Fresh();
    Assert.Empty(reg.Contribute(Ours()));

    Assert.True(reg.TryGet("shingledbar", out StageLadder? merged));
    Assert.Equal(2, merged!.Stages.Length);
    Assert.Equal("iiex:item/smithed/shingled-bar", merged.Shape);
  }

  [Fact]
  public void An_unclaimed_family_looks_up_nothing() {
    StageLadderRegistry reg = Fresh();
    reg.Contribute(Ours());

    Assert.False(reg.TryGet("castslab", out _));
    Assert.Null(reg.Ladder("castslab"));
    Assert.Null(reg.Ladder(null));
  }

  [Fact]
  public void Two_stock_families_stay_separate() {
    StageLadderRegistry reg = Fresh();
    reg.Contribute(Ours());
    reg.Contribute(
      Ladder(
        """
        {
          "family": "shingledslab",
          "stages": [ { "thickness": 3.00, "acceptedBy": [ "flat" ] } ]
        }
        """
      )
    );

    Assert.Equal(2, reg.Families.Count);
    Assert.Single(reg.Ladder("shingledslab")!.Stages);
  }

  #endregion

  #region A third party adds a machine family

  [Fact]
  public void A_new_family_accepting_an_existing_rung_widens_that_stage() {
    // The cheap extension: someone ships a serrated roll set and declares that it, too, takes the entry
    // stage. The stage is one rung either way, so it must not become two.
    StageLadderRegistry reg = Fresh();
    reg.Contribute(Ours());

    Assert.Empty(
      reg.Contribute(
        Ladder(
          """
          {
            "family": "shingledbar",
            "stages": [ { "thickness": 3.00, "element": "ShingledBar1", "acceptedBy": [ "serrated" ] } ]
          }
          """
        )
      )
    );

    StageLadder merged = reg.Ladder("shingledbar")!;
    Assert.Equal(2, merged.Stages.Length);
    Assert.Equal(
      ["ShingledBar1"],
      merged.AcceptedBy("serrated").Select(s => s.Element)
    );
    Assert.Equal(
      ["ShingledBar1", "Grooved250"],
      merged.AcceptedBy("grooved").Select(s => s.Element)
    );
  }

  [Fact]
  public void A_new_rung_extends_the_ladder() {
    StageLadderRegistry reg = Fresh();
    reg.Contribute(Ours());

    reg.Contribute(
      Ladder(
        """
        {
          "family": "shingledbar",
          "stages": [ { "thickness": 2.00, "element": "Serrated200", "acceptedBy": [ "serrated" ], "code": "othermod:splinerod" } ]
        }
        """
      )
    );

    StageLadder merged = reg.Ladder("shingledbar")!;
    Assert.Equal(3, merged.Stages.Length);
    Assert.Equal("othermod:splinerod", merged.StageAt(2.00f, "serrated")!.Code);
    Assert.Null(merged.StageAt(2.00f, "grooved"));
  }

  [Fact]
  public void A_fork_at_one_thickness_stays_two_stages() {
    // Same gauge, different geometry, different product. Merging these into one rung would lose a branch.
    StageLadderRegistry reg = Fresh();
    reg.Contribute(Ours());
    reg.Contribute(
      Ladder(
        """
        {
          "family": "shingledbar",
          "stages": [ { "thickness": 2.50, "element": "Flattened250", "acceptedBy": [ "flat" ], "code": "iiex:beam" } ]
        }
        """
      )
    );

    StageLadder merged = reg.Ladder("shingledbar")!;
    Assert.Equal(3, merged.Stages.Length);
    Assert.Equal("iiex:rolledrod", merged.StageAt(2.50f, "grooved")!.Code);
    Assert.Equal("iiex:beam", merged.StageAt(2.50f, "flat")!.Code);
  }

  #endregion

  #region Conflicts

  [Fact]
  public void Re_contributing_the_same_ladder_changes_nothing() {
    // Load order is not something a mod can control, so contributing twice must be safe.
    StageLadderRegistry reg = Fresh();
    reg.Contribute(Ours());
    Assert.Empty(reg.Contribute(Ours()));

    Assert.Equal(2, reg.Ladder("shingledbar")!.Stages.Length);
  }

  [Fact]
  public void Redrawing_an_occupied_stage_is_reported_and_the_first_declaration_stands() {
    // Silently taking the last writer would make the ladder depend on mod load order, which nothing can
    // reproduce. The clash is named instead, and the piece keeps rendering as it did.
    StageLadderRegistry reg = Fresh();
    reg.Contribute(Ours());

    var conflicts = reg.Contribute(
      Ladder(
        """
        {
          "family": "shingledbar",
          "stages": [ { "thickness": 2.50, "element": "Hijacked250", "acceptedBy": [ "grooved" ], "code": "othermod:rod" } ]
        }
        """
      )
    );

    Assert.Single(conflicts);
    Assert.Contains("2.5", conflicts[0]);
    Assert.Contains("grooved", conflicts[0]);
    Assert.Equal(
      "Grooved250",
      reg.Ladder("shingledbar")!.StageAt(2.50f, "grooved")!.Element
    );
  }

  [Fact]
  public void A_second_shape_file_for_one_family_is_reported() {
    // The renderer walks one family's ladder by thickness, so its stages must all be addressable from one
    // file. Two shapes for one family means one of them is never reached.
    StageLadderRegistry reg = Fresh();
    reg.Contribute(Ours());

    var conflicts = reg.Contribute(
      Ladder(
        """
        {
          "family": "shingledbar",
          "shape": "othermod:item/other-bar",
          "stages": [ { "thickness": 2.00, "acceptedBy": [ "serrated" ] } ]
        }
        """
      )
    );

    Assert.Single(conflicts);
    Assert.Contains("shape", conflicts[0]);
    Assert.Equal(
      "iiex:item/smithed/shingled-bar",
      reg.Ladder("shingledbar")!.Shape
    );
  }

  #endregion

  [Fact]
  public void Clearing_drops_every_family() {
    StageLadderRegistry reg = Fresh();
    reg.Contribute(Ours());
    reg.Clear();

    Assert.Empty(reg.Families);
    Assert.False(reg.TryGet("shingledbar", out _));
  }
}
