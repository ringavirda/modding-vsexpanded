using System.Collections.Generic;
using ExpandedLib.Testing;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The testing harness's scene grid rule, pinned against the code-first layout's opposite rule
/// (StructureLayoutTests.Parse_does_not_advance_the_column_on_a_space): a scene diagram is meant to read
/// at a glance, so a space between two glyphs is a gap, not a spacer.
/// </summary>
public class SceneDiagramTests {
  [Fact]
  public void A_space_between_two_glyphs_places_them_two_columns_apart() {
    var placed = new List<BlockPos>();
    new SceneDiagram()
      .On('A', p => placed.Add(p))
      .On('B', p => placed.Add(p))
      .Layer("A B");

    Assert.Equal(2, placed.Count);
    Assert.Contains(new BlockPos(0, 0, 0), placed);
    Assert.Contains(new BlockPos(2, 0, 0), placed);
  }
}
