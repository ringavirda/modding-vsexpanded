using System;
using ExpandedLib.Config;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>The shared mod-sectioned config document behind <c>ex_values.json</c> and
/// <c>ex_recipes.json</c>: several mods coexist as separate sections of one file, a whole-file
/// corruption degrades to defaults instead of clearing every section, and the in-memory document is
/// isolated per API instance.</summary>
public class ExConfigDocumentTests {
  private sealed class Section : IExVersionedConfig {
    public string? ConfigVersion { get; set; }
    public int Value { get; set; }
  }

  private static ICoreAPI FakeApi(
    JObject? existing,
    Action<JObject>? onStore = null
  ) {
    var api = Substitute.For<ICoreAPI>();
    api.Logger.Returns(Substitute.For<ILogger>());
    api.LoadModConfig<JObject>(Arg.Any<string>()).Returns(existing);
    if (onStore != null)
      api.When(a => a.StoreModConfig(Arg.Any<JObject>(), Arg.Any<string>()))
        .Do(ci => onStore(ci.Arg<JObject>()));
    return api;
  }

  [Fact]
  public void Two_mods_share_one_file_as_separate_sections() {
    JObject? saved = null;
    var doc = ExConfigDocument.ForFile(
      FakeApi(existing: null, onStore: j => saved = j),
      "ex_values.json"
    );

    doc.SetSection("iiex", new Section { Value = 1 });
    doc.SetSection("smex", new Section { Value = 2 });
    doc.Flush();

    Assert.Equal(1, saved!["iiex"]!["Value"]!.Value<int>());
    Assert.Equal(2, saved!["smex"]!["Value"]!.Value<int>());
    Assert.Equal(1, doc.GetSection<Section>("iiex")!.Value);
    Assert.Equal(2, doc.GetSection<Section>("smex")!.Value);
  }

  [Fact]
  public void Reads_an_existing_section_and_returns_null_for_a_missing_one() {
    var existing = new JObject { ["iiex"] = new JObject { ["Value"] = 9 } };
    var doc = ExConfigDocument.ForFile(FakeApi(existing), "ex_values.json");

    Assert.Equal(9, doc.GetSection<Section>("iiex")!.Value);
    Assert.True(doc.HasSection("iiex"));
    Assert.Null(doc.GetSection<Section>("smex"));
    Assert.False(doc.HasSection("smex"));
  }

  [Fact]
  public void A_corrupt_file_yields_an_empty_document_without_throwing() {
    var api = Substitute.For<ICoreAPI>();
    api.Logger.Returns(Substitute.For<ILogger>());
    api.LoadModConfig<JObject>("bad.json")
      .Returns(_ => throw new Exception("corrupt json"));

    // A whole-file parse failure must not clear every section; it degrades to defaults.
    var doc = ExConfigDocument.ForFile(api, "bad.json");
    Assert.Null(doc.GetSection<Section>("iiex"));
  }

  [Fact]
  public void Documents_are_isolated_per_api_instance() {
    var d1 = ExConfigDocument.ForFile(FakeApi(null), "ex_values.json");
    d1.SetSection("iiex", new Section { Value = 1 });

    // A different API instance gets its own document, not d1's in-memory state.
    var d2 = ExConfigDocument.ForFile(FakeApi(null), "ex_values.json");
    Assert.Null(d2.GetSection<Section>("iiex"));
  }

  [Fact]
  public void ForFile_returns_the_same_cached_document_for_one_api() {
    var api = FakeApi(null);
    var a = ExConfigDocument.ForFile(api, "ex_values.json");
    var b = ExConfigDocument.ForFile(api, "ex_values.json");
    Assert.Same(a, b);
  }

  #region Legacy section carry-over (a mod renamed or absorbed another)

  // A section is keyed by mod id, so a rename orphans the player's whole tuning: every value reverts
  // to its coded default on the next load, with no error and no log line, because a missing section
  // is indistinguishable from a fresh install. These cover the smex+hpex -> siex merge.

  [Fact]
  public void A_renamed_mod_takes_over_its_old_section() {
    var existing = new JObject { ["smex"] = new JObject { ["Value"] = 7 } };
    var doc = ExConfigDocument.ForFile(FakeApi(existing), "ex_values.json");

    doc.FoldLegacySections("siex", ["smex"]);

    Assert.Equal(7, doc.GetSection<Section>("siex")!.Value);
    // The old key is cleared, so the carry-over cannot run twice and resurrect stale values.
    Assert.False(doc.HasSection("smex"));
  }

  [Fact]
  public void An_absorbed_mods_section_fills_only_the_gaps() {
    var existing = new JObject {
      ["smex"] = new JObject { ["Value"] = 7 },
      ["hpex"] = new JObject { ["Value"] = 99, ["Other"] = 5 },
    };
    var doc = ExConfigDocument.ForFile(FakeApi(existing), "ex_values.json");

    doc.FoldLegacySections("siex", ["smex", "hpex"]);

    // The survivor is authoritative where both carried a key; the absorbed mod still contributes
    // what the survivor had no value for, so neither half's tuning is silently dropped.
    Assert.Equal(7, doc.GetSection<Section>("siex")!.Value);
    Assert.Equal(5, doc.GetSection<JObject>("siex")!["Other"]!.Value<int>());
    Assert.False(doc.HasSection("hpex"));
  }

  [Fact]
  public void An_existing_section_is_never_replaced_by_a_legacy_one() {
    var existing = new JObject {
      ["siex"] = new JObject { ["Value"] = 1 },
      ["smex"] = new JObject { ["Value"] = 2 },
    };
    var doc = ExConfigDocument.ForFile(FakeApi(existing), "ex_values.json");

    doc.FoldLegacySections("siex", ["smex"]);

    Assert.Equal(1, doc.GetSection<Section>("siex")!.Value);
  }

  [Fact]
  public void Carry_over_is_a_no_op_when_no_legacy_section_is_present() {
    var existing = new JObject { ["siex"] = new JObject { ["Value"] = 3 } };
    var doc = ExConfigDocument.ForFile(FakeApi(existing), "ex_values.json");

    doc.FoldLegacySections("siex", ["smex", "hpex"]);
    doc.FoldLegacySections("siex", []);

    Assert.Equal(3, doc.GetSection<Section>("siex")!.Value);
  }

  #endregion
}
