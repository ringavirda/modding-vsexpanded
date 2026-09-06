using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ExpandedLib.Testing;

/// <summary>
/// Reflects the API the wiki teaches against the API the assembly actually has. The wiki is the one
/// authored artifact a third party is asked to build against, and the only one this repository does not
/// test - goldens, lang keys, code literals and released block codes all have guards, so they stay true
/// and the wiki drifts.
/// <para>
/// Deliberately narrow, because a doc is not a compilation unit: it checks the two claims that are
/// unambiguous and that a reader acts on. A member named on a type this assembly owns must exist on
/// it, and an <c>Ex</c>-prefixed identifier written as code must name a type that exists.
/// Everything else - a consumer's own class, a vanilla or BCL call, a local - is out of scope by
/// construction, since neither rule can see it.
/// </para>
/// </summary>
public static class WikiParity {
  /// <summary>One drifted symbol: where it is written, what it names, and why it does not resolve.</summary>
  public sealed record Finding(
    string File,
    int Line,
    string Symbol,
    string Reason
  ) {
    /// <summary>The one-line form a failure message lists.</summary>
    public override string ToString() => $"{File}:{Line}  {Symbol} - {Reason}";
  }

  // A code span: a ```csharp fence, or an inline `backtick` run. Prose outside both is not checked -
  // an English sentence naming a type is not a claim about a signature.
  private static readonly Regex FencedCsharp = new(
    @"^```\s*(csharp|cs|c#)\s*$",
    RegexOptions.IgnoreCase | RegexOptions.Compiled
  );
  private static readonly Regex FenceEnd = new(
    @"^```\s*$",
    RegexOptions.Compiled
  );
  private static readonly Regex InlineCode = new(
    @"`([^`\n]+)`",
    RegexOptions.Compiled
  );

  private static readonly Regex QualifiedMember = new(
    @"\b([A-Z][A-Za-z0-9_]*)\s*(?:<[^<>()]*>)?\s*\.\s*([A-Za-z_][A-Za-z0-9_]*)",
    RegexOptions.Compiled
  );
  private static readonly Regex ExIdentifier = new(
    @"\bEx[A-Z][A-Za-z0-9_]*\b",
    RegexOptions.Compiled
  );

  // A return type, including a generic one whose arguments carry spaces (`Dictionary<string, string[]>`).
  // Without the inner-space allowance the declaration rules below silently skip every generic member,
  // which is most of the interesting surface.
  private const string TypeRef = @"[\w\.\?\[\]]+(?:<[^;=\{\r\n]*>)?";

  // `class MyPipe : BlockPipe` - the base a subsequent `override` in the same fence resolves against.
  private static readonly Regex ClassWithBase = new(
    @"\bclass\s+\w+\s*:\s*(?<base>[A-Z][A-Za-z0-9_]*)",
    RegexOptions.Compiled
  );

  // `public override string NetworkType` / `protected override void OnLoaded(`. The accessibility is
  // part of the claim: overriding a protected member as public is CS0507, and a page that teaches it
  // does not compile.
  private static readonly Regex OverrideDecl = new(
    @"\b(?<access>public|protected|internal|protected\s+internal)\s+(?:sealed\s+)?override\s+"
      + TypeRef
      + @"\s+(?<name>\w+)",
    RegexOptions.Compiled
  );

  // `public abstract Dictionary<...> AllowedOrientations` - a page declaring a member abstract that the
  // base actually implements teaches a consumer to write a member they do not have to.
  private static readonly Regex AbstractDecl = new(
    @"\babstract\s+" + TypeRef + @"\s+(?<name>\w+)",
    RegexOptions.Compiled
  );

  // `BlockPipe.cs:42` is a source citation, not a member access. Same for the other file kinds the
  // docs cite by name.
  private static readonly HashSet<string> FileExtensions =
  [
    "cs",
    "csproj",
    "dll",
    "json",
    "md",
    "png",
    "ps1",
    "sh",
    "sln",
    "xml",
    "zip",
  ];

  /// <summary>What a run examined and what it found. The counts exist so a caller can assert the guard
  /// is not inert: zero findings reads identically whether the wiki is correct or the extractor stopped
  /// seeing code.</summary>
  public sealed record Report(
    IReadOnlyList<Finding> Findings,
    int FilesRead,
    int SymbolsChecked
  );

