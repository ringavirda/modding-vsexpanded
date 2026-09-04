using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The wiki is the surface a third party is asked to build against, and it was the only authored
/// artifact here with no guard: goldens, lang keys, code literals and released block codes are all
/// tested, so they stay true while the documentation drifts. This is the same idiom extended to it.
/// </summary>
public class WikiParityTests {
  /// <summary>
  /// Identifiers the wiki writes as code that are deliberately not exlib types - a sample class a
  /// reader is meant to write themselves. Kept short and justified one by one: an entry here is a
  /// symbol this guard will never check again.
  /// </summary>
  private static readonly string[] KnownAbsent = [];

  private static string WikiDirectory =>
    DefinitionGoldens.SolutionRelative("docs/wiki");

  /// <summary>
  /// The source generators, which the wiki documents and reflection cannot see: the project sets
  /// <c>IncludeBuildOutput=false</c> and is consumed as an analyzer, so it ships no runtime assembly.
  /// Read out of the source rather than listed here, so deleting a generator deletes it from this set
  /// too - which is exactly the drift that produced a page documenting one that no longer exists.
  /// </summary>
  private static IEnumerable<string> GeneratorTypeNames() {
    string dir = DefinitionGoldens.SolutionRelative(
      "src/ExpandedLib.Generators"
    );
    var declared = new Regex(
      @"\bclass\s+(?<name>\w+)\s*:\s*[^{\r\n]*\bIIncrementalGenerator\b"
    );
    foreach (string file in Directory.EnumerateFiles(dir, "*.cs"))
      foreach (Match m in declared.Matches(File.ReadAllText(file)))
        yield return m.Groups["name"].Value;
  }

  private static WikiParity.Report Run() =>
    WikiParity.Check(
      WikiDirectory,
      typeof(ExDefinitions).Assembly,
      KnownAbsent,
      GeneratorTypeNames()
    );

  [Fact]
  public void Every_symbol_the_wiki_writes_as_code_resolves_against_the_assembly() {
    WikiParity.Report report = Run();

    Assert.True(
      report.Findings.Count == 0,
      $"{report.Findings.Count} wiki symbol(s) name something exlib does not have, out of "
        + $"{report.SymbolsChecked} checked. A reader who copies one of these gets a compile error, or "
        + "a call that no longer exists:\n  "
        + string.Join("\n  ", report.Findings.Select(f => f.ToString()))
    );
  }

  [Fact]
  public void The_guard_reads_the_wiki_and_reaches_real_api() {
    // Zero findings reads identically whether the wiki is correct or the extractor stopped seeing code
    // spans, so both halves of the corpus are asserted rather than assumed.
    WikiParity.Report report = Run();

    Assert.True(
      report.FilesRead > 0,
      $"no wiki markdown under {WikiDirectory} - the guard is inert"
    );
    Assert.True(
      report.SymbolsChecked > 0,
      "no wiki symbol resolved to an exlib type - the extractor read nothing the assembly owns"
    );
    Assert.True(
      GeneratorTypeNames().Any(),
      "no IIncrementalGenerator found in src/ExpandedLib.Generators - the generator names the wiki "
        + "may cite would be resolved from an empty set, so any of them would read as valid"
    );
  }
}
