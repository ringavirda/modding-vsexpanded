using System.Linq;
using ExpandedLib.Processes;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The stage ladder a sequence process walks: one stock family's states, each declaring the thickness it
/// sits at, which machine families accept it, and - only when it is a stopping point - the code of the item
/// it becomes. A stage is addressed by (thickness, accepting family), never by thickness alone, because two
/// families draw different geometry at the same gauge. See docs/design/mechanics/process-extension.md.
/// </summary>
public class StageLadderTests {
  private static JsonObject Json(string json) => new(JToken.Parse(json));

  // The narrow ladder as item-shingled-bar.json draws it: a shared entry stage both families take, then a
  // grooved and a flattened branch at the same three gauges.
  private const string NarrowLadder = """
    {
      "schema": 1,
      "family": "shingledbar",
      "shape": "iwex:item/smithed/shingled-bar",
      "stages": [
        { "thickness": 3.00, "element": "ShingledBar1", "acceptedBy": [ "grooved", "flat" ] },
        { "thickness": 2.75, "element": "Grooved275", "acceptedBy": [ "grooved" ] },
        { "thickness": 2.50, "element": "Grooved250", "acceptedBy": [ "grooved" ], "code": "iwex:rolledrod" },
        { "thickness": 2.75, "element": "Flattened275", "acceptedBy": [ "flat" ] },
        { "thickness": 2.50, "element": "Flattened250", "acceptedBy": [ "flat" ], "code": "iwex:beam" }
      ]
    }
    """;

  private static StageLadder Parse(string json) {
    Assert.True(
      StageLadder.TryParse(
        Json(json),
        out StageLadder? ladder,
        out string? error
      ),
      error
    );
    return ladder!;
  }

  private static string Rejects(string json) {
    Assert.False(StageLadder.TryParse(Json(json), out _, out string? error));
    Assert.NotNull(error);
    return error!;
  }

  #region Parsing

  [Fact]
  public void A_well_formed_ladder_parses_every_field() {
    StageLadder ladder = Parse(NarrowLadder);

    Assert.Equal("shingledbar", ladder.Family);
    Assert.Equal("iwex:item/smithed/shingled-bar", ladder.Shape);
    Assert.Equal(1, ladder.Schema);
    Assert.Equal(5, ladder.Stages.Length);

    ProcessStage entry = ladder.Stages[0];
    Assert.Equal(3.00f, entry.Thickness);
    Assert.Equal("ShingledBar1", entry.Element);
    Assert.Equal(["grooved", "flat"], entry.AcceptedBy);
    Assert.Null(entry.Code);
  }

  [Fact]
  public void A_ladder_naming_no_family_is_rejected() {
    // The family is the key the registry merges on, so a ladder without one has nowhere to go.
    Assert.Contains(
      "family",
      Rejects(NarrowLadder.Replace("\"family\"", "\"unused\""))
    );
  }

  [Fact]
  public void A_ladder_with_no_stages_is_rejected() {
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
        NarrowLadder.Replace(
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
        NarrowLadder.Replace(
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
        NarrowLadder.Replace(
          "\"element\": \"Flattened275\", \"acceptedBy\": [ \"flat\" ]",
          "\"element\": \"Flattened275\", \"acceptedBy\": [ \"grooved\" ]"
        )
      )
    );
  }

  [Fact]
  public void Two_families_may_declare_the_same_thickness() {
    // The fork's whole point: grooved and flat both draw a 2.75 state, and they are different geometry.
    StageLadder ladder = Parse(NarrowLadder);

    Assert.Equal("Grooved275", ladder.StageAt(2.75f, "grooved")?.Element);
    Assert.Equal("Flattened275", ladder.StageAt(2.75f, "flat")?.Element);
  }

  [Fact]
  public void A_missing_attribute_is_reported_rather_than_throwing() {
    Assert.False(StageLadder.TryParse(null, out _, out string? error));
    Assert.Contains(StageLadder.AttributeKey, error!);
  }

