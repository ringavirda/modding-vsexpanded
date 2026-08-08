using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks that every block code a mod registers resolves to a <b>name</b> in every locale it ships.
///
/// <para>
/// <b>An unresolved name key is invisible until a player looks at the block</b>, and then it renders
/// the raw key. Nothing in the build, the goldens or the runtime says a word - which is how
/// <c>heatingfurnacecore</c> and <c>puddlingfurnacecore</c> came to ship with no name key at all, and how
/// the tall hopper orphaned its bare key the moment it gained a <c>side</c> variant group.
/// </para>
///
/// <para>
/// <b>The bare-key trap.</b> A variant-grouped block's own <c>code</c> is never itself placeable -
/// every real instance carries a variant suffix - and VS's <c>Lang.GetMatching</c> has no dash-stripping
/// fallback. So <c>block-hopper-tall</c> can never be hit once the block declares
/// <c>side(north|…)</c>; it must be <c>block-hopper-tall*</c>. This is why coverage is checked against
/// <b>concrete</b> codes rather than base codes.
/// </para>
/// </summary>
public static class LangCoverage
{
  /// <summary>
  /// VS resolves a name with <c>Lang.GetMatching</c>, which honours a trailing <c>*</c> on a key. So a
  /// concrete code resolves when a locale has either its exact key or a wildcard key that prefixes it.
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
  )
  {
    string langDir = DefinitionGoldens.SolutionRelative(langDirRepoRelative);
    var codes = DefinitionCodes
      .ForDomain(domain, asm)
      .Select(r => r.Code[(domain.Length + 1)..]) // strip "domain:"
      .Distinct()
      .OrderBy(c => c, StringComparer.Ordinal)
      .ToList();

    var failures = new List<string>();

    foreach (string langFile in Directory.EnumerateFiles(langDir, "*.json").OrderBy(f => f))
    {
      var lang = JObject.Parse(File.ReadAllText(langFile));
      string locale = Path.GetFileNameWithoutExtension(langFile);

      var exact = new HashSet<string>(StringComparer.Ordinal);
      var wildcardPrefixes = new List<string>();
      foreach (var prop in lang.Properties())
      {
        if (prop.Name.EndsWith('*'))
          wildcardPrefixes.Add(prop.Name[..^1]);
        else
          exact.Add(prop.Name);
      }

      // Report one line per missing code per locale, but collapse the common case where a code is
      // missing from every locale - that is one authoring job, not three.
      foreach (string code in codes)
        if (!Resolves("block-" + code, exact, wildcardPrefixes))
          failures.Add($"{locale}: block-{code}");
    }

    return failures;
  }

  /// <summary>
  /// Every <c>blockdesc-</c> key that matches <b>no live block code</b>, as <c>"{locale}: {key}"</c>.
  ///
  /// <para>
  /// <b>The mirror image of <see cref="MissingNames"/>, and it catches what that cannot.</b> A
  /// description is optional - a block without one simply shows none - so a *missing* <c>blockdesc-</c>
  /// is not a defect. A <b>stale</b> one is: it used to describe a block, the block's code moved, and the
  /// description silently stopped appearing. Nothing else in the suite reads these keys, so the text just
  /// vanishes from the game with every test still green.
  /// </para>
  ///
  /// <para>
  /// Found by the 2026-08-03 naming wave, which moved <c>block-bunker-*</c> to <c>block-ore-bunker-*</c>
  /// and left <c>blockdesc-bunker-*</c> behind - one family's descriptions dead in three locales, caught
  /// only because a neighbouring test happened to list a <c>blockdesc-</c> key by hand.
  /// </para>
  /// </summary>
  public static IReadOnlyList<string> OrphanedDescriptions(
    string domain,
    Assembly asm,
    string langDirRepoRelative
  )
  {
    string langDir = DefinitionGoldens.SolutionRelative(langDirRepoRelative);
    var codes = DefinitionCodes
      .ForDomain(domain, asm)
      .Select(r => r.Code[(domain.Length + 1)..])
      .ToList();

    var orphans = new List<string>();
    foreach (string langFile in Directory.EnumerateFiles(langDir, "*.json").OrderBy(f => f))
    {
      var lang = JObject.Parse(File.ReadAllText(langFile));
      string locale = Path.GetFileNameWithoutExtension(langFile);

      foreach (var prop in lang.Properties())
      {
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
  /// The distinct base codes behind <see cref="MissingNames"/>, which is what an author actually has to
  /// write - one wildcard key usually covers a whole variant family.
  /// </summary>
  public static IReadOnlyList<string> MissingBaseCodes(IEnumerable<string> failures) =>
    [
      .. failures
        .Select(f => f[(f.IndexOf("block-", StringComparison.Ordinal) + 6)..])
        .Select(c => c.Split('-')[0])
        .Distinct()
        .OrderBy(c => c, StringComparer.Ordinal),
    ];
}
