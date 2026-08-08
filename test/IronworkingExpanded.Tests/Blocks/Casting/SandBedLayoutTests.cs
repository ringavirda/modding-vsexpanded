using System.Collections.Generic;
using System.Linq;
using IronworkingExpanded.BlockStructures.Casting;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The sand bed's carved surface: which shape element draws each slot, and how that composes with the
/// construction behaviour's own element list. Element names are string literals against art, so a typo or
/// a re-export that re-rolls an auto-name yields a missing chunk of bed rather than an exception;
/// <see cref="Every_element_this_can_emit_exists_in_the_shipped_shape"/> checks them against the shipped
/// shape.
/// </summary>
public class SandBedLayoutTests {
  private static List<BedSlotState> AllSand() =>
    [.. Enumerable.Repeat(BedSlotState.Sand, SandBedLayout.Slots.Length)];

  #region Slot reachability (every slot must be clickable)

  // The basin is the one slot at footprint offset (0,0), the offset FillerSlots excludes, so it is not
  // reachable through the filler interaction path that carries every other slot's clicks. It is served by
  // the principal's own OnBlockInteractStart instead.

  [Fact]
  public void Exactly_one_slot_is_not_a_filler_and_it_is_the_basin() {
    var notFillers = SandBedLayout
      .Slots.Except(SandBedLayout.FillerSlots)
      .ToList();

    // If this grows past one, the principal-click route below stops covering the remainder and those slots
    // become unclickable without an error.
    BedSlot only = Assert.Single(notFillers);
    Assert.Equal(new BedSlot(SandBedLayout.FirstRow, BedSlotSide.Centre), only);
    Assert.Equal((0, 0), SandBedLayout.OffsetOf(only));
  }

  [Fact]
  public void Every_slot_is_reachable_by_a_click_somewhere() {
    // The union of the filler path and the principal's own cell must be the whole bed. A slot in neither
    // set still persists, renders and holds metal, but can never be touched.
    var reachable = SandBedLayout.FillerSlots.ToHashSet();
    reachable.Add(SandBedLayout.SlotAt(0, 0)!.Value);

    Assert.Equal(SandBedLayout.Slots.ToHashSet(), reachable);
  }

  [Fact]
  public void The_principals_offset_resolves_back_to_a_real_slot() {
    // The principal route looks its slot up by offset (0,0) rather than being handed one, so the lookup
    // must resolve; a null makes the principal click a no-op.
    Assert.NotNull(SandBedLayout.SlotAt(0, 0));
  }

  #endregion

  #region Slot inventory

  [Fact]
  public void The_bed_is_four_rows_of_a_runner_between_two_molds() {
    Assert.Equal(12, SandBedLayout.Slots.Length);
    Assert.Equal(4, SandBedLayout.Slots.Count(s => !s.IsMold));
    Assert.Equal(8, SandBedLayout.Slots.Count(s => s.IsMold));
  }

  [Fact]
  public void Every_row_carries_molds_including_the_basins_own() {
    // Row 1 is no exception: the cells flanking the basin are molds like any other row's.
    for (int row = SandBedLayout.FirstRow; row <= SandBedLayout.LastRow; row++)
      Assert.Equal(2, SandBedLayout.Slots.Count(s => s.Row == row && s.IsMold));
  }

  [Fact]
  public void Slot_order_is_stable_because_states_persist_by_index() {
    // Slot states persist by index, so the order is part of the save format: reordering rewrites every
    // saved bed.
    Assert.Equal(new BedSlot(1, BedSlotSide.West), SandBedLayout.Slots[0]);
    Assert.Equal(new BedSlot(1, BedSlotSide.Centre), SandBedLayout.Slots[1]);
    Assert.Equal(new BedSlot(1, BedSlotSide.East), SandBedLayout.Slots[2]);
    Assert.Equal(new BedSlot(4, BedSlotSide.East), SandBedLayout.Slots[11]);
  }

  #endregion

  #region Slot <-> footprint offsets

  [Fact]
  public void The_first_rows_runner_is_the_principals_own_cell() {
    // It is the pour basin, and the one slot with no filler of its own.
    Assert.Equal(
      (0, 0),
      SandBedLayout.OffsetOf(new BedSlot(1, BedSlotSide.Centre))
    );
    Assert.DoesNotContain(
      new BedSlot(1, BedSlotSide.Centre),
      SandBedLayout.FillerSlots
    );
    Assert.Equal(
      SandBedLayout.Slots.Length - 1,
      SandBedLayout.FillerSlots.Count()
    );
  }

  [Fact]
  public void Offsets_and_slots_are_a_round_trip() {
    // The footprint is generated from these and clicks are mapped back through them, so a one-way error
    // puts a carve in the wrong hole.
    foreach (BedSlot slot in SandBedLayout.Slots) {
      (int dx, int dz) = SandBedLayout.OffsetOf(slot);
      Assert.Equal(slot, SandBedLayout.SlotAt(dx, dz));
    }
  }

