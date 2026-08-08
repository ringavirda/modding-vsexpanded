using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks that every block code a mod registers resolves to a name in every locale it ships. An unresolved
/// name key renders as the raw key in game, and nothing in the build, the goldens or the runtime reports it.
/// <para>
/// Coverage is checked against concrete codes rather than base codes: a variant-grouped block's own
/// <c>code</c> is never placeable, and <c>Lang.GetMatching</c> has no dash-stripping fallback, so
/// <c>block-hopper-tall</c> can never be hit once the block declares <c>side(north|...)</c> - the key must
/// be <c>block-hopper-tall*</c>.
/// </para>
/// </summary>
public static class LangCoverage {
  /// <summary>
  /// Whether a concrete code resolves. <c>Lang.GetMatching</c> honours a trailing <c>*</c> on a key, so
  /// either the exact key or a wildcard key that prefixes it counts.
  /// </summary>
  private static bool Resolves(
    string key,
    HashSet<string> exact,
    IReadOnlyList<string> wildcardPrefixes
  ) =>
    exact.Contains(key)
    || wildcardPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal));

  /// <summary>
  /// Every <c>(locale, code)</c> with no resolving <c>block-</c> name key. Empty means full coverage.
  /// </summary>
  public static IReadOnlyList<string> MissingNames(
    string domain,
    Assembly asm,
    string langDirRepoRelative
  ) {
    string langDir = DefinitionGoldens.SolutionRelative(langDirRepoRelative);
    var codes = DefinitionCodes
      .ForDomain(domain, asm)
      .Select(r => r.Code[(domain.Length + 1)..]) // strip "domain:"
      .Distinct()
      .OrderBy(c => c, StringComparer.Ordinal)
      .ToList();

    var failures = new List<string>();

    foreach (
      string langFile in Directory
        .EnumerateFiles(langDir, "*.json")
        .OrderBy(f => f)
    ) {
      var lang = JObject.Parse(File.ReadAllText(langFile));
      string locale = Path.GetFileNameWithoutExtension(langFile);

      var exact = new HashSet<string>(StringComparer.Ordinal);
      var wildcardPrefixes = new List<string>();
      foreach (var prop in lang.Properties()) {
        if (prop.Name.EndsWith('*'))
          wildcardPrefixes.Add(prop.Name[..^1]);
        else
          exact.Add(prop.Name);
      }

      // One line per missing code per locale; MissingBaseCodes collapses them into the set an author
      // actually has to write.
      foreach (string code in codes)
        if (!Resolves("block-" + code, exact, wildcardPrefixes))
          failures.Add($"{locale}: block-{code}");
    }

    return failures;
  }

  /// <summary>
  /// Every <c>blockdesc-</c> key that matches no live block code, as <c>"{locale}: {key}"</c>. A missing
  /// description is not a defect, since a block without one shows none; a stale key is, because the block it
  /// described was renamed and the text silently stopped appearing. Nothing else in the suite reads these
  /// keys.
  /// </summary>
  public static IReadOnlyList<string> OrphanedDescriptions(
    string domain,
    Assembly asm,
    string langDirRepoRelative
  ) {
    string langDir = DefinitionGoldens.SolutionRelative(langDirRepoRelative);
    var codes = DefinitionCodes
      .ForDomain(domain, asm)
      .Select(r => r.Code[(domain.Length + 1)..])
      .ToList();

    var orphans = new List<string>();
    foreach (
      string langFile in Directory
        .EnumerateFiles(langDir, "*.json")
        .OrderBy(f => f)
    ) {
      var lang = JObject.Parse(File.ReadAllText(langFile));
      string locale = Path.GetFileNameWithoutExtension(langFile);

      foreach (var prop in lang.Properties()) {
        if (!prop.Name.StartsWith("blockdesc-", StringComparison.Ordinal))
          continue;

        string target = prop.Name["blockdesc-".Length..];
        bool wildcard = target.EndsWith('*');
        string stem = wildcard ? target[..^1] : target;

        bool hit = wildcard
          ? codes.Any(c => c.StartsWith(stem, StringComparison.Ordinal))
          : codes.Any(c => c == stem);

        if (!hit)
          orphans.Add($"{locale}: {prop.Name}");
      }
    }
    return orphans;
  }

  /// <summary>
  /// The distinct base codes behind <see cref="MissingNames"/>, which is what an author has to write: one
  /// wildcard key usually covers a whole variant family.
  /// </summary>
  public static IReadOnlyList<string> MissingBaseCodes(
    IEnumerable<string> failures
  ) =>
    [
      .. failures
        .Select(f => f[(f.IndexOf("block-", StringComparison.Ordinal) + 6)..])
        .Select(c => c.Split('-')[0])
        .Distinct()
        .OrderBy(c => c, StringComparer.Ordinal),
    ];
}
