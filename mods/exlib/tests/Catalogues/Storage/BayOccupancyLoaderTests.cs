using System.Collections.Generic;
using ExpandedLib.Catalogues;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Reading the bay-occupancy catalogue off the asset manager: unknown-key rejection and the
/// <see cref="CatalogueLoadReport"/> the loader hands back. Parsing itself is covered by
/// <see cref="BayOccupancyTests"/>.
/// </summary>
public class BayOccupancyLoaderTests {
  private const string RackFile = """
    {
      "schema": 1,
      "store": "storagerack",
      "items": [
        { "item": "iiex:stock-rod", "cells": 1 },
        { "item": "iiex:caststock-slab", "cells": 3 }
      ]
    }
    """;

  private static List<BayOccupancySet> Parse(
    out List<string> errors,
    params (string Source, string Json)[] files
  ) => BayOccupancyLoader.Parse(files, out errors);

  #region Parse - unknown keys
  [Fact]
  public void A_rule_naming_an_unknown_key_is_reported_and_skipped() {
    List<BayOccupancySet> sets = Parse(
      out List<string> errors,
      (
        "iiex:config/bayoccupancy/bad.json",
        """
        {
          "store": "storagerack",
          "items": [ { "itm": "iiex:stock-rod", "cells": 1 } ]
        }
        """
      )
    );

    Assert.Empty(sets);
    string error = Assert.Single(errors);
    Assert.Contains("bad.json", error);
    Assert.Contains("itm", error);
  }
  #endregion

  #region Load(files, registry) - source attribution
  [Fact]
  public void An_earlier_malformed_file_does_not_misattribute_a_later_clash_to_the_store_name() {
    // Before the fix, one malformed file anywhere in the batch made every later clash fall back to
    // being named after the store rather than the file that actually lost it.
    var registry = new BayOccupancyRegistry();

    List<string> errors = BayOccupancyLoader.Load(
      [
        ("othermod:config/bayoccupancy/broken.json", "not json"),
        (
          "iiex:config/bayoccupancy/rack.json",
          """{ "store": "storagerack", "items": [ { "item": "iiex:stock-rod", "cells": 1 } ] }"""
        ),
        (
          "othermod:config/bayoccupancy/hijack.json",
          """{ "store": "storagerack", "items": [ { "item": "iiex:stock-rod", "cells": 3 } ] }"""
        ),
      ],
      registry
    );

    Assert.Contains(errors, e => e.Contains("broken.json"));
    string clash = Assert.Single(errors, e => e.Contains("hijack.json"));
    Assert.StartsWith("othermod:config/bayoccupancy/hijack.json:", clash);
  }
  #endregion

  #region Load(ICoreAPI) - the report
  [Fact]
  public void Load_from_the_asset_manager_reports_files_and_entries() {
    BayOccupancyRegistry.Shared.Clear();
    ICoreAPI api = FakeAssetApi.Create(
      "config/bayoccupancy/",
      ("iiex:config/bayoccupancy/rack.json", RackFile)
    );

    CatalogueLoadReport report = BayOccupancyLoader.Load(api);

    Assert.Equal("bayoccupancy", report.Catalogue);
    Assert.Equal(1, report.Files);
    Assert.Equal(2, report.Entries); // two rules in RackFile
    Assert.Empty(report.Errors);

    BayOccupancyRegistry.Shared.Clear();
  }
  #endregion
}
