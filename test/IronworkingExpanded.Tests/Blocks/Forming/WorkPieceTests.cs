using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Forming;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The work piece: stock part-way through a rolling schedule, carrying one gauge for the whole piece and how
/// far through the current gap it has got. The state lives on the item stack because the piece is carried
/// back around the mill between passes and so spends most of its life outside the machine.
/// <para>
/// A gap is two rounds and a round is one feed per side, so the gauge moves only when the last side catches
/// up. That is the whole reason a piece is never lopsided, and it is what these tests pin.
/// </para>
/// </summary>
public class WorkPieceTests {
  private static ItemStack Stack() =>
    new(new TestWorld().RegisterItem("iwex:stock-shingledbar"));

  #region The piece is one gauge across its whole width

  [Theory]
  [InlineData("shingledbar", 3f)]
  [InlineData("shingledbar", 2f)]
  [InlineData("shingledbar", 1.5f)]
  [InlineData("shingledbar", 1f)]
  [InlineData("shingledbar", 0.5f)]
  [InlineData("shingledslab", 3f)]
  [InlineData("shingledslab", 1f)]
  [InlineData("shingledslab", 0.5f)]
  public void A_pieces_width_is_the_whole_forms_width_however_it_is_divided(
    string formName,
    float thickness
  ) {
    StockForm form = StockForm.All[formName];

    // Dividing a piece for a narrow barrel is about how it is fed, not about what it is: the metal is the
    // same width either way, and that width is what the stage shapes are drawn to.
    Assert.Equal(
      form.WidthAt(thickness),
      new WorkPiece(form, thickness, 0f, new bool[2]).Width,
      3
    );
    Assert.Equal(
      form.WidthAt(thickness),
      new WorkPiece(form, thickness, 0f, [false]).Width,
      3
    );
  }

  [Fact]
  public void A_bite_is_the_piece_taken_a_side_at_a_time() {
    // What the rolls actually press across. The whole width would over-report the load by the side count.
    var piece = new WorkPiece(StockForm.ShingledBar, 1f, 0f, new bool[2]);

    Assert.Equal(piece.Width / 2f, piece.BiteWidthAt(1f), 3);
    Assert.Equal(
      piece.Width,
      new WorkPiece(StockForm.ShingledBar, 1f, 0f, [false]).BiteWidthAt(1f),
      3
    );
  }

  [Fact]
  public void A_fresh_piece_is_at_the_forms_as_shingled_thickness() {
    WorkPiece fresh = WorkPiece.Fresh(StockForm.ShingledBar);

    Assert.Equal(StockForm.ShingledBar.BaseThickness, fresh.Thickness);
    Assert.Equal(StockForm.ShingledBar.BaseWidth, fresh.Width, 3);
    Assert.Equal(StockForm.ShingledBar.BaseLength, fresh.Length, 3);
    Assert.Equal(1, fresh.Sides); // a fresh piece fits any barrel until it spreads
    Assert.Equal(0f, fresh.Gap); // and it is half way through nothing
  }

  [Fact]
  public void A_piece_never_spreads_past_the_forms_ceiling() {
    StockForm bloom = StockForm.ShingledBar;

    Assert.Equal(
      bloom.MaxWidth,
      new WorkPiece(bloom, 0.5f, 0f, [false]).Width,
      3
    );
  }

  #endregion

  #region A gap costs two rounds, and a round is one feed per side

  [Fact]
  public void Round_one_lands_the_half_step_and_round_two_lands_the_gap() {
    WorkPiece piece = WorkPiece.Fresh(StockForm.ShingledBar); // 3.0, one side

    WorkPiece half = piece.Feed(0, 2.5f);
    Assert.Equal(2.75f, half.Thickness, 3);
    Assert.Equal(2.5f, half.Gap); // it is half way through the 2.5 gap

    WorkPiece landed = half.Feed(0, 2.5f);
    Assert.Equal(2.5f, landed.Thickness, 3);
    Assert.Equal(0f, landed.Gap); // and standing on it again
  }

