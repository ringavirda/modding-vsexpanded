using System.Collections.Generic;

namespace ExpandedLib.Checks;

/// <summary>One check's findings for one domain. <paramref name="Errors"/> empty means the check
/// found nothing wrong; a check is still reported when it examined nothing at all, so a modder can
/// tell "passed" from "did not run" by reading the log rather than guessing.</summary>
/// <param name="Check">The check's name, e.g. <c>"RecipeCodes"</c>.</param>
/// <param name="Domain">The mod domain examined.</param>
/// <param name="Errors">One readable line per violation found.</param>
public sealed record CheckResult(
  string Check,
  string Domain,
  IReadOnlyList<string> Errors
);
