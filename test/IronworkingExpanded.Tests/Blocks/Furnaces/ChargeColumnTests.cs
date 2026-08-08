using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.Items;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The shaft column: an ordered list of charge bands with index <c>0</c> at the raceway end, pushed at the
/// top and consumed from the bottom. Pure data - the blocks over it are renderers - so everything the
/// counter-current furnace is built on can be pinned here before anything draws.
/// <para>
/// The two invariants worth guarding hardest are that <b>a split keeps both halves' temperature</b> (heat
/// belongs to the material, not to the quantity, which is what makes the melt condition exact at unit
/// granularity) and that <b>a warm band never absorbs a cold load</b> (merging across a temperature gap
/// would invent heat the furnace never produced).
/// </para>
/// </summary>
public class ChargeColumnTests
{
  private const string Coke = "game:coke";
  private const string Burden = "iwex:burden";
  private const string Charcoal = "game:charcoal";

  // Two distinguishable burden grades - same item, different flux ratio, which is burden's only quality
  // once coke is charged separately.
  private static readonly BurdenMix Fluxed = new(70f, 10f, 20f);
  private static readonly BurdenMix Lean = new(60f, 20f, 20f);

  private static ChargeSegment Seg(
    string material,
    int units,
    float temperature,
    BurdenMix mix = default
  ) => new(material, units, temperature, mix);

  /// <summary>
  /// The same tree after a trip through real attribute serialization - what a world save actually does to
  /// it, as opposed to handing the very same object back.
  /// </summary>
  private static TreeAttribute Serialized(TreeAttribute tree)
  {
    using var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
      tree.ToBytes(writer);

    stream.Position = 0;
    using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
    var read = new TreeAttribute();
    read.FromBytes(reader);
    return read;
  }

  #region Push and take

