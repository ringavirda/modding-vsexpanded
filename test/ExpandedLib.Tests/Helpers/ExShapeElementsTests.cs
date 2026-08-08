using ExpandedLib.Helpers;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The element-path matcher a block entity prunes its shape with. This is deliberately <b>not</b> the
/// engine's <c>selectiveElements</c> rule, whose per-segment prefix matching has silently kept or dropped
/// the wrong subtree in this codebase twice - naming an ancestor keeps far more than intended, naming an
/// element exactly drops its children. The failure is always invisible: a wrong set renders a hole, never
/// an exception, so the rule has to be pinned rather than eyeballed in game.
/// </summary>
public class ExShapeElementsTests
{
  private static readonly string[] Keep = ["Base/*", "Fettle/Cube13", "Items1/ShingledBlooms/*"];

  #region What is kept

  [Theory]
  // The named element itself, and everything beneath it.
  [InlineData("Fettle/Cube13")]
  [InlineData("Items1/ShingledBlooms")]
  [InlineData("Items1/ShingledBlooms/ShingledBloom1")]
  [InlineData("Base")]
  [InlineData("Base/Cube2")]
  // The ancestors on the way to a named element - without these the children are unreachable, which is
  // the half the engine's rule gets wrong when you name a leaf.
  [InlineData("Fettle")]
  [InlineData("Items1")]
  public void An_element_at_on_the_way_to_or_under_a_pattern_is_kept(string path) =>
    Assert.True(ExShapeElements.Matches(path, Keep));

  #endregion

  #region What is dropped

  [Theory]
  // A sibling of a kept element, at every depth.
  [InlineData("Fettle/Cube11")]
  [InlineData("Items1/ShingledSlabs")]
  [InlineData("Items1/ShingledSlabs/ShingledSlab1")]
  [InlineData("Items2")]
  [InlineData("Bed")]
  [InlineData("Pigs/Pig1")]
  public void Anything_else_is_dropped(string path) =>
    Assert.False(ExShapeElements.Matches(path, Keep));

  [Theory]
  // Whole-segment matching. These are the names that actually exist in the shipped shapes, and a bare
  // string prefix would keep every one of them: "Items1" would match "Items10", "Fettle/Cube13" would
  // match "Fettle/Cube130", and a hearth would quietly draw pieces nobody charged.
  [InlineData("Items10")]
  [InlineData("Items1x/ShingledBlooms")]
  [InlineData("Fettle/Cube130")]
  [InlineData("BaseExtension")]
  [InlineData("BaseExtension/Cube5")]
  public void A_longer_name_that_merely_starts_the_same_is_not_a_match(string path) =>
    Assert.False(ExShapeElements.Matches(path, Keep));

  #endregion

  #region Degenerate inputs

  [Fact]
  public void An_empty_pattern_set_keeps_nothing()
  {
    // Which is what makes "draw an empty hearth" expressible at all: no fettle, no pigs, no elements.
    Assert.False(ExShapeElements.Matches("Base", []));
    Assert.False(ExShapeElements.Matches("", []));
  }

  [Fact]
  public void The_bare_subtree_marker_keeps_everything()
  {
    Assert.True(ExShapeElements.Matches("anything/at/all", ["*"]));
  }

  [Fact]
  public void The_subtree_marker_is_optional_spelling()
  {
    // "Base" and "Base/*" must mean the same thing, or two call sites written by different hands
    // silently disagree about whether a group's children render.
    Assert.True(ExShapeElements.Matches("Base/Cube2", ["Base"]));
    Assert.True(ExShapeElements.Matches("Base/Cube2", ["Base/*"]));
  }

  #endregion
}
