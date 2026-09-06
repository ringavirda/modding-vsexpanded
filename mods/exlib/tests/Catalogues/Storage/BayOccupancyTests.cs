using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Catalogues;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The catalogue that says what an item occupies, and by omission what a store will not hold at all. It is
/// contributed to rather than owned: a mod makes its own stock rackable by shipping a file, never by
/// patching ours, which is the same contract the process catalogues carry.
/// </summary>
public class BayOccupancyTests {
  private const string Store = "storagerack";

  private static string File(
    string store,
    params (string Item, int Cells)[] rules
  ) =>
    $$"""
    {
      "schema": 1,
      "store": "{{store}}",
      "items": [
        {{string.Join(
          ",",
          rules.Select(r => $$"""{ "item": "{{r.Item}}", "cells": {{r.Cells}} }""")
        )}}
      ]
    }
    """;

  private static BayOccupancyRegistry Loaded(params string[] files) {
    var registry = new BayOccupancyRegistry();
    BayOccupancyLoader.Load(
      files.Select((json, i) => ($"file{i}.json", json)),
      registry
    );
    return registry;
  }

  #region Reading a catalogue

  [Fact]
  public void A_declared_item_occupies_what_it_declares() {
    BayOccupancyRegistry registry = Loaded(
      File(Store, ("iiex:stock-rod", 1), ("iiex:caststock-slab", 3))
    );

    Assert.Equal(1, registry.CellsFor(Store, "iiex:stock-rod"));
    Assert.Equal(3, registry.CellsFor(Store, "iiex:caststock-slab"));
  }

  [Fact]
  public void An_item_no_rule_names_is_one_the_store_refuses() {
    // Null rather than the default of one: the catalogue is the whitelist, and a store that fell back to
    // "one cell" for anything unlisted would be a chest that then has to draw arbitrary items.
    BayOccupancyRegistry registry = Loaded(File(Store, ("iiex:stock-rod", 1)));

    Assert.Null(registry.CellsFor(Store, "game:stone-granite"));
    Assert.Null(registry.CellsFor(Store, null));
  }

  [Fact]
  public void A_rule_is_for_one_store_and_not_for_every_store() {
    BayOccupancyRegistry registry = Loaded(File("crate", ("iiex:pig", 2)));

    Assert.Equal(2, registry.CellsFor("crate", "iiex:pig"));
    Assert.Null(registry.CellsFor(Store, "iiex:pig"));
    Assert.Null(registry.CellsFor(null, "iiex:pig"));
  }

  #endregion

  #region Families

  [Fact]
  public void A_trailing_star_sizes_a_whole_family_in_one_line() {
    BayOccupancyRegistry registry = Loaded(
      File(Store, ("iiex:caststock-*", 2))
    );

    Assert.Equal(2, registry.CellsFor(Store, "iiex:caststock-billet"));
    Assert.Equal(2, registry.CellsFor(Store, "iiex:caststock-slab"));
    Assert.Null(registry.CellsFor(Store, "iiex:stock-rod"));
  }

  [Fact]
  public void An_exact_code_beats_the_family_it_belongs_to_either_way_round() {
    // Declaration order must not decide it, or the same two rules in two files would size an item
    // differently depending on which mod loaded first.
    foreach (
      string json in new[]
      {
        File(Store, ("iiex:caststock-*", 1), ("iiex:caststock-slab", 3)),
        File(Store, ("iiex:caststock-slab", 3), ("iiex:caststock-*", 1)),
      }
    ) {
      BayOccupancyRegistry registry = Loaded(json);
      Assert.Equal(3, registry.CellsFor(Store, "iiex:caststock-slab"));
      Assert.Equal(1, registry.CellsFor(Store, "iiex:caststock-bloom"));
    }
  }

  [Fact]
  public void A_longer_prefix_beats_a_shorter_one() {
    BayOccupancyRegistry registry = Loaded(
      File(Store, ("iiex:*", 1), ("iiex:stock-*", 2))
    );

    Assert.Equal(2, registry.CellsFor(Store, "iiex:stock-beam"));
    Assert.Equal(1, registry.CellsFor(Store, "iiex:pig"));
  }

  #endregion

  #region Contributing

  [Fact]
  public void Two_mods_may_both_add_rules_to_one_store() {
    BayOccupancyRegistry registry = Loaded(
      File(Store, ("iiex:stock-rod", 1)),
      File(Store, ("othermod:billet", 2))
    );

    Assert.Equal(1, registry.CellsFor(Store, "iiex:stock-rod"));
    Assert.Equal(2, registry.CellsFor(Store, "othermod:billet"));
  }

  [Fact]
  public void A_second_rule_for_one_item_is_reported_and_the_first_stands() {
    // Taking the last writer would make a rack's capacity depend on mod load order.
    var registry = new BayOccupancyRegistry();
    List<string> errors = BayOccupancyLoader.Load(
      [
        ("a.json", File(Store, ("iiex:pig", 1))),
        ("b.json", File(Store, ("iiex:pig", 3))),
      ],
      registry
    );

    Assert.Equal(1, registry.CellsFor(Store, "iiex:pig"));
    Assert.Single(errors);
    Assert.Contains("iiex:pig", errors[0]);
  }

  [Fact]
  public void Restating_the_same_size_is_not_a_conflict() {
    var registry = new BayOccupancyRegistry();
    List<string> errors = BayOccupancyLoader.Load(
      [
        ("a.json", File(Store, ("iiex:pig", 2))),
        ("b.json", File(Store, ("iiex:pig", 2))),
      ],
      registry
    );

    Assert.Empty(errors);
    Assert.Equal(2, registry.CellsFor(Store, "iiex:pig"));
  }

  #endregion

  #region Refusing a bad file

  [Theory]
  [InlineData("""{ "schema": 1, "items": [] }""", "store")]
  [InlineData(
    """{ "schema": 1, "store": "s", "items": [ { "cells": 1 } ] }""",
    "item"
  )]
  [InlineData(
    """{ "schema": 1, "store": "s", "items": [ { "item": "a", "cells": 0 } ] }""",
    "least one"
  )]
  public void A_malformed_file_is_named_and_skipped(string json, string says) {
    var registry = new BayOccupancyRegistry();
    List<string> errors = BayOccupancyLoader.Load(
      [("bad.json", json), ("good.json", File(Store, ("iiex:pig", 1)))],
      registry
    );

    Assert.Single(errors);
    Assert.Contains("bad.json", errors[0]);
    Assert.Contains(says, errors[0]);

    // The good file still loaded: one bad declaration must not cost every other mod its rules.
    Assert.Equal(1, registry.CellsFor(Store, "iiex:pig"));
  }

  [Fact]
  public void A_file_that_is_not_json_is_named_and_skipped() {
    var registry = new BayOccupancyRegistry();
    List<string> errors = BayOccupancyLoader.Load(
      [
        ("torn.json", "{ not json"),
        ("good.json", File(Store, ("iiex:pig", 1))),
      ],
      registry
    );

    Assert.Single(errors);
    Assert.Contains("torn.json", errors[0]);
    Assert.Equal(1, registry.CellsFor(Store, "iiex:pig"));
  }

  #endregion
}