  [Fact]
  public void The_gauge_does_not_move_until_every_side_has_been_fed() {
    var piece = new WorkPiece(StockForm.ShingledBar, 3f, 0f, new bool[2]);

    WorkPiece oneSide = piece.Feed(1, 2.5f);
    Assert.Equal(3f, oneSide.Thickness); // nothing yet: the other side is still thick
    Assert.True(oneSide.IsFed(1));
    Assert.False(oneSide.IsFed(0));

    WorkPiece bothSides = oneSide.Feed(0, 2.5f);
    Assert.Equal(2.75f, bothSides.Thickness, 3);
    Assert.False(bothSides.IsFed(0)); // the round is spent and starts over
    Assert.False(bothSides.IsFed(1));
  }

  [Fact]
  public void Feeding_one_side_twice_does_not_complete_a_round_for_the_other() {
    // The credit is per side, so a player cannot walk a two-sided piece down by working one edge of it.
    var piece = new WorkPiece(StockForm.ShingledBar, 3f, 0f, new bool[2]);

    WorkPiece twice = piece.Feed(0, 2.5f).Feed(0, 2.5f);

    Assert.Equal(3f, twice.Thickness);
    Assert.True(twice.IsFed(0));
    Assert.False(twice.IsFed(1));
  }

  [Fact]
  public void Four_feeds_take_a_two_sided_piece_through_one_whole_gap() {
    WorkPiece piece = new WorkPiece(StockForm.ShingledBar, 3f, 0f, new bool[2])
      .Feed(0, 2.5f)
      .Feed(1, 2.5f) // round 1 lands 2.75
      .Feed(0, 2.5f)
      .Feed(1, 2.5f); // round 2 lands 2.5

    Assert.Equal(2.5f, piece.Thickness, 3);
    Assert.Equal(0f, piece.Gap);
    Assert.Equal(4, WorkPiece.PassesForGap(width: 9f, barrelWidth: 5f));
  }

  [Fact]
  public void A_gap_the_piece_is_not_half_way_through_starts_its_own_round_one() {
    // Half way through 2.5 and then offered 2.0: the piece has taken no half-step toward 2.0, so this is
    // that gap's round one and the target is half way from where the piece actually is.
    var half = new WorkPiece(StockForm.ShingledBar, 2.75f, 2.5f, [false]);

    Assert.Equal(2.375f, half.RoundTarget(2.0f), 3);
    Assert.Equal(2.375f, half.Feed(0, 2.0f).Thickness, 3);
    Assert.Equal(2.0f, half.Feed(0, 2.0f).Gap);
  }

  [Fact]
  public void An_out_of_range_side_leaves_the_piece_alone() {
    WorkPiece piece = WorkPiece.Fresh(StockForm.ShingledBar);

    Assert.Equal(piece, piece.Feed(-1, 2.5f));
    Assert.Equal(piece, piece.Feed(3, 2.5f));
  }

  #endregion

  #region The round belongs to the barrel that started it

  [Fact]
  public void A_barrel_that_divides_the_piece_differently_starts_the_round_over() {
    // The feeds recorded were of a different set of sides and say nothing about these ones.
    var midRound = new WorkPiece(
      StockForm.ShingledBar,
      2.75f,
      2.5f,
      [true, false]
    );

    WorkPiece regrouped = midRound.ForSides(3);

    Assert.Equal(3, regrouped.Sides);
    Assert.False(regrouped.IsFed(0));
    Assert.Equal(2.75f, regrouped.Thickness); // the metal is untouched
    Assert.Equal(2.5f, regrouped.Gap); // and so is the gap it is half way through
  }

  [Fact]
  public void A_barrel_that_divides_it_the_same_way_keeps_the_round() {
    var midRound = new WorkPiece(StockForm.ShingledBar, 3f, 0f, [true, false]);

    Assert.True(midRound.ForSides(2).IsFed(0));
    Assert.True(midRound.ForSides(0).IsFed(0)); // a nonsense count changes nothing
  }

  [Theory]
  [InlineData(3f, 6f, 1)] // fits the barrel whole
  [InlineData(6f, 6f, 1)] // exactly fills it
  [InlineData(9f, 6f, 2)]
  [InlineData(9f, 4f, 3)]
  [InlineData(3f, 0f, 1)] // a barrel with no width is not a division
  public void Sides_are_the_piece_measured_against_the_barrel(
    float width,
    float barrel,
    int expected
  ) => Assert.Equal(expected, WorkPiece.SidesFor(width, barrel));

  #endregion

  #region Cropping (what a shear takes out of a piece)

