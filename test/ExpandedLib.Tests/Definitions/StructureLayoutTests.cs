using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The ASCII layer DSLs that let a multiblock structure / filler footprint be authored the way you'd draw it
/// (a top-down grid per Y level) instead of a coordinate array. Pins the grid parse, that the multiblock and
/// filler layouts generate the right cells, and - crucially - that the shared parity oracle treats a structure
/// as an unordered set so a hand-written coordinate table and a DSL drawing of the same cells are equivalent.
/// </summary>
public class StructureLayoutTests
{
  #region Grid parsing

  [Fact]
  public void Parse_maps_rows_to_z_and_columns_to_x_skipping_dots()
  {
    var cells = StructureLayout.Parse(
      xLeft: -1,
      zTop: 0,
      new List<(int, string)>
      {
        (
          0,
          """
          . A .
          B . C
          """
        ),
      }
    );

    Assert.Equal(3, cells.Count);
    Assert.Contains(new LayoutCell(0, 0, 0, 'A'), cells); // col1,row0
    Assert.Contains(new LayoutCell(-1, 0, 1, 'B'), cells); // col0,row1
    Assert.Contains(new LayoutCell(1, 0, 1, 'C'), cells); // col2,row1
  }

  [Fact]
  public void Parse_trims_surrounding_blank_lines_so_z_starts_at_zTop()
  {
    var cells = StructureLayout.Parse(
      0,
      5,
      new List<(int, string)> { (2, "\n\n  X  \n\n") }
    );
    var cell = Assert.Single(cells);
    Assert.Equal(new LayoutCell(0, 2, 5, 'X'), cell);
  }

  [Fact]
  public void ParseVertical_maps_rows_down_in_y_and_columns_to_z()
  {
    var cells = StructureLayout.ParseVertical(
      zLeft: 0,
      yTop: 2,
      new List<(int, string)>
      {
        (
          0,
          """
          A . B
          . C .
          """
        ),
      }
    );

    // A front elevation at x=0: row0 -> y=2 (top), row1 -> y=1; columns run +Z from zLeft.
    Assert.Equal(3, cells.Count);
    Assert.Contains(new LayoutCell(0, 2, 0, 'A'), cells); // col0,row0
    Assert.Contains(new LayoutCell(0, 2, 2, 'B'), cells); // col2,row0
    Assert.Contains(new LayoutCell(0, 1, 1, 'C'), cells); // col1,row1
  }

  [Fact]
  public void ParseFrontal_maps_rows_down_in_y_and_columns_to_x()
  {
    var cells = StructureLayout.ParseFrontal(
      xLeft: -1,
      yTop: 2,
      new List<(int, string)>
      {
        (
          0,
          """
          A . B
          . C .
          """
        ),
      }
    );

    // A front elevation at z=0: row0 -> y=2 (top), row1 -> y=1; columns run +X from xLeft.
    Assert.Equal(3, cells.Count);
    Assert.Contains(new LayoutCell(-1, 2, 0, 'A'), cells); // col0,row0
    Assert.Contains(new LayoutCell(1, 2, 0, 'B'), cells); // col2,row0
    Assert.Contains(new LayoutCell(0, 1, 0, 'C'), cells); // col1,row1
  }

  #endregion

  #region Multiblock layout

  [Fact]
  public void MultiblockLayout_generates_the_drawn_cells()
  {
    JObject structure = (JObject)
      ExBlockDef
        .Create("d", "c")
        .MultiblockLayout(s =>
          s.Origin(-1, 0)
            .Legend('C', "mod:core*")
            .Legend('#', "exlib:structurefiller")
            .Layer(
              0,
              """
              # C #
              # # #
              """
            )
        )
        .ToJson()["attributes"]!["multiblockStructure"]!;

    // 6 cells, core at origin, fillers elsewhere; block codes both present.
    Assert.Equal(6, ((JArray)structure["offsets"]!).Count);
    JObject bn = (JObject)structure["blockNumbers"]!;
    Assert.True(bn.ContainsKey("mod:core*"));
    Assert.True(bn.ContainsKey("exlib:structurefiller"));
  }

  [Fact]
  public void MultiblockLayout_rejects_an_unlegended_symbol()
  {
    Assert.Throws<System.InvalidOperationException>(() =>
      ExBlockDef
        .Create("d", "c")
        .MultiblockLayout(s => s.Legend('#', "mod:f").Layer(0, "# ?"))
    );
  }

  #endregion

  #region Filler layout

  [Fact]
  public void FillerLayout_maps_glyphs_to_attach_and_skips_origin()
  {
    var cells = StructureFootprint.Layout(f =>
      f.Origin(-1, 0)
        .Layer(
          0,
          """
          + O +
          # # #
          """
        )
    );

    // Origin (0,0,0) drawn as 'O' is skipped; '+' attach, '#' solid.
    Assert.DoesNotContain(cells, c => c is { X: 0, Y: 0, Z: 0 });
    Assert.Equal(5, cells.Count);
    Assert.All(
      cells,
      c => Assert.Equal(c.Z == 0, c.AllowAttach) // row0 cells are '+', row1 are '#'
    );
  }

