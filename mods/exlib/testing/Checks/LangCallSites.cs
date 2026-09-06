using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks that every lang key a mod's own source hands to <c>Lang.Get</c>, <c>ActionLangCode</c> or
/// <c>SendIngameError</c> exists in every locale it ships. <see cref="LangCoverage"/> checks the other
/// direction - that registered block codes have names - and never looks at a call site, so a key
/// misspelt in C# renders raw in game with the goldens, the build and the runtime all silent.
/// <para>
/// Only literal keys are checked. A key built by concatenation (<c>"iiex:bf-state-" + state</c>) names a
/// family rather than a key, and is skipped along with the two-argument <c>SendIngameError</c> overload,
/// which supplies its own text and never consults a lang file.
/// </para>
/// </summary>
public static class LangCallSites {
  #region Call-site scanning

  // The three forms that name a key. Each is matched up to the value only; the literals themselves come
  // out of the region that follows - a call's balanced argument list, or an assignment's right-hand side -
  // so a ternary's arms are seen too.
  private static readonly Regex LangCall = new(
    @"\bLang\.Get(?:IfExists|Matching)?\s*\(",
    RegexOptions.Compiled
  );
  private static readonly Regex ActionLangCode = new(
    @"\bActionLangCode\s*[=:]\s*",
    RegexOptions.Compiled
  );
  private static readonly Regex ErrorCall = new(
    @"\bSendIngameError\s*\(",
    RegexOptions.Compiled
  );

  // Case-insensitive on purpose. A key is lower-case by convention, so a capital in one is a typo - and a
  // pattern that only matched lower-case would SKIP that literal rather than fail on it, which is the
  // quietest possible outcome for the one thing this file exists to catch.
  private static readonly Regex Literal = new(
    "\"([a-z0-9]+:[a-z0-9-]+)\"",
    RegexOptions.Compiled | RegexOptions.IgnoreCase
  );
  private static readonly Regex BareLiteral = new(
    "\"([a-z0-9-]+)\"",
    RegexOptions.Compiled | RegexOptions.IgnoreCase
  );

  /// <summary>
  /// The source text between an opening parenthesis and the one that closes it. String contents are not
  /// parsed, so a parenthesis inside a literal would end the region early; no call site in this repo
  /// carries one, and a truncated region under-reports rather than inventing a key.
  /// </summary>
  private static string ArgumentRegion(string source, int openParen) {
    int depth = 0;
    for (int i = openParen; i < source.Length; i++) {
      if (source[i] == '(')
        depth++;
      else if (source[i] == ')' && --depth == 0)
        return source[(openParen + 1)..i];
    }
    return source[(openParen + 1)..];
  }

  /// <summary>
  /// The right-hand side of an assignment starting at <paramref name="start"/>: everything up to the
  /// first <c>,</c> or <c>;</c> outside brackets, or the bracket that closes the initializer or argument
  /// list the assignment sits in. A ternary carries no brackets of its own, so both of its arms fall
  /// inside the region - which is the whole reason this exists rather than reading the one literal that
  /// happens to sit against the <c>=</c>.
  /// </summary>
  private static string AssignedRegion(string source, int start) {
    int depth = 0;
    for (int i = start; i < source.Length; i++) {
      char c = source[i];
      if (c is '(' or '[' or '{')
        depth++;
      else if (c is ')' or ']' or '}') {
        if (depth == 0)
          return source[start..i];
        depth--;
      } else if (depth == 0 && c is ',' or ';')
        return source[start..i];
    }
    return source[start..];
  }

  /// <summary>A literal glued to a neighbour with <c>+</c> is one piece of a key, not a key.</summary>
  private static bool IsConcatenated(string region, Match literal) {
    string before = region[..literal.Index].TrimEnd();
    string after = region[(literal.Index + literal.Length)..].TrimStart();
    return before.EndsWith('+') || after.StartsWith('+');
  }

