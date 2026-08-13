using IronworkingExpanded.BlockStructures.Forming;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Scaling for the composed mesh - the route that draws the half-step, the state between two declared rungs
/// that no ladder names and no art draws. The scale is taken from the same numbers the simulation uses, so
/// the rendered piece matches its simulated dimensions. The final appearance is verified in-game.
/// </summary>
public class StockMeshTests {
  [Fact]
  public void A_fresh_piece_is_its_base_shape_untouched() {
    Vec3f scale = StockMesh.ScaleOf(WorkPiece.Fresh(StockForm.ShingledBar));

    Assert.Equal(1f, scale.X, 3);
    Assert.Equal(1f, scale.Y, 3);
    Assert.Equal(1f, scale.Z, 3);
  }

  [Fact]
  public void A_rolled_piece_is_thinner_wider_and_longer() {
    // Every axis moves, each taken from the piece's own dimensions.
    Vec3f scale = StockMesh.ScaleOf(
      new WorkPiece(StockForm.ShingledBar, 1f, 0f, [false])
    );

    Assert.True(scale.Y < 1f, "it should be thinner");
    Assert.True(scale.X > 1f, "and wider");
    Assert.True(scale.Z > 1f, "and longer");
  }

  [Fact]
  public void A_half_step_sits_between_the_two_rungs_it_lies_between() {
    // The half-step is the whole reason this route exists: 2.75 is drawn nowhere, so it is composed, and it
    // has to read as further along than 3.0 and less far than 2.5.
    float atBase = StockMesh.ScaleOf(WorkPiece.Fresh(StockForm.ShingledBar)).X;
    float atHalfStep = StockMesh
      .ScaleOf(new WorkPiece(StockForm.ShingledBar, 2.75f, 2.5f, [false]))
      .X;
    float atGap = StockMesh
      .ScaleOf(new WorkPiece(StockForm.ShingledBar, 2.5f, 0f, [false]))
      .X;

    Assert.True(atBase < atHalfStep && atHalfStep < atGap);
  }

  [Fact]
  public void How_a_piece_is_divided_does_not_change_how_it_looks() {
    // A piece is one gauge across its whole width; the side count is how it is fed, not what it is.
    Assert.Equal(
      StockMesh
        .ScaleOf(new WorkPiece(StockForm.ShingledBar, 1f, 0f, [false]))
        .X,
      StockMesh
        .ScaleOf(new WorkPiece(StockForm.ShingledBar, 1f, 0f, new bool[3]))
        .X,
      3
    );
  }

  [Fact]
  public void A_form_with_no_authored_size_falls_back_to_the_base_shape() {
    var degenerate = new StockForm("degenerate", 0f, 3f, 8f, 16f, 0.5f);

    Vec3f scale = StockMesh.ScaleOf(new WorkPiece(degenerate, 1f, 0f, [false]));

    Assert.Equal(1f, scale.X, 3);
    Assert.Equal(1f, scale.Y, 3);
    Assert.Equal(1f, scale.Z, 3);
  }

  #region Cache key

  [Fact]
  public void Pieces_that_look_the_same_share_a_cached_mesh() {
    // Only geometry may affect the key, or the cache grows one entry per stack instead of per appearance.
    var a = new WorkPiece(StockForm.ShingledBar, 2f, 0f, [false, false]);
    var b = new WorkPiece(StockForm.ShingledBar, 2f, 1.5f, [true, true]); // mid round, identical shape

    Assert.Equal(StockMesh.CacheKey(a), StockMesh.CacheKey(b));
    // And the division does not either, since it never showed.
    Assert.Equal(
      StockMesh.CacheKey(a),
      StockMesh.CacheKey(new WorkPiece(StockForm.ShingledBar, 2f, 0f, [false]))
    );
  }

  [Fact]
  public void Pieces_that_look_different_do_not() {
    var bloom = new WorkPiece(StockForm.ShingledBar, 2f, 0f, [false]);

    Assert.NotEqual(
      StockMesh.CacheKey(bloom),
      StockMesh.CacheKey(new WorkPiece(StockForm.ShingledBar, 1f, 0f, [false]))
    );
    Assert.NotEqual(
      StockMesh.CacheKey(bloom),
      StockMesh.CacheKey(new WorkPiece(StockForm.ShingledSlab, 2f, 0f, [false]))
    );
    // A fork draws one gauge two ways, so the branch is part of the appearance.
    Assert.NotEqual(
      StockMesh.CacheKey(bloom),
      StockMesh.CacheKey(bloom with { Family = "grooved" })
    );
  }

  #endregion
}
