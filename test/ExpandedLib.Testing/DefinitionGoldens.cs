using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// The shared golden-file oracle for code-first definition parity. Each migrated def (block, item or recipe
/// file) has a committed <c>goldens/{domain}/{Location.Path}</c> file - the byte-faithful record of the JSON it
/// injects - and a mod's parity test is then a thin, data-driven pair: "every def reproduces its golden"
/// (<see cref="CheckGolden"/>) + "every def has a golden and every golden has a def"
/// (<see cref="CheckCompleteness"/>). This replaces the per-family test classes that each carried a large inline
/// golden string plus a copy of the same gather/lookup/assert scaffold.
/// <para>
/// Defs are collected straight off a mod assembly (<see cref="Collect"/>) WITHOUT touching the process-wide
/// <see cref="ExDefinitions"/> registry, so parity tests stay isolated and parallel. Goldens are read from the
/// source tree (like the lang-parity guard reads shipped assets), so no build-output copy step is needed;
/// <see cref="WriteAll"/> re-blesses them from the current def output after an intended change. Comparison is
/// semantic via <see cref="DefinitionParity"/> (number-type / key-order / multiblock-cell agnostic).
/// </para>
/// </summary>
public static class DefinitionGoldens
{
  /// <summary>Every code-first def (blocks + items + recipe files) <paramref name="asm"/> declares for
  /// <paramref name="domain"/> - collected by scanning the assembly and invoking each provider's static
  /// <c>Definitions(domain)</c> factory, without registering anything into the process-wide registry.</summary>
  public static IReadOnlyList<IExDef> Collect(string domain, Assembly asm)
  {
    var defs = new List<IExDef>();
    foreach (Type type in ReflectionScan.GetCandidateTypes(asm))
    {
      defs.AddRange(ExDefinitions.DefinitionsOf(type, domain));
      defs.AddRange(ExDefinitions.ItemDefinitionsOf(type, domain));
      defs.AddRange(ExDefinitions.RecipeDefinitionsOf(type, domain));
    }
    return defs;
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
  )
  {
    IExDef def = Collect(domain, asm).Single(d => RelativePath(d) == relativePath);
    string file = FullPath(goldenRoot, def);
    if (!File.Exists(file))
      return (false, $"missing golden file: {file}");

    JToken expected = JToken.Parse(File.ReadAllText(file));
    return DefinitionParity.Equal(expected, def.ToJson(), out string normalized)
      ? (true, "")
      : (false, $"{relativePath} code-first def diverged from its golden:\n{normalized}");
  }

  /// <summary>Completeness of the golden set for a mod: <paramref name="missing"/> = defs with no golden file
  /// (a new, unmigrated def), <paramref name="orphans"/> = golden files under <c>{goldenRoot}/{domain}/</c> that
  /// no def claims (a deleted def). Both empty == the goldens exactly cover the defs.</summary>
  public static (IReadOnlyList<string> missing, IReadOnlyList<string> orphans) CheckCompleteness(
    string domain,
    Assembly asm,
    string goldenRoot
  )
  {
    var defs = Collect(domain, asm);
    var claimed = defs
      .Select(d => Path.GetFullPath(FullPath(goldenRoot, d)))
      .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var missing = defs
      .Where(d => !File.Exists(FullPath(goldenRoot, d)))
      .Select(RelativePath)
      .OrderBy(p => p)
      .ToList();

    string domainRoot = Path.Combine(goldenRoot, domain);
    var orphans = (
      Directory.Exists(domainRoot)
        ? Directory.EnumerateFiles(domainRoot, "*.json", SearchOption.AllDirectories)
        : []
    )
      .Where(f => !claimed.Contains(Path.GetFullPath(f)))
      .Select(f => Path.GetRelativePath(goldenRoot, f).Replace('\\', '/'))
      .OrderBy(p => p)
      .ToList();

    return (missing, orphans);
  }

  /// <summary>Re-blesses every golden under <paramref name="goldenRoot"/> from the current def output - run
  /// once to create the tree, or after an intended def change to accept the new JSON. Opt-in (call only when
  /// <see cref="WriteRequested"/>), never part of a normal test run.</summary>
  public static void WriteAll(string domain, Assembly asm, string goldenRoot)
  {
    foreach (IExDef def in Collect(domain, asm))
    {
      string file = FullPath(goldenRoot, def);
      Directory.CreateDirectory(Path.GetDirectoryName(file)!);
      File.WriteAllText(file, def.ToJson().ToString());
    }
  }

  /// <summary>True when <c>EXLIB_WRITE_GOLDENS=1</c> - the opt-in switch a regeneration test guards on.</summary>
  public static bool WriteRequested =>
    Environment.GetEnvironmentVariable("EXLIB_WRITE_GOLDENS") == "1";

  /// <summary>Resolves a repo-root-relative path (e.g. <c>test/PipesAndPowerExpanded.Tests/goldens</c>) to an
  /// absolute path by walking up from the test binary to the solution root - the same source-tree anchor the
  /// lang-parity guard uses, so goldens are read/written in place with no build-output copy.</summary>
  public static string SolutionRelative(string repoRelativePath) =>
    Path.Combine(RepoRoot(), repoRelativePath.Replace('/', Path.DirectorySeparatorChar));

  private static string FullPath(string goldenRoot, IExDef def) =>
    Path.Combine(
      goldenRoot,
      def.Location.Domain,
      def.Location.Path.Replace('/', Path.DirectorySeparatorChar)
    );

  private static string RepoRoot()
  {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "VintageStory.sln")))
      dir = dir.Parent;
    return dir?.FullName
      ?? throw new InvalidOperationException(
        "Could not locate the repo root (VintageStory.sln) from " + AppContext.BaseDirectory
      );
  }
}
