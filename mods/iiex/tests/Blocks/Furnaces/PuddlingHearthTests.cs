using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The puddling hearth's bed: fettling, charging, and melting the charge down into a bath. Until now
/// nothing under <c>test/</c> named this class at all - the pure helpers were covered
/// (<see cref="FurnacePartsTests"/> tests <c>PuddlingHearthLayout</c> and <c>HearthRows</c>) but every
/// rule the block entity itself applies was a live mechanic a refactor could have deleted silently.
/// </summary>
public class PuddlingHearthTests {
  #region Harness

  private static BlockEntityPuddlingHearth Bed() {
    var world = new TestWorld();
    var bed = new BlockEntityPuddlingHearth {
      Pos = new BlockPos(0, 16, 0),
      // The base's ToTreeAttributes writes Block.Code, so a bed with no block cannot be serialised.
      Block = TestBlocks.Configure(
        new BlockPuddlingHearth(),
        "iiex:furnace-puddlinghearth-n",
        1,
        ("type", "puddlinghearth"),
        ("side", "north")
      ),
    };
    world.Place(bed.Pos, bed.Block, bed);
    world.Attach(bed);
    return bed;
  }

  /// <summary>
  /// A bed fettled and charged to capacity - one complete heat. Row order is not free: fettling the
  /// centre is enough to block both flanks, so a full bed can only be built flanks first and centre last.
  /// <see cref="A_bed_can_only_be_filled_flanks_first"/> states that as a rule rather than leaving it
  /// buried in this helper.
  /// </summary>
  private static BlockEntityPuddlingHearth FullBed() {
    BlockEntityPuddlingHearth bed = Bed();
    foreach (
      HearthRows.Row row in new[]
      {
        HearthRows.Row.Left,
        HearthRows.Row.Right,
        HearthRows.Row.Centre,
      }
    ) {
      Assert.True(bed.TryFettle(row), $"could not fettle {row}");
      for (int i = 0; i < PuddlingHearthLayout.PigsPerRow; i++)
        Assert.True(bed.TryChargePig(row), $"could not charge {row}");
    }
    return bed;
  }

  #endregion

  #region The charge rules that already shipped

  [Fact]
  public void A_row_must_be_fettled_before_it_takes_a_pig() {
    BlockEntityPuddlingHearth bed = Bed();

    Assert.False(bed.TryChargePig(HearthRows.Row.Centre));
    Assert.True(bed.NeedsFettle(HearthRows.Row.Centre));

    Assert.True(bed.TryFettle(HearthRows.Row.Centre));
    Assert.True(bed.TryChargePig(HearthRows.Row.Centre));
    Assert.Equal(1, bed.PigCount);
  }

  [Fact]
  public void A_loaded_row_refuses_a_second_fettling() {
    // Re-fettling would bury the charge, so the refusal is what keeps the bed readable.
    BlockEntityPuddlingHearth bed = Bed();
    Assert.True(bed.TryFettle(HearthRows.Row.Centre));

    Assert.False(bed.TryFettle(HearthRows.Row.Centre));

    Assert.True(bed.TryChargePig(HearthRows.Row.Centre));
    Assert.False(bed.TryFettle(HearthRows.Row.Centre));
  }

  [Fact]
  public void A_row_takes_no_more_than_its_three_pigs() {
    BlockEntityPuddlingHearth bed = Bed();
    Assert.True(bed.TryFettle(HearthRows.Row.Left));

    for (int i = 0; i < PuddlingHearthLayout.PigsPerRow; i++)
      Assert.True(bed.TryChargePig(HearthRows.Row.Left));

    Assert.False(bed.TryChargePig(HearthRows.Row.Left));
    Assert.Equal(PuddlingHearthLayout.PigsPerRow, bed.PigCount);
  }

  /// <summary>
  /// The reach rule as the block entity applies it, which is not the same as
  /// <c>HearthRows.CanReach</c> being correct: a loaded centre blocks both flanks and never itself, so a
  /// bed is worked centre-last and unloaded centre-first.
  /// </summary>
  [Fact]
  public void A_loaded_centre_blocks_both_flanks_and_never_itself() {
    BlockEntityPuddlingHearth bed = Bed();
    Assert.True(bed.TryFettle(HearthRows.Row.Centre));

    Assert.False(bed.TryFettle(HearthRows.Row.Left));
    Assert.False(bed.TryFettle(HearthRows.Row.Right));
    Assert.True(bed.TryChargePig(HearthRows.Row.Centre));
  }

  /// <summary>
  /// The reach rule read as a loading order, which is the form the player meets it in: fettling the
  /// centre blocks both flanks at once, so a bed worked centre-first can never be filled. The unloading
  /// order is the mirror of it - a full bed is drawn centre-first, because the centre is what is in the
  /// way.
  /// </summary>
  [Fact]
  public void A_bed_can_only_be_filled_flanks_first() {
    BlockEntityPuddlingHearth centreFirst = Bed();
    Assert.True(centreFirst.TryFettle(HearthRows.Row.Centre));
    Assert.False(centreFirst.TryFettle(HearthRows.Row.Left));
    Assert.False(centreFirst.TryFettle(HearthRows.Row.Right));

    BlockEntityPuddlingHearth flanksFirst = Bed();
    Assert.True(flanksFirst.TryFettle(HearthRows.Row.Left));
    Assert.True(flanksFirst.TryFettle(HearthRows.Row.Right));
    Assert.True(flanksFirst.TryFettle(HearthRows.Row.Centre));
  }