  [Fact]
  public void A_stopping_point_generates_an_item_unless_it_opts_out() {
    // The default is to build the item, because the common case is a new product. Opting out is how a
    // declaration points at a code that already exists - a vanilla rod, or one the mod ships itself.
    StageLadder ladder = Parse(NarrowLadder);
    Assert.True(ladder.StageAt(2.50f, "grooved")!.Generate);

    StageLadder optedOut = Parse(
      NarrowLadder.Replace(
        "\"code\": \"iwex:rolledrod\"",
        "\"code\": \"iwex:rolledrod\", \"generate\": false"
      )
    );
    Assert.False(optedOut.StageAt(2.50f, "grooved")!.Generate);
  }

  [Fact]
  public void A_renamed_code_carries_the_name_it_had() {
    // Exlib sees only the current catalogue, so a code that vanished and one that appeared are
    // indistinguishable from a rename without the hint.
    StageLadder ladder = Parse(
      NarrowLadder.Replace(
        "\"code\": \"iwex:rolledrod\"",
        "\"code\": \"iwex:rolledrod\", \"formerCodes\": [ \"iwex:wirerod\" ]"
      )
    );

    Assert.Equal(
      ["iwex:wirerod"],
      ladder.StageAt(2.50f, "grooved")!.FormerCodes
    );
    Assert.Empty(ladder.StageAt(2.75f, "grooved")!.FormerCodes);
  }

  [Fact]
  public void An_undeclared_schema_reads_as_the_first_one() {
    // Ladders authored before the field existed are schema 1 by definition; nothing else has shipped.
    StageLadder ladder = Parse(NarrowLadder.Replace("\"schema\": 1,", ""));
    Assert.Equal(1, ladder.Schema);
  }

  [Fact]
  public void A_ladder_from_a_newer_build_is_refused_rather_than_mis_read() {
    // Reading it as the form we do know would silently mis-parse someone's content. The message names
    // both numbers because the fix is to update the library.
    string error = Rejects(
      NarrowLadder.Replace("\"schema\": 1,", "\"schema\": 99,")
    );

    Assert.Contains("99", error);
  }

  #endregion

  #region Walking a family's branch

  [Fact]
  public void A_family_walks_only_the_stages_it_accepts_thickest_first() {
    StageLadder ladder = Parse(NarrowLadder);

    Assert.Equal(
      [3.00f, 2.75f, 2.50f],
      ladder.AcceptedBy("grooved").Select(s => s.Thickness)
    );
    Assert.Equal(
      ["ShingledBar1", "Grooved275", "Grooved250"],
      ladder.AcceptedBy("grooved").Select(s => s.Element)
    );
  }

  [Fact]
  public void The_two_branches_share_their_entry_stage_and_diverge_after_it() {
    StageLadder ladder = Parse(NarrowLadder);

    Assert.Equal(
      ["ShingledBar1", "Flattened275", "Flattened250"],
      ladder.AcceptedBy("flat").Select(s => s.Element)
    );
  }

  [Fact]
  public void A_family_the_ladder_never_names_walks_nothing() {
    Assert.Empty(Parse(NarrowLadder).AcceptedBy("slitting"));
  }

  [Fact]
  public void A_stage_is_a_stopping_point_only_when_it_names_a_code() {
    StageLadder ladder = Parse(NarrowLadder);

    Assert.True(ladder.StageAt(2.50f, "grooved")!.IsStoppingPoint);
    Assert.Equal("iwex:rolledrod", ladder.StageAt(2.50f, "grooved")!.Code);
    Assert.False(ladder.StageAt(2.75f, "grooved")!.IsStoppingPoint);
  }

  [Fact]
  public void A_thickness_off_the_ladder_addresses_no_stage() {
    Assert.Null(Parse(NarrowLadder).StageAt(2.60f, "grooved"));
    Assert.Null(Parse(NarrowLadder).StageAt(2.50f, "slitting"));
  }

  #endregion
}
