using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks that the asset paths a mod's code-first definitions name exist on disk. The goldens pin what
/// a def emits, which is a different question: a def can emit a stable but wrong <c>shape.base</c>, and
/// an unresolved shape shows up only in game, as a block with no model.
/// <para>
/// Only mod-domain paths are checked. A <c>game:</c> shape lives in the Vintage Story install, which a
/// headless test does not require; the mod's own <c>assets/{domain}/shapes/</c> tree is in the repo.
/// </para>
/// </summary>
public static class DefinitionAssets {
  /// <summary>
  /// Every mod-domain shape reference in <paramref name="asm"/>'s definitions for
  /// <paramref name="domain"/> that has no file behind it, as readable
  /// <c>"{block code}: {shape path} -> {expected file}"</c> lines. Empty means every shape resolves.
  /// </summary>
  public static IReadOnlyList<string> MissingShapes(string domain, Assembly asm) {
    var missing = new List<string>();

    foreach (IExDef def in DefinitionGoldens.Collect(domain, asm)) {
      foreach (string reference in ShapeReferences(def.ToJson())) {
        var location = new Vintagestory.API.Common.AssetLocation(reference);
        if (location.Domain == "game")
          continue; // lives in the game install, not this repo

        if (!Resolves(location.Domain, location.Path))
          missing.Add(
            $"{def.Location.Domain}:{def.Location.Path} -> '{reference}' "
              + $"(expected assets/{location.Domain}/shapes/{location.Path}.json)"
          );
      }
    }
    return missing;
  }

  /// <summary>
  /// Whether a shape path has a file behind it. A path may carry <c>{variant}</c> placeholders the game
  /// substitutes at load (<c>stonepath-slab-{cover}</c> to <c>-free</c> or <c>-snow</c>), so a
  /// placeholder path is checked as a family: at least one matching file must exist. That is weaker
  /// than resolving every variant, because the variant values live on the def and the substitution
  /// rules are the game's, but it still catches a path pointing where nothing exists.
  /// </summary>
  private static bool Resolves(string domain, string path) {
    string root = DefinitionGoldens.SolutionRelative($"assets/{domain}/shapes");
    string relative = path.Replace('/', Path.DirectorySeparatorChar) + ".json";

    if (!path.Contains('{'))
      return File.Exists(Path.Combine(root, relative));

    string pattern = Regex.Replace(relative, @"\{[^}]*\}", "*");
    string directory = Path.GetDirectoryName(Path.Combine(root, pattern))!;
    return Directory.Exists(directory)
      && Directory.EnumerateFiles(directory, Path.GetFileName(pattern)).Any();
  }

  // Every "base" under a shape-bearing property: the single `shape` form and the `shapeByType` map
  // (emitted lowercased as `shapebytype`), at any nesting depth. Matching on the property-name prefix
  // covers a def that grows a new shape-bearing property without naming it here.
  private static IEnumerable<string> ShapeReferences(JToken token) {
    foreach (JProperty property in token.Children<JProperty>()) {
      bool isShape = property.Name.StartsWith(
        "shape",
        StringComparison.OrdinalIgnoreCase
      );

      if (isShape)
        foreach (string found in BaseValues(property.Value))
          yield return found;
      else
        foreach (string found in ShapeReferences(property.Value))
          yield return found;
    }

    if (token is JArray array)
      foreach (JToken item in array)
        foreach (string found in ShapeReferences(item))
          yield return found;
  }

  // The "base" strings inside a shape value - either the object itself, or one per entry of a
  // by-type map.
  private static IEnumerable<string> BaseValues(JToken shape) {
    if (shape is not JObject obj)
      yield break;

    if (obj["base"] is JValue { Value: string direct }) {
      yield return direct;
      yield break;
    }

    foreach (JProperty entry in obj.Properties())
      if (
        entry.Value is JObject byType
        && byType["base"] is JValue { Value: string code }
      )
        yield return code;
  }
}
