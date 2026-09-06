using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Catalogues;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Forming;
using IronIndustryExpanded.BlockStructures.Forming.BlockEntities;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The shipped crop table, checked as the route a player walks rather than as data. Every row has to name
/// a stage the mill can actually reach on a family that reaches it, and an output something registers -
/// a row failing either is a stopping point that silently is not one, which is exactly what the four
/// dangling roll-set output codes were.
/// See docs/design/items/rolled-parts.md and docs/design/machines/shear.md.
/// </summary>
public class ShippedCropTableTests {
  private const string Domain = "iiex";

  /// <summary>The shipped table, parsed exactly as <c>ProcessJobLoader</c> reads it at world load.</summary>
  private static ProcessJobSet Shipped() {
    string path = Path.Combine(
      RepoPaths.Assets(Domain),
      "config",
      "processjobs",
      "shear.json"
    );
    Assert.True(File.Exists(path), $"no shipped crop table at {path}");

    Assert.True(
      ProcessJobSet.TryParse(
        new JsonObject(JToken.Parse(File.ReadAllText(path))),
        out ProcessJobSet? set,
        out string? error
      ),
      $"the shipped crop table does not parse: {error}"
    );
    return set!;
  }

  #region The table is real

  [Fact]
  public void The_shipped_table_declares_the_crops_the_shear_can_reach_today() {
    ProcessJobSet set = Shipped();

    Assert.Equal(BlockEntityShear.MachineKey, set.Machine);
    // Five rows, and the count is asserted so a table emptied by a bad edit cannot pass every other check
    // in this class by having nothing to check. The cast rows are siex's file, not this one.
    Assert.Equal(5, set.Jobs.Length);
  }

  [Fact]
  public void Every_crop_names_a_stage_its_family_actually_reaches() {
    var routes = new ProcessRouteRegistry();
    foreach (ProcessRoute route in ShippedRoutes())
      routes.Contribute(route);

    var offenders = new List<string>();
    foreach (ProcessJob job in Shipped().Jobs) {
      if (job.Stage is not { } stage)
        continue; // a whole-item job is addressed by code, not by gauge

      string form = job.Input.Split(':').Last().Replace("stock-", "");
      ProcessRoute? route = routes.Route(form);
      if (route == null) {
        offenders.Add($"{job.Input}: no stage route ships for '{form}'");
        continue;
      }

      // The gauge has to be a rung the job's roller family actually walks. A crop keyed on a gauge no
      // set reaches is a product the player can see declared and never obtain.
      if (
        !route
          .RungsFor(job.Family)
          .Any(s => ProcessRoute.SameThickness(s.Thickness, stage))
      )
        offenders.Add(
          $"{job.Input}: '{form}' has no rung at {stage} that family '{job.Family}' walks"
        );
    }

    Assert.True(
      offenders.Count == 0,
      "crop row(s) name a stage the mill cannot reach:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  [Fact]
  public void Every_crop_output_names_something_that_exists() {
    var ours = RolledItemDefinitions
      .Definitions(Domain)
      .Select(d => $"{d.Domain}:{d.Code}")
      .ToHashSet();

    var offenders = new List<string>();
    foreach (ProcessJob job in Shipped().Jobs) {
      // A vanilla output is the game's to resolve and this harness holds no registry for it; the
      // cross-mod guard in the siex suite is what judges those.
      if (job.Output.StartsWith("game:", System.StringComparison.Ordinal))
        continue;
      if (!ours.Contains(job.Output))
        offenders.Add($"{job.Output} (from {job.Input} at {job.Stage})");
    }

    Assert.True(
      offenders.Count == 0,
      "crop row(s) output a code no rolled item declares - the stroke would produce nothing:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  [Fact]
  public void Every_crop_asks_less_drive_than_the_mill_does() {
    // The shear takes one bite where the mill sustains a pass, so no crop should be the harder of the
    // two to power: a line that can roll the stock must be able to cut it.
    var offenders = Shipped()
      .Jobs.Where(j => j.MinTorque >= IiexValues.RollingLoadTorque)
      .Select(j =>
        $"{j.Input} at {j.Stage}: {j.MinTorque} >= mill's {IiexValues.RollingLoadTorque}"
      )
      .ToList();

    Assert.True(
      offenders.Count == 0,
      "crop row(s) demand at least what the mill does:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  #endregion

  private static IEnumerable<ProcessRoute> ShippedRoutes() {
    string dir = Path.Combine(RepoPaths.Assets(Domain), "config", "processroutes");
    foreach (string file in Directory.EnumerateFiles(dir, "*.json")) {
      Assert.True(
        ProcessRoute.TryParse(
          new JsonObject(JToken.Parse(File.ReadAllText(file))),
          out ProcessRoute? route,
          out string? error
        ),
        $"shipped route {Path.GetFileName(file)} does not parse: {error}"
      );
      yield return route!;
    }
  }
}