  [Fact]
  public void A_full_bed_is_nine_pigs() {
    BlockEntityPuddlingHearth bed = FullBed();

    Assert.Equal(PuddlingHearthLayout.PigCapacity, bed.PigCount);
    Assert.True(bed.IsFullyCharged);
  }

  [Fact]
  public void Clearing_the_bed_takes_the_pigs_and_the_fettling_together() {
    BlockEntityPuddlingHearth bed = FullBed();

    bed.ClearBed();

    Assert.Equal(0, bed.PigCount);
    foreach (HearthRows.Row row in HearthRows.All)
      Assert.True(bed.NeedsFettle(row));
  }

  #endregion

  #region Serialization

  [Fact]
  public void A_charged_bed_survives_a_tree_round_trip() {
    BlockEntityPuddlingHearth bed = FullBed();
    var tree = new TreeAttribute();
    bed.ToTreeAttributes(tree);

    BlockEntityPuddlingHearth loaded = Bed();
    loaded.FromTreeAttributes(tree, loaded.Api.World);

    Assert.Equal(bed.PigCount, loaded.PigCount);
    foreach (HearthRows.Row row in HearthRows.All)
      Assert.False(loaded.NeedsFettle(row));
  }

  /// <summary>
  /// An edited or older save must not put the bed in a state the element set has no drawing for: the
  /// shape carries three pigs per row and nothing beyond.
  /// </summary>
  [Fact]
  public void A_pig_count_beyond_the_art_is_clamped_on_read() {
    var tree = new TreeAttribute();
    tree.SetInt("pigs0", 99);
    tree.SetBool("fettled0", true);

    BlockEntityPuddlingHearth loaded = Bed();
    loaded.FromTreeAttributes(tree, loaded.Api.World);

    Assert.Equal(PuddlingHearthLayout.PigsPerRow, loaded.PigCount);
  }

  #endregion

  #region Melting down

  [Fact]
  public void A_charge_melts_down_over_the_melt_cadence_and_leaves_a_bath() {
    BlockEntityPuddlingHearth bed = FullBed();

    Assert.False(bed.HasBath);
    Assert.Equal(0f, bed.MeltProgress);

    // Part-way down: the pigs are still standing, so the bed still draws them.
    Assert.False(bed.MeltDown(0.5f));
    Assert.Equal(PuddlingHearthLayout.PigCapacity, bed.PigCount);
    Assert.False(bed.HasBath);

    Assert.True(bed.MeltDown(0.5f));

    Assert.True(bed.HasBath);
    Assert.Equal(0, bed.PigCount);
    Assert.Equal(
      PuddlingHearthLayout.PigCapacity * ItemPig.PigUnits,
      bed.BathUnits
    );
  }

  /// <summary>
  /// Nine is the bed's capacity, not a gate on the process: a part-charged hearth melts what it has and
  /// makes a smaller bath, which is what keeps mass the only thing that decides the yield.
  /// </summary>
  [Fact]
  public void A_part_charged_bed_melts_what_it_has() {
    BlockEntityPuddlingHearth bed = Bed();
    Assert.True(bed.TryFettle(HearthRows.Row.Left));
    Assert.True(bed.TryChargePig(HearthRows.Row.Left));
    Assert.True(bed.TryChargePig(HearthRows.Row.Left));

    Assert.True(bed.MeltDown(1f));

    Assert.True(bed.HasBath);
    Assert.Equal(2 * ItemPig.PigUnits, bed.BathUnits);
  }

  [Fact]
  public void An_empty_bed_melts_nothing() {
    BlockEntityPuddlingHearth bed = Bed();

    Assert.False(bed.MeltDown(1f));

    Assert.False(bed.HasBath);
    Assert.Equal(0, bed.BathUnits);
  }

  /// <summary>
  /// A heat under way takes no more charge. Charging into a half-melted bed would either weld cold pig to
  /// a bath or silently vanish into the melt arithmetic; refusing is the only honest answer.
  /// </summary>
  [Fact]
  public void A_bed_that_has_started_melting_takes_no_more_fettle_or_pig() {
    BlockEntityPuddlingHearth bed = Bed();
    Assert.True(bed.TryFettle(HearthRows.Row.Left));
    Assert.True(bed.TryChargePig(HearthRows.Row.Left));

    bed.MeltDown(0.2f);

    Assert.False(bed.TryChargePig(HearthRows.Row.Left));
    Assert.False(bed.TryFettle(HearthRows.Row.Right));
  }