  [Fact]
  public void A_fresh_piece_is_whole_and_worth_its_jobs_whole_count() {
    WorkPiece piece = WorkPiece.Fresh(StockForm.ShingledBar);

    Assert.False(piece.IsPartCropped);
    Assert.Equal(4, piece.CropsLeft(4));
    Assert.False(piece.IsSpent(4));
  }

  [Fact]
  public void Each_crop_takes_one_product_and_leaves_the_rest_in_the_piece() {
    WorkPiece piece = WorkPiece.Fresh(StockForm.ShingledBar);

    piece = piece.Crop(4);
    Assert.True(piece.IsPartCropped);
    Assert.Equal(3, piece.CropsLeft(4));

    piece = piece.Crop(4).Crop(4);
    Assert.Equal(1, piece.CropsLeft(4));
    Assert.False(piece.IsSpent(4));
  }

  [Fact]
  public void A_worked_out_piece_is_spent_and_cannot_yield_another() {
    WorkPiece piece = WorkPiece.Fresh(StockForm.ShingledBar);
    for (int i = 0; i < 4; i++)
      piece = piece.Crop(4);

    Assert.True(piece.IsSpent(4));
    Assert.Equal(0, piece.CropsLeft(4));
    // The stroke that takes the last product is the one that removes the piece, so a further crop is a no-op
    // rather than a negative tally.
    Assert.Equal(4, piece.Crop(4).Cropped);
  }

  [Fact]
  public void A_crop_moves_nothing_but_the_tally() {
    // A part-cropped piece is still stock at the same stage on the same branch: it is shorter, and length is
    // the art's rather than the piece's.
    var piece = new WorkPiece(
      StockForm.ShingledBar,
      1.5f,
      1.0f,
      [true, false],
      "flat"
    );

    WorkPiece cropped = piece.Crop(6);

    Assert.Equal(piece with { Cropped = 1 }, cropped);
  }

  [Fact]
  public void The_jobs_count_stays_the_authority_when_it_is_retuned() {
    // The piece tallies what has gone, not what is left, so a modder raising a crop row from 4 to 6 gives
    // every piece already in a world the two extra crops instead of stranding it on the old number.
    WorkPiece piece = WorkPiece.Fresh(StockForm.ShingledBar).Crop(4).Crop(4);

    Assert.Equal(2, piece.CropsLeft(4));
    Assert.Equal(4, piece.CropsLeft(6));
    Assert.True(piece.IsSpent(2));
  }

  #endregion

  #region Stack round-trip (the piece survives the carry-back)

  [Fact]
  public void A_piece_survives_a_round_trip_through_its_stack() {
    // The piece is dropped and picked up again between every pass, so the stack is where its state lives.
    var original = new WorkPiece(
      StockForm.ShingledBar,
      2.75f,
      2.5f,
      [true, false],
      "flat"
    );
    ItemStack stack = Stack();
    original.ToStack(stack);

    WorkPiece? read = WorkPiece.FromStack(stack);

    Assert.NotNull(read);
    Assert.Equal(StockForm.ShingledBar, read!.Form);
    Assert.Equal(2.75f, read.Thickness, 3);
    Assert.Equal(2.5f, read.Gap, 3); // the gap it is half way through survives
    Assert.Equal(original.Fed, read.Fed); // and so does the round in progress
    Assert.Equal("flat", read.Family);
  }

  [Fact]
  public void Rewriting_a_stack_replaces_the_previous_state() {
    ItemStack stack = Stack();
    new WorkPiece(StockForm.ShingledBar, 2.75f, 2.5f, [true], "flat").ToStack(
      stack
    );
    WorkPiece.Fresh(StockForm.ShingledBar).ToStack(stack);

    WorkPiece? read = WorkPiece.FromStack(stack);

    Assert.Equal(StockForm.ShingledBar.BaseThickness, read!.Thickness);
    Assert.Equal(0f, read.Gap);
    Assert.Null(read.Family);
  }

  [Fact]
  public void The_crop_tally_survives_the_carry_back_too() {
    var piece = new WorkPiece(
      StockForm.ShingledBar,
      1.5f,
      0f,
      [false],
      "flat",
      Cropped: 2
    );
    ItemStack stack = Stack();
    piece.ToStack(stack);

    Assert.Equal(2, WorkPiece.FromStack(stack)!.Cropped);
  }

