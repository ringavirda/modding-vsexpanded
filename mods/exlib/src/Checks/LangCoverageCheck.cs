using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Checks;

/// <summary>
/// Checks that every block code a mod registers resolves to a name in the <c>en</c> locale, matched
/// against <see cref="ICheckSource.BlockCodes"/> - concrete registered codes, not a def's base code,
/// since a variant-grouped block's own base code is never placeable. An unresolved <c>en</c> key
/// renders as the raw key in game regardless of the player's own locale, since <c>en</c> is what
/// every other translation falls back to.
/// <para>
/// Parity of a non-<c>en</c> locale against <c>en</c> is a repository-time concern
/// (<c>ExpandedLib.Testing.LangCoverage</c>, <c>LangParityTests</c>), not this one: a missing
/// Ukrainian key falls back to English rather than showing raw, so it is a translation gap to track,
/// not a defect to fail a server load over.
/// </para>
/// </summary>
public static class LangCoverageCheck {
  private const string EnglishLocale = "en";

  /// <summary>Every unresolved <c>block-</c> name key for <paramref name="domain"/> in the <c>en</c>
  /// locale, as the check's <see cref="CheckResult"/>.</summary>
  public static CheckResult Run(ICheckSource source, string domain) {
    List<string> codes =
    [
      .. source
        .BlockCodes.Where(c => c.Domain == domain)
        .Select(c => c.Path)
        .Distinct()
        .OrderBy(c => c, StringComparer.Ordinal),
    ];

    var errors = new List<string>();
    foreach ((string locale, JObject lang) in source.Lang(domain)) {
      if (locale != EnglishLocale)
        continue;

      var exact = new HashSet<string>(StringComparer.Ordinal);
      var wildcardPrefixes = new List<string>();
      foreach (JProperty prop in lang.Properties()) {
        if (prop.Name.EndsWith('*'))
          wildcardPrefixes.Add(prop.Name[..^1]);
        else
          exact.Add(prop.Name);
      }

      foreach (string code in codes) {
        string key = "block-" + code;
        if (
          !exact.Contains(key)
          && !wildcardPrefixes.Any(p =>
            key.StartsWith(p, StringComparison.Ordinal)
          )
        )
          errors.Add($"{locale}: {key}");
      }
    }
    return new CheckResult("LangCoverage", domain, errors);
  }
}