  [Fact]
  public void Slots_occupy_distinct_cells() {
    Assert.Equal(
      SandBedLayout.Slots.Length,
      SandBedLayout.Slots.Select(SandBedLayout.OffsetOf).Distinct().Count()
    );
  }

  [Fact]
  public void Every_footprint_cell_is_a_slot() {
    // No passive cells: 3 wide x 4 deep is 12 cells and 12 slots, so a click anywhere on the bed carves or
    // harvests.
    Assert.Equal(3 * SandBedLayout.Rows, SandBedLayout.Slots.Length);
  }

  [Fact]
  public void Cells_off_the_bed_are_not_slots() {
    Assert.Null(SandBedLayout.SlotAt(2, 1)); // beyond the flanks
    Assert.Null(SandBedLayout.SlotAt(0, -1)); // in front of the basin
    Assert.Null(SandBedLayout.SlotAt(0, SandBedLayout.Rows)); // past the last row
  }

  #endregion

  #region What a slot will accept

  [Fact]
  public void A_runner_slot_takes_a_runner_and_never_a_mold() {
    var spine = new BedSlot(2, BedSlotSide.Centre);
    Assert.True(spine.Accepts(BedSlotState.Runner));
    Assert.True(spine.Accepts(BedSlotState.Sand));
    Assert.False(spine.Accepts(BedSlotState.Mold));
  }

  [Fact]
  public void A_mold_slot_takes_a_mold_and_never_a_runner() {
    var flank = new BedSlot(2, BedSlotSide.West);
    Assert.True(flank.Accepts(BedSlotState.Mold));
    Assert.True(flank.Accepts(BedSlotState.Sand));
    Assert.False(flank.Accepts(BedSlotState.Runner));
  }

  [Fact]
  public void An_impossible_state_renders_as_sand_rather_than_throwing() {
    // A persisted value the slot cannot hold renders as uncarved rather than crashing a chunk render.
    Assert.Equal(
      SandBedLayout.ElementFor(
        new BedSlot(1, BedSlotSide.Centre),
        BedSlotState.Sand
      ),
      SandBedLayout.ElementFor(
        new BedSlot(1, BedSlotSide.Centre),
        BedSlotState.Mold
      )
    );
  }

  #endregion

  #region Cavity size

  [Theory]
  [InlineData(1, 2)]
  [InlineData(2, 3)]
  [InlineData(3, 3)]
  [InlineData(4, 2)]
  public void The_end_rows_are_shorter_than_the_middle_ones(
    int row,
    int impressions
  ) {
    // Straight off the art: rows 1 and 4 give ground to the basin's shoulders and the back wall, so they
    // hold two impressions where the middle rows hold three.
    Assert.Equal(impressions, SandBedLayout.ImpressionsPerMold(row));
  }

  [Fact]
  public void A_fully_carved_bed_is_twenty_castings() {
    // 2+3+3+2 a side, both sides. Pinned directly rather than derived from the row table, because it is
    // the number a heat is planned around.
    Assert.Equal(20, SandBedLayout.BedCapacity);
  }

  [Fact]
  public void A_molds_cavity_is_its_rows_impressions_at_one_casting_each() {
    foreach (BedSlot slot in SandBedLayout.Slots.Where(s => s.IsMold))
      Assert.Equal(
        SandBedLayout.ImpressionsPerMold(slot.Row)
          * IronworkingExpanded.Items.ItemPig.PigUnits,
        SandBedLayout.CapacityOf(slot, BedSlotState.Mold)
      );
  }

  [Fact]
  public void A_middle_row_mold_swallows_more_per_pour_than_an_end_row_one() {
    Assert.True(
      SandBedLayout.CapacityOf(
        new BedSlot(2, BedSlotSide.West),
        BedSlotState.Mold
      )
        > SandBedLayout.CapacityOf(
          new BedSlot(1, BedSlotSide.West),
          BedSlotState.Mold
        )
    );
  }

  [Fact]
  public void A_brick_and_a_pig_share_a_cavity_so_neither_pour_strands_a_remainder() {
    // One mold shape casts both, so the cavity must divide exactly by either unit. Otherwise a slag pour
    // into a three-impression row yields two bricks and strands the third's worth.
    Assert.Equal(
      IronworkingExpanded.Items.ItemPig.PigUnits,
      IronworkingExpanded.Items.SlagItemDefinitions.SlagBrickUnits
    );
    foreach (BedSlot slot in SandBedLayout.Slots.Where(s => s.IsMold))
      Assert.Equal(
        0,
        SandBedLayout.CapacityOf(slot, BedSlotState.Mold)
          % IronworkingExpanded.Items.SlagItemDefinitions.SlagBrickUnits
      );
  }

