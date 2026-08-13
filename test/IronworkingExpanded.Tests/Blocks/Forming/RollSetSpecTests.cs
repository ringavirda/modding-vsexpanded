using IronworkingExpanded.BlockStructures.Forming;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The roll-set tooling contract. A set carries what the tooling itself decides - which roller family it
/// is, which stock it will bite, how wide its barrel is and what torque it needs to turn - and nothing
/// about what the metal becomes. The states are the stock family's stage ladder
/// (<see cref="MillScheduleTests"/>), so a set names no product and a product needs no set edited.
/// See docs/design/items/roll-sets.md.
/// </summary>
public class RollSetSpecTests {
  private static JsonObject Json(string json) => new(JToken.Parse(json));

  private const string FlatSet = """
    {
      "schema": 1,
      "family": "flat",
      "accepts": [ "shingledbar", "billet" ],
      "barrelWidth": 4.0,
      "minTorque": 2.0
    }
    """;

  private static RollSetSpec Parse(string json) {
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

  private static string Rejects(string json) {
    Assert.False(RollSetSpec.TryParse(Json(json), out _, out string? error));
    Assert.NotNull(error);
    return error!;
  }

  #region Parsing

  [Fact]
  public void A_well_formed_set_parses_every_field() {
    RollSetSpec spec = Parse(FlatSet);

    Assert.Equal(1, spec.Schema);
    Assert.Equal("flat", spec.Family);
    Assert.Equal(["shingledbar", "billet"], spec.Accepts);
    Assert.Equal(4.0f, spec.BarrelWidth);
    Assert.Equal(2.0f, spec.MinTorque);
  }

  [Fact]
  public void An_undeclared_schema_reads_as_the_first_one() {
    Assert.Equal(1, Parse(FlatSet.Replace("\"schema\": 1,", "")).Schema);
  }

  [Fact]
  public void A_set_from_a_newer_build_is_refused_rather_than_mis_read() {
    Assert.Contains(
      "99",
      Rejects(FlatSet.Replace("\"schema\": 1,", "\"schema\": 99,"))
    );
  }

  [Fact]
  public void A_set_belonging_to_no_roller_family_is_rejected() {
    // The family is what selects the set's branch of a stage ladder, so a set without one can roll nothing.
    Assert.Contains(
      "family",
      Rejects(FlatSet.Replace("\"family\"", "\"unused\""))
    );
  }

  [Fact]
  public void A_set_that_bites_nothing_is_rejected() {
    Assert.Contains(
      "accepts",
      Rejects(FlatSet.Replace("[ \"shingledbar\", \"billet\" ]", "[ ]"))
    );
  }

  [Fact]
  public void A_barrel_width_is_required_because_the_mill_cannot_guess_it() {
    Assert.Contains(
      "barrelWidth",
      Rejects(FlatSet.Replace("\"barrelWidth\"", "\"unused\""))
    );
  }

  [Fact]
  public void A_missing_attribute_is_reported_rather_than_throwing() {
    Assert.False(RollSetSpec.TryParse(null, out _, out string? error));
    Assert.Contains(RollSetSpec.AttributeKey, error!);
  }

  [Fact]
  public void A_set_only_bites_the_forms_it_declares() {
    RollSetSpec spec = Parse(FlatSet);

    Assert.True(spec.AcceptsForm("shingledbar"));
    Assert.False(spec.AcceptsForm("shingledslab"));
    Assert.False(spec.AcceptsForm(null));
  }

  #endregion

  #region Passes per gap (the width lesson)

  [Fact]
  public void A_gap_always_costs_at_least_two_passes() {
    // One bite does not come out flat and even: the piece is passed, turned over and passed again, so a
    // reduction always costs at least two passes however small the stock.
    RollSetSpec spec = Parse(FlatSet);

    Assert.Equal(2, spec.PassesAt(1f));
    Assert.Equal(2, spec.PassesAt(spec.BarrelWidth)); // exactly filling the barrel still fits in one bite
  }

  [Fact]
  public void Stock_wider_than_the_barrel_must_be_taken_in_side_by_side_strips() {
    RollSetSpec spec = Parse(FlatSet); // barrel 4 wide

    Assert.False(spec.OverhangsBarrel(4f));
    Assert.True(spec.OverhangsBarrel(4.24f));

    Assert.Equal(4, spec.PassesAt(4.24f)); // two strips, two passes each
    Assert.Equal(4, spec.PassesAt(8f));
    Assert.Equal(6, spec.PassesAt(8.1f)); // three strips
  }

  [Fact]
  public void A_narrow_set_gets_dearer_as_the_work_flattens_and_a_wide_one_does_not() {
    // A shingled bloom starts 3 wide and spreads as it is reduced. The narrow barrel stops taking it in one
    // strip partway down the schedule; the wide barrel never does.
    RollSetSpec narrow = Parse(FlatSet); // barrel 4
    RollSetSpec wide = Parse(
      FlatSet.Replace("\"barrelWidth\": 4.0", "\"barrelWidth\": 20.0")
    );

    const float startWidth = 3f;
    const float startThickness = 3f;
    float[] gaps = [1.5f, 1.0f, 0.5f];

    int narrowTotal = 0;
    int wideTotal = 0;
    foreach (float gap in gaps) {
      float width = RollingPass.SpreadWidth(startWidth, startThickness, gap);
      narrowTotal += narrow.PassesAt(width);
      wideTotal += wide.PassesAt(width);
    }

    Assert.Equal(2 * gaps.Length, wideTotal); // wide: the two-pass floor at every gap
    Assert.True(
      narrowTotal > wideTotal,
      $"the narrow set should cost more overall ({narrowTotal} vs {wideTotal})"
    );
  }

  #endregion
}