  [Fact]
  public void FillerLayout_slice_reads_a_vertical_column_and_skips_origin()
  {
    // A front elevation of the x=0 plane: a full top row over a single lower cell, with the origin
    // (0,0,0) marked 'O' at the lower-left - the engine beam-column shape in miniature.
    var cells = StructureFootprint.Layout(f =>
      f.Origin(0, 1) // zLeft=0, yTop=1
        .Slice(
          0,
          """
          # # #
          O # .
          """
        )
    );

    Assert.Equal(4, cells.Count);
    Assert.Contains(cells, c => c is { X: 0, Y: 1, Z: 0 });
    Assert.Contains(cells, c => c is { X: 0, Y: 1, Z: 1 });
    Assert.Contains(cells, c => c is { X: 0, Y: 1, Z: 2 });
    Assert.Contains(cells, c => c is { X: 0, Y: 0, Z: 1 });
    Assert.DoesNotContain(cells, c => c is { Y: 0, Z: 0 }); // origin gap
  }

  [Fact]
  public void FillerLayout_face_reads_a_thin_in_z_disc_in_the_xy_plane()
  {
    // A north-facing wheel: the 3x3 disc lies in X-Y at z=0, origin at bottom-centre, hub one up.
    var cells = StructureFootprint.Layout(f =>
      f.Origin(-1, 2) // xLeft=-1, yTop=2
        .Face(
          0,
          """
          # # #
          # # #
          # O #
          """
        )
    );

    Assert.Equal(8, cells.Count); // 9 minus the skipped origin
    Assert.All(cells, c => Assert.Equal(0, c.Z)); // the whole disc is thin in Z
    Assert.Contains(cells, c => c is { X: 0, Y: 1, Z: 0 }); // hub cell above the origin
    Assert.DoesNotContain(cells, c => c is { X: 0, Y: 0, Z: 0 }); // origin gap
  }

  [Fact]
  public void FillerLayout_rejects_mixing_horizontal_layers_and_vertical_slices()
  {
    Assert.Throws<System.InvalidOperationException>(() =>
      StructureFootprint.Layout(f => f.Layer(0, "#").Slice(0, "#"))
    );
  }

  [Fact]
  public void FillerLayout_rejects_mixing_slices_and_faces()
  {
    Assert.Throws<System.InvalidOperationException>(() =>
      StructureFootprint.Layout(f => f.Slice(0, "#").Face(0, "#"))
    );
  }

  [Fact]
  public void FillerLayout_rejects_the_principal_marker_off_the_origin()
  {
    // '#' at col0 is the origin (skipped); 'O' at col1 is (1,0,0) - a misplaced principal, so it errors
    // rather than being silently dropped.
    Assert.Throws<System.InvalidOperationException>(() =>
      StructureFootprint.Layout(f => f.Layer(0, "# O"))
    );
  }

  #endregion

  #region Parity oracle set-semantics

  [Fact]
  public void DefinitionParity_treats_a_multiblock_as_an_unordered_renumberable_set()
  {
    // Same cells + codes, but different offset order AND different w-numbering => still equal.
    JObject a = JObject.Parse(
      """
      { "attributes": { "multiblockStructure": {
        "blockNumbers": { "mod:a": 1, "mod:b": 2 },
        "offsets": [ { "x": 0, "y": 0, "z": 0, "w": 1 }, { "x": 1, "y": 0, "z": 0, "w": 2 } ] } } }
      """
    );
    JObject b = JObject.Parse(
      """
      { "attributes": { "multiblockStructure": {
        "blockNumbers": { "mod:b": 5, "mod:a": 9 },
        "offsets": [ { "x": 1, "y": 0, "z": 0, "w": 5 }, { "x": 0, "y": 0, "z": 0, "w": 9 } ] } } }
      """
    );
    Assert.True(DefinitionParity.Equal(a, b));
  }

  [Fact]
  public void DefinitionParity_catches_a_wrong_multiblock_cell()
  {
    JObject a = JObject.Parse(
      """{ "attributes": { "multiblockStructure": { "blockNumbers": { "mod:a": 1 }, "offsets": [ { "x": 0, "y": 0, "z": 0, "w": 1 } ] } } }"""
    );
    JObject b = JObject.Parse(
      """{ "attributes": { "multiblockStructure": { "blockNumbers": { "mod:a": 1 }, "offsets": [ { "x": 9, "y": 0, "z": 0, "w": 1 } ] } } }"""
    );
    Assert.False(DefinitionParity.Equal(a, b));
  }

  [Fact]
  public void DefinitionParity_treats_filler_offsets_as_a_set_with_explicit_attach()
  {
    // Reordered, and one form omits allowAttach:false while the other states it => equal.
    JObject a = JObject.Parse(
      """{ "attributes": { "fillerOffsets": [ { "x": 1, "y": 0, "z": 0, "allowAttach": true }, { "x": 0, "y": 0, "z": 1 } ] } }"""
    );
    JObject b = JObject.Parse(
      """{ "attributes": { "fillerOffsets": [ { "x": 0, "y": 0, "z": 1, "allowAttach": false }, { "x": 1, "y": 0, "z": 0, "allowAttach": true } ] } }"""
    );
    Assert.True(DefinitionParity.Equal(a, b));
  }

  #endregion
}
