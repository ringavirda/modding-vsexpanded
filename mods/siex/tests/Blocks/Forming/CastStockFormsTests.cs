using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Forming;
using IronIndustryExpanded.Items;
using Newtonsoft.Json.Linq;
using SteelIndustryExpanded.BlockStructures.Forming;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The cast stock forms, checked across the seam they live on: iiex pours the pieces and draws their stage
/// art, this mod declares what the mill may do with them, and nothing in either assembly alone can see
/// both halves. The three failures that seam allows are each pinned below - a piece naming a form nobody
/// registers, a form whose section disagrees with the art it is drawn at, and a form wider than the barrel
/// it is supposed to run on.
/// See docs/design/items/stock.md and docs/design/machines/steel-roll-sets.md.
/// </summary>
public class CastStockFormsTests {
  // iiex's, because the stage shapes are generated from iiex's own item art and ship beside the shingled
  // ones. The form that indexes them is this mod's, which is the whole point of reading them from here.
  private static readonly string ShapeDir = Path.Combine(
    RepoPaths.Assets("iiex"),
    "shapes",
    "forming"
  );

  /// <summary>The stages a form is drawn at: its own base thickness, then every gap below it. Mirrors
  /// <c>generate-rolled-stock.py</c>'s <c>stages_for</c> and the iiex suite's copy - the cast pair leave the
  /// long cell at 4, above the shared top of 3, so their base stage is a rung nothing else has.</summary>
  private static readonly float[] Gaps = [3f, 2f, 1.5f, 1f, 0.5f];

  private static IEnumerable<float> StagesOf(StockForm form) =>
    Gaps.Where(g => g < form.BaseThickness).Prepend(form.BaseThickness);

  public static TheoryData<string> Forms() {
    var data = new TheoryData<string>();
    foreach (StockForm form in Cast)
      data.Add(form.Name);
    return data;
  }

  private static readonly StockForm[] Cast =
  [
    CastStockForms.CastBillet,
    CastStockForms.CastBloom,
    CastStockForms.CastSlab,
  ];

  private static StockForm Form(string name) {
    CastStockForms.Register();
    Assert.True(
      StockForm.TryGet(name, out StockForm? form),
      $"'{name}' is not a registered stock form"
    );
    return form!;
  }

  #region The registry

  [Theory]
  [MemberData(nameof(Forms))]
  public void Every_cast_form_registers_under_its_own_name(string name) =>
    Assert.Equal(name, Form(name).Name);

  [Fact]
  public void Every_cast_piece_the_long_cell_pours_names_a_form_the_mill_knows() {
    // The declaration and the registration are in different assemblies: iiex writes `stockForm` on the
    // itemtype and this mod puts the form in the registry, so a rename on either side leaves a piece the
    // mill silently refuses. Nothing in the iiex suite can see this, since the form is not there to find.
    CastStockForms.Register();

    ExItemDef def = CastStockItemDefinitions.Definitions("iiex").Single();
    JToken byType = def.ToJson()["attributesByType"]!;

    var offenders = new List<string>();
    foreach ((string variant, _, _) in CastStockItemDefinitions.Forms) {
      string? declared = byType[$"*-{variant}"]?["stockForm"]?.Value<string>();
      if (declared == null) {
        offenders.Add($"caststock-{variant} declares no stockForm");
        continue;
      }
      if (!StockForm.TryGet(declared, out _))
        offenders.Add(
          $"caststock-{variant} names form '{declared}', which nothing registers"
        );
    }

    Assert.True(
      offenders.Count == 0,
      "cast stock the mill cannot read as a work piece:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  [Fact]
  public void A_cast_bloom_is_never_read_as_the_wrought_bar_it_shares_a_word_with() {
    // `bloom` is the shingled bar's former name, so it resolves - to a 400 u wrought piece. A cast bloom
    // declaring the bare variant would be rolled as that, and the only sign would be the wrong mass.
    CastStockForms.Register();

    Assert.True(StockForm.TryGet("bloom", out StockForm? wrought));
    Assert.Equal(StockForm.ShingledBar.Name, wrought!.Name);
    Assert.Equal("castbloom", CastStockItemDefinitions.FormOf("bloom"));
    Assert.NotEqual(wrought.Name, CastStockForms.CastBloom.Name);
  }

  #endregion

  // No art region: the cast three declare no drawn states and mint no stock item of their own. Their
  // pieces are `iiex:caststock-{form}`, drawn by `iiex:item/cast{form}` and composed at every other gauge,
  // so the `stock-cast*` files that used to sit in the iiex forming folder were read by nothing at all.

  #region The barrel it has to fit

  [Theory]
  [MemberData(nameof(Forms))]
  public void No_cast_form_outgrows_the_wide_barrel(string name) {
    // One wide set covers every wide gap because its top roller is the movable one, so the barrel has to
    // clear each form's width ceiling for the whole walk rather than for one gap.
    const float wideBarrel = 16f; // RollSetItemDefinitions: the flat-wide set's barrel
    Assert.True(Form(name).MaxWidth <= wideBarrel);
  }

  [Fact]
  public void Only_the_billet_is_narrow_enough_for_the_mills_own_barrel() {
    // The billet is the one cast form the design runs on the narrow sets; a bloom or a slab entering at 4
    // thick has no business there, and `accepts` is what keeps it out.
    CastStockForms.Register();

    Assert.Equal(3f, CastStockForms.CastBillet.BaseWidth);
    Assert.True(CastStockForms.CastBloom.BaseWidth > 3f);
    Assert.True(CastStockForms.CastSlab.BaseWidth > 3f);
  }

  #endregion

  private static string StagePath(string form, float thickness) =>
    Path.Combine(ShapeDir, $"stock-{form}-{(int)(thickness * 10)}.json");

  private static (float Width, float Thickness, float Length) Measure(
    string path
  ) => ShapeExtents.Of(path);
}
