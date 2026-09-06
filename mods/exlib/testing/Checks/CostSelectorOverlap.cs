using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks that no two recipe-cost selectors in a catalogue can match the same block.
/// <c>ExRecipeCosts</c> applies every catalogue entry in sequence rather than taking the first match,
/// so two selectors hitting one recipe leaves the later entry overwriting the earlier's costs in
/// dictionary enumeration order, which is not a contract.
/// <para>
/// Hierarchical codes make this reachable: <c>iiex:slag-path-*</c> swallows <c>slag-path-slab</c>,
/// where the flat <c>slagpath-*</c> could not match <c>slagpathslab</c>. The naming rule that follows
/// is that a path segment may only become a folder if it is not itself a block.
/// </para>
/// <para>
/// Compared within a <see cref="RecipeCostEntry.Type"/>, never across: <c>ExRecipeCosts.Apply</c>
/// dispatches on it, so a machine that is both crafted and built carries one row of each against the
/// same selector and neither can overwrite the other.
/// </para>
/// </summary>
public static class CostSelectorOverlap {
  /// <summary>
  /// Every pair of same-type catalogue entries whose selectors can both match one code. Empty means no
  /// overlap. <paramref name="sampleCodes"/> is the mod's registered codes: overlap is decided against
  /// the codes that ship, not against pattern algebra.
  /// </summary>
  public static IReadOnlyList<string> Overlaps(
    IReadOnlyDictionary<string, RecipeCostEntry> catalogue,
    IEnumerable<string> sampleCodes
  ) {
    var selectors = catalogue
      .Where(kv => !string.IsNullOrEmpty(kv.Value.Match))
      .Select(kv =>
        (
          Key: kv.Key,
          Type: kv.Value.Type ?? "grid",
          Pattern: new AssetLocation(kv.Value.Match!)
        )
      )
      .ToList();

    var codes = sampleCodes.Select(c => new AssetLocation(c)).ToList();
    var findings = new List<string>();

    for (int i = 0; i < selectors.Count; i++)
      for (int j = i + 1; j < selectors.Count; j++) {
        if (
          !string.Equals(
            selectors[i].Type,
            selectors[j].Type,
            StringComparison.OrdinalIgnoreCase
          )
        )
          continue;

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
            $"'{selectors[i].Key}' ({selectors[i].Type} {selectors[i].Pattern}) and "
              + $"'{selectors[j].Key}' ({selectors[j].Type} {selectors[j].Pattern}) "
              + $"both match: {string.Join(", ", both)}"
          );
      }

    return findings;
  }

  /// <summary>The mod's registered codes, read from its own definitions.</summary>
  public static IEnumerable<string> CodesOf(string domain, Assembly asm) =>
    DefinitionCodes.ForDomain(domain, asm).Select(r => r.Code);
}