  [Fact]
  public void Plain_sand_has_no_cavity_at_all() {
    // Zero is the signal to drop the override entirely rather than size a cavity that is not there.
    var flank = new BedSlot(2, BedSlotSide.West);
    var spine = new BedSlot(2, BedSlotSide.Centre);
    Assert.Equal(0, SandBedLayout.CapacityOf(flank, BedSlotState.Sand));
    Assert.True(SandBedLayout.CapacityOf(spine, BedSlotState.Runner) > 0); // a conduit still holds metal
  }

  #endregion

  #region Element naming

  [Theory]
  [InlineData(1, BedSlotSide.Centre, BedSlotState.Runner, "RunnerCenter1")]
  [InlineData(1, BedSlotSide.Centre, BedSlotState.Sand, "RunnerCenter1Full")]
  [InlineData(2, BedSlotSide.Centre, BedSlotState.Runner, "RunnerCenter2")]
  [InlineData(3, BedSlotSide.West, BedSlotState.Mold, "Mold3W")]
  [InlineData(3, BedSlotSide.West, BedSlotState.Sand, "Mold3WFull")]
  [InlineData(4, BedSlotSide.East, BedSlotState.Mold, "Mold4E")]
  public void Each_state_names_its_element(
    int row,
    BedSlotSide side,
    BedSlotState state,
    string expected
  ) {
    Assert.Equal(
      expected,
      SandBedLayout.ElementFor(new BedSlot(row, side), state)
    );
  }

  [Fact]
  public void Full_means_full_of_sand_not_full_of_metal() {
    // `Full` is the uncarved element. Inverted, every uncarved bed renders as carved and every carved one
    // as flat, with nothing to report it.
    var flank = new BedSlot(1, BedSlotSide.West);
    Assert.Equal(
      "Mold1WFull",
      SandBedLayout.ElementFor(flank, BedSlotState.Sand)
    );
    Assert.Equal("Mold1W", SandBedLayout.ElementFor(flank, BedSlotState.Mold));
  }

  #endregion

  #region Selective-element paths

  [Fact]
  public void Every_path_keeps_its_whole_subtree() {
    // VS matches SelectiveElements per path segment: an exact name renders that element but drops its
    // children, so a mold comes out as its bare rim. Every entry ends in the subtree wildcard.
    Assert.All(
      SandBedLayout.CarvedElements(AllSand()),
      p => Assert.EndsWith("/*", p)
    );
  }

  [Fact]
  public void No_ancestor_is_ever_named_on_its_own() {
    // Naming a row group renders both states it contains at once, carved and full stacked in the same
    // cavity. Ancestors already render as prefixes of the deeper entries, so they are never listed.
    string[] paths = SandBedLayout.CarvedElements(AllSand());
    foreach (string group in new[] { "SandRunners", "SandRunners/RunnerRow1" }) {
      Assert.DoesNotContain(group, paths);
      Assert.DoesNotContain(group + "/*", paths);
    }
  }

  [Fact]
  public void A_carved_bed_draws_exactly_one_element_per_slot() {
    string[] paths = SandBedLayout.CarvedElements(AllSand());
    Assert.Equal(SandBedLayout.Slots.Length, paths.Length);
    Assert.Equal(paths.Length, paths.Distinct().Count()); // no slot drawn twice
  }

  [Fact]
  public void Carving_one_slot_changes_only_that_slot() {
    List<BedSlotState> states = AllSand();
    string[] before = SandBedLayout.CarvedElements(states);
    states[0] = BedSlotState.Mold; // row 1 west

    string[] after = SandBedLayout.CarvedElements(states);
    Assert.Single(after.Except(before));
    Assert.Contains("SandRunners/RunnerRow1/Mold1W/*", after);
  }

  [Fact]
  public void A_short_or_missing_state_list_renders_an_uncarved_bed() {
    // A truncated or absent state list renders plain sand, not an empty bed.
    Assert.Equal(
      SandBedLayout.CarvedElements(AllSand()),
      SandBedLayout.CarvedElements(null)
    );
    Assert.Equal(
      SandBedLayout.CarvedElements(AllSand()),
      SandBedLayout.CarvedElements([BedSlotState.Sand, BedSlotState.Sand])
    );
  }

  #endregion

  #region Composing with the construction behaviour

  // What the construction stages leave behind: brickwork, then the sand fill as one whole-group entry.
  private static readonly string[] Built =
  [
    "Base/*",
    "BaseExtension/*",
    "SandRunners/*",
  ];

  [Fact]
  public void Before_construction_completes_the_builder_is_left_alone() {
    Assert.Equal(
      Built,
      SandBedLayout.Compose(Built, AllSand(), constructed: false)
    );
  }

