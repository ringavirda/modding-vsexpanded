using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Verify;

/// <summary>
/// Informational-only count of a domain's translation gap: an <c>en</c> key with no counterpart in
/// another shipped locale falls back to English in game rather than showing raw, so it is a
/// translation backlog to track, never a defect to fail a run over - see
/// <c>ExpandedLib.Checks.LangCoverageCheck</c>'s remarks for the same distinction on the checked-in
/// harness side.
/// </summary>
public static class LangParityChecker {
  /// <summary>One <see cref="FindingLevel.Info"/> finding per non-<c>en</c> locale among
  /// <paramref name="langFiles"/> - the mod's own <c>lang/</c> files only, so a merged domain's
  /// vanilla or dependency translations are never counted as this mod's gap.</summary>
  public static List<Finding> Run(
    string domain,
    IEnumerable<(string Path, JToken Json)> langFiles
  ) {
    var findings = new List<Finding>();
    var files = langFiles.ToList();
    if (files.FirstOrDefault(f => System.IO.Path.GetFileNameWithoutExtension(f.Path) == "en").Json
      is not JObject en)
      return findings;
    var enKeys = new HashSet<string>(en.Properties().Select(p => p.Name), StringComparer.Ordinal);

    foreach ((string path, JToken json) in files) {
      string locale = System.IO.Path.GetFileNameWithoutExtension(path);
      if (locale == "en" || json is not JObject other)
        continue;
      var otherKeys = new HashSet<string>(other.Properties().Select(p => p.Name), StringComparer.Ordinal);
      int missing = enKeys.Count(k => !otherKeys.Contains(k));
      if (missing > 0)
        findings.Add(
          new Finding(
            FindingLevel.Info,
            "LangParity",
            $"{domain}:{path}",
            null,
            $"{locale}: missing {missing} of {enKeys.Count} 'en' key(s)"
          )
        );
    }
    return findings;
  }
}
