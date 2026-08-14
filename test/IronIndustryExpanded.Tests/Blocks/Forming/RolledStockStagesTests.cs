using System.Collections.Generic;
using System.IO;
using System.Linq;
using IronIndustryExpanded.BlockStructures.Forming;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The staged stock shapes are generated from the two authored bases (shingled bloom and slab) by
/// <c>scripts/generate-rolled-stock.py</c>, one per gap in the roll schedule. Being derived, they can drift
/// from the schedule they illustrate, so all three dimensions are checked against it: thickness and width are
/// what the mechanics read, and length follows from volume conservation off the actual (possibly capped)
/// width. See docs/design/items/stock.md.
/// </summary>
public class RolledStockStagesTests {
  private const string ShapeDir = "../../../../../assets/iiex/shapes/forming";

  private static readonly float[] Gaps = [3f, 2f, 1.5f, 1f, 0.5f];

  private static string StagePath(string form, float thickness) =>
    Path.Combine(ShapeDir, $"stock-{form}-{(int)(thickness * 10)}.json");

  private static (float Width, float Thickness, float Length) Measure(
    string path
  ) {
    JObject shape = JObject.Parse(File.ReadAllText(path));
    var xs = new List<float>();
    var ys = new List<float>();
    var zs = new List<float>();
    foreach (JToken el in shape["elements"]!)
      foreach (string key in new[] { "from", "to" }) {
        xs.Add((float)el[key]![0]!);
        ys.Add((float)el[key]![1]!);
        zs.Add((float)el[key]![2]!);
      }
    return (xs.Max() - xs.Min(), ys.Max() - ys.Min(), zs.Max() - zs.Min());
  }

  public static TheoryData<string, float> Stages() {
    var data = new TheoryData<string, float>();
    foreach (string form in StockForm.All.Keys)
      foreach (float thickness in Gaps)
        data.Add(form, thickness);
    return data;
  }

  [Theory]
  [MemberData(nameof(Stages))]
  public void Every_stage_shape_exists(string form, float thickness) {
    Assert.True(
      File.Exists(StagePath(form, thickness)),
      $"missing stage shape for {form} at t={thickness}"
    );
  }

  [Theory]
  [MemberData(nameof(Stages))]
  public void A_stage_shape_is_as_thick_as_its_gap(string form, float thickness) {
    Assert.Equal(thickness, Measure(StagePath(form, thickness)).Thickness, 2);
  }

  [Theory]
  [MemberData(nameof(Stages))]
  public void A_stage_shape_is_as_wide_as_the_spread_model_says(
    string form,
    float thickness
  ) {
    // Width decides whether the piece overhangs the barrel, and so how many passes the gap costs.
    Assert.Equal(
      StockForm.All[form].WidthAt(thickness),
      Measure(StagePath(form, thickness)).Width,
      2
    );
  }

  [Theory]
  [MemberData(nameof(Stages))]
  public void A_stage_shape_is_as_long_as_conserving_its_volume_demands(
    string form,
    float thickness
  ) {
    StockForm stock = StockForm.All[form];
    float baseLength = Measure(StagePath(form, stock.BaseThickness)).Length;
    float expected =
      baseLength
      * RollingPass.LengthMultiplier(
        stock.BaseWidth,
        stock.BaseThickness,
        thickness,
        stock.WidthAt(thickness)
      );

    Assert.Equal(expected, Measure(StagePath(form, thickness)).Length, 1);
  }

  [Theory]
  [InlineData("shingledbar")]
  [InlineData("shingledslab")]
  public void A_piece_that_has_stopped_widening_runs_out_lengthways_instead(
    string form
  ) {
    // Once the width ceiling is reached the last gap cannot spread, so the piece elongates sharply. Length
    // is left uncapped in the art for that reason.
    StockForm stock = StockForm.All[form];
    Assert.Equal(stock.MaxWidth, stock.WidthAt(0.5f), 2); // capped by the last gap

    float atOne = Measure(StagePath(form, 1f)).Length;
    float atHalf = Measure(StagePath(form, 0.5f)).Length;
    Assert.True(
      atHalf > atOne * 1.5f,
      $"expected a sharp elongation, got {atOne} -> {atHalf}"
    );
  }

  [Fact]
  public void A_bloom_lands_on_plate_geometry_at_the_one_voxel_gap() {
    // The schedule is tuned so the bloom reaches nearly its full width where it becomes one voxel thick.
    // One voxel by about eight is the proportion of the vanilla metal plate the piece is cut into.
    StockForm bloom = StockForm.ShingledBar;
    float atPlateGap = bloom.WidthAt(1f);

    Assert.True(
      atPlateGap > bloom.MaxWidth * 0.9f,
      $"should be nearly {bloom.MaxWidth} wide, was {atPlateGap}"
    );
    Assert.True(atPlateGap <= bloom.MaxWidth);
    Assert.Equal(1f, Measure(StagePath("shingledbar", 1f)).Thickness, 2);
  }

  [Fact]
  public void A_bloom_outgrows_the_narrow_barrel_partway_down_the_schedule() {
    // The bloom starts inside the narrow barrel and outgrows it partway down, so its early gaps cost two
    // passes and its late ones cost four.
    const float barrel = 6f; // RollSetItemDefinitions: the flat set's barrel
    Assert.True(
      StockForm.ShingledBar.WidthAt(3f) <= barrel,
      "a fresh bloom should fit the narrow barrel"
    );
    Assert.True(
      StockForm.ShingledBar.WidthAt(0.5f) > barrel,
      "a rolled-out bloom should overhang it"
    );
  }

  [Fact]
  public void A_slab_never_fits_a_narrow_barrel_which_is_why_it_needs_wide_rolls() {
    Assert.True(StockForm.ShingledSlab.WidthAt(3f) > 6f);
  }

  [Fact]
  public void No_form_is_ever_wider_than_the_wide_barrel_can_swallow() {
    // The wide set never needs side-by-side strips, which holds only if its barrel clears every form's
    // width ceiling.
    const float wideBarrel = 16f; // RollSetItemDefinitions: the flat-wide set's barrel
    foreach (StockForm stock in StockForm.All.Values)
      Assert.True(
        stock.MaxWidth <= wideBarrel,
        $"{stock.Name} can outgrow the wide barrel"
      );
  }
}
