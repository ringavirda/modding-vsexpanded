using IronworkingExpanded.BlockStructures.Casting;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The casting cell's interaction and cast-outcome rules. Pins precedence (a hot cast is never a
/// fall-through; a hardened cast is collected before ram/imprint) and the misrun / intake gates.
/// </summary>
public class CastingCellLogicTests
{
  // Convenience: an empty-handed click.
  private static CellAction EmptyHand(SandLevel sand, bool impression, bool metal, bool hardened) =>
    CastingCellLogic.Decide(false, false, true, sand, impression, metal, hardened);

  #region Interaction precedence

  [Fact]
  public void Empty_hand_on_a_hardened_cast_harvests()
  {
    Assert.Equal(CellAction.Harvest, EmptyHand(SandLevel.Full, true, true, true));
  }

  [Fact]
  public void Empty_hand_on_a_still_liquid_cast_is_refused_not_fall_through()
  {
    Assert.Equal(CellAction.TooHot, EmptyHand(SandLevel.Full, true, true, false));
  }

  [Fact]
  public void Metal_present_blocks_ramming_and_imprinting()
  {
    // Holding sand or a pattern does nothing while metal is in the cell.
    Assert.Equal(
      CellAction.None,
      CastingCellLogic.Decide(true, false, false, SandLevel.Full, true, true, false)
    );
    Assert.Equal(
      CellAction.None,
      CastingCellLogic.Decide(false, true, false, SandLevel.Full, true, true, true)
    );
  }

  #endregion

  #region Ram / imprint

  [Theory]
  [InlineData(SandLevel.Empty)]
  [InlineData(SandLevel.Half)]
  public void Sand_rams_an_empty_or_half_cell_to_full(SandLevel sand)
  {
    Assert.Equal(
      CellAction.RamSand,
      CastingCellLogic.Decide(true, false, false, sand, false, false, false)
    );
  }

  [Fact]
  public void Sand_on_a_full_cell_does_nothing()
  {
    Assert.Equal(
      CellAction.None,
      CastingCellLogic.Decide(true, false, false, SandLevel.Full, false, false, false)
    );
  }

  [Fact]
  public void A_pattern_imprints_only_full_impression_free_sand()
  {
    Assert.Equal(
      CellAction.Imprint,
      CastingCellLogic.Decide(false, true, false, SandLevel.Full, false, false, false)
    );
    // Not on half sand,
    Assert.Equal(
      CellAction.None,
      CastingCellLogic.Decide(false, true, false, SandLevel.Half, false, false, false)
    );
    // and not over an existing impression.
    Assert.Equal(
      CellAction.None,
      CastingCellLogic.Decide(false, true, false, SandLevel.Full, true, false, false)
    );
  }

  #endregion

  #region Misrun / intake

  [Fact]
  public void A_full_cavity_below_pour_temp_is_a_misrun()
  {
    Assert.True(CastingCellLogic.IsMisrun(cavityFull: true, metalTemp: 1000f, minPourTemp: 1150f));
  }

  [Fact]
  public void At_or_above_pour_temp_is_a_good_cast()
  {
    Assert.False(CastingCellLogic.IsMisrun(true, 1200f, 1150f));
    Assert.False(CastingCellLogic.IsMisrun(true, 1150f, 1150f));
  }

  [Fact]
  public void A_partial_cavity_is_never_a_misrun_yet()
  {
    Assert.False(CastingCellLogic.IsMisrun(cavityFull: false, metalTemp: 900f, minPourTemp: 1150f));
  }

  [Fact]
  public void Zero_min_pour_temp_disables_the_misrun_check()
  {
    Assert.False(CastingCellLogic.IsMisrun(true, 20f, 0f));
  }

  [Fact]
  public void Intake_needs_an_impression_and_an_unfilled_liquid_cavity()
  {
    Assert.True(CastingCellLogic.CanIntake(hasImpression: true, cavityFull: false, solidified: false));
    Assert.False(CastingCellLogic.CanIntake(false, false, false)); // no impression
    Assert.False(CastingCellLogic.CanIntake(true, true, false)); // already full
    Assert.False(CastingCellLogic.CanIntake(true, false, true)); // solidified
  }

  #endregion

  #region What counts as molding sand

  [Fact]
  public void Green_sand_is_the_one_material_the_cell_rams()
  {
    Assert.True(CastingCellLogic.IsMoldingSand("iwex:greensand"));
    Assert.Equal("iwex:greensand", CastingCellLogic.GreenSandCode);
  }

