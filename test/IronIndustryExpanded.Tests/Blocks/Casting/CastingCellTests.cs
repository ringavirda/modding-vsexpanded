using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Interaction routing for the 1x1 sand casting cell, and the pattern-size guard that enforces
/// <c>MoldSpec.Size</c>.
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
}
