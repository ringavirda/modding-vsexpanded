using ExpandedLib.Testing;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The looping-animation rule (<see cref="LoopingAnimations"/>) over siex's own shipped shapes.
/// </summary>
public class LoopingAnimationTests {
  [Fact]
  public void Siexs_shapes_carry_no_looping_defect() {
    var offenders = LoopingAnimations.Check(RepoPaths.Assets("siex"));
    Assert.True(offenders.Count == 0, string.Join("\n", offenders));
  }

  [Fact]
  public void The_corpus_reaches_the_shipped_shapes() {
    // A path filter that silently matches nothing would make the rule above pass trivially.
    Assert.NotEmpty(LoopingAnimations.ShapeFiles(RepoPaths.Assets("siex")));
  }
}
