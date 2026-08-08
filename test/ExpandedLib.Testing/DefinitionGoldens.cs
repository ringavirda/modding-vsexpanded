using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using ExpandedLib.Metals;
using ExpandedLib.Registries;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Golden-file oracle for code-first definition parity. Each migrated def has a committed
/// <c>goldens/{domain}/{Location.Path}</c> file recording the JSON it injects, so a mod's parity test is two
/// data-driven checks: every def reproduces its golden (<see cref="CheckGolden"/>) and the golden set exactly
/// covers the defs (<see cref="CheckCompleteness"/>).
/// <para>
/// Defs are collected off a mod assembly (<see cref="Collect"/>) without touching the process-wide
/// <see cref="ExDefinitions"/> registry, so parity tests stay isolated. Goldens are read from and written to
/// the source tree, with no build-output copy step; comparison is semantic via
/// <see cref="DefinitionParity"/>, ignoring number type, key order and multiblock cell form.
/// </para>
/// </summary>
public static class DefinitionGoldens {
  /// <summary>Every code-first def (blocks + items + recipe files) <paramref name="asm"/> declares for
  /// <paramref name="domain"/> - collected by scanning the assembly and invoking each provider's static
  /// <c>Definitions(domain)</c> factory, without registering anything into the process-wide registry.</summary>
  public static IReadOnlyList<IExDef> Collect(string domain, Assembly asm) {
    var defs = new List<IExDef>();
    foreach (Type type in ReflectionScan.GetCandidateTypes(asm)) {
      defs.AddRange(ExDefinitions.DefinitionsOf(type, domain));
      defs.AddRange(ExDefinitions.ItemDefinitionsOf(type, domain));
      defs.AddRange(ExDefinitions.RecipeDefinitionsOf(type, domain));
    }
    // Generated metal families have no provider class; they are emitted from the config/metals JSON at
    // runtime. Feeding the emitter the same JSON off the source tree golden-checks them like any other def.
    defs.AddRange(EmittedFamilies(domain));
    return defs;
  }

  // The metal-family item defs the emitter produces for the given domain, read from the source
  // config/metals catalogue. Every domain's catalogue is scanned because a metal is emitted into the domain
  // its molten item names, which need not match the folder it ships in.
  private static IEnumerable<ExItemDef> EmittedFamilies(string domain) {
    string assetsRoot = SolutionRelative("assets");
    if (!Directory.Exists(assetsRoot))
      return [];

    var metals = new List<MetalDef>();
    foreach (
      string file in Directory.EnumerateFiles(
        assetsRoot,
        "*.json",
        SearchOption.AllDirectories
      )
    ) {
      if (!file.Replace('\\', '/').Contains("/config/metals/"))
        continue;
      MetalDef? metal = JsonConvert.DeserializeObject<MetalDef>(
        File.ReadAllText(file)
      );
      if (metal != null)
        metals.Add(metal);
    }
    return MetalFamilyEmitter.Emit(metals).Where(d => d.Domain == domain);
  }

  /// <summary>The def's golden path relative to the golden root: <c>{domain}/{Location.Path}</c> (a stable,
  /// serializable key that doubles as the xUnit theory case id).</summary>
  public static string RelativePath(IExDef def) =>
    def.Location.Domain + "/" + def.Location.Path;

  /// <summary>One xUnit theory case per def, keyed by its <see cref="RelativePath"/>.</summary>
  public static IEnumerable<object[]> Cases(string domain, Assembly asm) =>
    Collect(domain, asm)
      .Select(d => RelativePath(d))
      .OrderBy(p => p)
      .Select(p => new object[] { p });

  /// <summary>Checks the def whose <see cref="RelativePath"/> is <paramref name="relativePath"/> against its
  /// committed golden under <paramref name="goldenRoot"/>. Returns <c>(true, "")</c> on match, else a readable
  /// diff message (the normalized actual token, or a missing-file note).</summary>
  public static (bool ok, string message) CheckGolden(
    string domain,
    Assembly asm,
    string relativePath,
    string goldenRoot
  ) {
    IExDef def = Collect(domain, asm)
      .Single(d => RelativePath(d) == relativePath);
    string file = FullPath(goldenRoot, def);
    if (!File.Exists(file))
      return (false, $"missing golden file: {file}");

    JToken expected = JToken.Parse(File.ReadAllText(file));
    return DefinitionParity.Equal(expected, def.ToJson(), out string normalized)
      ? (true, "")
      : (
        false,
        $"{relativePath} code-first def diverged from its golden:\n{normalized}"
      );
  }

