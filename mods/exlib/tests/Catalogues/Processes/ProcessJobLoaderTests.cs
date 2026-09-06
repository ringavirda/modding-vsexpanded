using System.Collections.Generic;
using ExpandedLib.Catalogues;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Reading the terminal-job catalogue off the asset manager: unknown-key rejection and the
/// <see cref="CatalogueLoadReport"/> the loader hands back. Parsing itself is covered by
/// <see cref="ProcessJobTests"/>.
/// </summary>
public class ProcessJobLoaderTests {
  private const string ShearFile = """
    {
      "schema": 1,
      "machine": "shear",
      "jobs": [
        { "input": "iiex:stock-bloom", "stage": 2.0, "family": "grooved",
          "output": "game:rod-iron", "count": 4, "minTorque": 0.3 },
        { "input": "iiex:nailplate", "output": "game:metalnailsandstrips", "count": 4 }
      ]
    }
    """;

  private static List<ProcessJobSet> Parse(
    out List<string> errors,
    params (string Source, string Json)[] files
  ) => ProcessJobLoader.Parse(files, out errors);

  #region Parse - unknown keys
  [Fact]
  public void A_job_naming_an_unknown_key_is_reported_and_skipped() {
    List<ProcessJobSet> sets = Parse(
      out List<string> errors,
      (
        "iiex:config/processjobs/bad.json",
        """
        {
          "machine": "shear",
          "jobs": [ { "inpt": "iiex:stock-bloom", "output": "game:rod-iron", "count": 4 } ]
        }
        """
      )
    );

    Assert.Empty(sets);
    string error = Assert.Single(errors);
    Assert.Contains("bad.json", error);
    Assert.Contains("inpt", error);
  }
  #endregion

  #region Load(files, registry) - source attribution
  [Fact]
  public void An_earlier_malformed_file_does_not_misattribute_a_later_clash_to_the_machine_name() {
    // Before the fix, one malformed file anywhere in the batch made every later clash fall back to
    // being named after the machine rather than the file that actually lost it.
    var registry = new ProcessJobRegistry();

    List<string> errors = ProcessJobLoader.Load(
      [
        ("othermod:config/processjobs/broken.json", "not json"),
        (
          "iiex:config/processjobs/shear.json",
          """{ "machine": "shear", "jobs": [ { "input": "iiex:strip", "output": "game:rivet-iron", "count": 4 } ] }"""
        ),
        (
          "othermod:config/processjobs/hijack.json",
          """{ "machine": "shear", "jobs": [ { "input": "iiex:strip", "output": "othermod:rivet", "count": 6 } ] }"""
        ),
      ],
      registry
    );

    Assert.Contains(errors, e => e.Contains("broken.json"));
    string clash = Assert.Single(errors, e => e.Contains("hijack.json"));
    Assert.StartsWith("othermod:config/processjobs/hijack.json:", clash);
  }
  #endregion

  #region Load(ICoreAPI) - the report
  [Fact]
  public void Load_from_the_asset_manager_reports_files_and_entries() {
    ProcessJobRegistry.Shared.Clear();
    ICoreAPI api = FakeAssetApi.Create(
      "config/processjobs/",
      ("iiex:config/processjobs/shear.json", ShearFile)
    );

    CatalogueLoadReport report = ProcessJobLoader.Load(api);

    Assert.Equal("processjobs", report.Catalogue);
    Assert.Equal(1, report.Files);
    Assert.Equal(2, report.Entries); // two jobs in ShearFile
    Assert.Empty(report.Errors);

    ProcessJobRegistry.Shared.Clear();
  }
  #endregion
}