  [Fact]
  public void A_bath_does_not_melt_again() {
    BlockEntityPuddlingHearth bed = FullBed();
    Assert.True(bed.MeltDown(1f));
    int units = bed.BathUnits;

    Assert.False(bed.MeltDown(1f));

    Assert.Equal(units, bed.BathUnits);
  }

  [Fact]
  public void Clearing_the_bed_takes_the_bath_with_it() {
    BlockEntityPuddlingHearth bed = FullBed();
    bed.MeltDown(1f);

    bed.ClearBed();

    Assert.False(bed.HasBath);
    Assert.Equal(0, bed.BathUnits);
    Assert.Equal(0f, bed.MeltProgress);
  }

  [Fact]
  public void A_bath_survives_a_tree_round_trip() {
    BlockEntityPuddlingHearth bed = FullBed();
    bed.MeltDown(1f);
    var tree = new TreeAttribute();
    bed.ToTreeAttributes(tree);

    BlockEntityPuddlingHearth loaded = Bed();
    loaded.FromTreeAttributes(tree, loaded.Api.World);

    Assert.True(loaded.HasBath);
    Assert.Equal(bed.BathUnits, loaded.BathUnits);
  }

  [Fact]
  public void A_bath_beyond_the_beds_capacity_is_clamped_on_read() {
    var tree = new TreeAttribute();
    tree.SetFloat("meltProgress", 9f);
    tree.SetInt("meltedUnits", int.MaxValue);

    BlockEntityPuddlingHearth loaded = Bed();
    loaded.FromTreeAttributes(tree, loaded.Api.World);

    Assert.Equal(1f, loaded.MeltProgress);
    Assert.Equal(
      PuddlingHearthLayout.PigCapacity * ItemPig.PigUnits,
      loaded.BathUnits
    );
  }

  /// <summary>
  /// The fire going out sets the bath solid: nothing more can be gathered, and the bed has to be raked
  /// and started over. Balls already lying on it are solid iron and survive.
  /// </summary>
  [Fact]
  public void A_bath_that_freezes_can_no_longer_be_gathered_but_keeps_its_balls() {
    BlockEntityPuddlingHearth bed = FullBed();
    bed.MeltDown(1f);
    Assert.True(bed.TryRabble());

    bed.FreezeBath();

    Assert.True(bed.IsFrozen);
    Assert.False(bed.TryRabble());
    Assert.Equal(1, bed.BallsOnBed);
    Assert.True(bed.TryDrawBall());
    // Only the clean-out is left, even though metal is still in the bed.
    Assert.True(bed.IsWorkedOut);
  }

  [Fact]
  public void Freezing_survives_a_tree_round_trip_and_clears_on_a_clean_out() {
    BlockEntityPuddlingHearth bed = FullBed();
    bed.MeltDown(1f);
    bed.FreezeBath();

    var tree = new TreeAttribute();
    bed.ToTreeAttributes(tree);
    BlockEntityPuddlingHearth loaded = Bed();
    loaded.FromTreeAttributes(tree, loaded.Api.World);
    Assert.True(loaded.IsFrozen);

    loaded.ClearBed();
    Assert.False(loaded.IsFrozen);
    Assert.False(loaded.HasBath);
  }

  /// <summary>
  /// An empty bed cannot freeze into a state the clean-out would then accept, which would let a player
  /// rake a bed that never held anything and collect the cinder anyway.
  /// </summary>
  [Fact]
  public void A_bed_with_no_bath_does_not_freeze() {
    BlockEntityPuddlingHearth bed = FullBed();

    bed.FreezeBath();

    Assert.False(bed.IsFrozen);
    Assert.False(bed.IsWorkedOut);
  }

  #endregion

  #region What the bed draws

  [Fact]
  public void A_melted_bed_draws_the_bath_instead_of_the_pigs() {
    int[] pigs = [3, 3, 3];
    bool[] fettled = [true, true, true];

    string[] cold = PuddlingHearthLayout.ElementsFor(pigs, fettled);
    string[] melted = PuddlingHearthLayout.ElementsFor(pigs, fettled, true);

    Assert.Contains(
      PuddlingHearthLayout.PigElement(HearthRows.Row.Left, 0),
      cold
    );
    Assert.DoesNotContain(PuddlingHearthLayout.BathElement, cold);

    Assert.Contains(PuddlingHearthLayout.BathElement, melted);
    foreach (HearthRows.Row row in HearthRows.All)
      for (int i = 0; i < PuddlingHearthLayout.PigsPerRow; i++)
        Assert.DoesNotContain(PuddlingHearthLayout.PigElement(row, i), melted);
  }

  /// <summary>
  /// The fettling stays drawn under the bath. It is a reagent the process consumes, not a lining, and it
  /// is still on the bed until the clean-out takes it.
  /// </summary>
  [Fact]
  public void The_fettling_stays_drawn_under_the_bath() {
    string[] melted = PuddlingHearthLayout.ElementsFor(
      [3, 3, 3],
      [true, true, true],
      true
    );

    foreach (HearthRows.Row row in HearthRows.All)
      Assert.Contains(PuddlingHearthLayout.FettleElement(row), melted);
  }

  #endregion
}