  /// <summary>
  /// The keys in <paramref name="region"/>. <paramref name="domain"/> filters domain-qualified literals
  /// to the mod's own - a <c>Lang.Get("game:…")</c> is vanilla's key and not this lang file's problem.
  /// <c>null</c> takes every domain, which is what an <c>ActionLangCode</c> wants: a block may legitimately
  /// advertise a key from the library it is built on.
  /// </summary>
  private static IEnumerable<string> KeysIn(
    string region,
    Regex literals,
    string? domain
  ) {
    foreach (Match m in literals.Matches(region)) {
      if (IsConcatenated(region, m))
        continue;
      string value = m.Groups[1].Value;
      if (
        literals == Literal
        && domain != null
        && !value.StartsWith(domain + ':', StringComparison.OrdinalIgnoreCase)
      )
        continue;
      yield return value;
    }
  }

  /// <summary>
  /// Every <c>(file, key)</c> a mod's source names, as the lang cache would hold it: a bare key gains the
  /// mod's own domain, an error code becomes <c>game:ingameerror-{code}</c>, and an already-qualified key
  /// is used verbatim - the same normalisation vanilla's <c>TranslationService</c> applies on load.
  /// </summary>
  public static IReadOnlyList<(string File, string Key)> Keys(
    string domain,
    string srcDir
  ) {
    string root = srcDir;
    var found = new List<(string, string)>();
    foreach (
      string path in Directory.EnumerateFiles(
        root,
        "*.cs",
        SearchOption.AllDirectories
      )
    ) {
      string rel = Path.GetRelativePath(root, path).Replace('\\', '/');
      if (
        rel.Contains("/bin/", StringComparison.Ordinal)
        || rel.Contains("/obj/", StringComparison.Ordinal)
        || rel.EndsWith(".g.cs", StringComparison.Ordinal)
      )
        continue;

      string source = File.ReadAllText(path);
      foreach (Match call in LangCall.Matches(source)) {
        string region = ArgumentRegion(source, call.Index + call.Length - 1);
        foreach (string key in KeysIn(region, Literal, domain))
          found.Add((rel, key));
      }
      foreach (Match call in ActionLangCode.Matches(source)) {
        string region = AssignedRegion(source, call.Index + call.Length);
        foreach (string key in KeysIn(region, Literal, null))
          found.Add((rel, key));
      }
      foreach (Match call in ErrorCall.Matches(source)) {
        string region = ArgumentRegion(source, call.Index + call.Length - 1);
        // The two-argument overload carries its own text and never reads a lang file. A ternary picking
        // between two codes is still one argument, so commas are counted outside its arms.
        if (TopLevelCommas(region) > 0)
          continue;
        foreach (string code in KeysIn(region, BareLiteral, domain))
          found.Add((rel, "game:ingameerror-" + code));
      }
    }
    return found;
  }

  private static int TopLevelCommas(string region) {
    int depth = 0,
      commas = 0;
    foreach (char c in region) {
      if (c is '(' or '[')
        depth++;
      else if (c is ')' or ']')
        depth--;
      else if (c == ',' && depth == 0)
        commas++;
    }
    return commas;
  }

  #endregion

  #region Coverage

  /// <summary>
  /// Every <c>(locale, file, key)</c> a call site names that the locale does not carry. Empty means every
  /// hand-written key in the mod's source resolves.
  /// </summary>
  public static IReadOnlyList<string> Unresolvable(
    string domain,
    string srcDir,
    string langDir
  ) {
    var keys = Keys(domain, srcDir);

    var failures = new List<string>();
    foreach (
      string langFile in Directory
        .EnumerateFiles(langDir, "*.json")
        .OrderBy(f => f)
    ) {
      string locale = Path.GetFileNameWithoutExtension(langFile);
      var shipped = new HashSet<string>(StringComparer.Ordinal);
      foreach (
        JProperty entry in JObject
          .Parse(File.ReadAllText(langFile))
          .Properties()
      )
        shipped.Add(
          entry.Name.Contains(':', StringComparison.Ordinal)
            ? entry.Name
            : domain + ':' + entry.Name
        );

      foreach ((string file, string key) in keys)
        if (!shipped.Contains(key))
          failures.Add($"{locale}: {key} ({file})");
    }
    return
    [
      .. failures
        .Distinct(StringComparer.Ordinal)
        .OrderBy(f => f, StringComparer.Ordinal),
    ];
  }

  #endregion
}
