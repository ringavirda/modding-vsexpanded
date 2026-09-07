using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace ExpandedLib.Verify;

/// <summary>
/// Checks that every <c>{ "type": "item"|"block", "code": ... }</c> pair a mod's <c>recipes/</c>
/// JSON carries - an ingredient slot or an output, whichever schema the recipe kind uses - names a
/// code the loaded domains actually declare. Recognised structurally rather than per recipe schema
/// (grid, alloy, smithing, clayforming, knapping, ...) since they all place the same two sibling
/// keys on the object that names one collectible; a recipe kind this tool has never seen still gets
/// checked.
/// </summary>
public static class RecipeCodeChecker {
  /// <summary>Every recipe code reference in <paramref name="recipeFiles"/> that resolves to
  /// nothing in <paramref name="catalogue"/>. <paramref name="recipeFiles"/> is the mod's own
  /// <c>recipes/</c> files only - never a merged domain's vanilla or dependency content, which is
  /// not this mod's mistake to report even when it shares the same domain (an overlay into
  /// <c>assets/game/...</c>, most often).</summary>
  public static List<Finding> Run(
    BlockItemCatalogue catalogue,
    string domain,
    IEnumerable<(string Path, JToken Json)> recipeFiles
  ) {
    var findings = new List<Finding>();
    foreach ((string path, JToken json) in recipeFiles) {
      foreach (JToken recipe in json is JArray arr ? arr : [json]) {
        if (recipe is not JObject root)
          continue;
        Dictionary<string, string[]> holes = Placeholders(root);
        foreach (JObject reference in FindReferences(root))
          CheckOne(reference, holes, catalogue, domain, path, findings);
      }
    }
    return findings;
  }

  // Any object carrying both "type" ("item"/"block") and "code" siblings names one collectible,
  // regardless of whether it sits in an ingredient slot, an ingredient array, or an output - so the
  // tree is walked generically instead of once per recipe schema.
  private static IEnumerable<JObject> FindReferences(JToken node) {
    if (node is JObject obj) {
      if (
        obj["code"] is JValue { Type: JTokenType.String }
        && obj["type"] is JValue { Value: string t }
        && (t == "item" || t == "block")
      )
        yield return obj;
      foreach (JProperty prop in obj.Properties())
        foreach (JObject found in FindReferences(prop.Value))
          yield return found;
    } else if (node is JArray arr) {
      foreach (JToken item in arr)
        foreach (JObject found in FindReferences(item))
          yield return found;
    }
  }

  // The {name} holes a code can carry, mapped to the states a top-level "ingredients" slot binds
  // them to - the same convention ExpandedLib.Checks.RecipeCodesCheck resolves for grid outputs.
  private static Dictionary<string, string[]> Placeholders(JObject recipe) {
    var holes = new Dictionary<string, string[]>(StringComparer.Ordinal);
    if (recipe["ingredients"] is not JObject ingredients)
      return holes;
    foreach (JProperty slot in ingredients.Properties()) {
      if (
        slot.Value["name"] is not { } name
        || slot.Value["allowedVariants"] is not JArray states
      )
        continue;
      holes[(string)name!] = [.. states.Select(s => (string)s!)];
    }
    return holes;
  }

  private static void CheckOne(
    JObject reference,
    Dictionary<string, string[]> holes,
    BlockItemCatalogue catalogue,
    string sourceDomain,
    string sourcePath,
    List<Finding> findings
  ) {
    string type = (string)reference["type"]!;
    string code = (string)reference["code"]!;

    foreach (string concrete in Expand(code, holes)) {
      var loc = new AssetLocation(concrete);
      string targetDomain = loc.Domain;

      if (
        !catalogue.Blocks.ContainsKey(targetDomain)
        && !catalogue.Items.ContainsKey(targetDomain)
      ) {
        findings.Add(
          new Finding(
            FindingLevel.Info,
            "RecipeCode",
            $"{sourceDomain}:{sourcePath}",
            null,
            $"domain '{targetDomain}' is not loaded - cannot verify {type} code {concrete}"
          )
        );
        continue;
      }

      HashSet<string> declared =
        type == "block"
          ? catalogue.Blocks[targetDomain]
          : catalogue.Items[targetDomain];
      HashSet<string> unresolved = catalogue.UnresolvedPrefixes[targetDomain];

      bool resolves =
        declared.Any(c => WildcardMatch(loc.Path, c))
        || unresolved.Any(prefix =>
          loc.Path.StartsWith(prefix, StringComparison.Ordinal)
        );

      if (!resolves)
        findings.Add(
          new Finding(
            FindingLevel.Error,
            "RecipeCode",
            $"{sourceDomain}:{sourcePath}",
            null,
            $"unresolved {type} code: {concrete}"
          )
        );
    }
  }

  private static bool WildcardMatch(string reference, string declared) {
    var a = new AssetLocation("game", reference);
    var b = new AssetLocation("game", declared);
    return WildcardUtil.Match(a, b) || WildcardUtil.Match(b, a);
  }

  private static IEnumerable<string> Expand(
    string code,
    Dictionary<string, string[]> holes
  ) {
    IEnumerable<string> codes = [code];
    foreach (var (name, states) in holes) {
      string hole = "{" + name + "}";
      codes = codes.SelectMany(c =>
        c.Contains(hole, StringComparison.Ordinal)
          ? states.Select(s => c.Replace(hole, s, StringComparison.Ordinal))
          : (IEnumerable<string>)[c]
      );
    }
    // A hole this recipe's own "ingredients" map cannot explain (a non-grid schema, most often) is
    // dropped rather than checked as literal text - it is never this mod's own mistake to report,
    // only a shape this tool cannot resolve.
    return codes.Where(c => !c.Contains('{', StringComparison.Ordinal));
  }
}