  /// <summary>
  /// Every symbol in <paramref name="wikiDirectory"/>'s markdown that names something
  /// <paramref name="assembly"/> does not have. <paramref name="knownAbsent"/> exempts an identifier the
  /// docs invent on purpose - a sample type a reader is meant to write themselves.
  /// </summary>
  /// <param name="alsoDefined">Type names that exist outside <paramref name="assembly"/> and cannot be
  /// reflected - the source generators, which ship no runtime assembly. Supplied as a set discovered
  /// from source rather than a hand-written allowance, so a generator that is deleted stops being known
  /// here too. That is the whole point: a deleted generator is what this guard was written for.</param>
  public static Report Check(
    string wikiDirectory,
    Assembly assembly,
    IEnumerable<string>? knownAbsent = null,
    IEnumerable<string>? alsoDefined = null
  ) {
    var exempt = new HashSet<string>(knownAbsent ?? [], StringComparer.Ordinal);
    Dictionary<string, Type> types = PublicTypesBySimpleName(assembly);
    var defined = new HashSet<string>(
      types.Keys.Concat(alsoDefined ?? []),
      StringComparer.Ordinal
    );
    var findings = new List<Finding>();
    int files = 0,
      checkedSymbols = 0;

    foreach (
      string path in Directory
        .EnumerateFiles(wikiDirectory, "*.md", SearchOption.TopDirectoryOnly)
        .OrderBy(p => p, StringComparer.Ordinal)
    ) {
      files++;
      string name = Path.GetFileName(path);
      string[] lines = File.ReadAllLines(path);
      foreach (var (line, code) in CodeSpans(lines))
        checkedSymbols += CheckSpan(
          name,
          line,
          code,
          types,
          defined,
          exempt,
          findings
        );
      foreach (var (line, block, heading) in FencedCsharpBlocks(lines))
        checkedSymbols += CheckDeclarations(
          name,
          line,
          block,
          heading,
          types,
          findings
        );
    }
    return new Report(findings, files, checkedSymbols);
  }

  // Returns how many symbols this span actually resolved against the assembly - a candidate naming a
  // type exlib does not own is not a check, it is a skip, and counting it would hide an inert run.
  private static int CheckSpan(
    string file,
    int line,
    string code,
    Dictionary<string, Type> types,
    HashSet<string> defined,
    HashSet<string> exempt,
    List<Finding> findings
  ) {
    int examined = 0;
    foreach (Match m in QualifiedMember.Matches(code)) {
      string typeName = m.Groups[1].Value;
      string member = m.Groups[2].Value;
      if (
        exempt.Contains(typeName)
        || FileExtensions.Contains(member)
        || !types.TryGetValue(typeName, out Type? type)
      )
        continue;

      examined++;
      if (!HasMember(type, member))
        findings.Add(
          new Finding(
            file,
            line,
            $"{typeName}.{member}",
            $"{typeName} has no member '{member}'"
          )
        );
    }

    foreach (Match m in ExIdentifier.Matches(code)) {
      string id = m.Value;
      if (exempt.Contains(id))
        continue;
      examined++;
      // An attribute is written without its suffix at the use site - `[ExConfigRange(1, 100)]` names
      // ExConfigRangeAttribute - so both spellings resolve.
      if (!defined.Contains(id) && !defined.Contains(id + "Attribute"))
        findings.Add(
          new Finding(
            file,
            line,
            id,
            "names no type in exlib or its generators"
          )
        );
    }
    return examined;
  }

  // The declaration-shape checks, which need a whole fenced block rather than one line: an `override`
  // only means something against the base named on the enclosing `class X : Base` line.
  private static int CheckDeclarations(
    string file,
    int startLine,
    string block,
    string? headingType,
    Dictionary<string, Type> types,
    List<Finding> findings
  ) {
    // The base these declarations are measured against: the `class X : Base` inside the fence, or -
    // for a bare signature list, which is where a doc's members drift furthest from the code - the type
    // its own heading names ("Key `BlockNetworkNode` members to know").
    Match baseMatch = ClassWithBase.Match(block);
    string? baseName = baseMatch.Success
      ? baseMatch.Groups["base"].Value
      : headingType;
    if (baseName == null || !types.TryGetValue(baseName, out Type? baseType))
      return 0;

    int examined = 0;

    foreach (Match m in OverrideDecl.Matches(block)) {
      string name = m.Groups["name"].Value;
      MemberInfo? member = FindOverridable(baseType, name);
      if (member == null)
        continue;

      examined++;
      string wants = m.Groups["access"].Value;
      string? actual = AccessibilityOf(member);
      if (actual != null && wants != actual)
        findings.Add(
          new Finding(
            file,
            startLine,
            $"{baseName}.{name}",
            $"declared '{wants} override' but {baseName} declares it '{actual}' (CS0507)"
          )
        );
    }

    foreach (Match m in AbstractDecl.Matches(block)) {
      string name = m.Groups["name"].Value;
      MemberInfo? member = FindOverridable(baseType, name);
      if (member == null)
        continue;

      examined++;
      if (!IsAbstract(member))
        findings.Add(
          new Finding(
            file,
            startLine,
            $"{baseName}.{name}",
            $"declared abstract, but {baseName} implements it - a consumer need not write it"
          )
        );
    }
    return examined;
  }

