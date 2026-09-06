using System;
using ExpandedLib.Structures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The symbol-to-payload map every layout DSL's legend is built from: the two duplicate-mapping
/// policies and the "declared but never drawn" check against a <see cref="CellGrid"/>.
/// </summary>
public class SymbolLegendTests {
  [Fact]
  public void TryGet_answers_false_for_an_unmapped_symbol() {
    var legend = new SymbolLegend<string>(DuplicatePolicy.Throw);
    Assert.False(legend.TryGet('A', out _));
  }

  [Fact]
  public void TryGet_answers_the_mapped_payload() {
    var legend = new SymbolLegend<string>(DuplicatePolicy.Throw);
    legend.Map('A', "block");
    Assert.True(legend.TryGet('A', out string? payload));
    Assert.Equal("block", payload);
  }

  [Fact]
  public void Throw_policy_rejects_a_symbol_mapped_twice() {
    var legend = new SymbolLegend<string>(DuplicatePolicy.Throw);
    legend.Map('A', "first");
    Assert.Throws<InvalidOperationException>(() => legend.Map('A', "second"));
  }

  [Fact]
  public void Replace_policy_lets_a_later_mapping_win() {
    var legend = new SymbolLegend<string>(DuplicatePolicy.Replace);
    legend.Map('A', "first");
    legend.Map('A', "second");
    Assert.True(legend.TryGet('A', out string? payload));
    Assert.Equal("second", payload);
  }

  [Fact]
  public void Unused_lists_a_mapped_symbol_the_grid_never_drew() {
    var legend = new SymbolLegend<string>(DuplicatePolicy.Throw);
    legend.Map('A', "drawn").Map('B', "never drawn");

    var grid = new CellGrid(GridPlane.Horizontal, 0, 0);
    grid.Add(0, "A");

    Assert.Equal(['B'], legend.Unused(grid));
  }

  [Fact]
  public void Unused_is_empty_when_every_mapped_symbol_was_drawn() {
    var legend = new SymbolLegend<string>(DuplicatePolicy.Throw);
    legend.Map('A', "drawn");

    var grid = new CellGrid(GridPlane.Horizontal, 0, 0);
    grid.Add(0, "A");

    Assert.Empty(legend.Unused(grid));
  }
}
