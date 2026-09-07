using System.Collections.Generic;
using ExpandedLib.Testing;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The looping-animation rule (<see cref="LoopingAnimations"/>) over iiex's own shipped shapes and the
/// shared <c>game</c> lang overlay it ships alongside them.
/// </summary>
public class LoopingAnimationTests {
  private static readonly string[] Trees =
  [
    RepoPaths.Assets("iiex"),
    RepoPaths.Assets("game"),
  ];

  [Fact]
  public void Iiexs_shapes_carry_no_looping_defect() {
    var offenders = new List<string>();
    foreach (string tree in Trees)
      offenders.AddRange(LoopingAnimations.Check(tree));

    Assert.True(offenders.Count == 0, string.Join("\n", offenders));
  }

  [Fact]
  public void The_corpus_reaches_the_shipped_shapes() {
    // A path filter that silently matches nothing would make the rule above pass trivially.
    Assert.NotEmpty(LoopingAnimations.ShapeFiles(RepoPaths.Assets("iiex")));
  }
}
