using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ExpandedLib.Industry;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Boundary guard: the family-specific half of exlib is its own assembly, <c>exlib.industry.dll</c>,
/// under <c>ExpandedLib.Industry.*</c> and outside the supported <c>ExpandedLib.*</c> contract. The
/// dependency runs one way only - the domain layer builds on the framework, never the reverse - and
/// a type named here that lands back in the framework assembly has crossed that line.
/// </summary>
public class IndustryBoundaryTests {
  #region Corpus

  private static readonly string[] IndustryTypeNames =
  [
    "PipeNetwork",
    "MoltenNetwork",
    "MpEnergyNetwork",
    "MetalRegistry",
    "HeatBalance",
    "ExSounds",
    "ExParticles",
    "ExMoldGate",
    "ExMoldDrops",
    "BEBehaviorMoltenCell",
    "BEBehaviorMPFillerPort",
    "MPAnim",
    "Roles",
  ];

  #endregion

  [Fact]
  public void Every_industry_type_lives_in_the_Industry_assembly() {
    var asm = typeof(IndustryModule).Assembly;
    foreach (string name in IndustryTypeNames) {
      var type = asm.GetTypes().SingleOrDefault(t => t.Name == name);
      Assert.True(
        type != null,
        $"exlib.industry.dll declares no type named '{name}' - was it renamed, or did it move back "
          + "into the framework assembly?"
      );
      Assert.StartsWith("ExpandedLib.Industry", type!.Namespace);
    }
  }

  // The full set of namespaces the tree's top-level folders are allowed to declare (Task G1,
  // docs/design/conventions.md "How exlib is laid out"). A sub-folder never adds a segment, so a
  // type declaring anything outside this set has grown a namespace no folder maps to.
  private static readonly string[] ContractNamespaces =
  [
    "ExpandedLib",
    "ExpandedLib.Registries",
    "ExpandedLib.Config",
    "ExpandedLib.Definitions",
    "ExpandedLib.Blocks",
    "ExpandedLib.Migrations",
    "ExpandedLib.Structures",
    "ExpandedLib.Machines",
    "ExpandedLib.Networks",
    "ExpandedLib.Catalogues",
    "ExpandedLib.Checks",
    "ExpandedLib.Helpers",
    "ExpandedLib.Legacy",
  ];

  [Fact]
  public void Every_contract_namespace_is_a_top_level_folder() {
    var asm = typeof(ExpandedLibModSystem).Assembly;
    var stray = asm.GetTypes()
      .Where(t => t.Namespace != null && t.Namespace.StartsWith("ExpandedLib"))
      .Select(t => t.Namespace!)
      .Distinct()
      .Where(ns => !ContractNamespaces.Contains(ns))
      .OrderBy(ns => ns)
      .ToList();

    Assert.True(
      stray.Count == 0,
      "namespace(s) outside the folder-mapped contract set: " + string.Join(", ", stray)
    );
  }

  [Fact]
  public void The_Industry_assembly_declares_only_Industry_namespaces() {
    var asm = typeof(IndustryModule).Assembly;
    var stray = asm.GetTypes()
      .Where(t => t.Namespace != null && t.Namespace.StartsWith("ExpandedLib"))
      .Where(t => !IsIndustryNamespace(t.Namespace))
      .Select(t => t.Namespace!)
      .Distinct()
      .OrderBy(ns => ns, StringComparer.Ordinal)
      .ToList();

    Assert.True(
      stray.Count == 0,
      "exlib.industry.dll declares namespace(s) outside ExpandedLib.Industry: "
        + string.Join(", ", stray)
    );
  }

  #region Reference boundary

  private const string IndustryNamespacePrefix = "ExpandedLib.Industry";

  private static bool IsIndustryNamespace(string? ns) =>
    ns != null
    && (ns == "ExpandedLib.Industry" || ns.StartsWith(IndustryNamespacePrefix + "."));

  [Fact]
  public void The_framework_assembly_does_not_reference_the_Industry_one() {
    string industry = typeof(IndustryModule).Assembly.GetName().Name!;
    var referenced = typeof(ExpandedLibModSystem).Assembly.GetReferencedAssemblies()
      .Select(a => a.Name)
      .ToList();

    Assert.False(
      referenced.Contains(industry, StringComparer.OrdinalIgnoreCase),
      $"{typeof(ExpandedLibModSystem).Assembly.GetName().Name} references {industry}. The framework "
        + "must not depend on the family's domain layer; the dependency runs the other way."
    );
  }

  // The assembly check above only fails once the framework has actually been compiled against the
  // domain layer, which cannot happen without a project reference that would make the two
  // circular - so the compiler stops it first, with an error naming neither side clearly. A text
  // scan catches the same mistake at its source, pointing at the file and line that added the
  // using directive or the qualified name.
  private static readonly Regex IndustryMention = new(
    @"\bExpandedLib\.Industry\b",
    RegexOptions.Compiled
  );

  [Fact]
  public void No_framework_source_mentions_the_Industry_namespace() {
    string srcRoot = Path.Combine(RepoPaths.Mod("exlib"), "src");
    var hits = new List<string>();

    foreach (
      string path in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
    ) {
      string[] lines = File.ReadAllLines(path);
      for (int i = 0; i < lines.Length; i++)
        if (IndustryMention.IsMatch(lines[i]))
          hits.Add($"{Path.GetRelativePath(srcRoot, path)}:{i + 1}");
    }

    Assert.True(
      hits.Count == 0,
      "framework source under mods/exlib/src names ExpandedLib.Industry:\n"
        + string.Join("\n", hits)
    );
  }

  #endregion
}
