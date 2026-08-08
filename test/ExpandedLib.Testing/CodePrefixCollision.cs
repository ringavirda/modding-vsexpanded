using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks that no block's base code is a proper prefix of another's at a <c>-</c> boundary. Wildcards
/// are routinely built from a base code (<c>SomeFamily.Code + "*"</c>) and used as multiblock
/// <c>Legend</c> entries, so a prefix collision widens such a wildcard onto a foreign block and lets a
/// structure complete with the wrong block in a cell, with every code involved still resolving.
/// <para>
/// The check is a boundary rule, not a substring rule: <c>slag-path</c> and <c>slag-pathslab</c> do not
/// collide, because the character after <c>slag-path</c> is <c>s</c> rather than <c>-</c>. The
/// convention that satisfies it: within a family, either every member lives in a <c>type</c> variant
/// under one shared code or none does.
/// </para>
/// </summary>
public static class CodePrefixCollision {
  /// <summary>
  /// Every pair of distinct base codes where one is a prefix of the other at a <c>-</c> boundary, as
  /// readable lines. Empty means no wildcard built from a base code can stray outside its family.
  /// </summary>
  public static IReadOnlyList<string> Collisions(string domain, Assembly asm) {
    var codes = DefinitionGoldens
      .Collect(domain, asm)
      .OfType<ExBlockDef>()
      .Select(d => d.Code)
      .Distinct()
      .OrderBy(c => c)
      .ToList();

    var findings = new List<string>();
    foreach (string shorter in codes)
      foreach (string longer in codes) {
        if (shorter == longer || !longer.StartsWith(shorter + "-"))
          continue;

        findings.Add(
          $"'{domain}:{shorter}' is a prefix of '{domain}:{longer}' - a wildcard "
            + $"'{domain}:{shorter}-*' built from the shorter code also matches the longer one"
        );
      }

    return findings;
  }
}
