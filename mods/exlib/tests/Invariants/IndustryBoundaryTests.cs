using System.Linq;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Boundary guard: the family-specific half of exlib lives under <c>ExpandedLib.Industry.*</c>,
/// separate from the supported <c>ExpandedLib.*</c> contract. A type named here that lands back in
/// core (or anywhere else) has crossed the boundary the framework plan draws between the two.
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
    "ExBlockNames",
    "ExMoldGate",
    "ExMoldDrops",
    "BEBehaviorMoltenCell",
    "BEBehaviorMPFillerPort",
    "MPAnim",
    "Roles",
  ];

  #endregion

  [Fact]
  public void Every_industry_type_lives_under_the_Industry_namespace() {
    var asm = typeof(ExpandedLibModSystem).Assembly;
    foreach (string name in IndustryTypeNames) {
      var type = asm.GetTypes().SingleOrDefault(t => t.Name == name);
      Assert.True(type != null, $"exlib.dll declares no type named '{name}' - was it renamed?");
      Assert.StartsWith("ExpandedLib.Industry.", type!.Namespace);
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
      .Where(t => !t.Namespace!.StartsWith("ExpandedLib.Industry"))
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
}
