using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Forming;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The work piece: stock part-way through a rolling schedule, carrying a thickness per strip across its
/// width. The state lives on the item stack because the piece is carried back around the mill between passes
/// and so spends most of its life outside the machine. Strips sum back to the whole piece - each spreads on
/// its own share of the base width and of the ceiling - so an evenly-rolled piece is exactly as wide as
/// <see cref="StockForm.WidthAt"/> reports, which is what the shipped stage shapes are drawn to.
/// </summary>
public class WorkPieceTests {
  // An evenly-rolled piece divided into `sides`.
  private static WorkPiece Even(StockForm form, float thickness, int sides) =>
    new(form, [.. Enumerable.Repeat(thickness, sides)], new bool[sides]);

  private static ItemStack Stack() =>
    new(new TestWorld().RegisterItem("iwex:stock-bloom"));

  #region Strips reconstruct the whole piece

  [Theory]
  [InlineData("bloom", 3f)]
  [InlineData("bloom", 2f)]
  [InlineData("bloom", 1.5f)]
  [InlineData("bloom", 1f)]
  [InlineData("bloom", 0.5f)]
  [InlineData("slab", 3f)]
  [InlineData("slab", 1f)]
  [InlineData("slab", 0.5f)]
  public void An_evenly_rolled_piece_is_exactly_as_wide_as_the_whole_piece_model(
    string formName,
    float thickness
  ) {
    StockForm form = StockForm.All[formName];
    var piece = Even(form, thickness, sides: 2);

    // Summing the strips must reproduce StockForm.WidthAt, or the strip view and the stage shapes diverge.
    Assert.Equal(form.WidthAt(thickness), piece.Width, 3);
  }

  [Fact]
  public void A_half_rolled_piece_is_lopsided_and_sits_between_the_two_even_widths() {
    StockForm bloom = StockForm.Bloom;
    var half = new WorkPiece(bloom, [3f, 1f], new bool[2]); // right side down, left untouched

    Assert.False(half.IsEven);
    Assert.Equal(3f, half.Thickest);
    Assert.Equal(1f, half.Thinnest);

    Assert.True(
      half.Width > bloom.WidthAt(3f),
      "the rolled side should have spread"
    );
    Assert.True(
      half.Width < bloom.WidthAt(1f),
      "but the unrolled side has not"
    );
  }

  [Fact]
  public void A_fresh_piece_is_even_at_the_forms_as_shingled_thickness() {
    WorkPiece fresh = WorkPiece.Fresh(StockForm.Bloom);

    Assert.True(fresh.IsEven);
    Assert.Equal(StockForm.Bloom.BaseThickness, fresh.Thickest);
    Assert.Equal(StockForm.Bloom.BaseWidth, fresh.Width, 3);
    Assert.Equal(1, fresh.Sides); // a fresh piece fits any barrel until it spreads
  }

  [Fact]
  public void Rolling_one_strip_leaves_the_others_alone() {
    WorkPiece before = Even(StockForm.Bloom, 3f, sides: 2);
    WorkPiece after = before.WithStrip(0, 1.5f);

    Assert.Equal(1.5f, after.Strips[0]);
    Assert.Equal(3f, after.Strips[1]);
    Assert.Equal(3f, before.Strips[0]); // the record copies, so the original is untouched
  }

  [Fact]
  public void A_strip_never_spreads_past_its_share_of_the_forms_ceiling() {
    // Each strip is capped at its share of MaxWidth, so an unevenly-rolled piece cannot exceed the form's
    // width ceiling.
    StockForm bloom = StockForm.Bloom;
    var piece = Even(bloom, 0.5f, sides: 2);

    Assert.Equal(bloom.MaxWidth / 2, piece.StripWidth(0.5f), 3);
    Assert.Equal(bloom.MaxWidth, piece.Width, 3);
  }

  #endregion

  #region Stack round-trip (the piece survives the carry-back)

  [Fact]
  public void A_piece_survives_a_round_trip_through_its_stack() {
    // The piece is dropped and picked up again between every pass, so the stack is where its state lives.
    var original = new WorkPiece(StockForm.Bloom, [1.5f, 3f], [true, false]);
    ItemStack stack = Stack();
    original.ToStack(stack);

    WorkPiece? read = WorkPiece.FromStack(stack);

    Assert.NotNull(read);
    Assert.Equal(StockForm.Bloom, read!.Form);
    Assert.Equal(original.Strips, read.Strips);
    Assert.Equal(original.Turned, read.Turned); // the turn-over survives the round trip too
  }

  [Fact]
  public void Rewriting_a_stack_replaces_the_previous_state() {
    ItemStack stack = Stack();
    WorkPiece.Fresh(StockForm.Bloom).ToStack(stack);
    Even(StockForm.Bloom, 0.5f, sides: 2).ToStack(stack);

    Assert.Equal([0.5f, 0.5f], WorkPiece.FromStack(stack)!.Strips);
  }

  [Fact]
  public void A_stack_with_no_work_state_is_not_a_work_piece() {
    Assert.Null(WorkPiece.FromStack(null));
    Assert.Null(WorkPiece.FromStack(Stack())); // never rolled, carries nothing
  }

  [Fact]
  public void An_unknown_form_is_refused_rather_than_guessed() {
    // A stack naming a form that no longer exists must not fall back to a default form.
    ItemStack stack = Stack();
    stack.Attributes.SetString("stockForm", "nosuchform");

    Assert.Null(WorkPiece.FromStack(stack));
  }

  #endregion
}