  [Fact]
  public void Raw_sand_is_refused_however_much_of_it_you_carry()
  {
    // The binder is the whole point: dry sand holds no impression, so no rock type qualifies. Pinned across
    // the variant set because "accept any sand" is exactly the behaviour green sand replaced, and a partial
    // relapse (one rock type slipping through a prefix match) would be invisible in play.
    foreach (string rock in new[] { "andesite", "basalt", "granite", "claystone", "sandstone" })
      Assert.False(CastingCellLogic.IsMoldingSand($"game:sand-{rock}"));
  }

  [Fact]
  public void Another_mods_green_sand_does_not_satisfy_this_station()
  {
    // Matching on the full code, not the path, so a same-named item from elsewhere cannot stand in.
    Assert.False(CastingCellLogic.IsMoldingSand("othermod:greensand"));
    Assert.False(CastingCellLogic.IsMoldingSand("greensand"));
  }

  [Fact]
  public void An_empty_hand_is_not_sand()
  {
    Assert.False(CastingCellLogic.IsMoldingSand(null));
    Assert.False(CastingCellLogic.IsMoldingSand(""));
  }

  #endregion

  #region Shake-out leaves the cell ready

  [Fact]
  public void Shake_out_rakes_the_cell_back_to_full_sand()
  {
    // Breaking the casting free wrecks the impression, not the bed. So the cell needs no sand handed back
    // and none re-rammed: the next thing it wants is a pattern, and the loop is impress -> pour -> shake out.
    Assert.Equal(SandLevel.Full, CastingCellLogic.AfterShakeOut);
  }

  [Fact]
  public void A_shaken_out_cell_immediately_accepts_a_pattern_again()
  {
    // The whole point of the change. Under the old half-sand rule this click did nothing, and a cell that
    // silently ignores a held pattern reads as broken rather than as needing a step the player forgot.
    Assert.Equal(
      CellAction.Imprint,
      CastingCellLogic.Decide(
        holdingSand: false,
        holdingPattern: true,
        emptyHand: false,
        sand: CastingCellLogic.AfterShakeOut,
        hasImpression: false,
        hasMetal: false,
        isHardened: false
      )
    );
  }

  [Fact]
  public void A_shaken_out_cell_has_no_use_for_more_sand()
  {
    // And it must not quietly eat a stack the player is holding for some other cell.
    Assert.Equal(
      CellAction.None,
      CastingCellLogic.Decide(
        holdingSand: true,
        holdingPattern: false,
        emptyHand: false,
        sand: CastingCellLogic.AfterShakeOut,
        hasImpression: false,
        hasMetal: false,
        isHardened: false
      )
    );
  }

  #endregion

  #region Filling shape (which rammed-sand mesh renders)

  [Fact]
  public void A_bare_cell_shows_no_sand()
  {
    Assert.Null(CastingCellLogic.FillingShape(SandLevel.Empty, false, null));
    // Even a stray impression flag cannot conjure sand out of an empty shell.
    Assert.Null(CastingCellLogic.FillingShape(SandLevel.Empty, true, "iwex:casting/cell-filling-plate"));
  }

  [Fact]
  public void A_legacy_half_rammed_cell_still_renders()
  {
    // Nothing produces Half any more, but saved cells carry it and must not turn into invisible shells until
    // someone rams them.
    Assert.Equal(
      CastingCellLogic.HalfFillShape,
      CastingCellLogic.FillingShape(SandLevel.Half, false, null)
    );
  }

  [Fact]
  public void Full_impression_free_sand_shows_the_flat_base_fill()
  {
    Assert.Equal(
      CastingCellLogic.BaseFillShape,
      CastingCellLogic.FillingShape(SandLevel.Full, false, null)
    );
  }

  [Fact]
  public void An_impressed_cell_shows_the_patterns_cavity()
  {
    Assert.Equal(
      "iwex:casting/cell-filling-plate",
      CastingCellLogic.FillingShape(SandLevel.Full, true, "iwex:casting/cell-filling-plate")
    );
  }

  [Fact]
  public void An_impression_with_an_unresolved_shape_falls_back_to_the_flat_base_fill()
  {
    Assert.Equal(
      CastingCellLogic.BaseFillShape,
      CastingCellLogic.FillingShape(SandLevel.Full, true, null)
    );
  }

  #endregion
}
