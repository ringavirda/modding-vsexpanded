using System;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The typed <c>multiblockStructure</c> builder: named block numbers, a computed <c>Fill</c> for regular
/// sub-volumes, and bespoke <c>At</c> cells, with build-time validation of block-number references and
/// cell collisions. Offset order is pinned because the emitted array is compared position-by-position
/// against the hand-written JSON.
/// </summary>
public class MultiblockBuilderTests {
  private static JObject Structure(Action<MultiblockBuilder> configure) =>
    (JObject)
      ExBlockDef.Create("d", "c").Multiblock(configure).ToJson()["attributes"]![
        "multiblockStructure"
      ]!;

  #region Emission / order

  [Fact]
  public void Number_emits_the_blocknumbers_map() {
    JObject bn = (JObject)
      Structure(m => m.Number("mod:a*", 1).Number("mod:b", 2).At(0, 0, 0, 1))[
        "blockNumbers"
      ]!;
    Assert.Equal(1, (int)bn["mod:a*"]!);
    Assert.Equal(2, (int)bn["mod:b"]!);
  }

  [Fact]
  public void Fill_emits_offsets_in_z_outer_y_mid_x_inner_order() {
    JArray offsets = (JArray)
      Structure(m => m.Number("f", 5).Fill(0, 0, 0, 1, 1, 0, 5))["offsets"]!;

    JArray expected = JArray.Parse(
      """
      [
        { "x": 0, "y": 0, "z": 0, "w": 5 },
        { "x": 1, "y": 0, "z": 0, "w": 5 },
        { "x": 0, "y": 1, "z": 0, "w": 5 },
        { "x": 1, "y": 1, "z": 0, "w": 5 }
      ]
      """
    );
    Assert.True(
      JToken.DeepEquals(expected, offsets),
      "Fill order diverged:\n" + offsets
    );
  }

  [Fact]
  public void At_and_Fill_compose_in_call_order() {
    JArray offsets = (JArray)
      Structure(m =>
        m.Number("a", 1).Number("f", 8).At(0, 0, 0, 1).Fill(0, 0, 1, 0, 0, 2, 8)
      )["offsets"]!;

    Assert.Equal(3, offsets.Count);
    Assert.Equal(1, (int)offsets[0]!["w"]!); // the At cell first
    Assert.Equal(0, (int)offsets[0]!["z"]!);
    Assert.Equal(1, (int)offsets[1]!["z"]!); // then the Fill, starting at z=1
    Assert.Equal(2, (int)offsets[2]!["z"]!);
  }

  #endregion

  #region Validation

  [Fact]
  public void Build_rejects_an_offset_referencing_an_undeclared_number() {
    var ex = Assert.Throws<InvalidOperationException>(() =>
      Structure(m => m.Number("a", 1).At(0, 0, 0, 1).At(1, 0, 0, 9))
    );
    Assert.Contains("9", ex.Message);
    Assert.Contains("blockNumbers", ex.Message);
  }

  [Fact]
  public void At_rejects_a_duplicate_cell_position() {
    var ex = Assert.Throws<ArgumentException>(() =>
      Structure(m => m.Number("a", 1).At(0, 0, 0, 1).At(0, 0, 0, 1))
    );
    Assert.Contains("duplicate", ex.Message);
  }

  [Fact]
  public void Fill_that_overlaps_an_existing_cell_is_a_duplicate() {
    Assert.Throws<ArgumentException>(() =>
      Structure(m => m.Number("f", 8).At(0, 0, 0, 8).Fill(0, 0, 0, 1, 0, 0, 8))
    );
  }

  [Fact]
  public void Fill_rejects_an_inverted_range_instead_of_emitting_nothing() {
    // An inverted range would emit an empty sub-volume rather than fail, so the builder rejects it.
    var ex = Assert.Throws<ArgumentException>(() =>
      Structure(m => m.Number("f", 8).Fill(1, 0, 0, -1, 0, 0, 8))
    );
    Assert.Contains("inverted", ex.Message);
  }

  [Fact]
  public void A_valid_structure_builds_with_every_number_resolved() {
    JObject s = Structure(m =>
      m.Number("mod:body", 8)
        .Number("mod:core", 1)
        .At(0, 0, 0, 1)
        .Fill(-1, 0, 1, 1, 0, 1, 8)
    );
    Assert.Equal(4, ((JArray)s["offsets"]!).Count); // 1 core + 3 body
  }

  #endregion
}
