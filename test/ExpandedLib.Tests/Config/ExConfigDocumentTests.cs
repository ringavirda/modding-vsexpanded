using System;
using ExpandedLib.Registries.Config;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>The shared mod-sectioned config document backing the <c>ex_values.json</c> /
/// <c>ex_recipes.json</c> fold: several mods coexist as separate sections of one file, a whole-file
/// corruption degrades to defaults instead of wiping everyone, and the in-memory document is isolated
/// per API instance (which gives per-test isolation for free).</summary>
public class ExConfigDocumentTests
{
  private sealed class Section : IExVersionedConfig
  {
    public string? ConfigVersion { get; set; }
    public int Value { get; set; }
  }

  private static ICoreAPI FakeApi(
    JObject? existing,
    Action<JObject>? onStore = null
  )
  {
    var api = Substitute.For<ICoreAPI>();
    api.Logger.Returns(Substitute.For<ILogger>());
    api.LoadModConfig<JObject>(Arg.Any<string>()).Returns(existing);
    if (onStore != null)
      api.When(a => a.StoreModConfig(Arg.Any<JObject>(), Arg.Any<string>()))
        .Do(ci => onStore(ci.Arg<JObject>()));
    return api;
  }

  [Fact]
  public void Two_mods_share_one_file_as_separate_sections()
  {
    JObject? saved = null;
    var doc = ExConfigDocument.ForFile(
      FakeApi(existing: null, onStore: j => saved = j),
      "ex_values.json"
    );

    doc.SetSection("ppex", new Section { Value = 1 });
    doc.SetSection("smex", new Section { Value = 2 });
    doc.Flush();

    Assert.Equal(1, saved!["ppex"]!["Value"]!.Value<int>());
    Assert.Equal(2, saved!["smex"]!["Value"]!.Value<int>());
    Assert.Equal(1, doc.GetSection<Section>("ppex")!.Value);
    Assert.Equal(2, doc.GetSection<Section>("smex")!.Value);
  }

  [Fact]
  public void Reads_an_existing_section_and_returns_null_for_a_missing_one()
  {
    var existing = new JObject { ["ppex"] = new JObject { ["Value"] = 9 } };
    var doc = ExConfigDocument.ForFile(FakeApi(existing), "ex_values.json");

    Assert.Equal(9, doc.GetSection<Section>("ppex")!.Value);
    Assert.True(doc.HasSection("ppex"));
    Assert.Null(doc.GetSection<Section>("smex"));
    Assert.False(doc.HasSection("smex"));
  }

  [Fact]
  public void A_corrupt_file_yields_an_empty_document_without_throwing()
  {
    var api = Substitute.For<ICoreAPI>();
    api.Logger.Returns(Substitute.For<ILogger>());
    api.LoadModConfig<JObject>("bad.json")
      .Returns(_ => throw new Exception("corrupt json"));

    // A whole-file parse failure must not take out every section - it degrades to defaults.
    var doc = ExConfigDocument.ForFile(api, "bad.json");
    Assert.Null(doc.GetSection<Section>("ppex"));
  }

  [Fact]
  public void Documents_are_isolated_per_api_instance()
  {
    var d1 = ExConfigDocument.ForFile(FakeApi(null), "ex_values.json");
    d1.SetSection("ppex", new Section { Value = 1 });

    // A different API (as each test has) gets its own document, not d1's in-memory state.
    var d2 = ExConfigDocument.ForFile(FakeApi(null), "ex_values.json");
    Assert.Null(d2.GetSection<Section>("ppex"));
  }

  [Fact]
  public void ForFile_returns_the_same_cached_document_for_one_api()
  {
    var api = FakeApi(null);
    var a = ExConfigDocument.ForFile(api, "ex_values.json");
    var b = ExConfigDocument.ForFile(api, "ex_values.json");
    Assert.Same(a, b);
  }
}
