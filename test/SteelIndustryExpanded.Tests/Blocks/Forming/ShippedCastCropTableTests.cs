using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Processes;
using ExpandedLib.Testing;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockStructures.Forming;
using IronIndustryExpanded.BlockStructures.Forming.BlockEntities;
using IronIndustryExpanded.Items;
using Newtonsoft.Json.Linq;
using SteelIndustryExpanded.BlockStructures.Forming;
using Vintagestory.API.Datastructures;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The cast half of the shear's crop table, checked as the route a player walks. The iiex suite has the
/// same guard over the wrought half and cannot have this one: the machine, the outputs and the shear are
/// iiex's while the input's form and its route are this mod's, so only here do both ends of a row exist
/// at once. See docs/design/items/rolled-parts.md and docs/design/machines/shear.md.
/// </summary>
public class ShippedCastCropTableTests {
  private const string Domain = "siex";

  private static string ConfigDir(string catalogue) =>
    Path.Combine(
      DefinitionGoldens.RepoRoot(),
      "assets",
      Domain,
      "config",
      catalogue
    );

  /// <summary>The shipped table, parsed exactly as <c>ProcessJobLoader</c> reads it at world load.</summary>
  private static ProcessJobSet Shipped() {
    string path = Path.Combine(ConfigDir("processjobs"), "shear.json");
    Assert.True(File.Exists(path), $"no shipped cast crop table at {path}");

    Assert.True(
      ProcessJobSet.TryParse(
        new JsonObject(JToken.Parse(File.ReadAllText(path))),
        out ProcessJobSet? set,
        out string? error
      ),
      $"the shipped cast crop table does not parse: {error}"
    );
    return set!;
  }

