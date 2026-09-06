using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The published boundary: every public type of <c>exlib.dll</c> outside <c>ExpandedLib.Industry</c>
/// is either named on the Supported API page or carries <c>[EditorBrowsable(Never)]</c> (public
/// because the engine needs it, not for callers). Keeps the page and the assembly in step in both
/// directions - a type dropped from the page or added to the assembly is caught here, not by a
/// stranger reading a stale table.
/// </summary>
public class PublicSurfaceTests {
  // Words the page's tables use in code spans that are not type names: generic parameter
  // placeholders and the like. Kept short and justified, same idiom as WikiParityTests.KnownAbsent.
  private static readonly string[] NotATypeName = [];

  private static string PagePath =>
    Path.Combine(RepoPaths.Mod("exlib"), "wiki", "Supported-API.md");

  // Only the Type column of a table row: `| \`Name\` | ... |`. The description column quotes
  // vanilla API members and JSON keys in the same backtick style, which are not exlib types and
  // are not the page's claim - only the first cell names something this guard must resolve.
  private static readonly Regex TableRow = new(
    @"^\|\s*`([A-Za-z][A-Za-z0-9_.<>,\s]*)`\s*\|",
    RegexOptions.Multiline
  );

  private static bool IsPubliclyVisible(Type t) {
    if (t.IsPublic)
      return true;
    if (!t.IsNestedPublic)
      return false;
    for (Type? d = t.DeclaringType; d != null; d = d.DeclaringType) {
      if (!(d.IsPublic || d.IsNestedPublic))
        return false;
    }
    return true;
  }

  // A C# 14 extension block compiles to a synthetic container type (`<G>$<hash>`) and its members to
  // a synthetic method-holder nested inside it (`<M>$<hash>`); the legacy shims under
  // ExpandedLib.Legacy declare their extension members this way. Neither carries
  // [CompilerGenerated], but no real identifier contains '<', so the name alone tells them apart from
  // an author-declared type.
  private static bool IsCompilerSynthesized(Type t) =>
    t.Name.Contains('<') || t.IsDefined(typeof(CompilerGeneratedAttribute), false);

  private static IEnumerable<Type> ContractTypes() =>
    typeof(ExpandedLibModSystem).Assembly.GetTypes()
      .Where(IsPubliclyVisible)
      .Where(t => t.Namespace != null && !t.Namespace.StartsWith("ExpandedLib.Industry"))
      .Where(t => !IsCompilerSynthesized(t))
      .Where(t => t.GetCustomAttributesData().All(a => a.AttributeType.Name != "IsByRefLikeAttribute"));

  private static bool IsHidden(Type t) =>
    t.GetCustomAttributesData().Any(a => a.AttributeType == typeof(EditorBrowsableAttribute));

  // Strips generic arity: List`1 -> List. The page writes ExConfigRegister<T> for the one generic
  // type it lists, so that literal form is also accepted.
  private static string SimpleName(Type t) {
    int tick = t.Name.IndexOf('`');
    return tick < 0 ? t.Name : t.Name[..tick];
  }

  // A nested type is named `Outer.Inner` on the page - its own simple name alone is ambiguous once
  // more than one type nests a member with the same name.
  private static string QualifiedName(Type t) =>
    t.IsNested ? $"{SimpleName(t.DeclaringType!)}.{SimpleName(t)}" : SimpleName(t);

  private static int Arity(Type t) => t.GetGenericArguments().Length;

  // A row's first cell, split into the bare name and its declared arity: `Foo` is arity 0,
  // `Foo<T>` is arity 1, `Foo<T, U>` is arity 2 - so a non-generic Foo does not satisfy a row
  // written for the generic one and vice versa.
  private static (string Name, int Arity) ParseRow(string raw) {
    int lt = raw.IndexOf('<');
    if (lt < 0)
      return (raw, 0);
    int gt = raw.LastIndexOf('>');
    int arity = raw[(lt + 1)..gt].Count(c => c == ',') + 1;
    return (raw[..lt], arity);
  }

  private static IEnumerable<string> ListedNames(string page) =>
    TableRow.Matches(page).Select(m => m.Groups[1].Value);

  [Fact]
  public void Every_public_type_outside_Industry_is_listed_or_hidden() {
    string page = File.ReadAllText(PagePath);
    // Bare name plus the arity its row was written at - <T> is arity 1, <TSet, TRegistry> is 2 - so a
    // row for a two-parameter generic does not also satisfy the one-parameter case and vice versa.
    var listed = ListedNames(page).Select(ParseRow).ToHashSet();
    var missing = new List<string>();
    foreach (Type t in ContractTypes()) {
      if (IsHidden(t))
        continue;
      string name = QualifiedName(t);
      if (!listed.Contains((name, Arity(t))))
        missing.Add($"{t.Namespace}.{name}");
    }

    Assert.True(
      missing.Count == 0,
      $"{missing.Count} public type(s) outside ExpandedLib.Industry are neither listed on "
        + "Supported-API.md nor marked [EditorBrowsable(Never)]:\n  "
        + string.Join("\n  ", missing)
    );
  }

