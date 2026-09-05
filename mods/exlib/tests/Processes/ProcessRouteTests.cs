using System.Linq;
using ExpandedLib.Processes;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The stage route a sequence process walks: one stock family's states, each declaring the thickness it
/// sits at, which machine families accept it, and - only when it is a stopping point - the code of the item
/// it becomes. A stage is addressed by (thickness, accepting family), never by thickness alone, because two
/// families draw different geometry at the same gauge. See docs/design/mechanics/process-extension.md.
/// </summary>
public class ProcessRouteTests {
  private static JsonObject Json(string json) => new(JToken.Parse(json));

  // The narrow route as item-shingled-bar.json draws it: a shared entry stage both families take, then a
  // grooved and a flattened branch at the same three gauges.
  private const string NarrowRoute = """
    {
      "schema": 1,
      "family": "shingledbar",
      "shape": "iiex:item/smithed/shingled-bar",
      "stages": [
        { "thickness": 3.00, "element": "ShingledBar1", "acceptedBy": [ "grooved", "flat" ] },
        { "thickness": 2.75, "element": "Grooved275", "acceptedBy": [ "grooved" ] },
        { "thickness": 2.50, "element": "Grooved250", "acceptedBy": [ "grooved" ], "code": "iiex:rolledrod" },
        { "thickness": 2.75, "element": "Flattened275", "acceptedBy": [ "flat" ] },
        { "thickness": 2.50, "element": "Flattened250", "acceptedBy": [ "flat" ], "code": "iiex:beam" }
      ]
    }
    """;

  private static ProcessRoute Parse(string json) {
    Assert.True(
      ProcessRoute.TryParse(
        Json(json),
        out ProcessRoute? route,
        out string? error
      ),
      error
    );
    return route!;
  }

  private static string Rejects(string json) {
    Assert.False(ProcessRoute.TryParse(Json(json), out _, out string? error));
    Assert.NotNull(error);
    return error!;
  }

  #region Parsing

  [Fact]
  public void A_well_formed_route_parses_every_field() {
    ProcessRoute route = Parse(NarrowRoute);

    Assert.Equal("shingledbar", route.Family);
    Assert.Equal("iiex:item/smithed/shingled-bar", route.Shape);
    Assert.Equal(1, route.Schema);
    Assert.Equal(5, route.Stages.Length);

    ProcessStage entry = route.Stages[0];
    Assert.Equal(3.00f, entry.Thickness);
    Assert.Equal("ShingledBar1", entry.Element);
    Assert.Equal(["grooved", "flat"], entry.AcceptedBy);
    Assert.Null(entry.Code);
  }

  [Fact]
  public void A_route_naming_no_family_is_rejected() {
    // The family is the key the registry merges on, so a route without one has nowhere to go.
    Assert.Contains(
      "family",
      Rejects(NarrowRoute.Replace("\"family\"", "\"unused\""))
    );
  }

  [Fact]
  public void A_route_with_no_stages_is_rejected() {
    Assert.Contains(
      "stages",
      Rejects("""{ "family": "shingledbar", "stages": [ ] }""")
    );
  }

  [Fact]
  public void A_stage_no_family_accepts_is_rejected() {
    // A stage nothing accepts is a state no machine can reach, so it is caught at load rather than read as
    // a dead rung the walk silently steps over.
    Assert.Contains(
      "acceptedBy",
      Rejects(
        NarrowRoute.Replace(
          "\"acceptedBy\": [ \"grooved\", \"flat\" ]",
          "\"acceptedBy\": [ ]"
        )
      )
    );
  }

  [Fact]
  public void A_thickness_at_or_below_zero_is_rejected() {
    Assert.Contains(
      "thickness",
      Rejects(
        NarrowRoute.Replace(
          "\"thickness\": 2.50, \"element\": \"Grooved250\"",
          "\"thickness\": 0, \"element\": \"Grooved250\""
        )
      )
    );
  }

  [Fact]
  public void Two_stages_at_one_thickness_for_one_family_are_rejected() {
    // Ambiguous: the walk could not say which of the two a piece at that gauge is on.
    Assert.Contains(
      "2.75",
      Rejects(
        NarrowRoute.Replace(
          "\"element\": \"Flattened275\", \"acceptedBy\": [ \"flat\" ]",
          "\"element\": \"Flattened275\", \"acceptedBy\": [ \"grooved\" ]"
        )
      )
    );
  }

  [Fact]
  public void Two_families_may_declare_the_same_thickness() {
    // The fork's whole point: grooved and flat both draw a 2.75 state, and they are different geometry.
    ProcessRoute route = Parse(NarrowRoute);

    Assert.Equal("Grooved275", route.StageAt(2.75f, "grooved")?.Element);
    Assert.Equal("Flattened275", route.StageAt(2.75f, "flat")?.Element);
  }

  [Fact]
  public void A_missing_attribute_is_reported_rather_than_throwing() {
    Assert.False(ProcessRoute.TryParse(null, out _, out string? error));
    Assert.Contains(ProcessRoute.AttributeKey, error!);
  }

  [Fact]
  public void A_stopping_point_generates_an_item_unless_it_opts_out() {
    // The default is to build the item, because the common case is a new product. Opting out is how a
    // declaration points at a code that already exists - a vanilla rod, or one the mod ships itself.
    ProcessRoute route = Parse(NarrowRoute);
    Assert.True(route.StageAt(2.50f, "grooved")!.Generate);

    ProcessRoute optedOut = Parse(
      NarrowRoute.Replace(
        "\"code\": \"iiex:rolledrod\"",
        "\"code\": \"iiex:rolledrod\", \"generate\": false"
      )
    );
    Assert.False(optedOut.StageAt(2.50f, "grooved")!.Generate);
  }