  private static IEnumerable<ProcessRoute> ShippedRoutes() {
    foreach (
      string file in Directory.EnumerateFiles(
        ConfigDir("processroutes"),
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
      yield return route!;
    }
  }

  /// <summary>Item code to the form the piece rolls as, read off the shipped itemtype rather than sliced
  /// out of the code - the mill reads the same attribute, so a row addressed any other way could name a
  /// form the machine never sees.</summary>
  private static IReadOnlyDictionary<string, string> FormsByCode() {
    JToken byType = CastStockItemDefinitions
      .Definitions("iiex")
      .Single()
      .ToJson()["attributesByType"]!;

    return CastStockItemDefinitions.Forms.ToDictionary(
      f => $"iiex:caststock-{f.Form}",
      f => byType[$"*-{f.Form}"]!["stockForm"]!.Value<string>()!
    );
  }

  #region The table is real

  [Fact]
  public void The_shipped_table_declares_the_cast_crops_that_are_reachable_today() {
    ProcessJobSet set = Shipped();

    Assert.Equal(BlockEntityShear.MachineKey, set.Machine);
    // Five rows, and the count is asserted so a table emptied by a bad edit cannot pass every other check
    // here by having nothing to check. Three of the design's eight cast rows are absent on purpose: the
    // billet's grooved 2.25 crop needs the mid-gap rule, the bloom's 3.0 crop yields stock rather than a
    // product, and the slab's 2.0 heavyplate waits on M.8.
    Assert.Equal(4, set.Jobs.Length);
  }

  [Fact]
  public void Every_cast_crop_takes_a_piece_that_can_reach_the_shear() {
    IReadOnlyDictionary<string, string> forms = FormsByCode();

    var offenders = Shipped()
      .Jobs.Where(j => !forms.ContainsKey(j.Input))
      .Select(j => $"{j.Input} is not a piece the long cell pours")
      .ToList();

    Assert.True(
      offenders.Count == 0,
      "crop row(s) name an input that does not exist:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  [Fact]
  public void Every_cast_crop_names_a_stage_its_family_actually_reaches() {
    CastStockForms.Register();
    IReadOnlyDictionary<string, string> forms = FormsByCode();

    var routes = new ProcessRouteRegistry();
    foreach (ProcessRoute route in ShippedRoutes())
      routes.Contribute(route);

    var offenders = new List<string>();
    foreach (ProcessJob job in Shipped().Jobs) {
      if (job.Stage is not { } stage)
        continue; // a whole-item job is addressed by code, not by gauge

      string form = forms[job.Input];
      ProcessRoute? route = routes.Route(form);
      if (route == null) {
        offenders.Add($"{job.Input}: no stage route ships for '{form}'");
        continue;
      }

      // The gauge has to be a rung the job's roller family actually walks. A crop keyed on a gauge no set
      // reaches is a product the player can see declared and never obtain.
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
      "cast crop row(s) name a stage the mill cannot reach:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  [Fact]
  public void Every_cast_crop_output_names_something_that_exists() {
    var rolled = RolledItemDefinitions
      .Definitions("iiex")
      .Select(d => $"{d.Domain}:{d.Code}")
      .ToHashSet();

    var offenders = new List<string>();
    foreach (ProcessJob job in Shipped().Jobs) {
      // A vanilla output is the game's to resolve and this harness holds no registry for it.
      if (job.Output.StartsWith("game:", System.StringComparison.Ordinal))
        continue;
      if (!rolled.Contains(job.Output))
        offenders.Add($"{job.Output} (from {job.Input} at {job.Stage})");
    }

    Assert.True(
      offenders.Count == 0,
      "cast crop row(s) output a code no rolled item declares - the stroke would produce nothing:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  #endregion

  #region What the line has to be able to do

  [Fact]
  public void Every_cast_crop_asks_less_drive_than_the_mill_does() {
    // The shear takes one bite where the mill sustains a pass, so no crop should be the harder of the two
    // to power: a line that can roll the stock must be able to cut it.
    var offenders = Shipped()
      .Jobs.Where(j => j.MinTorque >= IiexValues.RollingLoadTorque)
      .Select(j =>
        $"{j.Input} at {j.Stage}: {j.MinTorque} >= mill's {IiexValues.RollingLoadTorque}"
      )
      .ToList();

    Assert.True(
      offenders.Count == 0,
      "cast crop row(s) demand at least what the mill does:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  [Fact]
  public void Every_cast_crop_asks_for_a_blade_the_steel_tier_supplies() {
    // Cast stock is the steel line's, and the blade ladder is where that is expressed - an iron blade cuts
    // wrought stock and nothing harder. A row left at the default tier would let the whole cast catalogue
    // fall out of a shear a player builds before the converter.
    var offenders = Shipped()
      .Jobs.Where(j => j.MinTier < 2)
      .Select(j => $"{j.Input} at {j.Stage}: tier {j.MinTier}")
      .ToList();

    Assert.True(
      offenders.Count == 0,
      "cast crop row(s) are cuttable with an iron blade:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  [Fact]
  public void The_cast_table_merges_with_the_wrought_one_rather_than_replacing_it() {
    // Both mods ship a `shear` file. The registry merges by machine and keeps the first declaration of a
    // given (input, stage, family), so the only way these two can collide is by claiming one another's
    // input - and a silent replacement would delete a working route.
    var registry = new ProcessJobRegistry();
    Assert.Empty(registry.Contribute(WroughtTable()));
    Assert.Empty(registry.Contribute(Shipped()));

    Assert.Equal(
      WroughtTable().Jobs.Length + Shipped().Jobs.Length,
      registry.Jobs(BlockEntityShear.MachineKey).Count
    );
  }

  private static ProcessJobSet WroughtTable() {
    string path = Path.Combine(
      DefinitionGoldens.RepoRoot(),
      "assets",
      "iiex",
      "config",
      "processjobs",
      "shear.json"
    );
    Assert.True(
      ProcessJobSet.TryParse(
        new JsonObject(JToken.Parse(File.ReadAllText(path))),
        out ProcessJobSet? set,
        out string? error
      ),
      $"the shipped wrought crop table does not parse: {error}"
    );
    return set!;
  }

  #endregion
}
