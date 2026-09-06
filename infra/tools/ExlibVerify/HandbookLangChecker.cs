using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Verify;

/// <summary>
/// Checks that every <c>title</c>/<c>text</c> lang key a mod's <c>config/handbook/*.json</c> page
/// descriptor names (the <c>GuiHandbookTextPage</c> shape <c>vssurvivalmod</c> loads from that
/// folder: <c>pageCode</c>, <c>title</c>, <c>text</c>) resolves in that key's own domain's
/// <c>lang/en.json</c> - the one locale every other translation falls back to, so a key missing only
/// there renders as the raw <c>domain:key</c> string regardless of the player's locale.
/// </summary>
public static class HandbookLangChecker {
  /// <summary>Every unresolved <c>title</c>/<c>text</c> key among <paramref name="handbookFiles"/> -
  /// the mod's own <c>config/handbook/</c> files only, never a merged domain's dependency content.</summary>
  public static List<Finding> Run(
    AssetStore store,
    string domain,
    IEnumerable<(string Path, JToken Json)> handbookFiles
  ) {
    var findings = new List<Finding>();
    foreach ((string path, JToken json) in handbookFiles) {
      if (json is not JObject page)
        continue;
      CheckKey(store, domain, path, page, "title", findings);
      CheckKey(store, domain, path, page, "text", findings);
    }
    return findings;
  }

  private static void CheckKey(
    AssetStore store,
    string domain,
    string path,
    JObject page,
    string field,
    List<Finding> findings
  ) {
    string? value = (string?)page[field];
    if (string.IsNullOrEmpty(value))
      return;

    int colon = value.IndexOf(':');
    string keyDomain = colon < 0 ? domain : value[..colon];
    string key = colon < 0 ? value : value[(colon + 1)..];

    JToken? lang = store.TryGet(keyDomain, "lang/en.json");
    if (lang is not JObject langObj || langObj[key] == null)
      findings.Add(
        new Finding(
          FindingLevel.Error,
          "HandbookLang",
          $"{domain}:{path}",
          null,
          $"{field} '{value}' has no matching key in {keyDomain}:lang/en.json"
        )
      );
  }
}
