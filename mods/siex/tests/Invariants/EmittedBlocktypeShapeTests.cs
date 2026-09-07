using System.Collections.Generic;
using ExpandedLib.Testing;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The selector-coverage rule (<see cref="SelectorCoverage"/>) over siex's own golden blocktypes.
/// </summary>
public class EmittedBlocktypeShapeTests {
  [Fact]
  public void Every_block_variant_resolves_a_shape() {
    var findings = new List<string>();
    foreach (string path in SelectorCoverage.GoldenBlocktypes("siex"))
      findings.AddRange(SelectorCoverage.Check(path).shapeByType);

    Assert.True(findings.Count == 0, string.Join("\n", findings));
  }

  [Fact]
  public void Every_handbook_group_selector_matches_a_shipped_code() {
    var findings = new List<string>();
    foreach (string path in SelectorCoverage.GoldenBlocktypes("siex"))
      findings.AddRange(SelectorCoverage.Check(path).groupBy);

    Assert.True(findings.Count == 0, string.Join("\n", findings));
  }

  [Fact]
  public void The_golden_corpus_is_not_empty() {
    // The rules above pass trivially if the path filter stops matching - a renamed goldens/ or
    // blocktypes/ folder would otherwise read as "every selector resolves".
    Assert.NotEmpty(SelectorCoverage.GoldenBlocktypes("siex"));
  }
}
