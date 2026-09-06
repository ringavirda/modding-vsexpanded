using System;
using System.Collections.Generic;
using System.Linq;

namespace ExpandedLib.Checks;

/// <summary>
/// Checks that no block's base code is a proper prefix of another's at a <c>-</c> boundary. Wildcards
/// are routinely built from a base code (<c>SomeFamily.Code + "*"</c>) and used as multiblock
/// <c>Legend</c> entries, so a prefix collision widens such a wildcard onto a foreign block and lets a
/// structure complete with the wrong block in a cell, with every code involved still resolving.
/// </summary>
public static class CodePrefixCollisionCheck {
  /// <summary>Every collision found for <paramref name="domain"/>, as the check's <see cref="CheckResult"/>.</summary>
  public static CheckResult Run(ICheckSource source, string domain) {
    List<string> codes =
    [
      .. source
        .BlockDefinitions(domain)
        .Select(d => d.Code)
        .Distinct()
        .OrderBy(c => c, StringComparer.Ordinal),
    ];

    var errors = new List<string>();
    foreach (string shorter in codes)
      foreach (string longer in codes) {
        if (shorter == longer || !longer.StartsWith(shorter + "-", StringComparison.Ordinal))
          continue;

        errors.Add(
          $"'{domain}:{shorter}' is a prefix of '{domain}:{longer}' - a wildcard "
            + $"'{domain}:{shorter}-*' built from the shorter code also matches the longer one"
        );
      }

    return new CheckResult("CodePrefixCollision", domain, errors);
  }
}