  [Fact]
  public void Push_then_take_round_trips_the_same_units_and_material()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180f, default);
    column.Push(Burden, 9, 1140f, Fluxed);
    Assert.Equal(12, column.TotalUnits);

    // The raceway gets the coke that was laid first, not the burden lying on top of it.
    List<ChargeSegment> first = column.Take(3);
    Assert.Equal([Seg(Coke, 3, 1180f)], first);
    Assert.Equal(9, column.TotalUnits);

    List<ChargeSegment> second = column.Take(9);
    Assert.Equal([Seg(Burden, 9, 1140f, Fluxed)], second);
    Assert.Equal(0, column.TotalUnits);
    Assert.Empty(column.Segments);
  }

  [Fact]
  public void A_partial_take_splits_the_boundary_segment_keeping_both_temperatures()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180f, default);
    column.Push(Burden, 9, 1140f, Fluxed);

    List<ChargeSegment> taken = column.Take(5);

    // 2 of the 9 burden units come off; a proportional split would hand back 1140 * 2/9 = 253 °C.
    Assert.Equal([Seg(Coke, 3, 1180f), Seg(Burden, 2, 1140f, Fluxed)], taken);
    // ...and the 7 that stay behind are still at 1140, not at 1140 * 7/9 = 887 °C.
    Assert.Equal([Seg(Burden, 7, 1140f, Fluxed)], column.Segments);
  }

  [Fact]
  public void A_take_spanning_three_segments_returns_them_raceway_first()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180f, default);
    column.Push(Burden, 9, 1140f, Fluxed);
    column.Push(Charcoal, 4, 980f, default);

    List<ChargeSegment> taken = column.Take(14);

    // Three different materials, so the order of the result is unambiguous.
    Assert.Equal(
      [
        Seg(Coke, 3, 1180f),
        Seg(Burden, 9, 1140f, Fluxed),
        Seg(Charcoal, 2, 980f),
      ],
      taken
    );
    Assert.Equal([Seg(Charcoal, 2, 980f)], column.Segments);
  }

  [Fact]
  public void Taking_more_than_the_column_holds_returns_everything_and_empties_it()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180f, default);
    column.Push(Burden, 9, 1140f, Fluxed);

    List<ChargeSegment> taken = column.Take(50);

    Assert.Equal(
      [Seg(Coke, 3, 1180f), Seg(Burden, 9, 1140f, Fluxed)],
      taken
    );
    Assert.Equal(0, column.TotalUnits);
    Assert.Empty(column.Segments);
    // And the now-starved column keeps answering, because descent runs every tick.
    Assert.Empty(column.Take(1));
  }

  [Fact]
  public void Taking_from_an_empty_column_returns_empty_and_does_not_throw()
  {
    var column = new ChargeColumn();

    Assert.Empty(column.Take(5));
    Assert.Empty(column.LowestUnits(5));
    Assert.Equal(0, column.TotalUnits);
    Assert.Null(column.TopMaterial);
  }

  [Fact]
  public void Pushing_zero_or_negative_units_is_a_no_op()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 0, 1180f, default);
    column.Push(Coke, -4, 1180f, default);
    Assert.Empty(column.Segments);

    column.Push(Coke, 3, 1180f, default);
    column.Push(Coke, 0, 1180f, default);
    // Not even a merge into a matching top band - nothing was charged.
    Assert.Equal([Seg(Coke, 3, 1180f)], column.Segments);
  }

  [Fact]
  public void Pushing_without_a_material_is_a_no_op()
  {
    var column = new ChargeColumn();
    column.Push(null!, 3, 1180f, default);
    column.Push("", 3, 1180f, default);
    Assert.Empty(column.Segments);

    column.Push(Coke, 3, 1180f, default);
    column.Push(null!, 3, 1180f, default);
    // A band with no code cannot be written to a tree at all, so accepting one moves the failure into the
    // engine's chunk save, ticks away from the push that caused it. It is refused at the door instead.
    Assert.Equal([Seg(Coke, 3, 1180f)], column.Segments);
  }

  #endregion

  #region Taking from the top

  // The mirror of the region above, and the only thing a player standing at the shaft can do to a column
  // by hand. It is a separate operation from Take rather than a flag on it because the column's two ends
  // are asymmetric on purpose: the raceway eats the bottom, the hopper lays on the top.

  [Fact]
  public void A_top_take_lifts_the_last_thing_that_was_laid_not_the_first()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180f, default);
    column.Push(Burden, 9, 1140f, Fluxed);

    List<ChargeSegment> taken = column.TakeTop(9);

    // The whole point, stated so it cannot pass by symmetry: a bottom take of 9 would hand back
    // [coke 3, burden 6] and leave burden behind. This hands back the burden and leaves the coke.
    Assert.Equal([Seg(Burden, 9, 1140f, Fluxed)], taken);
    Assert.Equal([Seg(Coke, 3, 1180f)], column.Segments);
  }

  [Fact]
  public void A_partial_top_take_splits_the_boundary_segment_keeping_both_temperatures()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180f, default);
    column.Push(Burden, 9, 1140f, Fluxed);

    List<ChargeSegment> taken = column.TakeTop(5);

    // Same rule as descent's split: heat belongs to the material, not to the quantity. A proportional
    // split would hand back 1140 * 5/9 = 633 °C and leave 1140 * 4/9 = 507 °C behind.
    Assert.Equal([Seg(Burden, 5, 1140f, Fluxed)], taken);
    Assert.Equal(
      [Seg(Coke, 3, 1180f), Seg(Burden, 4, 1140f, Fluxed)],
      column.Segments
    );
  }

  [Fact]
  public void A_top_take_spanning_three_segments_returns_them_stockline_first()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180f, default);
    column.Push(Burden, 9, 1140f, Fluxed);
    column.Push(Charcoal, 4, 980f, default);

    List<ChargeSegment> taken = column.TakeTop(14);

    // Three different materials, so the order of the result is unambiguous - and it is the exact reverse
    // of what the same take from the raceway end returns.
    Assert.Equal(
      [
        Seg(Charcoal, 4, 980f),
        Seg(Burden, 9, 1140f, Fluxed),
        Seg(Coke, 1, 1180f),
      ],
      taken
    );
    Assert.Equal([Seg(Coke, 2, 1180f)], column.Segments);
  }

  [Fact]
  public void Taking_more_off_the_top_than_the_column_holds_empties_it_without_over_taking()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180f, default);
    column.Push(Burden, 9, 1140f, Fluxed);

    List<ChargeSegment> taken = column.TakeTop(50);

    Assert.Equal(
      [Seg(Burden, 9, 1140f, Fluxed), Seg(Coke, 3, 1180f)],
      taken
    );
    Assert.Equal(0, column.TotalUnits);
    Assert.Empty(column.Segments);
    Assert.Empty(column.TakeTop(1));
  }

  [Fact]
  public void Peeking_at_the_top_removes_nothing()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180f, default);
    column.Push(Burden, 9, 1140f, Fluxed);

    Assert.Equal([Seg(Burden, 5, 1140f, Fluxed)], column.HighestUnits(5));
    Assert.Equal(12, column.TotalUnits);
    // ...and it reports exactly what the matching take would have removed.
    Assert.Equal(column.HighestUnits(5), column.TakeTop(5));
  }

  [Fact]
  public void Peeking_or_taking_off_an_empty_column_returns_empty_and_does_not_throw()
  {
    var column = new ChargeColumn();

    Assert.Empty(column.TakeTop(5));
    Assert.Empty(column.HighestUnits(5));
    Assert.Equal(0, column.TotalUnits);
  }

  [Fact]
  public void A_take_from_each_end_of_the_same_column_meets_in_the_middle()
  {
    // The two operations have to compose: a column worked from both ends must end up holding exactly what
    // neither took, with no double-counted band at the seam.
    var column = new ChargeColumn();
    column.Push(Coke, 10, 1180f, default);
    column.Push(Burden, 10, 1140f, Fluxed);
    column.Push(Charcoal, 10, 980f, default);

    column.Take(12); // the raceway eats all the coke and 2 of the burden
    column.TakeTop(12); // the player lifts all the charcoal and 2 of the burden

    Assert.Equal([Seg(Burden, 6, 1140f, Fluxed)], column.Segments);
    Assert.Equal(6, column.TotalUnits);
  }

  #endregion

  #region Taking a mid-span

  // The third take, and the one that makes a chilled furnace recoverable. `Take` is descent and
  // `TakeTop` is the hopper's inverse; a player digging into the shaft wall reaches neither end.
  //
  // A chill sits at the bottom of the shaft by definition, so "recoverable from the top" leaves the one
  // failure recoverability exists for unrecoverable while the gate reads green. Every case here is
  // therefore stated about a span that touches neither end unless it says otherwise.

  [Fact]
  public void A_span_across_a_boundary_returns_the_spanned_units_raceway_first()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 4, 1180f, default);
    column.Push(Burden, 6, 1140f, Fluxed);
    column.Push(Coke, 5, 980f, default);

    // Units [2, 8): the top 2 of the lower coke band, then the lower 4 of the burden.
    List<ChargeSegment> taken = column.TakeSpan(2, 6);

    Assert.Equal(
      [Seg(Coke, 2, 1180f), Seg(Burden, 4, 1140f, Fluxed)],
      taken
    );
    Assert.Equal(9, column.TotalUnits); // 15 - 6
  }

  [Fact]
  public void Both_boundary_segments_keep_their_temperature_across_a_span_take()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 4, 1180f, default);
    column.Push(Burden, 6, 1140f, Fluxed);
    column.Push(Coke, 5, 980f, default);

    List<ChargeSegment> taken = column.TakeSpan(2, 6);

    // The same rule as every other take: heat belongs to the material, not to the quantity. A proportional
    // split would hand back 1180 * 2/4 = 590 C and leave the same behind.
    Assert.Equal(1180f, taken[0].Temperature);
    Assert.Equal(1140f, taken[1].Temperature);
    Assert.Equal(1180f, column.Segments[0].Temperature);
    Assert.Equal(1140f, column.Segments[1].Temperature);
  }

  /// <summary>
  /// <b>Everything above the gap falls, and the band order survives it.</b> That is the whole reason the
  /// break is expressible at all: in the column model settling is one list rebuild with no block writes -
  /// the behaviour that made vanilla's coal piles unusable and drove this redesign.
  /// </summary>
  [Fact]
  public void The_units_above_close_down_over_the_gap_with_band_order_intact()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 4, 1180f, default);
    column.Push(Burden, 6, 1140f, Fluxed);
    column.Push(Coke, 5, 980f, default);
    column.Push(Burden, 3, 900f, Lean);

    column.TakeSpan(4, 6); // exactly the burden band

    // The two coke bands are now adjacent - and they do not merge, because their temperatures are 200 C
    // apart. Settling must not invent heat any more than a split may.
    Assert.Equal(
      [
        Seg(Coke, 4, 1180f),
        Seg(Coke, 5, 980f),
        Seg(Burden, 3, 900f, Lean),
      ],
      column.Segments
    );
  }

  /// <summary>
  /// The other half of settling: when the two sides of the gap meet as the <b>same</b> band they merge,
  /// under exactly <c>Push</c>'s rule. Without it a shaft dug into and refilled would accumulate a seam per
  /// repair, and the save tree would grow one segment at a time with nothing to show for it.
  /// </summary>
  [Fact]
  public void Two_like_bands_meeting_across_the_gap_merge_rather_than_leaving_a_seam()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 4, 1180f, default);
    column.Push(Burden, 6, 1140f, Fluxed);
    column.Push(Coke, 5, 1180f, default); // same material, same mix, same temperature

    column.TakeSpan(4, 6);

    Assert.Equal([Seg(Coke, 9, 1180f)], column.Segments);
  }

  [Fact]
  public void A_span_inside_one_segment_leaves_that_segment_split_around_the_gap_and_rejoined()
  {
    var column = new ChargeColumn();
    column.Push(Burden, 10, 1140f, Fluxed);

    List<ChargeSegment> taken = column.TakeSpan(3, 4);

    Assert.Equal([Seg(Burden, 4, 1140f, Fluxed)], taken);
    // Both halves are the same band at the same temperature, so they rejoin as one - the gap closes
    // completely rather than leaving a 3 + 3 seam where the dig was.
    Assert.Equal([Seg(Burden, 6, 1140f, Fluxed)], column.Segments);
  }

  [Fact]
  public void Two_grades_of_one_material_do_not_merge_across_the_gap()
  {
    var column = new ChargeColumn();
    column.Push(Burden, 4, 1140f, Fluxed);
    column.Push(Coke, 5, 1140f, default);
    column.Push(Burden, 4, 1140f, Lean);

    column.TakeSpan(4, 5); // the coke

    // Same material, same temperature, different grade. Merging would invent a grade the player never
    // made - the same reason the hoppers refuse two grades into one tank.
    Assert.Equal(
      [Seg(Burden, 4, 1140f, Fluxed), Seg(Burden, 4, 1140f, Lean)],
      column.Segments
    );
  }

  #endregion

  #region Taking a mid-span - the clamping contract

  // Clamped exactly as `Take` is, and for the same reason: a break is a live world event, not an
  // authoring mistake. Nothing here may throw.

  [Fact]
  public void A_span_starting_past_the_top_takes_nothing_and_changes_nothing()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 4, 1180f, default);

    Assert.Empty(column.TakeSpan(10, 5));
    Assert.Equal([Seg(Coke, 4, 1180f)], column.Segments);
  }

  [Fact]
  public void A_span_running_off_the_top_returns_only_what_was_there()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 4, 1180f, default);
    column.Push(Burden, 6, 1140f, Fluxed);

    List<ChargeSegment> taken = column.TakeSpan(6, 99);

    Assert.Equal([Seg(Burden, 4, 1140f, Fluxed)], taken);
    Assert.Equal([Seg(Coke, 4, 1180f), Seg(Burden, 2, 1140f, Fluxed)], column.Segments);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-3)]
  public void A_non_positive_span_is_a_no_op(int units)
  {
    var column = new ChargeColumn();
    column.Push(Coke, 4, 1180f, default);

    Assert.Empty(column.TakeSpan(1, units));
    Assert.Equal([Seg(Coke, 4, 1180f)], column.Segments);
  }

  [Fact]
  public void A_negative_start_clamps_to_the_raceway_end_rather_than_shifting_the_span()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 4, 1180f, default);
    column.Push(Burden, 6, 1140f, Fluxed);

    // Clamped, not translated: -2 means "from the bottom", so this takes units [0, 5) and not [0, 3).
    // Shifting the span instead would quietly return three units where five were asked for.
    List<ChargeSegment> taken = column.TakeSpan(-2, 5);

    Assert.Equal([Seg(Coke, 4, 1180f), Seg(Burden, 1, 1140f, Fluxed)], taken);
    Assert.Equal([Seg(Burden, 5, 1140f, Fluxed)], column.Segments);
  }

  [Fact]
  public void A_span_on_an_empty_column_takes_nothing()
  {
    var column = new ChargeColumn();

    Assert.Empty(column.TakeSpan(0, 8));
    Assert.Empty(column.Segments);
  }

  [Fact]
  public void A_span_covering_the_whole_column_empties_it()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 4, 1180f, default);
    column.Push(Burden, 6, 1140f, Fluxed);

    List<ChargeSegment> taken = column.TakeSpan(0, 10);

    Assert.Equal([Seg(Coke, 4, 1180f), Seg(Burden, 6, 1140f, Fluxed)], taken);
    Assert.Empty(column.Segments);
    Assert.Equal(0, column.TotalUnits);
  }

  #endregion

  #region Band order

  [Fact]
  public void Order_survives_a_long_interleaved_sequence_of_pushes_and_takes()
  {
    var column = new ChargeColumn();

    column.Push(Coke, 3, 20f, default); // [coke 3]
    column.Push(Burden, 9, 25f, Fluxed); // [coke 3, burdenF 9]
    column.Take(2); // [coke 1, burdenF 9]
    column.Push(Coke, 4, 30f, default); // burden on top -> a new course starts
    column.Push(Coke, 2, 30f, default); // ...and this joins it
    column.Take(1); // [burdenF 9, coke 6]
    column.Push(Burden, 5, 40f, Lean); // [burdenF 9, coke 6, burdenL 5]
    column.Take(12); // eats burdenF whole and bites 3 off the coke

    Assert.Equal(
      [Seg(Coke, 3, 30f), Seg(Burden, 5, 40f, Lean)],
      column.Segments
    );
    Assert.Equal(8, column.TotalUnits);
    Assert.Equal(Burden, column.TopMaterial);
  }

  [Fact]
  public void The_empty_space_appears_at_the_top_so_a_take_leaves_the_topmost_band_alone()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180f, default);
    column.Push(Burden, 9, 1140f, Fluxed);
    column.Push(Coke, 4, 980f, default);
    ChargeSegment topBefore = column.Segments[^1];

    column.Take(5);

    // The stockline drops; the band at the stockline is bit-for-bit what it was.
    Assert.Equal(topBefore, column.Segments[^1]);
    Assert.Equal(
      [Seg(Burden, 7, 1140f, Fluxed), Seg(Coke, 4, 980f)],
      column.Segments
    );
  }

  #endregion

  #region Merge

  [Fact]
  public void Two_ambient_pushes_of_the_same_material_and_mix_become_one_segment()
  {
    var column = new ChargeColumn();
    column.Push(Burden, 5, 20f, Fluxed);
    column.Push(Burden, 7, 20f, Fluxed);

    // A course laid in two handfuls has to read as one stripe on the shaft wall.
    Assert.Equal([Seg(Burden, 12, 20f, Fluxed)], column.Segments);
  }

  [Fact]
  public void A_cold_push_onto_a_warm_segment_of_the_same_material_stays_two_segments()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 900f, default);
    column.Push(Coke, 3, 20f, default);

    // Merging here would silently hand the new load 460 °C the furnace never produced.
    Assert.Equal([Seg(Coke, 3, 900f), Seg(Coke, 3, 20f)], column.Segments);
  }

  // The next two temperatures are literals on purpose, and they straddle the one-degree window from
  // either side. Writing them as `20f + TempMergeEpsilon` would make them track the constant, so the pair
  // would pass for every possible value of it - the epsilon was measured to survive being inflated 879x
  // with the whole suite green. As written, shrinking the window below 1 °C breaks the first and widening
  // it to 1.5 °C breaks the second.

  [Fact]
  public void A_push_exactly_one_degree_warmer_still_joins_the_top_band()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 20f, default);
    column.Push(Coke, 3, 21f, default);

    // Ambient drifts by about this much across a charging session, so a course laid over several trips
    // still has to read as one stripe. The boundary is inclusive: exactly one degree merges.
    Assert.Equal([Seg(Coke, 6, 20.5f)], column.Segments);
  }

  [Fact]
  public void A_push_one_and_a_half_degrees_warmer_stays_two_segments()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 20f, default);
    column.Push(Coke, 3, 21.5f, default);

    // Half a degree past the window is already a different band. The window absorbs float drift between
    // two loads that arrived at the same ambient; it is not a licence for a warm band to swallow a cold
    // load, which is the thing that would invent heat the furnace never produced.
    Assert.Equal([Seg(Coke, 3, 20f), Seg(Coke, 3, 21.5f)], column.Segments);
  }

  [Fact]
  public void A_merges_temperature_is_the_unit_weighted_average()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 20f, default);
    column.Push(Coke, 9, 20.5f, default); // within the epsilon, so it merges

    ChargeSegment merged = Assert.Single(column.Segments);
    Assert.Equal(12, merged.Units);
    // (20*3 + 20.5*9) / 12. Not the newer value (20.5), not the older (20), and not the
    // un-weighted mean of the two (20.25).
    Assert.Equal(20.375f, merged.Temperature, 4);
  }

  [Fact]
  public void Same_material_with_a_different_mix_does_not_merge()
  {
    var column = new ChargeColumn();
    column.Push(Burden, 5, 20f, Fluxed);
    column.Push(Burden, 7, 20f, Lean);

    // Flux ratio has to survive descent, so two grades cannot be averaged into one band.
    Assert.Equal(
      [Seg(Burden, 5, 20f, Fluxed), Seg(Burden, 7, 20f, Lean)],
      column.Segments
    );
  }

  #endregion

  #region Peek

  [Fact]
  public void LowestUnits_spanning_two_segments_reads_their_mixes_without_mutating()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 4, 1180f, default);
    column.Push(Burden, 6, 1140f, Fluxed);

    List<ChargeSegment> lowest = column.LowestUnits(7);

    Assert.Equal([Seg(Coke, 4, 1180f), Seg(Burden, 3, 1140f, Fluxed)], lowest);
    Assert.Equal(default, lowest[0].Mix);
    Assert.Equal(Fluxed, lowest[1].Mix);
    // The melt condition is evaluated before anything is committed, so the column must be untouched.
    Assert.Equal(
      [Seg(Coke, 4, 1180f), Seg(Burden, 6, 1140f, Fluxed)],
      column.Segments
    );
    Assert.Equal(10, column.TotalUnits);
  }

  [Fact]
  public void LowestUnits_never_reports_more_than_the_column_holds()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 4, 1180f, default);

    Assert.Equal([Seg(Coke, 4, 1180f)], column.LowestUnits(99));
    Assert.Empty(column.LowestUnits(0));
    Assert.Equal(4, column.TotalUnits);
  }

  [Theory]
  [InlineData(0)] // a starved tick asks for nothing
  [InlineData(1)] // one unit off the bottom band
  [InlineData(3)] // exactly the bottom band
  [InlineData(4)] // one unit into the second
  [InlineData(11)] // exactly through the second
  [InlineData(13)] // mid-third, the splitting case
  [InlineData(16)] // the whole column
  [InlineData(25)] // more than it holds
  public void What_a_peek_reports_is_exactly_what_the_take_removes(int units)
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180f, default);
    column.Push(Burden, 8, 1140f, Fluxed);
    column.Push(Charcoal, 5, 980f, default);
    int before = column.TotalUnits;

    // The melt condition peeks, decides, then commits, so the two can never be allowed to disagree. They
    // are not one code path: Take re-walks the segment list with its own removal loop, and that loop is
    // where a later edit could drift away from what the peek promised.
    List<ChargeSegment> peeked = column.LowestUnits(units);
    List<ChargeSegment> taken = column.Take(units);

    Assert.Equal(peeked, taken);

    int reported = 0;
    foreach (ChargeSegment segment in taken)
      reported += segment.Units;

    Assert.Equal(Math.Min(units, before), reported);
    // The column has to shrink by what was handed back, not by what was asked for.
    Assert.Equal(before - reported, column.TotalUnits);
  }

  #endregion

  #region Counter-current

  // The gas leaves the raceway at the flame temperature and rises through everything above it. These pin
  // the operation itself; that the furnace actually invokes it every tick, and that a band therefore
  // arrives at the raceway hotter for having descended a taller shaft, is pinned at the furnace level -
  // the operation cannot tell whether anyone calls it.

  private const float Flame = 1500f;
  private const float Ambient = 20f;

  /// <summary>How much heat a pass put into the column, in unit·°C above <see cref="Ambient"/>.</summary>
  private static float HeatAbove(ChargeColumn column, float floor)
  {
    float total = 0f;
    foreach (ChargeSegment segment in column.Segments)
      total += (segment.Temperature - floor) * segment.Units;
    return total;
  }

  /// <summary>A charged round: coke, burden, coke - three bands only because three loads of the same
  /// material at the same temperature would merge into one, and a profile needs bands to live on.</summary>
  private static ChargeColumn Round(int bands, int units = 8, float temperature = Ambient)
  {
    var column = new ChargeColumn();
    for (int i = 0; i < bands; i++)
      column.Push(i % 2 == 0 ? Coke : Burden, units, temperature, i % 2 == 0 ? default : Fluxed);
    return column;
  }

  [Fact]
  public void An_empty_column_hands_the_gas_straight_back()
  {
    var column = new ChargeColumn();

    // An unburdened furnace ticks through here every second between campaigns.
    Assert.Equal(Flame, column.RiseGasThrough(Flame, 4f, 0.05f, 64));
    Assert.Empty(column.Segments);
  }

  [Fact]
  public void A_band_warms_toward_the_gas_and_never_past_it()
  {
    ChargeColumn column = Round(1);

    column.RiseGasThrough(Flame, 4f, 0.05f, 64);

    ChargeSegment band = Assert.Single(column.Segments);
    Assert.True(band.Temperature > Ambient, "the band absorbed nothing");
    Assert.True(band.Temperature < Flame, "the band ended hotter than the gas that warmed it");
  }

  [Fact]
  public void The_profile_after_a_pass_is_hottest_at_the_raceway_and_cools_upward()
  {
    ChargeColumn column = Round(3);

    column.RiseGasThrough(Flame, 4f, 0.05f, 64);

    // Emergent, not authored: the gas is stripped as it climbs, so each band gets a cooler gas than the
    // one below it. This is the temperature profile the whole counter-current model exists to produce.
    for (int i = 1; i < column.Segments.Count; i++)
      Assert.True(
        column.Segments[i].Temperature < column.Segments[i - 1].Temperature,
        $"band {i} is not cooler than band {i - 1}"
      );
  }

  [Fact]
  public void The_gas_leaving_the_top_is_cooler_than_the_gas_that_entered_the_raceway()
  {
    ChargeColumn column = Round(3);

    float stack = column.RiseGasThrough(Flame, 4f, 0.05f, 64);

    Assert.True(stack < Flame, "the column took nothing out of the gas");
    // Whatever left the column is heat the shaft did not keep, so the two have to add up.
    Assert.True(stack > Ambient);
  }

  [Fact]
  public void A_taller_column_takes_more_out_of_the_same_gas()
  {
    ChargeColumn shortColumn = Round(2);
    ChargeColumn tallColumn = Round(4);

    float shortStack = shortColumn.RiseGasThrough(Flame, 4f, 0.05f, 64);
    float tallStack = tallColumn.RiseGasThrough(Flame, 4f, 0.05f, 64);

    // "A taller shaft is more efficient" as it reads at this layer: the same gas leaves a taller column
    // cooler, because more of it stayed in the shaft. A well-charged tall furnace exhausting cooler is the
    // counter-current model's own signature.
    Assert.True(tallStack < shortStack, $"tall {tallStack} did not out-strip short {shortStack}");
    Assert.True(HeatAbove(tallColumn, Ambient) > HeatAbove(shortColumn, Ambient));

    // What is not a single-pass property, and must not be asserted here: that a taller shaft's burden
    // arrives at the raceway hotter. Band 0 sees the same gas at the same capacity whatever stands above
    // it, so its temperature after one pass is identical in both columns. The taller shaft wins because a
    // parcel charged at its stockline waits through more passes before descending to the raceway - a
    // descent-time integral, pinned on the furnace, not on the column.
    Assert.Equal(
      shortColumn.Segments[0].Temperature,
      tallColumn.Segments[0].Temperature,
      4
    );
  }

  [Fact]
  public void Splitting_a_band_in_two_changes_neither_the_gas_that_leaves_nor_the_heat_absorbed()
  {
    var whole = new ChargeColumn();
    whole.Push(Coke, 8, Ambient, default);

    var split = new ChargeColumn();
    split.Push(Coke, 4, Ambient, default);
    // Same material and temperature would merge, so the halves are told apart by their mix - the point is
    // the arithmetic, and two bands of 4 is what a mid-band Take leaves behind.
    split.Push(Burden, 4, Ambient, Fluxed);

    var wholeTwo = new ChargeColumn();
    wholeTwo.Push(Burden, 8, Ambient, Fluxed);

    float wholeStack = whole.RiseGasThrough(Flame, 4f, 0.05f, 64);
    float splitStack = split.RiseGasThrough(Flame, 4f, 0.05f, 64);
    float wholeTwoStack = wholeTwo.RiseGasThrough(Flame, 4f, 0.05f, 64);

    // This is why the transfer compounds per unit and not per band. The segment list is cut wherever
    // charging and consumption happened to leave it - and Coalesce re-cuts it every tick - so a rule that
    // counted bands would make the shaft's efficiency depend on the player's charging rhythm.
    Assert.True(
      Math.Abs(splitStack - wholeStack) < 0.5f,
      $"split {splitStack} vs whole {wholeStack}"
    );
    Assert.True(
      Math.Abs(HeatAbove(split, Ambient) - HeatAbove(whole, Ambient)) < 5f,
      "the split column absorbed a different amount of heat"
    );
    // The two whole columns differ only in material, so they must agree exactly - that pins the comparison
    // above against the possibility that both readings moved.
    Assert.Equal(wholeStack, wholeTwoStack, 4);
  }

  [Fact]
  public void Halving_the_gas_and_passing_twice_lands_where_one_full_pass_did()
  {
    ChargeColumn once = Round(1);
    ChargeColumn twice = Round(1);

    once.RiseGasThrough(Flame, 1f, 0.05f, 64);
    twice.RiseGasThrough(Flame, 0.5f, 0.05f, 64);
    twice.RiseGasThrough(Flame, 0.5f, 0.05f, 64);

    // MaxAwayCatchupSteps replays up to 600 one-second sub-ticks at chunk load, and the production tick
    // clamps dt at 2x. A warming rule that is not dt-proportional either melts a shaft on reload or leaves
    // an away furnace cold. dt rides on the capacity - how much gas passed - not on a per-metre rate.
    float full = once.Segments[0].Temperature - Ambient;
    float halved = twice.Segments[0].Temperature - Ambient;
    Assert.True(
      Math.Abs(full - halved) < full * 0.02f,
      $"one pass rose {full} °C, two half passes rose {halved} °C"
    );
    // Not exact, and it cannot be: an explicit step evaluates the driving force at the start of the step,
    // so a finer step sees the band already warmed and delivers slightly less. Second order in
    // capacity/units, which is small in every shipped calibration.
    Assert.True(halved < full);
  }

  [Fact]
  public void Warming_moves_only_the_temperature()
  {
    ChargeColumn column = Round(3);
    List<ChargeSegment> before = [.. column.Segments];

    column.RiseGasThrough(Flame, 4f, 0.05f, 64);

    Assert.Equal(before.Count, column.Segments.Count);
    for (int i = 0; i < before.Count; i++)
    {
      // Order, material, quantity and mix all have to survive descent untouched - the mix in particular,
      // because it is what the melt condition reads when the band finally reaches the raceway.
      Assert.Equal(before[i].Material, column.Segments[i].Material);
      Assert.Equal(before[i].Units, column.Segments[i].Units);
      Assert.Equal(before[i].Mix, column.Segments[i].Mix);
      Assert.NotEqual(before[i].Temperature, column.Segments[i].Temperature);
    }
  }

  [Theory]
  [InlineData(0f, 0.05f)] // no gas passed - an unlit furnace
  [InlineData(-4f, 0.05f)] // a negative capacity would run the exchange backwards
  [InlineData(float.NaN, 0.05f)] // a NaN admitted once poisons every later compare
  [InlineData(4f, 0f)] // a shaft that transfers nothing
  [InlineData(4f, -0.05f)]
  [InlineData(4f, float.NaN)]
  public void A_pass_with_nothing_to_carry_it_leaves_the_column_exactly_as_it_was(
    float capacity,
    float transfer
  )
  {
    ChargeColumn column = Round(2);
    List<ChargeSegment> before = [.. column.Segments];

    float stack = column.RiseGasThrough(Flame, capacity, transfer, 64);

    Assert.Equal(Flame, stack);
    Assert.Equal(before, column.Segments);
  }

  [Fact]
  public void A_transfer_above_one_saturates_rather_than_running_backwards()
  {
    ChargeColumn column = Round(1);

    column.RiseGasThrough(Flame, 4f, 5f, 64);

    // (1-f)^U goes negative and then oscillates in sign for f > 1, so the clamp is what stops a band being
    // cooled by hot gas on an odd unit count.
    ChargeSegment band = Assert.Single(column.Segments);
    Assert.True(band.Temperature > Ambient);
    Assert.True(band.Temperature < Flame);
  }

  [Fact]
  public void A_band_never_ends_hotter_than_the_gas_and_the_charge_mixed()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 1, Ambient, default);

    // A single unit of charge under an enormous gas flow: the unclamped rise here is ~740 000 °C.
    float stack = column.RiseGasThrough(Flame, 1000f, 0.5f, 64);

    ChargeSegment band = Assert.Single(column.Segments);
    Assert.True(band.Temperature < Flame, $"band reached {band.Temperature} °C under a {Flame} °C gas");
    Assert.True(stack >= band.Temperature, "the gas left colder than the band it warmed");
  }

  [Fact]
  public void The_column_gains_exactly_the_heat_the_gas_lost()
  {
    ChargeColumn column = Round(4);
    float before = HeatAbove(column, 0f);

    float stack = column.RiseGasThrough(Flame, 4f, 0.05f, 64);

    // The guard against inventing heat. An earlier draft took an ambient floor and clamped each band up
    // to it, which reads harmless and is not: the shaft gains warmth the gas never handed over, so a
    // column can arrive at the raceway hotter than the coke charged with it could ever have made it.
    // It also pins that a band saved before bands carried heat (FromTree defaults to 0 °C) warms from
    // 0 °C rather than being quietly topped up to ambient.
    float gained = HeatAbove(column, 0f) - before;
    float lost = 4f * (Flame - stack);
    Assert.True(
      Math.Abs(gained - lost) < 1f,
      $"the column gained {gained} unit·°C while the gas lost {lost}"
    );
  }

  [Fact]
  public void Gas_colder_than_the_charge_leaves_the_column_alone()
  {
    ChargeColumn column = Round(2, temperature: 900f);
    List<ChargeSegment> before = [.. column.Segments];

    float stack = column.RiseGasThrough(400f, 4f, 0.05f, 64);

    // Cooling is not this door's job - a shaft that stops burning loses its heat to the world, not up its
    // own stack. Warming only, so a cold gas is a no-op rather than a reverse pass.
    Assert.Equal(before, column.Segments);
    Assert.Equal(400f, stack);
  }

  [Fact]
  public void A_cold_band_above_a_band_hotter_than_the_gas_is_still_reached()
  {
    var column = new ChargeColumn();
    // Hotter than the gas arriving: the blast faltered, or the coke fraction fell, so the flame is cooler
    // than charge still carrying heat from a hotter stretch of the campaign. Ordinary, not pathological.
    column.Push(Coke, 8, Flame + 100f, default);
    column.Push(Burden, 8, Ambient, Fluxed);

    column.RiseGasThrough(Flame, 4f, 0.05f, 64);

    // The walk must pass a band it cannot warm, not stop at it. Ending the walk there leaves everything
    // above it permanently cold - and the band that triggers it is at the raceway, so a single stretch of
    // weak blast would freeze the whole shaft above for the rest of the campaign.
    Assert.Equal(Flame + 100f, column.Segments[0].Temperature);
    Assert.True(column.Segments[1].Temperature > Ambient);
  }

  [Fact]
  public void Warming_that_brings_two_bands_together_re_merges_them_on_the_spot()
  {
    var column = new ChargeColumn();
    column.Push(Burden, 4, 1499f, Fluxed);
    column.Push(Burden, 4, 1497.5f, Fluxed); // 1.5 °C apart, so Push refused to merge them

    column.RiseGasThrough(Flame, 20f, 0.5f, 64);

    // At the raceway everything saturates toward the same flame temperature, so bands the player laid
    // separately come back together - and the pass lays every piece down through Push's own merge rule,
    // so they fold back on the spot rather than waiting for a separate tidy-up someone could forget.
    ChargeSegment merged = Assert.Single(column.Segments);
    Assert.Equal(8, merged.Units);
    Assert.Equal(Fluxed, merged.Mix);
  }

  [Fact]
  public void The_pass_never_merges_across_a_material_or_a_grade()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 4, 900f, default);
    column.Push(Burden, 4, 900f, Fluxed);
    column.Push(Burden, 4, 900f, Lean);

    // Same gas, same temperature, adjacent - and still three bands, because the flux ratio has to survive
    // descent for the melt to read the grade that actually arrives.
    column.RiseGasThrough(1000f, 4f, 0.05f, 64);

    Assert.Equal(3, column.Segments.Count);
    Assert.Equal(Coke, column.Segments[0].Material);
    Assert.Equal(Fluxed, column.Segments[1].Mix);
    Assert.Equal(Lean, column.Segments[2].Mix);
  }

  [Fact]
  public void A_gradient_the_gas_produced_survives_the_pass_that_produced_it()
  {
    ChargeColumn column = Round(4);

    column.RiseGasThrough(Flame, 4f, 0.05f, 64);

    // The merge must not eat the model's own output. Every band came out at a different temperature, so
    // all four have to still be there - a merge rule loose enough to fold these would flatten the profile
    // the whole counter-current model exists to produce.
    Assert.Equal(4, column.Segments.Count);
  }

  [Fact]
  public void A_tall_band_develops_a_gradient_inside_itself()
  {
    var column = new ChargeColumn();
    column.Push(Burden, 96, Ambient, Fluxed); // charged in one go: one band, three blocks tall

    column.RiseGasThrough(Flame, 4f, 0.05f, 32);

    // Without the resolution split this is a single 96-unit body that warms uniformly, so a freshly
    // charged shaft has no temperature profile at all and nothing melts until the entire column reaches
    // the melting point at once. Found by a scenario, not by review: the cold furnace charges every column
    // in one call, so every column was exactly this case.
    Assert.True(column.Segments.Count > 1, "the band warmed as one lump");
    Assert.Equal(96, column.TotalUnits);
    for (int i = 1; i < column.Segments.Count; i++)
      Assert.True(column.Segments[i].Temperature < column.Segments[i - 1].Temperature);
  }

  [Fact]
  public void Charging_a_lit_shaft_all_campaign_does_not_leave_a_band_per_drip()
  {
    var column = new ChargeColumn();
    const int ticks = 240;
    for (int tick = 0; tick < ticks; tick++)
    {
      column.Push(Burden, 2, Ambient, Fluxed); // the hopper drips every tick, onto a shaft under gas
      column.RiseGasThrough(Flame, 2f, 0.05f, 64);
    }

    // A silent save leak, not a performance note: every drip that fails to merge is four more parallel
    // array entries per column, on every column, riding the chunk save for the length of a campaign - and
    // nothing about it is visible until the save is measured. The leak is exact: laying the pass's
    // pieces down without the merge rule leaves 240 bands from 240 drips - one apiece - because Push
    // merges onto the top band only within a degree and warming moves that band out of reach on the very
    // first tick. It leaves 151.
    // What claws it back is the model's own doing: once the shaft is deep enough to strip the gas before
    // it reaches the stockline, the top band stops warming by a whole degree in a tick and fresh drips
    // merge onto it again. This column is never consumed, which is the worst case on purpose - descent
    // retires bands off the bottom at the same time and is what bounds a real campaign.
    Assert.True(
      column.Segments.Count < ticks * 2 / 3,
      $"{ticks} drips left {column.Segments.Count} bands"
    );
    Assert.Equal(ticks * 2, column.TotalUnits);
  }

  #endregion

  #region Read-only surface

  [Fact]
  public void The_exposed_segments_cannot_be_mutated_from_outside()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180f, default);

    // Handing out the backing list behind an interface leaves a cast that reaches straight into the
    // column - and a later phase wants to warm a descending band, so this is the tempting illegitimate
    // route to it. When that arrives it has to come as an API, not as a cast.
    var asList = column.Segments as IList<ChargeSegment>;
    Assert.NotNull(asList);
    Assert.True(asList!.IsReadOnly);
    Assert.Throws<NotSupportedException>(() => asList.Add(Seg(Burden, 99, 5f)));
    Assert.Equal(3, column.TotalUnits);
  }

  #endregion

  #region Top material

  [Fact]
  public void Top_material_reports_the_topmost_band_and_null_when_empty()
  {
    var column = new ChargeColumn();
    Assert.Null(column.TopMaterial);

    column.Push(Coke, 3, 20f, default);
    Assert.Equal(Coke, column.TopMaterial);

    // The band-order rule reads this to decide whether an incoming load starts a fresh course.
    column.Push(Burden, 9, 20f, Fluxed);
    Assert.Equal(Burden, column.TopMaterial);

    column.Take(11); // the coke and most of the burden
    Assert.Equal(Burden, column.TopMaterial);

    column.Take(1);
    Assert.Null(column.TopMaterial);
  }

  #endregion

  #region Materialisation

  // What the blocks over a column draw. The band size arrives as a parameter, never off IwexValues, so
  // every case below is a fact about the rule rather than about the shipped tuning.

  /// <summary>
  /// The column charged for the band tests: 32 u coke, then 32 u of one burden grade, then 64 u of
  /// another - 128 u, which at the shipped 8 u a band is exactly one block. The two grades are the same
  /// <em>item</em> at different flux ratios, so a rule that read only the material would merge them.
  /// </summary>
  private static ChargeColumn Charged()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 32, 1180f, default);
    column.Push(Burden, 32, 1140f, Fluxed);
    column.Push(Burden, 64, 1100f, Lean);
    return column;
  }

  [Fact]
  public void An_ore_pile_is_thirty_two_ITEMS_and_a_remelt_pile_is_three_thousand_UNITS()
  {
    // A band is measured in items, not units, and there are two kinds of pile - see
    // docs/design/layered-charge.md.
    // An ore charge (blast furnace) is 16 bands x 2 items = 32 items of coke and burden. A remelt charge
    // (cupola) is metal units up to a cap, because a 5 u bit, a 25 u chunk and a 375 u pig all have to
    // fit the same pile. Read only - nothing in the suite edits either key.
    Assert.Equal(2, IwexValues.ChargeItemsPerBand);
    Assert.Equal(
      32,
      IwexValues.ChargeItemsPerBand * ChargeColumn.BandsPerBlock
    );
    Assert.Equal(3000, IwexValues.CupolaChargeMetalUnitsPerBlock);

    // The remelt quantum is 3000/16 = 187.5 units a band - not an integer, which is exactly why the
    // column takes a per-block figure and derives band boundaries by multiplying before dividing. A
    // per-band constant could not express it, and rounding one would drift the top band of every pile.
    Assert.NotEqual(
      0,
      IwexValues.CupolaChargeMetalUnitsPerBlock % ChargeColumn.BandsPerBlock
    );

    // Deliberately no relation to any hopper pile cap: that cap was vanilla's coal-pile mesh limit,
    // never a statement about how much charge a block holds. A column's ceiling is its own cell count
    // times the quantum above, which nothing else has an opinion about.
  }

  [Fact]
  public void A_column_is_one_block_tall_up_to_a_full_block_and_two_one_unit_past_it()
  {
    Assert.Equal(0, ChargeColumn.BlocksTall(0, 128));
    Assert.Equal(1, ChargeColumn.BlocksTall(1, 128));
    // 128 u is a full block - the boundary Phase 3 places and removes a chargepile on.
    Assert.Equal(1, ChargeColumn.BlocksTall(128, 128));
    Assert.Equal(2, ChargeColumn.BlocksTall(129, 128));
    Assert.Equal(2, ChargeColumn.BlocksTall(256, 128));
    Assert.Equal(3, ChargeColumn.BlocksTall(257, 128));
  }

  [Fact]
  public void A_course_straddling_a_block_boundary_draws_as_one_continuous_stripe()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 64, 1180f, default); // bands 0-7
    column.Push(Burden, 128, 1140f, Fluxed); // bands 8-23, straight across the boundary at band 16

    List<ChargeBandRun> lower = column.BandsAt(0, 128);
    List<ChargeBandRun> upper = column.BandsAt(1, 128);

    Assert.Equal(2, ChargeColumn.BlocksTall(192, 128));
    Assert.Equal(
      [new ChargeBandRun(Coke, default, 8), new ChargeBandRun(Burden, Fluxed, 8)],
      lower
    );
    Assert.Equal([new ChargeBandRun(Burden, Fluxed, 8)], upper);

    // The visual the whole model is aiming at: the top of the lower block and the bottom of the upper
    // one are the same material at the same grade, so the course reads as one stripe in section. Snapping
    // bands to block boundaries would have cut the 128 u course into two 64 u ones with a seam.
    Assert.Equal(lower[^1].Material, upper[0].Material);
    Assert.Equal(lower[^1].Mix, upper[0].Mix);
    Assert.Equal(16, lower[^1].Bands + upper[0].Bands);
  }

  [Fact]
  public void Each_band_carries_the_material_and_the_mix_of_the_segment_it_came_from()
  {
    // The pile renderer textures per band by material and grade, so the flux ratio has to survive the walk: the
    // two burden runs are the same item and must not collapse into one twelve-band stripe.
    Assert.Equal(
      [
        new ChargeBandRun(Coke, default, 4),
        new ChargeBandRun(Burden, Fluxed, 4),
        new ChargeBandRun(Burden, Lean, 8),
      ],
      Charged().BandsAt(0, 128)
    );
  }

  [Fact]
  public void The_rule_holds_at_a_non_default_band_size()
  {
    ChargeColumn column = Charged();

    // Halving the band size doubles the height of the same charge and moves the boundary into the middle
    // of the fluxed course - so this cannot pass by echoing the 8 u answer.
    Assert.Equal(2, ChargeColumn.BlocksTall(128, 64));
    Assert.Equal(
      [
        new ChargeBandRun(Coke, default, 8),
        new ChargeBandRun(Burden, Fluxed, 8),
      ],
      column.BandsAt(0, 64)
    );
    Assert.Equal([new ChargeBandRun(Burden, Lean, 16)], column.BandsAt(1, 64));
  }

  [Fact]
  public void A_band_split_between_two_segments_draws_the_one_filling_most_of_it()
  {
    var minority = new ChargeColumn();
    minority.Push(Coke, 3, 1180f, default); // 3 of the first band's 8 units
    minority.Push(Burden, 13, 1140f, Fluxed);

    // A sliver thinner than half a band is below the wall's resolution; drawing it as a full band would
    // show a course of coke the furnace barely has.
    Assert.Equal([new ChargeBandRun(Burden, Fluxed, 2)], minority.BandsAt(0, 128));

    var tied = new ChargeColumn();
    tied.Push(Coke, 4, 1180f, default); // exactly half the first band
    tied.Push(Burden, 12, 1140f, Fluxed);

    // An exact tie goes to the lower segment, so the rule is deterministic rather than dependent on
    // which side the walk happened to see last.
    Assert.Equal(
      [new ChargeBandRun(Coke, default, 1), new ChargeBandRun(Burden, Fluxed, 1)],
      tied.BandsAt(0, 128)
    );
  }

  [Fact]
  public void The_top_block_draws_only_the_bands_that_are_actually_there()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 140, 1180f, default);

    Assert.Equal(2, ChargeColumn.BlocksTall(140, 128));
    Assert.Equal([new ChargeBandRun(Coke, default, 16)], column.BandsAt(0, 128));
    // 12 u over the boundary: one full band and one part-filled one, which still needs drawing.
    Assert.Equal([new ChargeBandRun(Coke, default, 2)], column.BandsAt(1, 128));
    // Nothing above the stockline.
    Assert.Empty(column.BandsAt(2, 128));
    Assert.Empty(new ChargeColumn().BandsAt(0, 128));
  }

  [Fact]
  public void A_block_index_far_above_the_column_draws_nothing_rather_than_wrapping_into_it()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 1000, 1180f, default);

    // The renderer gets its block index by subtracting two world Y values, so a stale one is garbage
    // rather than "a bit too high" - and the band arithmetic used to overflow int at exactly 2^24 and
    // land negative, i.e. under the `blockIndex < 0` guard rather than over it. The wrap drew a full
    // 16-band block of the bottom segment's material floating above the stockline.
    Assert.Empty(column.BandsAt(99, 128)); // above the top, no overflow involved
    Assert.Empty(column.BandsAt(16_777_216, 128)); // int.MaxValue / (16 * 8) - the exact wrap point
    Assert.Empty(column.BandsAt(int.MaxValue, 128));
    Assert.Empty(column.BandsAt(int.MinValue, 128));

    // The band size scales where the wrap would have been, so pinning one band size would leave the
    // trap live at every other. BlocksTall has to survive the same widening: at a band size past
    // 134 M its own units-per-block multiply overflows too, and the two must agree about the top block.
    Assert.Empty(column.BandsAt(int.MaxValue, 16));
    Assert.Equal(1, ChargeColumn.BlocksTall(1000, 200_000_000));
    Assert.Empty(column.BandsAt(1, 200_000_000));
    Assert.Equal(
      [new ChargeBandRun(Coke, default, 1)],
      column.BandsAt(0, 200_000_000)
    );
  }

  #endregion

  #region Persistence

  [Fact]
  public void A_multi_segment_column_round_trips_exactly_through_a_serialized_tree()
  {
    var column = new ChargeColumn();
    column.Push(Coke, 3, 1180.5f, default);
    column.Push(Burden, 9, 1140.25f, Fluxed);
    column.Push(Coke, 3, 980f, default);
    column.Push(Burden, 11, 940.125f, Lean);

    var tree = new TreeAttribute();
    column.ToTree(tree);

    // Through the bytes, not back out of the same object: a save writes the tree, so an attribute type
    // that cannot survive that would still pass an in-memory round-trip and lose the charge in the world.
    var restored = new ChargeColumn();
    restored.FromTree(Serialized(tree));

    Assert.Equal(column.Segments, restored.Segments);
    Assert.Equal(column.TotalUnits, restored.TotalUnits);
    Assert.Equal(Burden, restored.TopMaterial);
    // Spot-check the fields a parallel-array write could silently transpose.
    Assert.Equal(Lean, restored.Segments[3].Mix);
    Assert.Equal(default, restored.Segments[2].Mix);
    Assert.Equal(940.125f, restored.Segments[3].Temperature);
    Assert.Equal(1180.5f, restored.Segments[0].Temperature);
  }

  [Fact]
  public void An_emptied_column_round_trips_as_empty_and_clears_what_was_loaded()
  {
    var loaded = new ChargeColumn();
    loaded.Push(Burden, 9, 1140f, Fluxed);

    var tree = new TreeAttribute();
    loaded.ToTree(tree);
    loaded.Take(9);
    loaded.ToTree(tree); // a column drawn down to nothing must not reload its old charge

    var restored = new ChargeColumn();
    restored.Push(Coke, 5, 20f, default); // pre-existing content is replaced, not appended to
    restored.FromTree(Serialized(tree));

    Assert.Empty(restored.Segments);
    Assert.Equal(0, restored.TotalUnits);
  }

  [Fact]
  public void A_tree_carrying_a_non_positive_band_drops_it_rather_than_over_taking()
  {
    var source = new ChargeColumn();
    source.Push(Coke, 5, 20f, default);
    source.Push(Burden, 10, 1140f, Fluxed);
    source.Push(Charcoal, 4, 980f, default);

    var tree = new TreeAttribute();
    source.ToTree(tree);
    // A hand-edited, truncated or version-skewed save - no push can produce either of these.
    ((IntArrayAttribute)tree["segUnits"]).value[0] = -5;
    ((IntArrayAttribute)tree["segUnits"]).value[2] = 0;

    var column = new ChargeColumn();
    column.FromTree(Serialized(tree));

    // The surviving band keeps its own temperature and mix, so dropping a band does not shift the reads
    // off the parallel arrays.
    Assert.Equal([Seg(Burden, 10, 1140f, Fluxed)], column.Segments);
    Assert.Equal(10, column.TotalUnits);

    // The negative count is the dangerous one: loaded as-is it runs the take clamp backwards, so the
    // raceway is handed 3 u while 8 u quietly leave the column.
    Assert.Equal([Seg(Burden, 3, 1140f, Fluxed)], column.Take(3));
    Assert.Equal(7, column.TotalUnits);
  }

  [Fact]
  public void A_tree_carrying_a_band_with_no_material_drops_it()
  {
    var source = new ChargeColumn();
    source.Push(Coke, 5, 20f, default);
    source.Push(Burden, 10, 1140f, Fluxed);

    var tree = new TreeAttribute();
    source.ToTree(tree);
    ((StringArrayAttribute)tree["segMaterials"]).value[0] = null!;

    var column = new ChargeColumn();
    column.FromTree(tree);

    // Mirror of the push guard: a band the save cannot describe is dropped on read too, or it would be
    // written back out unwritable and take the next chunk save with it.
    Assert.Equal([Seg(Burden, 10, 1140f, Fluxed)], column.Segments);
  }

  [Fact]
  public void A_tree_with_no_column_state_reads_as_an_empty_column()
  {
    var column = new ChargeColumn();
    column.Push(Burden, 9, 1140f, Fluxed);

    column.FromTree(new TreeAttribute());

    Assert.Empty(column.Segments);
  }

  #endregion
}
