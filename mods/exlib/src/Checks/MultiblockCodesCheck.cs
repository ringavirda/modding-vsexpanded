using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Checks;

/// <summary>
/// Checks that every block code a <c>multiblockStructure</c> layout asks for is a block some mod
/// registers. A cell naming a block that is not in the registry throws nothing: the structure simply
/// can never be completed. Only mod-domain codes are checked - a <c>game:</c> code or an alternation
/// group (<c>@(air|coalpile)</c>) is vanilla's to answer for, not a mod's.
/// <para>
/// Matched against <see cref="ICheckSource.BlockCodes"/> - concrete registered codes - rather than
/// against a def's bare <c>code</c> field, so this class needs no knowledge of any def outside the
/// domain being checked other than what the source's codes already carry.
/// </para>
/// </summary>
public static class MultiblockCodesCheck {
  /// <summary>Every unresolvable layout code in <paramref name="domain"/>'s defs, as the check's <see cref="CheckResult"/>.</summary>
  public static CheckResult Run(ICheckSource source, string domain) {
    var codesByDomain = new Dictionary<string, HashSet<string>>(
      StringComparer.Ordinal
    );
    foreach (AssetLocation code in source.BlockCodes) {
      var set = codesByDomain.TryGetValue(code.Domain, out var s)
        ? s
        : codesByDomain[code.Domain] = new HashSet<string>(
          StringComparer.Ordinal
        );
      set.Add(code.Path);
    }

    var errors = new List<string>();
    foreach (ExBlockDef def in source.BlockDefinitions(domain)) {
      if (
        def.ToJson()["attributes"]?["multiblockStructure"]?["blockNumbers"]
        is not JObject numbers
      )
        continue;

      foreach (var entry in numbers) {
        if (
          !IsModDomainCode(
            entry.Key,
            out string wantDomain,
            out string wantPath
          )
        )
          continue;

        if (
          !codesByDomain.TryGetValue(wantDomain, out var codes)
          || !AnyProvides(codes, wantPath)
        )
          errors.Add($"{domain}:{def.Code} wants '{entry.Key}'");
      }
    }
    return new CheckResult("MultiblockCodes", domain, errors);
  }

  // An alternation group or a domainless/vanilla code is out of scope; anything else with a domain
  // that is not "game" is some mod's to account for.
  internal static bool IsModDomainCode(
    string code,
    out string domain,
    out string path
  ) {
    domain = path = "";
    if (code.StartsWith('@'))
      return false;
    int colon = code.IndexOf(':');
    if (colon <= 0)
      return false;
    domain = code[..colon];
    path = code[(colon + 1)..];
    if (domain == "*" || path.StartsWith('@'))
      return false;
    return domain != "game";
  }

  // Whether some registered code satisfies the layout's (possibly wildcarded) one, matched segment by
  // '-'-delimited segment over the shorter of the two: a registered code may be a longer, more
  // specific variant of the wanted path ("furnace-tuyere-n-normal" satisfies "furnace-tuyere-n") or the
  // wanted path may be the longer, more specific one ("hopper-tall" satisfies a bare "hopper"). A `*`
  // segment ("furnace-blastcore-*-n", any tier) matches any one segment of the other side; a segment
  // ending in `*` ("convertercontrol*") matches by prefix within that segment, letting a wanted path
  // glue the wildcard onto a still-open variant without a leading dash.
  internal static bool AnyProvides(
    HashSet<string> registeredCodes,
    string wantedPath
  ) {
    string[] want = wantedPath.Split('-');
    foreach (string code in registeredCodes) {
      if (SegmentsMatch(want, code.Split('-')))
        return true;
    }
    return false;
  }

  private static bool SegmentsMatch(string[] want, string[] code) {
    int common = Math.Min(want.Length, code.Length);
    for (int i = 0; i < common; i++) {
      string w = want[i];
      if (w == "*")
        continue;
      if (w.EndsWith('*')) {
        if (!code[i].StartsWith(w[..^1], StringComparison.Ordinal))
          return false;
      } else if (w != code[i]) {
        return false;
      }
    }
    return true;
  }
}
