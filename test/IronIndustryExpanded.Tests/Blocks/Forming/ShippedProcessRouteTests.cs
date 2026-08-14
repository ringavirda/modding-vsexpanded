using System.Collections.Generic;
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
/// The shipped process routes as art contracts. A stage naming an element the shape file does not have is
/// the quietest failure in this system: nothing throws, nothing logs, the renderer falls back to the
/// composed mesh and the piece simply looks wrong at that one gauge - which is exactly the gauge someone
/// went to the trouble of drawing. See docs/design/mechanics/process-extension.md.
/// </summary>
public class ShippedProcessRouteTests {
  private static string Dir(string domain, string catalogue) =>
    Path.Combine(
      DefinitionGoldens.RepoRoot(),
      "assets",
      domain,
      "config",
      catalogue
    );

  private static IEnumerable<(string File, ProcessRoute Route)> Shipped() {
    foreach (
      string file in Directory.EnumerateFiles(
        Dir("iiex", "processroutes"),
        "*.json"
      )
    ) {
      Assert.True(
        ProcessRoute.TryParse(
          new JsonObject(JToken.Parse(File.ReadAllText(file))),
          out ProcessRoute? route,
          out string? error
        ),
        $"shipped route {Path.GetFileName(file)} does not parse: {error}"
      );
      yield return (Path.GetFileName(file), route!);
    }
  }

  /// <summary>Every element name in a shipped runtime shape, children included - the renderer matches on
  /// the element's own name wherever it sits in the tree.</summary>
  private static HashSet<string> ElementsOf(string shape) {
    string path = Path.Combine(
      DefinitionGoldens.RepoRoot(),
      "assets",
      shape.Replace("iiex:", "iiex/shapes/") + ".json"
    );
    Assert.True(
      File.Exists(path),
      $"route names shape '{shape}', missing at {path}"
    );

    var names = new HashSet<string>();
    void Walk(JToken? elements) {
      foreach (JToken el in elements ?? new JArray()) {
        if ((string?)el["name"] is { } name)
          names.Add(name);
        Walk(el["children"]);
      }
    }
    Walk(JObject.Parse(File.ReadAllText(path))["elements"]);
    return names;
  }

  #region The corpus is real

  [Fact]
  public void Routes_ship_and_at_least_one_draws_its_stages() {
    // Both checks below walk the shipped set, and the element check only bites on a route that names a
    // shape - so an empty directory or a corpus that suddenly composed everything would pass silently.
    var routes = Shipped().ToList();

    Assert.NotEmpty(routes);
    Assert.Contains(routes, r => r.Route.Shape != null);
  }

  #endregion

  #region Declared art exists

  [Fact]
  public void Every_stage_element_exists_in_the_shape_its_route_names() {
    var offenders = new List<string>();
    foreach ((string file, ProcessRoute route) in Shipped()) {
      if (route.Shape == null)
        continue;

      HashSet<string> drawn = ElementsOf(route.Shape);
      foreach (ProcessStage stage in route.Stages) {
        if (stage.Element == null)
          continue;
        if (!drawn.Contains(stage.Element))
          offenders.Add(
            $"{file}: stage {stage.Thickness} names element '{stage.Element}', "
              + $"which {route.Shape} does not have"
          );
      }
    }

    Assert.True(
      offenders.Count == 0,
      "route stage(s) name art that is not there, so they render as the composed fallback:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  [Fact]
  public void A_route_that_names_no_shape_declares_no_elements() {
    // The two are one decision. A stage carrying an element with nowhere to look it up is a declaration
    // that reads as drawn and behaves as composed.
    var offenders = Shipped()
      .Where(r => r.Route.Shape == null)
      .SelectMany(r =>
        r.Route.Stages.Where(s => s.Element != null)
          .Select(s =>
            $"{r.File}: stage {s.Thickness} names '{s.Element}' with no route shape"
          )
      )
      .ToList();

    Assert.True(offenders.Count == 0, string.Join("\n  ", offenders));
  }

  #endregion

  #region Half-steps

  [Fact]
  public void Every_half_step_is_the_midpoint_of_a_gap_its_branch_actually_takes() {
    // A gap is taken in two rounds, so the state between two settings is exactly half way. That makes the
    // check exact rather than a range: a half-step anywhere else is art for a state the piece is never in.
    //
    // The walk starts at the form's ENTRY thickness, which is not a rung - stock arrives at it rather
    // than being set to it - so the topmost half-step is above every rung. Bounding by the rungs alone
    // called all four shipped ones wrong.
    var offenders = new List<string>();
    foreach ((string file, ProcessRoute route) in Shipped()) {
      if (!StockForm.TryGet(route.Family, out StockForm? form))
        continue; // a route for a family no form registers is another test's business

      foreach (
        string family in route
          .Stages.SelectMany(s => s.AcceptedBy)
          .Distinct(System.StringComparer.OrdinalIgnoreCase)
      ) {
        float[] walk =
        [
          form!.BaseThickness,
          .. route.RungsFor(family).Select(s => s.Thickness),
        ];
        var midpoints = new List<float>();
        for (int i = 0; i + 1 < walk.Length; i++)
          midpoints.Add((walk[i] + walk[i + 1]) / 2f);

        foreach (
          ProcessStage half in route.Stages.Where(s =>
            s.HalfStep && s.IsAcceptedBy(family)
          )
        )
          if (
            !midpoints.Any(m => ProcessRoute.SameThickness(m, half.Thickness))
          )
            offenders.Add(
              $"{file}: '{family}' half-step at {half.Thickness} is not the midpoint of any gap; "
                + $"the walk is [{string.Join(", ", walk)}]"
            );
      }
    }

    Assert.True(
      offenders.Count == 0,
      "half-step(s) sit somewhere the two-round model never puts a piece:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  #endregion
}