  private static MemberInfo? FindOverridable(Type baseType, string name) {
    const BindingFlags flags =
      BindingFlags.Public
      | BindingFlags.NonPublic
      | BindingFlags.Instance
      | BindingFlags.FlattenHierarchy;

    for (Type? t = baseType; t != null; t = t.BaseType) {
      MemberInfo[] found = t.GetMember(name, flags);
      if (found.Length > 0)
        return found[0];
    }
    return null;
  }

  private static MethodInfo? Accessor(MemberInfo m) =>
    m switch {
      PropertyInfo p => p.GetMethod ?? p.SetMethod,
      MethodInfo mi => mi,
      _ => null,
    };

  private static string? AccessibilityOf(MemberInfo m) {
    MethodInfo? a = Accessor(m);
    if (a == null)
      return null;
    if (a.IsPublic)
      return "public";
    if (a.IsFamily)
      return "protected";
    if (a.IsAssembly)
      return "internal";
    return a.IsFamilyOrAssembly ? "protected internal" : null;
  }

  private static bool IsAbstract(MemberInfo m) =>
    Accessor(m)?.IsAbstract ?? false;

  // A heading naming one backticked type: "### Key `BlockNetworkNode` members to know".
  private static readonly Regex HeadingType = new(
    @"^#{1,6}\s+.*`(?<type>[A-Z][A-Za-z0-9_]*)(?:<[^`]*>)?`",
    RegexOptions.Compiled
  );

  // The ```csharp fences as whole blocks, with the 1-based line each opens on and the type its nearest
  // preceding heading names, if any.
  private static IEnumerable<(
    int Line,
    string Block,
    string? HeadingType
  )> FencedCsharpBlocks(string[] lines) {
    string? heading = null;
    for (int i = 0; i < lines.Length; i++) {
      Match h = HeadingType.Match(lines[i]);
      if (h.Success)
        heading = h.Groups["type"].Value;
      else if (lines[i].StartsWith('#'))
        heading = null;

      if (!FencedCsharp.IsMatch(lines[i].Trim()))
        continue;
      int close = i + 1;
      while (close < lines.Length && !FenceEnd.IsMatch(lines[close].Trim()))
        close++;
      yield return (i + 1, string.Join("\n", lines[(i + 1)..close]), heading);
      i = close;
    }
  }

  // Simple name -> type, generic arity stripped (`ExConfigRegister\`1` is written `ExConfigRegister<T>`).
  // A name shared by two types keeps the first; the check only asks whether a member exists, and an
  // ambiguity that made a real drift invisible would need two types of one name, which this repo has none of.
  private static Dictionary<string, Type> PublicTypesBySimpleName(
    Assembly assembly
  ) {
    var map = new Dictionary<string, Type>(StringComparer.Ordinal);
    foreach (Type t in assembly.GetExportedTypes()) {
      int tick = t.Name.IndexOf('`');
      string simple = tick < 0 ? t.Name : t.Name[..tick];
      map.TryAdd(simple, t);
    }
    return map;
  }

  // Members are looked up on the type and everything it inherits, public and protected alike: the wiki
  // teaches subclassing, so a protected member is a documented one.
  private static bool HasMember(Type type, string member) {
    const BindingFlags flags =
      BindingFlags.Public
      | BindingFlags.NonPublic
      | BindingFlags.Instance
      | BindingFlags.Static
      | BindingFlags.FlattenHierarchy;

    for (Type? t = type; t != null; t = t.BaseType)
      if (t.GetMember(member, flags).Length > 0)
        return true;

    return type.GetInterfaces().Any(i => i.GetMember(member, flags).Length > 0);
  }

  // Fenced csharp blocks plus inline code spans, each with the 1-based line it starts on.
  private static IEnumerable<(int Line, string Code)> CodeSpans(string[] lines) {
    bool inCsharp = false;
    for (int i = 0; i < lines.Length; i++) {
      string raw = lines[i];
      if (inCsharp) {
        if (FenceEnd.IsMatch(raw.Trim()))
          inCsharp = false;
        else
          yield return (i + 1, raw);
        continue;
      }
      if (FencedCsharp.IsMatch(raw.Trim())) {
        inCsharp = true;
        continue;
      }
      // A fence of another language (json, bash) is skipped wholesale.
      if (raw.TrimStart().StartsWith("```", StringComparison.Ordinal)) {
        int close = i + 1;
        while (close < lines.Length && !FenceEnd.IsMatch(lines[close].Trim()))
          close++;
        i = close;
        continue;
      }
      foreach (Match m in InlineCode.Matches(raw))
        yield return (i + 1, m.Groups[1].Value);
    }
  }
}
