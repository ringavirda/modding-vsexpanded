using ExpandedLib.Helpers;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The element-path matcher a block entity prunes its shape with. It is not the engine's
/// <c>selectiveElements</c> rule: matching is whole-segment, an ancestor on the way to a named element
/// is kept, and naming an element keeps its children. A wrong pattern set renders a hole rather than
/// throwing, so the rule is pinned here.
/// </summary>
public class ExShapeElementsTests {
  private static readonly string[] Keep =
  [
    "Base/*",
    "Fettle/Cube13",
    "Items1/ShingledBlooms/*",
  ];

  #region What is kept

  [Theory]
  // The named element itself, and everything beneath it.
  [InlineData("Fettle/Cube13")]
  [InlineData("Items1/ShingledBlooms")]
  [InlineData("Items1/ShingledBlooms/ShingledBloom1")]
  [InlineData("Base")]
  [InlineData("Base/Cube2")]
  // The ancestors on the way to a named element; without these the named children are unreachable.
  [InlineData("Fettle")]
  [InlineData("Items1")]
  public void An_element_at_on_the_way_to_or_under_a_pattern_is_kept(
    string path
  ) => Assert.True(ExShapeElements.Matches(path, Keep));

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
  // Whole-segment matching. These names exist in the shipped shapes, and a bare string prefix would
  // keep all of them: "Items1" would match "Items10" and "Fettle/Cube13" would match "Fettle/Cube130".
  [InlineData("Items10")]
  [InlineData("Items1x/ShingledBlooms")]
  [InlineData("Fettle/Cube130")]
  [InlineData("BaseExtension")]
  [InlineData("BaseExtension/Cube5")]
  public void A_longer_name_that_merely_starts_the_same_is_not_a_match(
    string path
  ) => Assert.False(ExShapeElements.Matches(path, Keep));

  #endregion

  #region Degenerate inputs

  [Fact]
  public void An_empty_pattern_set_keeps_nothing() {
    // An empty set is how a fully empty shape is expressed.
    Assert.False(ExShapeElements.Matches("Base", []));
    Assert.False(ExShapeElements.Matches("", []));
  }

  [Fact]
  public void The_bare_subtree_marker_keeps_everything() {
    Assert.True(ExShapeElements.Matches("anything/at/all", ["*"]));
  }

  [Fact]
  public void The_subtree_marker_is_optional_spelling() {
    // "Base" and "Base/*" mean the same thing, so both spellings render a group's children.
    Assert.True(ExShapeElements.Matches("Base/Cube2", ["Base"]));
    Assert.True(ExShapeElements.Matches("Base/Cube2", ["Base/*"]));
  }

  #endregion

  #region Copy semantics

  // A bed wearing a fuel texture, with a child that wears it too, beside a group neither pattern keeps.
  private static Shape Fixture() =>
    new() {
      Elements =
      [
        new ShapeElement
        {
          Name = "Bed",
          FacesResolved =
          [
            new ShapeElementFace { Texture = "#coke" },
            new ShapeElementFace { Texture = "#brick" },
          ],
          Children =
          [
            new ShapeElement
            {
              Name = "Cube1",
              FacesResolved = [new ShapeElementFace { Texture = "#coke" }],
            },
            new ShapeElement { Name = "Cube2", FacesResolved = [] },
          ],
        },
        new ShapeElement { Name = "Pigs", FacesResolved = [] },
      ],
    };

  [Fact]
  public void Retexturing_repoints_every_matching_face_at_every_depth() {
    Shape copy = ExShapeElements.Retextured(Fixture(), "coke", "charcoal");

    Assert.Equal("#charcoal", copy.Elements[0].FacesResolved![0].Texture);
    Assert.Equal("#brick", copy.Elements[0].FacesResolved![1].Texture);
    Assert.Equal(
      "#charcoal",
      copy.Elements[0].Children![0].FacesResolved![0].Texture
    );
  }

  [Fact]
  public void Retexturing_does_not_write_through_to_the_shape_it_copied() {
    // ShapeElement.Clone copies FacesResolved with a plain array clone, so a cloned element's faces are
    // the *same objects* as the source's. Repointing one in place therefore rewrites the asset every
    // other block draws from - a firebox charged with charcoal would retexture the bed of every firebox
    // in the world. Load-bearing now that shapes are loaded once and cached rather than per tesselation.
    Shape source = Fixture();
    Shape copy = ExShapeElements.Retextured(source, "coke", "charcoal");

    Assert.Equal("#coke", source.Elements[0].FacesResolved![0].Texture);
    Assert.Equal(
      "#coke",
      source.Elements[0].Children![0].FacesResolved![0].Texture
    );
    Assert.NotSame(
      source.Elements[0].FacesResolved![0],
      copy.Elements[0].FacesResolved![0]
    );
  }

  [Fact]
  public void Pruning_does_not_write_through_to_the_shape_it_copied() {
    Shape source = Fixture();
    Shape copy = ExShapeElements.Pruned(source, ["Bed/Cube1"]);

    Assert.Single(copy.Elements); // Pigs dropped
    Assert.Single(copy.Elements[0].Children!); // Cube2 dropped

    Assert.Equal(2, source.Elements.Length);
    Assert.Equal(2, source.Elements[0].Children!.Length);
  }

  #endregion
}
