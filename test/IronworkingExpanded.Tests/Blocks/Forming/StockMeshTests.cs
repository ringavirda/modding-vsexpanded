using System.Linq;
using IronworkingExpanded.BlockStructures.Forming;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Laying out the sides of a part-rolled piece for rendering. The mesh is composed from the form's one base
/// shape rather than authored per state, and the placement is driven by the <b>same numbers the simulation
/// uses</b> - so what the player sees in their hand cannot drift from how the piece actually behaves.
/// <para>
/// Pinned because this is arithmetic that fails quietly: a side placed slightly out reads as a modelling slip,
/// not a bug, and nobody would think to check it. The final look still wants an in-game eye.
/// </para>
/// </summary>
public class StockMeshTests
{
  private static WorkPiece Even(StockForm form, float thickness, int sides) =>
    new(form, [.. Enumerable.Repeat(thickness, sides)], new bool[sides]);

  [Fact]
  public void A_fresh_piece_is_its_base_shape_untouched()
  {
    SidePlacement p = StockMesh.SideOf(WorkPiece.Fresh(StockForm.Bloom), 0);

    Assert.Equal(1f, p.Scale.X, 3);
    Assert.Equal(1f, p.Scale.Y, 3);
    Assert.Equal(1f, p.Scale.Z, 3);
    Assert.Equal(0f, p.OffsetX, 3);
  }

  [Fact]
  public void A_rolled_side_is_thinner_wider_and_longer()
  {
    // Every axis moves, and each one comes from the model rather than a fudge factor.
    var piece = Even(StockForm.Bloom, 1f, sides: 1);
    SidePlacement p = StockMesh.SideOf(piece, 0);

    Assert.True(p.Scale.Y < 1f, "it should be thinner");
    Assert.True(p.Scale.X > 1f, "and wider");
    Assert.True(p.Scale.Z > 1f, "and longer");
  }

  [Fact]
  public void Sides_abut_rather_than_overlapping_or_leaving_a_gap()
  {
    // The seam is what sells a half-rolled piece as one object; a gap or an overlap reads as broken art.
    var half = new WorkPiece(StockForm.Bloom, [3f, 1f], new bool[2]);

    SidePlacement left = StockMesh.SideOf(half, 0);
    SidePlacement right = StockMesh.SideOf(half, 1);
    float leftWidth = half.StripWidth(half.Strips[0]);
    float rightWidth = half.StripWidth(half.Strips[1]);

    float leftEdgeRight = StockMesh.CentreX + left.OffsetX + leftWidth / 2f;
    float rightEdgeLeft = StockMesh.CentreX + right.OffsetX - rightWidth / 2f;
    Assert.Equal(leftEdgeRight, rightEdgeLeft, 3);
  }

  [Fact]
  public void The_whole_piece_stays_centred_however_lopsided_it_is()
  {
    var half = new WorkPiece(StockForm.Bloom, [3f, 0.5f], new bool[2]);

    float outerLeft =
      StockMesh.CentreX
      + StockMesh.SideOf(half, 0).OffsetX
      - half.StripWidth(half.Strips[0]) / 2f;
    float outerRight =
      StockMesh.CentreX
      + StockMesh.SideOf(half, 1).OffsetX
      + half.StripWidth(half.Strips[1]) / 2f;

    Assert.Equal(half.Width, outerRight - outerLeft, 3); // spans exactly the piece's width
    Assert.Equal(StockMesh.CentreX, (outerLeft + outerRight) / 2f, 3); // and stays centred
  }

  [Fact]
  public void An_unevenly_rolled_piece_is_visibly_lopsided()
  {
    // The rolled side has spread, so it takes up more of the width than the untouched one. That asymmetry is
    // the whole point of composing the mesh at all.
    var half = new WorkPiece(StockForm.Bloom, [3f, 1f], new bool[2]);

    Assert.True(
      StockMesh.SideOf(half, 1).Scale.X > StockMesh.SideOf(half, 0).Scale.X,
      "the worked side should be the wider one"
    );
  }

  [Fact]
  public void An_out_of_range_side_falls_back_to_the_base_shape()
  {
    SidePlacement p = StockMesh.SideOf(WorkPiece.Fresh(StockForm.Bloom), 5);
    Assert.Equal(1f, p.Scale.X, 3);
    Assert.Equal(0f, p.OffsetX, 3);
  }

  #region Cache key

  [Fact]
  public void Pieces_that_look_the_same_share_a_cached_mesh()
  {
    // Only geometry may affect the key, or the cache grows one entry per stack instead of per appearance.
    var a = new WorkPiece(StockForm.Bloom, [2f, 1f], [false, false]);
    var b = new WorkPiece(StockForm.Bloom, [2f, 1f], [true, true]); // mid turn-over, identical shape

    Assert.Equal(StockMesh.CacheKey(a), StockMesh.CacheKey(b));
  }

  [Fact]
  public void Pieces_that_look_different_do_not()
  {
    var bloom = new WorkPiece(StockForm.Bloom, [2f, 1f], new bool[2]);
    Assert.NotEqual(
      StockMesh.CacheKey(bloom),
      StockMesh.CacheKey(new WorkPiece(StockForm.Bloom, [1f, 1f], new bool[2]))
    );
    Assert.NotEqual(
      StockMesh.CacheKey(bloom),
      StockMesh.CacheKey(new WorkPiece(StockForm.Slab, [2f, 1f], new bool[2]))
    );
    Assert.NotEqual(
      StockMesh.CacheKey(bloom),
      StockMesh.CacheKey(Even(StockForm.Bloom, 2f, sides: 1))
    );
  }

  #endregion
}