  /// <summary>Completeness of the golden set for a mod: <paramref name="missing"/> = defs with no golden file
  /// (a new, unmigrated def), <paramref name="orphans"/> = golden files under <c>{goldenRoot}/{domain}/</c> that
  /// no def claims (a deleted def). Both empty == the goldens exactly cover the defs.</summary>
  public static (
    IReadOnlyList<string> missing,
    IReadOnlyList<string> orphans
  ) CheckCompleteness(string domain, Assembly asm, string goldenRoot) {
    var defs = Collect(domain, asm);
    var claimed = defs.Select(d => Path.GetFullPath(FullPath(goldenRoot, d)))
      .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var missing = defs.Where(d => !File.Exists(FullPath(goldenRoot, d)))
      .Select(RelativePath)
      .OrderBy(p => p)
      .ToList();

    string domainRoot = Path.Combine(goldenRoot, domain);
    var orphans = (
      Directory.Exists(domainRoot)
        ? Directory.EnumerateFiles(
          domainRoot,
          "*.json",
          SearchOption.AllDirectories
        )
        : []
    )
      .Where(f => !claimed.Contains(Path.GetFullPath(f)))
      .Select(f => Path.GetRelativePath(goldenRoot, f).Replace('\\', '/'))
      .OrderBy(p => p)
      .ToList();

    return (missing, orphans);
  }

  /// <summary>
  /// Re-blesses the goldens under <paramref name="goldenRoot"/> from the current def output. Opt-in: call
  /// only when <see cref="WriteRequested"/>, never as part of a normal test run.
  /// <para>
  /// <c>EXLIB_WRITE_GOLDENS=1</c> rewrites every golden in the domain, which accepts unread any drift in the
  /// files that were not being changed. Setting it to a comma-separated list of path fragments instead
  /// restricts the rewrite to goldens whose <c>domain/path</c> contains one of them
  /// (<c>EXLIB_WRITE_GOLDENS=iwex/blocktypes/furnace/blastcore</c> blesses one file).
  /// </para>
  /// </summary>
  public static void WriteAll(string domain, Assembly asm, string goldenRoot) {
    IReadOnlyList<string> only = WriteFilter;

    foreach (IExDef def in Collect(domain, asm)) {
      // Matched on the same domain-qualified relative path the parity test reports, so a failure message
      // can be pasted straight into the variable.
      string relative = def.Location.Domain + "/" + def.Location.Path;
      if (
        only.Count > 0
        && !only.Any(f => relative.Contains(f, StringComparison.Ordinal))
      )
        continue;

      string file = FullPath(goldenRoot, def);
      Directory.CreateDirectory(Path.GetDirectoryName(file)!);
      File.WriteAllText(file, def.ToJson().ToString());
    }
  }

  /// <summary>True when <c>EXLIB_WRITE_GOLDENS</c> is set to anything non-empty - the opt-in switch a
  /// regeneration test guards on. <c>1</c> means every golden; anything else is a path filter, see
  /// <see cref="WriteAll"/>.</summary>
  public static bool WriteRequested =>
    !string.IsNullOrWhiteSpace(
      Environment.GetEnvironmentVariable("EXLIB_WRITE_GOLDENS")
    );

  /// <summary>The path fragments <c>EXLIB_WRITE_GOLDENS</c> names, or empty for "every golden" (<c>1</c>).</summary>
  private static IReadOnlyList<string> WriteFilter {
    get {
      string value =
        Environment.GetEnvironmentVariable("EXLIB_WRITE_GOLDENS") ?? "";
      if (value.Trim() is "" or "1")
        return [];
      return
      [
        .. value
          .Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries
              | StringSplitOptions.TrimEntries
          )
          .Select(p => p.Replace('\\', '/').Replace(".json", "")),
      ];
    }
  }

  /// <summary>Resolves a repo-root-relative path (e.g. <c>test/LowPressureExpanded.Tests/goldens</c>) to an
  /// absolute path by walking up from the test binary to the solution root, so source-tree files are read
  /// and written in place.</summary>
  public static string SolutionRelative(string repoRelativePath) =>
    Path.Combine(
      RepoRoot(),
      repoRelativePath.Replace('/', Path.DirectorySeparatorChar)
    );

  private static string FullPath(string goldenRoot, IExDef def) =>
    Path.Combine(
      goldenRoot,
      def.Location.Domain,
      def.Location.Path.Replace('/', Path.DirectorySeparatorChar)
    );

  private static string RepoRoot() {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (
      dir != null
      && !File.Exists(Path.Combine(dir.FullName, "VintageStory.sln"))
    )
      dir = dir.Parent;
    return dir?.FullName
      ?? throw new InvalidOperationException(
        "Could not locate the repo root (VintageStory.sln) from "
          + AppContext.BaseDirectory
      );
  }
}
