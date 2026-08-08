using IronworkingExpanded.BlockStructures.Forming;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The roll-set tooling contract. A set carries its own schedule in its item attributes, so adding a rolled
/// product is an item def and nothing else. The gap sequence is cut into the barrel widest first and must
/// strictly descend; it is walked one segment at a time; and each gap is a stopping point, so the product is
/// the thickness the stock stops at. See docs/design/items/roll-sets.md.
/// </summary>
public class RollSetSpecTests {
  private static JsonObject Json(string json) => new(JToken.Parse(json));

  private const string FlatSet = """
    {
      "family": "flat",
      "accepts": [ "bloom", "billet" ],
      "gaps": [ 1.5, 1.0, 0.5 ],
      "outputs": [
        { "gap": 1.0, "code": "iwex:plate-iron" },
        { "gap": 0.5, "code": "iwex:sheet-iron" }
      ],
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

    Assert.Equal("flat", spec.Family);
    Assert.Equal(["bloom", "billet"], spec.Accepts);
    Assert.Equal([1.5f, 1.0f, 0.5f], spec.Gaps);
    Assert.Equal(4.0f, spec.BarrelWidth);
    Assert.Equal(2.0f, spec.MinTorque);
    Assert.Equal("iwex:plate-iron", spec.Outputs[1.0f]);
  }

  [Fact]
  public void Gaps_must_strictly_descend_along_the_barrel() {
    // An out-of-order gap would let the stock skip a reduction or take a negative one, so it is rejected at
    // load time rather than at the mill.
    Assert.Contains(
      "descend",
      Rejects(FlatSet.Replace("[ 1.5, 1.0, 0.5 ]", "[ 1.0, 1.5, 0.5 ]"))
    );
    Assert.Contains(
      "descend",
      Rejects(FlatSet.Replace("[ 1.5, 1.0, 0.5 ]", "[ 1.0, 1.0 ]"))
    );
  }

  [Fact]
  public void A_set_that_bites_nothing_or_makes_nothing_is_rejected() {
    Assert.Contains(
      "accepts",
      Rejects(FlatSet.Replace("[ \"bloom\", \"billet\" ]", "[ ]"))
    );
    Assert.Contains(
      "outputs",
      Rejects(FlatSet.Replace("\"outputs\"", "\"unused\""))
    );
  }

  [Fact]
  public void An_output_must_sit_on_a_real_gap() {
    // An output on no gap is a product the stock can never stop at.
    Assert.Contains(
      "not one of",
      Rejects(FlatSet.Replace("\"gap\": 1.0", "\"gap\": 1.25"))
    );
  }

  [Fact]
  public void A_missing_attribute_is_reported_rather_than_throwing() {
    Assert.False(RollSetSpec.TryParse(null, out _, out string? error));
    Assert.Contains("rollset", error!);
  }

  #endregion

  #region Walking the barrel

  [Fact]
  public void The_next_gap_is_the_first_one_thinner_than_the_stock() {
    RollSetSpec spec = Parse(FlatSet);

    Assert.Equal(1.5f, spec.NextGap(2.0f)); // fresh stock takes the widest gap first
    Assert.Equal(1.0f, spec.NextGap(1.5f)); // then the next one along
    Assert.Equal(0.5f, spec.NextGap(1.0f));
  }

  [Fact]
  public void Stock_that_has_walked_the_whole_barrel_has_no_next_pass() {
    RollSetSpec spec = Parse(FlatSet);
    Assert.Null(spec.NextGap(0.5f));
    Assert.Null(spec.NextGap(0.25f));
  }

  [Fact]
  public void You_cannot_skip_a_segment_to_get_a_deeper_bite() {
    // From 1.5 the only next step is 1.0, never straight to 0.5. The segment spacing is what keeps every
    // pass inside delta_max without the mill checking it.
    RollSetSpec spec = Parse(FlatSet);
    Assert.Equal(0.5f, spec.NextDraft(1.5f), 4); // 1.5 -> 1.0, not 1.5 -> 0.5
    Assert.Equal(0f, spec.NextDraft(0.5f), 4); // finished
  }

  [Fact]
  public void The_product_is_the_thickness_you_stop_at() {
    RollSetSpec spec = Parse(FlatSet);

    Assert.Equal("iwex:plate-iron", spec.OutputAt(1.0f));
    Assert.Equal("iwex:sheet-iron", spec.OutputAt(0.5f));
    Assert.Null(spec.OutputAt(1.5f)); // a real gap, but not a named stopping point - still just stock
    Assert.Null(spec.OutputAt(0.75f)); // not a gap at all
  }

  [Fact]
  public void A_set_only_bites_the_forms_it_declares() {
    RollSetSpec spec = Parse(FlatSet);

    Assert.True(spec.AcceptsForm("bloom"));
    Assert.False(spec.AcceptsForm("slab"));
    Assert.False(spec.AcceptsForm(null));
  }

  [Fact]
  public void A_single_gap_set_is_a_wide_one_that_needs_a_train() {
    // A single-gap set fills the whole barrel, so it has no segment sequence to walk and is reduced by a
    // train of stands instead. `IsWide` is what tells the mill and the handbook which kind of set it is.
    RollSetSpec wide = Parse(
      """
      {
        "family": "flat",
        "accepts": [ "slab" ],
        "gaps": [ 1.0 ],
        "outputs": [ { "gap": 1.0, "code": "iwex:plate-iron" } ],
        "barrelWidth": 20.0
      }
      """
    );

    Assert.True(wide.IsWide);
    Assert.False(Parse(FlatSet).IsWide);
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

    int narrowTotal = 0;
    int wideTotal = 0;
    foreach (float gap in narrow.Gaps) {
      float width = RollingPass.SpreadWidth(startWidth, startThickness, gap);
      narrowTotal += narrow.PassesAt(width);
      wideTotal += wide.PassesAt(width);
    }

    Assert.Equal(2 * narrow.Gaps.Length, wideTotal); // wide: the two-pass floor at every gap
    Assert.True(
      narrowTotal > wideTotal,
      $"the narrow set should cost more overall ({narrowTotal} vs {wideTotal})"
    );
  }

  [Fact]
  public void A_barrel_width_is_required_because_the_mill_cannot_guess_it() {
    Assert.Contains(
      "barrelWidth",
      Rejects(FlatSet.Replace("\"barrelWidth\"", "\"unused\""))
    );
  }

  #endregion
}
