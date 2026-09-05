using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Interaction routing for the 1x1 sand casting cell, the pattern-size guard that enforces
/// <c>MoldSpec.Size</c>, and what a pour shakes out as.
/// </summary>
public class CastingCellTests {
  #region Pattern size

  [Fact]
  public void A_longcell_pattern_is_refused_by_the_one_by_one_cell() {
    // Without the guard the cell accepts a pattern whose cavity is 24 voxels long, takes its capacity
    // from it, and casts a slab out of a 1x1 block.
    var rig = CastingCellScenes.RammedFull();

    bool handled = rig.Interact(rig.PatternStack("castslab", size: "longcell"));

    Assert.True(handled); // the click is consumed, not passed through
    Assert.False(rig.Cell.HasImpression); // but nothing is imprinted
    Assert.Equal("iiex-castingcell-wrongsize", rig.LastError);
  }

  [Fact]
  public void A_cell_sized_pattern_is_still_imprinted() {
    // Control for the refusal above: a size check that refused everything would also pass it.
    var rig = CastingCellScenes.RammedFull();

    bool handled = rig.Interact(rig.PatternStack("plate"));

    Assert.True(handled);
    Assert.True(rig.Cell.HasImpression);
    Assert.Null(rig.LastError);
  }

  [Fact]
  public void The_refused_pattern_leaves_the_cavity_capacity_untouched() {
    // Imprint sets the molten cell's capacity from the spec and nothing else clears it, so a longcell
    // pattern that slipped through would leave a 1x1 cell holding a long cell's capacity.
    var rig = CastingCellScenes.RammedFull();
    int before = rig
      .Cell.GetBehavior<ExpandedLib.Blocks.Structures.BEBehaviorMoltenCell>()!
      .MaxUnitCapacity;

    rig.Interact(
      rig.PatternStack("castslab", size: "longcell", capacity: 3000)
    );

    Assert.Equal(
      before,
      rig.Cell.GetBehavior<ExpandedLib.Blocks.Structures.BEBehaviorMoltenCell>()!.MaxUnitCapacity
    );
  }

  [Fact]
  public void A_pattern_carrying_no_mold_spec_is_refused_with_its_own_code() {
    // A separate error code from the size refusal: a broken pattern and a pattern meant for another
    // station are different problems for the player.
    var rig = CastingCellScenes.RammedFull();

    rig.Interact(rig.SpeclessPatternStack());

    Assert.False(rig.Cell.HasImpression);
    Assert.Equal("iiex-castingcell-badpattern", rig.LastError);
  }

  #endregion

  #region Pour and shake-out

  [Fact]
  public void A_cast_poured_hot_and_shaken_out_cold_yields_the_part() {
    // The misrun rule reads the pour, not the cast: shake-out waits for the metal to harden far below
    // any pour minimum, so a cell judging the live temperature would scrap every good cast.
    var rig = CastingCellScenes.RammedFull();
    rig.Interact(rig.PatternStack("plate"));
    rig.PourUntilFull(1200f).CoolToHardened();

    rig.InteractEmptyHanded();

    ItemStack part = Assert.Single(rig.Harvested);
    Assert.Equal("iiex:cast-plate", part.Collectible.Code.ToString());
    Assert.False(rig.Cell.HasImpression); // shake-out wrecks the impression
  }

  [Fact]
  public void A_cast_poured_below_the_pour_minimum_is_a_misrun() {
    // Same cell, same cooling, only the pour is cold: the cavity still fills (the fixture's metal is
    // liquid at 1000 C) and shake-out recovers the metal as bits rather than the part.
    var rig = CastingCellScenes.RammedFull();
    rig.Interact(rig.PatternStack("plate", capacity: 100));
    rig.PourUntilFull(1000f).CoolToHardened();

    rig.InteractEmptyHanded();

    ItemStack scrap = Assert.Single(rig.Harvested);
    Assert.Equal(rig.ScrapCode, scrap.Collectible.Code.ToString());
    Assert.Equal(100 / 5, scrap.StackSize); // every unit poured comes back, five to a bit
  }

  [Fact]
  public void A_cell_saved_without_a_pour_temperature_still_yields_its_part() {
    // A cell that filled before the pour temperature was kept carries no record of it. An unknown
    // pour is not a misrun, so such a cell shakes out its part rather than scrapping a cast that was
    // fine.
    var rig = CastingCellScenes.RammedFull();
    rig.Interact(rig.PatternStack("plate"));
    rig.PourUntilFull(1200f);

    var tree = new TreeAttribute();
    rig.Cell.ToTreeAttributes(tree);
    Assert.True(tree.HasAttribute("cc_filltemp")); // the premise: a filled cell keeps its pour
    tree.RemoveAttribute("cc_filltemp");
    rig.Cell.FromTreeAttributes(tree, rig.World.World);

    rig.CoolToHardened().InteractEmptyHanded();

    ItemStack part = Assert.Single(rig.Harvested);
    Assert.Equal("iiex:cast-plate", part.Collectible.Code.ToString());
  }

  #endregion
}
