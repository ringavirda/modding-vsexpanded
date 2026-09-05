using System.IO;
using System.Linq;
using ExpandedLib.Processes;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Forming;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Stock art against the simulation that reads it. There are two kinds and one model: the per-form BASE
/// shape an item is drawn at and the composed mesh is scaled from, and the per-stage elements a process
/// route names. Both have to agree with <see cref="StockForm"/>, or a piece renders at a gauge it does not
/// roll at. See docs/design/items/stock.md.
/// </summary>
public class RolledStockStagesTests {
  private static string ShapeDir => Path.Combine(RepoPaths.Assets("iiex"), "shapes");

  private static string RouteDir =>
    Path.Combine(RepoPaths.Assets("iiex"), "config", "processroutes");

  private static string ShapePath(StockForm form) =>
    Path.Combine(ShapeDir, form.Shape!.Replace("iiex:", "") + ".json");

  public static TheoryData<string> Forms() {
    var data = new TheoryData<string>();
    foreach (string name in StockForm.All.Keys)
      data.Add(name);
    return data;
  }

  private static StockForm Form(string name) => StockForm.All[name];

  #region The base shape an item is drawn at

  [Theory]
  [MemberData(nameof(Forms))]
  public void Every_form_ships_the_base_element_its_item_points_at(string name) {
    // StockItemDefinitions renders exactly this element, and ItemStockPiece scales it for every gauge no
    // route draws - so a form without it is an item with no art and no composed mesh either.
    StockForm form = Form(name);
    Assert.NotNull(form.Shape);
    Assert.NotNull(form.BaseElement);
    Assert.True(File.Exists(ShapePath(form)), $"missing {form.Shape}");

    // Throws with the element named if it is not in the file, which is the whole check.
    ShapeExtents.Of(ShapePath(form), form.BaseElement);
  }

  [Theory]
  [MemberData(nameof(Forms))]
  public void A_base_shape_is_drawn_at_the_section_its_form_declares(
    string name
  ) {
    StockForm form = Form(name);
    (float width, float thickness, float length) = ShapeExtents.Of(
      ShapePath(form),
      form.BaseElement
    );

    // Absolute, against the declaration - not against another stage. A relative check cannot see a whole
    // family drawn short, which is how both shingled ladders once shipped two voxels under.
    Assert.Equal(form.BaseWidth, width, 1);
    Assert.Equal(form.BaseThickness, thickness, 2);
    Assert.Equal(form.BaseLength, length, 1);
  }

  #endregion

  #region The stages a route draws

  /// <summary>Every (form, stage, element) a shipped route draws on a flat family.</summary>
  public static TheoryData<string, float, string> DrawnStages() {
    var data = new TheoryData<string, float, string>();
    foreach (ProcessRoute route in Routes()) {
      if (route.Shape == null || !StockForm.All.ContainsKey(route.Family))
        continue;

      foreach (ProcessStage stage in route.Stages) {
        // Flat families only. A grooved pass constrains the spread instead of letting it run sideways, so
        // its art is drawn square while the form spreads - one exponent per form cannot say both, and
        // asserting the model over grooved art would assert something the design does not claim.
        if (
          stage.Element == null
          || !stage.AcceptedBy.Any(f =>
            f.StartsWith("flat", System.StringComparison.OrdinalIgnoreCase)
          )
        )
          continue;
        data.Add(route.Family, stage.Thickness, stage.Element);
      }
    }
    return data;
  }

  [Theory]
  [MemberData(nameof(DrawnStages))]
  public void A_drawn_stage_is_the_width_the_spread_model_gives_it(
    string formName,
    float thickness,
    string element
  ) {
    // Within a tenth, not to the decimal. The art is authored to settled product sections and the model
    // approximates them: a beam is drawn 4.5 wide because that is what a beam is, where the exponent says
    // 4.23. The widest shipped disagreement is 6%, so 10% passes every authored stage and still catches a
    // form whose exponent is simply wrong - the shingled slab drawn at e = 1 against a declared 0.463 was
    // 24% out.
    StockForm form = Form(formName);
    float model = form.WidthAt(thickness);
    float drawn = Measure(form, element).Width;

    Assert.True(
      System.MathF.Abs(drawn - model) <= model * 0.1f,
      $"{formName} {element}: drawn {drawn} wide against the model's {model}"
    );
  }

  [Theory]
  [MemberData(nameof(DrawnStages))]
  public void A_drawn_stage_is_as_thick_as_the_gauge_it_stands_for(
    string formName,
    float thickness,
    string element
  ) => Assert.Equal(thickness, Measure(Form(formName), element).Thickness, 2);

  [Theory]
  [MemberData(nameof(DrawnStages))]
  public void A_drawn_stage_conserves_the_metal_it_started_with(
    string formName,
    float thickness,
    string element
  ) {
    // Volume rather than length: past the width ceiling the reduction runs out lengthways, so a length
    // check would have to fold the cap back in and this states the invariant directly.
    StockForm form = Form(formName);
    (float width, float t, float length) = Measure(form, element);

    float declared = form.BaseWidth * form.BaseThickness * form.BaseLength;
    Assert.True(
      System.MathF.Abs(width * t * length - declared) <= declared * 0.1f,
      $"{formName} {element}: {width * t * length} vx3 against the form's {declared}"
    );
  }

  #endregion

  #region The barrel every form has to fit

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

  [Fact]
  public void A_slab_never_fits_a_narrow_barrel_which_is_why_it_needs_wide_rolls() =>
    Assert.True(StockForm.ShingledSlab.WidthAt(3f) > 6f);

  #endregion

  private static System.Collections.Generic.IEnumerable<ProcessRoute> Routes() {
    foreach (string file in Directory.EnumerateFiles(RouteDir, "*.json"))
      if (
        ProcessRoute.TryParse(
          new JsonObject(JToken.Parse(File.ReadAllText(file))),
          out ProcessRoute? route,
          out _
        )
      )
        yield return route!;
  }

  private static (float Width, float Thickness, float Length) Measure(
    StockForm form,
    string element
  ) {
    string shape = Routes()
      .First(r => r.Family == form.Name && r.Shape != null)
      .Shape!;
    return ShapeExtents.Of(
      Path.Combine(ShapeDir, shape.Replace("iiex:", "") + ".json"),
      element
    );
  }
}