  [Fact]
  public void Every_listed_type_exists() {
    string page = File.ReadAllText(PagePath);

    // A name resolves when it is (a) a public or nested-public type of the exlib assembly, (b) a
    // generator type (source-extracted, no runtime assembly), or (c) a public type declared in
    // exlib's source but compiled only under a legacy `#if !GAME_GE_*` guard - absent from this
    // lane's assembly, but real on the lane(s) that compile it.
    var resolvable = new Dictionary<string, HashSet<int>>();
    void Add(string name, int arity) {
      if (!resolvable.TryGetValue(name, out var arities))
        resolvable[name] = arities = new HashSet<int>();
      arities.Add(arity);
    }

    foreach (
      Type t in typeof(ExpandedLibModSystem)
        .Assembly.GetTypes()
        .Where(IsPubliclyVisible)
        .Where(t => !IsCompilerSynthesized(t))
    ) {
      int arity = Arity(t);
      Add(SimpleName(t), arity);
      Add(QualifiedName(t), arity);
    }
    foreach (string name in GeneratorTypeNames())
      Add(name, 0);
    foreach (string name in LegacyOnlyTypeNames())
      Add(name, 0);

    var missing = new List<string>();
    foreach (Match m in TableRow.Matches(page)) {
      string raw = m.Groups[1].Value;
      (string name, int arity) = ParseRow(raw);
      if (NotATypeName.Contains(name))
        continue;
      if (resolvable.TryGetValue(name, out var arities) && arities.Contains(arity))
        continue;
      // Fall back to the last dotted segment, for a row that names a type through its namespace
      // rather than its declaring type.
      string lastSegment = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
      if (resolvable.TryGetValue(lastSegment, out var lastArities) && lastArities.Contains(arity))
        continue;
      missing.Add(raw);
    }

    Assert.True(
      missing.Count == 0,
      $"{missing.Count} name(s) on Supported-API.md do not resolve to a public exlib type:\n  "
        + string.Join("\n  ", missing.Distinct())
    );
  }

  /// <summary>
  /// Soft report for Task T10 (the untested-surface work): every public contract type whose simple
  /// name appears in no test file outside this guard directory is presumed untested. A crude text
  /// search rather than a call-graph - a type merely named in a comment or another type's doc counts
  /// as "touched" - so it undercounts real gaps as often as it overcounts, but it is cheap and moves
  /// in the right direction as behaviour tests are added. Not a hard gate: the ceiling here only
  /// tracks the shrinking backlog, it is not the parity check <see cref="PublicSurfaceTests"/>'s
  /// other two facts already run.
  /// </summary>
  [Fact]
  public void Untested_public_types_are_reported() {
    string testsDir = Path.Combine(RepoPaths.Mod("exlib"), "tests");
    string invariantsDir = Path.Combine(testsDir, "Invariants") + Path.DirectorySeparatorChar;

    List<string> testText = [
      .. Directory
        .EnumerateFiles(testsDir, "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
        .Where(f => !f.StartsWith(invariantsDir, StringComparison.Ordinal))
        .Select(File.ReadAllText),
    ];

    List<string> untested = [
      .. ContractTypes()
        .Select(SimpleName)
        .Distinct()
        .Where(name => !testText.Any(t => t.Contains(name, StringComparison.Ordinal)))
        .OrderBy(n => n, StringComparer.Ordinal),
    ];

    Assert.True(
      untested.Count < 60,
      $"{untested.Count} untested public type(s):\n  " + string.Join("\n  ", untested)
    );
  }

  // Same extraction WikiParityTests uses: the generators ship no runtime assembly, so their names
  // come from the source rather than reflection.
  private static IEnumerable<string> GeneratorTypeNames() {
    string dir = Path.Combine(RepoPaths.Mod("exlib"), "generators");
    var declared = new Regex(
      @"\bclass\s+(?<name>\w+)\s*:\s*[^{\r\n]*\bIIncrementalGenerator\b"
    );
    foreach (string file in Directory.EnumerateFiles(dir, "*.cs"))
      foreach (Match m in declared.Matches(File.ReadAllText(file)))
        yield return m.Groups["name"].Value;
  }

  // A file compiled only for a game version below the current one (`#if !GAME_GE_<ver>` - the
  // threshold moves with the manifest in mods/Directory.Build.props). Its public types are real on
  // the lane(s) that compile them and absent from this one, so reflection alone cannot confirm a
  // page row naming them; the declaration is read straight from source instead.
  private static readonly Regex LegacyGuard = new(@"#if\s+!GAME_GE_\d+_\d+");
  private static readonly Regex LegacyTypeDecl = new(
    @"^\s*public\s+(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+)*"
      + @"(?:class|struct|record|interface|enum)\s+(\w+)",
    RegexOptions.Multiline
  );

  private static IEnumerable<string> LegacyOnlyTypeNames() {
    string srcDir = Path.Combine(RepoPaths.Mod("exlib"), "src");
    foreach (string file in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)) {
      string text = File.ReadAllText(file);
      if (!LegacyGuard.IsMatch(text))
        continue;
      foreach (Match m in LegacyTypeDecl.Matches(text))
        yield return m.Groups[1].Value;
    }
  }
}
