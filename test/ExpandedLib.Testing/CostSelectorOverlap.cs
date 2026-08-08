using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Registries.Recipes;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks that no two recipe-cost selectors in a catalogue can match the same block.
///
/// <para>
/// <b>Why overlap is silent damage.</b> <c>ExRecipeCosts</c> applies <b>every</b> catalogue entry in
/// sequence (<c>foreach (var entry in catalogue.Values)</c>) rather than taking the first match, so two
/// selectors hitting one recipe means the later one overwrites the earlier's applied costs - and
/// dictionary enumeration order is not a contract. The result is a cost that depends on nothing the
/// author can see.
/// </para>
///
/// <para>
/// <b>Hierarchical codes make this newly reachable.</b> Flat names were accidentally safe:
/// <c>iwex:slagpath-*</c> could not match <c>slagpathslab-free</c>, because the character after
/// <c>slagpath</c> is <c>s</c>, not <c>-</c>. Rename them to <c>slag-path</c> and <c>slag-path-slab</c>
/// and the parent's wildcard swallows the child. The rule that falls out, and that the naming convention
/// now carries: <b>a path segment may only become a folder if it is not itself a block.</b>
/// </para>
/// </summary>
public static class CostSelectorOverlap
{
  /// <summary>
  /// Every pair of catalogue entries whose selectors can both match one code. Empty means no overlap.
  /// <paramref name="sampleCodes"/> is the mod's real registered codes - overlap is decided against
  /// what actually ships, not against pattern algebra.
  /// </summary>
  public static IReadOnlyList<string> Overlaps(
    IReadOnlyDictionary<string, RecipeCostEntry> catalogue,
    IEnumerable<string> sampleCodes
  )
  {
    var selectors = catalogue
      .Where(kv => !string.IsNullOrEmpty(kv.Value.Match))
      .Select(kv => (Key: kv.Key, Pattern: new AssetLocation(kv.Value.Match!)))
      .ToList();

    var codes = sampleCodes.Select(c => new AssetLocation(c)).ToList();
    var findings = new List<string>();

    for (int i = 0; i < selectors.Count; i++)
      for (int j = i + 1; j < selectors.Count; j++)
      {
        var both = codes
          .Where(c =>
            WildcardUtil.Match(selectors[i].Pattern, c)
            && WildcardUtil.Match(selectors[j].Pattern, c)
          )
          .Select(c => c.ToString())
          .Take(3)
          .ToList();

        if (both.Count > 0)
          findings.Add(
            $"'{selectors[i].Key}' ({selectors[i].Pattern}) and '{selectors[j].Key}' "
              + $"({selectors[j].Pattern}) both match: {string.Join(", ", both)}"
          );
      }

    return findings;
  }

  /// <summary>Convenience: the mod's real codes, from its own definitions.</summary>
  public static IEnumerable<string> CodesOf(string domain, Assembly asm) =>
    DefinitionCodes.ForDomain(domain, asm).Select(r => r.Code);
}