  [Fact]
  public void Completing_construction_expands_the_sand_group_into_one_entry_per_slot() {
    string[] composed = SandBedLayout.Compose(
      Built,
      AllSand(),
      constructed: true
    );

    // The bare group would draw both states of every slot stacked in the same cavity.
    Assert.DoesNotContain("SandRunners/*", composed);
    Assert.Contains("Base/*", composed); // the built structure survives
    Assert.Contains("BaseExtension/*", composed);
    Assert.Contains("SandRunners/RunnerRow1/RunnerCenter1Full/*", composed);
  }

  [Fact]
  public void A_freshly_built_bed_is_uncarved() {
    // Construction fills the bed with sand and stops; every runner and mold is carved afterwards. The
    // default state is therefore Sand for all twelve slots, and nothing conducts until something is cut.
    Assert.Equal(BedSlotState.Sand, default(BedSlotState));

    string[] composed = SandBedLayout.Compose(
      Built,
      new BedSlotState[SandBedLayout.Slots.Length],
      true
    );
    foreach (BedSlot slot in SandBedLayout.Slots)
      Assert.Contains(SandBedLayout.PathFor(slot, BedSlotState.Sand), composed);
  }

  [Fact]
  public void Composing_runs_over_the_builders_own_list_so_a_later_stage_cannot_clobber_it() {
    // The construction behaviour rebuilds from its list on every stage. Composing rather than replacing is
    // what carries the bed's carving through, and whatever the builder adds still comes out the far side.
    string[] built = [.. Built, "Chimney/*"];
    string[] composed = SandBedLayout.Compose(
      built,
      AllSand(),
      constructed: true
    );
    Assert.Contains("Chimney/*", composed);
  }

  [Fact]
  public void An_absent_builder_list_still_yields_a_drawable_bed() {
    Assert.Equal(
      SandBedLayout.CarvedElements(AllSand()),
      SandBedLayout.Compose(null, AllSand(), constructed: true)
    );
    Assert.Empty(SandBedLayout.Compose(null, AllSand(), constructed: false));
  }

  #endregion

  #region The art actually has these elements

  [Fact]
  public void Every_element_this_can_emit_exists_in_the_shipped_shape() {
    // Element names are literals against art, and a missing one renders as a hole in the bed rather than
    // raising. Checked against every state each slot can hold, so a re-export that renames one cube fails
    // here.
    HashSet<string> inShape = ShapeElementNames("casting/sandcastingbed");

    foreach (BedSlot slot in SandBedLayout.Slots)
      foreach (
        BedSlotState state in new[]
        {
        BedSlotState.Sand,
        BedSlotState.Runner,
        BedSlotState.Mold,
        }
      ) {
        if (!slot.Accepts(state))
          continue;
        string element = SandBedLayout.ElementFor(slot, state);
        Assert.True(
          inShape.Contains(element),
          $"shape has no element '{element}' ({slot}, {state})"
        );
      }

    Assert.Contains(SandBedLayout.RunnersGroup, inShape);
    for (int row = SandBedLayout.FirstRow; row <= SandBedLayout.LastRow; row++)
      Assert.True(
        inShape.Contains(SandBedLayout.RowGroup(row)),
        SandBedLayout.RowGroup(row)
      );
  }

  [Fact]
  public void The_shipped_shape_has_no_leftovers_from_the_three_row_bed() {
    // Selective-element matching drops unknown names without an error, so a stale shipped shape still
    // loads. Asserting the retired names are absent is the only way to detect one.
    HashSet<string> inShape = ShapeElementNames("casting/sandcastingbed");
    foreach (
      string retired in new[]
      {
        "RunnerStart",
        "RunnerStart1",
        "StartSandW",
        "PigMold1W",
        "Mold1WBroken",
      }
    )
      Assert.False(
        inShape.Contains(retired),
        $"shipped shape still carries retired element '{retired}'"
      );
  }

  private static HashSet<string> ShapeElementNames(string shapePath) {
    string file = System.IO.Path.Combine(
      System.AppContext.BaseDirectory,
      "assets",
      "iwex",
      "shapes",
      shapePath.Replace('/', System.IO.Path.DirectorySeparatorChar) + ".json"
    );
    Assert.True(System.IO.File.Exists(file), $"shape asset not found: {file}");

    var names = new HashSet<string>();
    void Walk(Newtonsoft.Json.Linq.JToken? elements) {
      if (elements == null)
        return;
      foreach (Newtonsoft.Json.Linq.JToken el in elements) {
        if (el["name"]?.ToString() is { Length: > 0 } n)
          names.Add(n);
        Walk(el["children"]);
      }
    }
    Walk(
      Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(file))[
        "elements"
      ]
    );
    return names;
  }

  #endregion
}