  [Fact]
  public void An_uncropped_piece_carries_no_crop_state_at_all() {
    // Written as an absence rather than a zero, so two whole pieces still stack together and a piece rolled
    // before there was a shear reads the same as one that has never met one.
    ItemStack stack = Stack();
    new WorkPiece(StockForm.ShingledBar, 1.5f, 0f, [false], "flat", 3).ToStack(
      stack
    );
    Assert.NotNull(stack.Attributes["stockCropped"]);

    WorkPiece.Fresh(StockForm.ShingledBar).ToStack(stack);

    Assert.Null(stack.Attributes["stockCropped"]);
    Assert.Equal(0, WorkPiece.FromStack(stack)!.Cropped);
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

  [Fact]
  public void A_piece_rolled_under_the_per_side_model_reads_as_its_least_worked_side() {
    // The gauge it could always have been fed at. Nothing there recorded a gap, so the round starts over.
    ItemStack stack = Stack();
    stack.Attributes.SetString("stockForm", "shingledbar");
    stack.Attributes["stripThickness"] = new FloatArrayAttribute([1.5f, 3f]);
    stack.Attributes["stripTurned"] = new BoolArrayAttribute([true, false]);

    WorkPiece? read = WorkPiece.FromStack(stack);

    Assert.Equal(3f, read!.Thickness);
    Assert.Equal(2, read.Sides);
    Assert.Equal(0f, read.Gap);
    Assert.False(read.IsFed(0));
  }

  [Fact]
  public void Writing_a_migrated_piece_back_drops_the_per_side_keys() {
    // One-way: a piece that has been through the mill once stops carrying both forms.
    ItemStack stack = Stack();
    stack.Attributes.SetString("stockForm", "shingledbar");
    stack.Attributes["stripThickness"] = new FloatArrayAttribute([3f]);
    stack.Attributes["stripTurned"] = new BoolArrayAttribute([true]);

    WorkPiece.FromStack(stack)!.ToStack(stack);

    Assert.Null(stack.Attributes["stripThickness"]);
    Assert.Null(stack.Attributes["stripTurned"]);
  }

  #endregion

  #region Fresh stock off the crafting grid

  /// <summary>
  /// A stock item exactly as the game hands it over: the form is declared on the **item type** by
  /// <c>StockItemDefinitions</c> (<c>.Attribute("stockForm", …)</c>) and the stack itself carries
  /// nothing, because nothing has rolled it yet.
  /// </summary>
  private static ItemStack FreshStock(TestWorld world, StockForm form) {
    Item item = world.RegisterItem($"iwex:stock-{form.Name}");
    item.Attributes = new JsonObject(
      Newtonsoft.Json.Linq.JToken.Parse(
        $$"""{ "stockForm": "{{form.Name}}" }"""
      )
    );
    return new ItemStack(item);
  }

  [Fact]
  public void Stock_straight_off_the_grid_is_already_a_work_piece() {
    // Every other test here writes the form into the stack tree by hand, which nothing in the game
    // does before the first pass. Read only from the stack tree, a freshly crafted bloom is not a
    // work piece at all - and the mill refuses it as WrongForm, so nothing can be rolled, ever.
    ItemStack stack = FreshStock(new TestWorld(), StockForm.ShingledBar);

    WorkPiece? piece = WorkPiece.FromStack(stack);

    Assert.NotNull(piece);
    Assert.Equal(StockForm.ShingledBar, piece!.Form);
    Assert.Equal(StockForm.ShingledBar.BaseThickness, piece.Thickness);
  }

  [Fact]
  public void The_stack_state_wins_over_the_item_types_declaration() {
    // Once rolled, the piece's own state is the authority - the item type only says what it started
    // as, and a part-rolled piece must not be read back as fresh.
    var world = new TestWorld();
    ItemStack stack = FreshStock(world, StockForm.ShingledBar);
    new WorkPiece(StockForm.ShingledBar, 0.5f, 0f, [false]).ToStack(stack);

    Assert.Equal(0.5f, WorkPiece.FromStack(stack)!.Thickness);
  }

  [Fact]
  public void An_item_type_naming_an_unknown_form_is_still_refused() {
    var world = new TestWorld();
    Item item = world.RegisterItem("iwex:stock-phantom");
    item.Attributes = new JsonObject(
      Newtonsoft.Json.Linq.JToken.Parse("""{ "stockForm": "nosuchform" }""")
    );

    Assert.Null(WorkPiece.FromStack(new ItemStack(item)));
  }

  #endregion
}
