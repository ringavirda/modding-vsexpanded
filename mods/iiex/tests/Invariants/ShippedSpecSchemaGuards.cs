using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Catalogues;
using ExpandedLib.Definitions;
using IronIndustryExpanded.BlockStructures.Casting;
using IronIndustryExpanded.BlockStructures.Forming;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Every spec attribute we emit declares its <c>schema</c>. A parser reads an absent one as the first form,
/// so this is not about loading - it is that our emitted JSON is the template a third party copies, and a
/// template that omits the field teaches them to omit it. See
/// docs/design/mechanics/process-extension.md.
/// </summary>
public class ShippedSpecSchemaGuards {
  /// <summary>Every (item code, attribute key, attribute node) triple we ship a spec under. Both the plain
  /// <c>attributes</c> and the per-variant <c>attributesByType</c> routes, since the roll sets use the
  /// second and the stock items the first.</summary>
  private static IEnumerable<(
    string Code,
    string Key,
    JToken Node
  )> ShippedSpecs() {
    string[] keys = [RollSetSpec.AttributeKey, MoldSpec.AttributeKey];

    IEnumerable<ExItemDef> defs =
    [
      .. RollSetItemDefinitions.Definitions("iiex"),
      .. StockItemDefinitions.Definitions("iiex"),
      .. PatternItemDefinitions.Definitions("iiex"),
    ];

    foreach (ExItemDef def in defs) {
      JObject json = def.ToJson();
      string code = json["code"]?.ToString() ?? "?";

      foreach (string key in keys) {
        if (json["attributes"]?[key] is { } plain)
          yield return (code, key, plain);

        if (json["attributesByType"] is not JObject byType)
          continue;
        foreach (JProperty variant in byType.Properties())
          if (variant.Value[key] is { } perType)
            yield return ($"{code} {variant.Name}", key, perType);
      }
    }
  }

  [Fact]
  public void The_corpus_is_not_empty() {
    // Without this the guard below would pass by scanning nothing, and it scans emitted defs rather than
    // a directory, so a provider dropped from the list here reads as clean.
    Assert.NotEmpty(ShippedSpecs());
    Assert.Equal(2, ShippedSpecs().Select(s => s.Key).Distinct().Count());
  }

  [Fact]
  public void Every_shipped_stage_catalogue_file_declares_its_schema() {
    // The routes are config assets rather than item attributes, so they are scanned off the source tree
    // instead of off an emitted def - but they are the same contract and the same template.
    string[] silent =
    [
      .. ProcessRouteSeeds
        .Files()
        .Where(f => JToken.Parse(f.Json)[SpecSchema.Key] == null)
        .Select(f => f.Source),
    ];

    Assert.NotEmpty(ProcessRouteSeeds.Files());
    Assert.True(
      silent.Length == 0,
      "These shipped stage catalogues declare no schema:\n    "
        + string.Join("\n    ", silent)
    );
  }

  [Fact]
  public void Every_shipped_spec_declares_its_schema() {
    string[] silent =
    [
      .. ShippedSpecs()
        .Where(s => s.Node[SpecSchema.Key] == null)
        .Select(s => $"{s.Code} '{s.Key}'"),
    ];

    Assert.True(
      silent.Length == 0,
      "These emitted specs declare no schema, so anyone copying them as a template learns to omit it:\n    "
        + string.Join("\n    ", silent)
    );
  }

  [Fact]
  public void No_shipped_spec_declares_a_schema_this_build_cannot_read() {
    // Absent is the sibling guard's business, so it is skipped here rather than throwing through it: a
    // guard that dies on the case another guard already names reports the wrong failure.
    foreach ((string code, string key, JToken node) in ShippedSpecs()) {
      if (node[SpecSchema.Key] is not { } declared)
        continue;
      Assert.True(
        declared.Value<int>() >= SpecSchema.First,
        $"{code} '{key}' declares a schema below the first form"
      );
    }
  }
}
