using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Catalogues;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Reading the stage catalogue off disk. Routes live in <c>config/processroutes/</c> rather than on an
/// item, because item generation has to run before the object loader builds items - a route carried on an
/// itemtype could not be read in time to generate one. One file per stock family is the convention, but
/// nothing enforces it: the registry merges whatever arrives.
/// See docs/design/mechanics/process-extension.md.
/// </summary>
public class ProcessRouteLoaderTests {
  private const string BloomFile = """
    {
      "schema": 1,
      "family": "bloom",
      "stages": [
        { "thickness": 2.0, "acceptedBy": [ "grooved" ], "code": "iiex:rolledrod" },
        { "thickness": 1.0, "acceptedBy": [ "grooved" ] }
      ]
    }
    """;

  private static List<ProcessRoute> Parse(
    out List<string> errors,
    params (string Source, string Json)[] files
  ) => ProcessRouteLoader.Parse(files, out errors);

  [Fact]
  public void A_catalogue_file_becomes_a_route() {
    List<ProcessRoute> routes = Parse(
      out List<string> errors,
      ("iiex:config/processroutes/bloom.json", BloomFile)
    );

    Assert.Empty(errors);
    Assert.Equal("bloom", Assert.Single(routes).Family);
  }

  [Fact]
  public void A_malformed_file_is_named_and_skipped_rather_than_failing_the_load() {
    // One bad file must not cost every other mod its routes.
    List<ProcessRoute> routes = Parse(
      out List<string> errors,
      ("othermod:config/processroutes/broken.json", """{ "stages": [] }"""),
      ("iiex:config/processroutes/bloom.json", BloomFile)
    );

    Assert.Single(routes);
    Assert.Contains("broken.json", Assert.Single(errors));
  }

  [Fact]
  public void Text_that_is_not_json_is_reported_rather_than_thrown() {
    Parse(
      out List<string> errors,
      ("mod:config/processroutes/x.json", "not json")
    );
    Assert.Single(errors);
  }

  [Fact]
  public void Loading_replaces_the_registry_rather_than_adding_to_it() {
    // A world reload in the same process must not accumulate a second copy of every rung.
    var registry = new ProcessRouteRegistry();
    var files = new[] { ("iiex:config/processroutes/bloom.json", BloomFile) };

    ProcessRouteLoader.Load(files, registry);
    ProcessRouteLoader.Load(files, registry);

    Assert.Single(registry.Families);
    Assert.Equal(2, registry.Route("bloom")!.Stages.Length);
  }

  [Fact]
  public void Two_mods_contributing_one_family_merge_into_one_route() {
    var registry = new ProcessRouteRegistry();

    List<string> errors = ProcessRouteLoader.Load(
      [
        ("iiex:config/processroutes/bloom.json", BloomFile),
        (
          "othermod:config/processroutes/bloom-serrated.json",
          """
          {
            "family": "bloom",
            "stages": [ { "thickness": 2.0, "acceptedBy": [ "serrated" ], "code": "iiex:rolledrod" } ]
          }
          """
        ),
      ],
      registry
    );

    Assert.Empty(errors);
    Assert.Single(registry.Families);
    Assert.Equal(
      ["grooved", "serrated"],
      registry.Route("bloom")!.StageAt(2.0f, "serrated")!.AcceptedBy
    );
  }

  [Fact]
  public void A_clash_between_two_mods_is_reported_against_the_file_that_lost() {
    var registry = new ProcessRouteRegistry();

    List<string> errors = ProcessRouteLoader.Load(
      [
        ("iiex:config/processroutes/bloom.json", BloomFile),
        (
          "othermod:config/processroutes/hijack.json",
          """
          {
            "family": "bloom",
            "stages": [ { "thickness": 2.0, "acceptedBy": [ "grooved" ], "code": "othermod:rod" } ]
          }
          """
        ),
      ],
      registry
    );

    Assert.Contains("hijack.json", Assert.Single(errors));
    Assert.Equal(
      "iiex:rolledrod",
      registry.Route("bloom")!.StageAt(2.0f, "grooved")!.Code
    );
  }

  [Fact]
  public void An_earlier_malformed_file_does_not_misattribute_a_later_clash_to_the_family_name() {
    // Before the fix, one malformed file anywhere in the batch made every later clash fall back to
    // being named after the family rather than the file that actually lost it.
    var registry = new ProcessRouteRegistry();

    List<string> errors = ProcessRouteLoader.Load(
      [
        ("othermod:config/processroutes/broken.json", "not json"),
        ("iiex:config/processroutes/bloom.json", BloomFile),
        (
          "othermod:config/processroutes/hijack.json",
          """
          {
            "family": "bloom",
            "stages": [ { "thickness": 2.0, "acceptedBy": [ "grooved" ], "code": "othermod:rod" } ]
          }
          """
        ),
      ],
      registry
    );

    Assert.Contains(errors, e => e.Contains("broken.json"));
    string clash = Assert.Single(errors, e => e.Contains("hijack.json"));
    Assert.StartsWith("othermod:config/processroutes/hijack.json:", clash);
  }

  [Fact]
  public void Every_stage_that_names_a_code_is_reachable_from_the_loaded_set() {
    // What the item emitter reads at inject time: the codes, off the same parse the registry uses.
    List<ProcessRoute> routes = Parse(
      out _,
      ("iiex:config/processroutes/bloom.json", BloomFile)
    );

    Assert.Equal(
      ["iiex:rolledrod"],
      routes
        .SelectMany(l => l.Stages)
        .Where(s => s.IsStoppingPoint)
        .Select(s => s.Code)
    );
  }

  #region Parse - unknown keys
  [Fact]
  public void A_stage_naming_an_unknown_key_is_reported_and_skipped() {
    List<ProcessRoute> routes = Parse(
      out List<string> errors,
      (
        "iiex:config/processroutes/bad.json",
        """
        {
          "family": "bloom",
          "stages": [ { "thicknes": 2.0, "acceptedBy": [ "grooved" ] } ]
        }
        """
      )
    );

    Assert.Empty(routes);
    string error = Assert.Single(errors);
    Assert.Contains("bad.json", error);
    Assert.Contains("thicknes", error);
  }
  #endregion

  #region Load(ICoreAPI) - the report
  [Fact]
  public void Load_from_the_asset_manager_reports_files_and_entries() {
    ProcessRouteRegistry.Shared.Clear();
    ICoreAPI api = FakeAssetApi.Create(
      "config/processroutes/",
      ("iiex:config/processroutes/bloom.json", BloomFile)
    );

    CatalogueLoadReport report = ProcessRouteLoader.Load(api);

    Assert.Equal("processroutes", report.Catalogue);
    Assert.Equal(1, report.Files);
    Assert.Equal(2, report.Entries); // two stages in BloomFile
    Assert.Empty(report.Errors);

    ProcessRouteRegistry.Shared.Clear();
  }
  #endregion
}
