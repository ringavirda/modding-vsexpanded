using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks that every block code a <c>multiblockStructure</c> layout asks for is a block some mod defines. A
/// cell naming a block that is not in the registry throws nothing and leaves the goldens correct; the
/// structure simply can never be completed, and the golden harness pins what a def emits rather than what
/// its references mean.
/// <para>
/// Only mod-domain codes are checked. A <c>game:</c> code lives in the Vintage Story install, which a
/// headless test does not require; alternation groups (<c>@(air|coalpile)</c>) are skipped for the same
/// reason, their members being vanilla.
/// </para>
/// </summary>
public static class MultiblockCodes {
  /// <summary>
  /// Every mod-domain layout code across <paramref name="sources"/> that no definition in those same
  /// sources provides, as <c>"{block}: wants {code}"</c> lines. Empty means every cell of every structure
  /// names a block that exists. Pass every mod whose blocks the layouts may reference: a furnace layout
  /// names <c>exlib:</c> fillers as well as its own parts, so exlib has to be in the list or its filler
  /// reads as missing.
  /// </summary>
  public static IReadOnlyList<string> Unresolvable(
    params (string Domain, Assembly Assembly)[] sources
  ) => Unresolvable(out _, sources);

  /// <summary>
  /// As <see cref="Unresolvable(ValueTuple{string, Assembly}[])"/>, also reporting how many layout codes
  /// were examined. Callers must assert that number is non-zero: a renamed attribute or a def source that
  /// stopped being collected leaves the check passing while examining nothing.
  /// </summary>
  public static IReadOnlyList<string> Unresolvable(
    out int codesChecked,
    params (string Domain, Assembly Assembly)[] sources
  ) {
    codesChecked = 0;
    // Every code any of these mods defines, per domain.
    var defined = new Dictionary<string, HashSet<string>>(
      StringComparer.Ordinal
    );
    var defs = new List<(string Domain, IExDef Def)>();
    foreach ((string domain, Assembly asm) in sources) {
      var codes = defined.TryGetValue(domain, out var set)
        ? set
        : defined[domain] = new HashSet<string>(StringComparer.Ordinal);
      foreach (IExDef def in DefinitionGoldens.Collect(domain, asm)) {
        // Recipe defs serialise as an array, not an object: indexing one by name throws rather than
        // returning null, so the shape is checked before anything is read off it.
        if (def.ToJson() is not JObject json)
          continue;
        defs.Add((domain, def));
        if (json["code"]?.Value<string>() is { } code)
          codes.Add(code);
      }
    }

    var missing = new List<string>();
    foreach ((string domain, IExDef def) in defs) {
      JToken? numbers = ((JObject)def.ToJson())["attributes"]
        ?["multiblockStructure"]
        ?["blockNumbers"];
      if (numbers is not JObject map)
        continue;

      foreach (var entry in map) {
        string wanted = entry.Key;
        if (
          !IsModDomainCode(wanted, out string wantDomain, out string wantPath)
        )
          continue;
        codesChecked++;
        if (
          !defined.TryGetValue(wantDomain, out var codes)
          || !AnyProvides(codes, wantPath)
        )
          missing.Add(
            $"{domain}:{((JObject)def.ToJson())["code"]} wants '{wanted}'"
          );
      }
    }
    return missing;
  }

  // An alternation group or a domainless/vanilla code is out of scope; anything else with a domain that
  // is not "game" is ours to account for.
  private static bool IsModDomainCode(
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
    // A domain wildcard names no mod that could be held responsible for the code existing. The shaft legend
    // is written `*:@(air|coalpile|furnace-chargepile)` because vanilla's matcher compares domain and path
    // separately: a bare alternation is implicitly `game:` and could never admit `iwex:furnace-chargepile`.
    if (domain == "*" || path.StartsWith('@'))
      return false;
    return domain != "game";
  }

  /// <summary>
  /// Whether some defined code satisfies the layout's (possibly wildcarded) one. A layout writes a code at
  /// any depth - <c>puddlinghearth-north</c> (a specific variant), <c>heatinghearth*</c> (any), or
  /// <c>pipe-passthrough-fire-*</c> (a variant family of the <c>pipe</c> def) - so the match runs in both
  /// directions on whole segments, which keeps <c>hopper-tall</c> from being read as a variant of a
  /// non-existent <c>hopper</c>.
  /// </summary>
  private static bool AnyProvides(
    HashSet<string> definedCodes,
    string wantedPath
  ) {
    string want = wantedPath.TrimEnd('*');
    foreach (string code in definedCodes) {
      if (code == want)
        return true;
      // The layout named a variant of this def: "puddlinghearth-north" against def "puddlinghearth".
      if (
        want.StartsWith(code, StringComparison.Ordinal)
        && want.Length > code.Length
        && want[code.Length] == '-'
      )
        return true;
      // The layout wildcarded a prefix of this def: "heatinghearth*" against def "heatinghearth".
      if (code.StartsWith(want, StringComparison.Ordinal))
        return true;
    }
    return false;
  }
}