  [Fact]
  public void A_renamed_code_carries_the_name_it_had() {
    // Exlib sees only the current catalogue, so a code that vanished and one that appeared are
    // indistinguishable from a rename without the hint.
    ProcessRoute route = Parse(
      NarrowRoute.Replace(
        "\"code\": \"iiex:rolledrod\"",
        "\"code\": \"iiex:rolledrod\", \"formerCodes\": [ \"iiex:wirerod\" ]"
      )
    );

    Assert.Equal(
      ["iiex:wirerod"],
      route.StageAt(2.50f, "grooved")!.FormerCodes
    );
    Assert.Empty(route.StageAt(2.75f, "grooved")!.FormerCodes);
  }

  [Fact]
  public void An_undeclared_schema_reads_as_the_first_one() {
    // Routes authored before the field existed are schema 1 by definition; nothing else has shipped.
    ProcessRoute route = Parse(NarrowRoute.Replace("\"schema\": 1,", ""));
    Assert.Equal(1, route.Schema);
  }

  [Fact]
  public void A_route_from_a_newer_build_is_refused_rather_than_mis_read() {
    // Reading it as the form we do know would silently mis-parse someone's content. The message names
    // both numbers because the fix is to update the library.
    string error = Rejects(
      NarrowRoute.Replace("\"schema\": 1,", "\"schema\": 99,")
    );

    Assert.Contains("99", error);
  }

  #endregion

  #region Walking a family's branch

  [Fact]
  public void A_family_walks_only_the_stages_it_accepts_thickest_first() {
    ProcessRoute route = Parse(NarrowRoute);

    Assert.Equal(
      [3.00f, 2.75f, 2.50f],
      route.RungsFor("grooved").Select(s => s.Thickness)
    );
    Assert.Equal(
      ["ShingledBar1", "Grooved275", "Grooved250"],
      route.RungsFor("grooved").Select(s => s.Element)
    );
  }

  [Fact]
  public void The_two_branches_share_their_entry_stage_and_diverge_after_it() {
    ProcessRoute route = Parse(NarrowRoute);

    Assert.Equal(
      ["ShingledBar1", "Flattened275", "Flattened250"],
      route.RungsFor("flat").Select(s => s.Element)
    );
  }

  [Fact]
  public void A_family_the_route_never_names_walks_nothing() {
    Assert.Empty(Parse(NarrowRoute).RungsFor("slitting"));
  }

  #endregion

  #region Half-steps

  private const string HalfStepRoute = """
    { "schema": 1, "family": "rod", "stages": [
        { "thickness": 2.0, "acceptedBy": ["flat"], "element": "RolledRod200" },
        { "thickness": 1.75, "acceptedBy": ["flat"], "element": "Flattened175", "halfStep": true },
        { "thickness": 1.5, "acceptedBy": ["flat"], "element": "Flattened150" } ] }
    """;

  [Fact]
  public void A_half_step_is_not_a_gauge_the_machine_can_be_set_to() {
    // The whole point: a reduction is taken in two rounds, so the state between two rungs is real and
    // drawn - but offering it as a setting would double the gap bands on the deck and make the two-round
    // model a four-gap one.
    Assert.Equal(
      [2.0f, 1.5f],
      Parse(HalfStepRoute).RungsFor("flat").Select(s => s.Thickness)
    );
  }

  [Fact]
  public void A_half_step_is_still_found_by_the_thing_that_draws_it() {
    // Excluded from the walk, present in the route. If it were dropped entirely the renderer would fall
    // back to scaling the base shape, which is exactly the art this stage exists to replace.
    ProcessStage? half = Parse(HalfStepRoute).StageAt(1.75f, "flat");

    Assert.NotNull(half);
    Assert.True(half!.HalfStep);
    Assert.False(half.IsRung);
    Assert.Equal("Flattened175", half.Element);
  }

  [Fact]
  public void A_half_step_may_not_be_a_stopping_point() {
    // A product declared where the player cannot stop reads as reachable and is obtainable nowhere, so
    // it fails at parse rather than at claim time.
    Assert.False(
      ProcessRoute.TryParse(
        Json(
          """
          { "schema": 1, "family": "rod", "stages": [
              { "thickness": 1.75, "acceptedBy": ["flat"], "halfStep": true, "code": "iiex:nailplate" } ] }
          """
        ),
        out _,
        out string? error
      )
    );
    Assert.Contains("half-step", error);
  }

  [Fact]
  public void A_stage_is_a_stopping_point_only_when_it_names_a_code() {
    ProcessRoute route = Parse(NarrowRoute);

    Assert.True(route.StageAt(2.50f, "grooved")!.IsStoppingPoint);
    Assert.Equal("iiex:rolledrod", route.StageAt(2.50f, "grooved")!.Code);
    Assert.False(route.StageAt(2.75f, "grooved")!.IsStoppingPoint);
  }

  [Fact]
  public void A_thickness_off_the_route_addresses_no_stage() {
    Assert.Null(Parse(NarrowRoute).StageAt(2.60f, "grooved"));
    Assert.Null(Parse(NarrowRoute).StageAt(2.50f, "slitting"));
  }

  #endregion
}
