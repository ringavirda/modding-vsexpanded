using System.Linq;
using ExpandedLib.Processes;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The merged stage catalogue. Every process registry is contributed to rather than owned: a mod adding a
/// machine family declares the stages that family accepts, a mod adding a stock family declares its whole
/// route, and both land in the same place. That is what makes the two extension directions cost the same.
/// See docs/design/mechanics/process-extension.md.
/// </summary>
public class ProcessRouteRegistryTests {
  private static ProcessRoute Route(string json) {
    Assert.True(
      ProcessRoute.TryParse(
        new JsonObject(JToken.Parse(json)),
        out ProcessRoute? route,
        out string? error
      ),
      error
    );
    return route!;
  }

  // Ours: the narrow bar's grooved branch.
  private static ProcessRoute Ours() =>
    Route(
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

  private static ProcessRouteRegistry Fresh() => new();

  #region Contributing

  [Fact]
  public void A_contributed_route_is_looked_up_by_its_family() {
    ProcessRouteRegistry reg = Fresh();
    Assert.Empty(reg.Contribute(Ours()));

    Assert.True(reg.TryGet("shingledbar", out ProcessRoute? merged));
    Assert.Equal(2, merged!.Stages.Length);
    Assert.Equal("iiex:item/smithed/shingled-bar", merged.Shape);
  }

  [Fact]
  public void An_unclaimed_family_looks_up_nothing() {
    ProcessRouteRegistry reg = Fresh();
    reg.Contribute(Ours());

    Assert.False(reg.TryGet("castslab", out _));
    Assert.Null(reg.Route("castslab"));
    Assert.Null(reg.Route(null));
  }

  [Fact]
  public void Two_stock_families_stay_separate() {
    ProcessRouteRegistry reg = Fresh();
    reg.Contribute(Ours());
    reg.Contribute(
      Route(
        """
        {
          "family": "shingledslab",
          "stages": [ { "thickness": 3.00, "acceptedBy": [ "flat" ] } ]
        }
        """
      )
    );

    Assert.Equal(2, reg.Families.Count);
    Assert.Single(reg.Route("shingledslab")!.Stages);
  }

  #endregion

  #region A third party adds a machine family

  [Fact]
  public void A_new_family_accepting_an_existing_rung_widens_that_stage() {
    // The cheap extension: someone ships a serrated roll set and declares that it, too, takes the entry
    // stage. The stage is one rung either way, so it must not become two.
    ProcessRouteRegistry reg = Fresh();
    reg.Contribute(Ours());

    Assert.Empty(
      reg.Contribute(
        Route(
          """
          {
            "family": "shingledbar",
            "stages": [ { "thickness": 3.00, "element": "ShingledBar1", "acceptedBy": [ "serrated" ] } ]
          }
          """
        )
      )
    );

    ProcessRoute merged = reg.Route("shingledbar")!;
    Assert.Equal(2, merged.Stages.Length);
    Assert.Equal(
      ["ShingledBar1"],
      merged.RungsFor("serrated").Select(s => s.Element)
    );
    Assert.Equal(
      ["ShingledBar1", "Grooved250"],
      merged.RungsFor("grooved").Select(s => s.Element)
    );
  }

  [Fact]
  public void A_new_rung_extends_the_route() {
    ProcessRouteRegistry reg = Fresh();
    reg.Contribute(Ours());

    reg.Contribute(
      Route(
        """
        {
          "family": "shingledbar",
          "stages": [ { "thickness": 2.00, "element": "Serrated200", "acceptedBy": [ "serrated" ], "code": "othermod:splinerod" } ]
        }
        """
      )
    );

    ProcessRoute merged = reg.Route("shingledbar")!;
    Assert.Equal(3, merged.Stages.Length);
    Assert.Equal("othermod:splinerod", merged.StageAt(2.00f, "serrated")!.Code);
    Assert.Null(merged.StageAt(2.00f, "grooved"));
  }

  [Fact]
  public void A_fork_at_one_thickness_stays_two_stages() {
    // Same gauge, different geometry, different product. Merging these into one rung would lose a branch.
    ProcessRouteRegistry reg = Fresh();
    reg.Contribute(Ours());
    reg.Contribute(
      Route(
        """
        {
          "family": "shingledbar",
          "stages": [ { "thickness": 2.50, "element": "Flattened250", "acceptedBy": [ "flat" ], "code": "iiex:beam" } ]
        }
        """
      )
    );

    ProcessRoute merged = reg.Route("shingledbar")!;
    Assert.Equal(3, merged.Stages.Length);
    Assert.Equal("iiex:rolledrod", merged.StageAt(2.50f, "grooved")!.Code);
    Assert.Equal("iiex:beam", merged.StageAt(2.50f, "flat")!.Code);
  }

  #endregion

  #region Conflicts

  [Fact]
  public void Re_contributing_the_same_route_changes_nothing() {
    // Load order is not something a mod can control, so contributing twice must be safe.
    ProcessRouteRegistry reg = Fresh();
    reg.Contribute(Ours());
    Assert.Empty(reg.Contribute(Ours()));

    Assert.Equal(2, reg.Route("shingledbar")!.Stages.Length);
  }

  [Fact]
  public void Redrawing_an_occupied_stage_is_reported_and_the_first_declaration_stands() {
    // Silently taking the last writer would make the route depend on mod load order, which nothing can
    // reproduce. The clash is named instead, and the piece keeps rendering as it did.
    ProcessRouteRegistry reg = Fresh();
    reg.Contribute(Ours());

    var conflicts = reg.Contribute(
      Route(
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
      reg.Route("shingledbar")!.StageAt(2.50f, "grooved")!.Element
    );
  }

  [Fact]
  public void A_second_shape_file_for_one_family_is_reported() {
    // The renderer walks one family's route by thickness, so its stages must all be addressable from one
    // file. Two shapes for one family means one of them is never reached.
    ProcessRouteRegistry reg = Fresh();
    reg.Contribute(Ours());

    var conflicts = reg.Contribute(
      Route(
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
      reg.Route("shingledbar")!.Shape
    );
  }

  #endregion

  [Fact]
  public void Clearing_drops_every_family() {
    ProcessRouteRegistry reg = Fresh();
    reg.Contribute(Ours());
    reg.Clear();

    Assert.Empty(reg.Families);
    Assert.False(reg.TryGet("shingledbar", out _));
  }
}
