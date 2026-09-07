using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Catalogues;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The base every hand-parsed, contributed catalogue derives from: read files, audit keys, parse once,
/// merge, invoke contributors, report. Exercised here through a two-key toy catalogue rather than
/// through <see cref="ProcessRouteLoader"/>/<see cref="ProcessJobLoader"/>/<see cref="BayOccupancyLoader"/>,
/// so the base's own behaviour is proved independently of any one schema.
/// </summary>
public class ContributedCatalogueLoaderTests {
  // One declaration: an id and the items it lists. The registry keys on id and refuses a repeat.
  private sealed record TestEntry(string Id, List<string> Items);

  private sealed class TestRegistry {
    public readonly Dictionary<string, TestEntry> ById = [];
    public CatalogueContributors Contributors { get; } = new();

    public IReadOnlyList<string> Contribute(TestEntry set) {
      if (ById.ContainsKey(set.Id))
        return [$"'{set.Id}' is already declared"];
      ById[set.Id] = set;
      return [];
    }

    public void Clear() => ById.Clear();
  }

  // Counts how many times TryParse actually runs a file through the parser, so the "parsed once" fix
  // can be checked directly rather than inferred from the report.
  private sealed class TestLoader
    : ContributedCatalogueLoader<TestEntry, TestRegistry> {
    public int ParseCalls;

    private static readonly HashSet<string> RootKeys = ["id", "items"];

    protected override string AssetPath => "config/test/";
    protected override string CatalogueName => "test";

    protected override IReadOnlyList<string> UnknownKeys(JsonObject root) =>
      JsonKeyAudit.UnknownKeys(root, RootKeys);

    protected override bool TryParse(
      JsonObject root,
      out TestEntry set,
      out string? error
    ) {
      ParseCalls++;
      string id = root["id"].AsString("");
      if (string.IsNullOrEmpty(id)) {
        set = null!;
        error = "missing 'id'";
        return false;
      }
      set = new TestEntry(
        id,
        [.. (root["items"].AsArray<string>([]) ?? []).OfType<string>()]
      );
      error = null;
      return true;
    }

    protected override IReadOnlyList<string> Contribute(
      TestRegistry registry,
      TestEntry set
    ) => registry.Contribute(set);

    protected override int CountEntries(TestEntry set) => set.Items.Count;

    protected override CatalogueContributors Contributors(
      TestRegistry registry
    ) => registry.Contributors;

    protected override void Clear(TestRegistry registry) => registry.Clear();

    // Public wrappers: the production loaders expose these under their own names, static and thinly
    // named; a test loader just needs a way to call them.
    public new List<TestEntry> ParseFiles(
      IEnumerable<(string Source, string Json)> files,
      out List<string> errors
    ) => base.ParseFiles(files, out errors);

    public new List<string> MergeFiles(
      IEnumerable<(string Source, string Json)> files,
      TestRegistry registry
    ) => base.MergeFiles(files, registry);

    public new CatalogueLoadReport LoadCatalogue(
      ICoreAPI api,
      TestRegistry registry
    ) => base.LoadCatalogue(api, registry);
  }

  [Fact]
  public void A_file_with_only_known_keys_parses_and_merges() {
    var loader = new TestLoader();
    var registry = new TestRegistry();

    List<string> errors = loader.MergeFiles(
      [("mod:config/test/a.json", """{ "id": "a", "items": [ "x", "y" ] }""")],
      registry
    );

    Assert.Empty(errors);
    Assert.True(registry.ById.ContainsKey("a"));
    Assert.Equal(["x", "y"], registry.ById["a"].Items);
  }

  [Fact]
  public void An_unknown_key_is_reported_with_its_file() {
    var loader = new TestLoader();

    loader.ParseFiles(
      [("mod:config/test/bad.json", """{ "id": "a", "itms": [ "x" ] }""")],
      out List<string> errors
    );

    string error = Assert.Single(errors);
    Assert.Contains("bad.json", error);
    Assert.Contains("itms", error);
  }

  [Fact]
  public void Contributors_run_after_the_merge_and_see_it() {
    var loader = new TestLoader();
    var registry = new TestRegistry();
    bool sawMergedEntry = false;
    registry.Contributors.Register(_ =>
      sawMergedEntry = registry.ById.ContainsKey("a")
    );

    ICoreAPI api = FakeAssetApi.Create(
      "config/test/",
      ("mod:config/test/a.json", """{ "id": "a", "items": [ "x" ] }""")
    );
    loader.LoadCatalogue(api, registry);

    Assert.True(sawMergedEntry);
  }

  [Fact]
  public void The_report_counts_files_and_entries_off_one_parse() {
    var loader = new TestLoader();
    var registry = new TestRegistry();
    ICoreAPI api = FakeAssetApi.Create(
      "config/test/",
      ("mod:config/test/a.json", """{ "id": "a", "items": [ "x", "y" ] }"""),
      ("mod:config/test/b.json", """{ "id": "b", "items": [ "z" ] }""")
    );

    CatalogueLoadReport report = loader.LoadCatalogue(api, registry);

    Assert.Equal("test", report.Catalogue);
    Assert.Equal(2, report.Files);
    Assert.Equal(3, report.Entries); // 2 + 1 items, off the same parse that merged them
    Assert.Empty(report.Errors);
    Assert.Equal(2, loader.ParseCalls); // once per file, not once to count and once to merge
  }
}
