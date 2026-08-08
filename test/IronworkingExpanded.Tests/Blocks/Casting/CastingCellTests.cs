using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The 1×1 sand casting cell's interaction routing, and the pattern-size guard that makes
/// <c>MoldSpec.Size</c> mean something.
/// </summary>
public class CastingCellTests
{
  #region Pattern size

  [Fact]
  public void A_longcell_pattern_is_refused_by_the_one_by_one_cell()
  {
    // MoldSpec.Size was parsed, validated and round-tripped by two tests, and read by nothing in src/.
    // Without this guard the cell accepts a pattern whose cavity is 24 voxels long, sets its capacity
    // from it, and casts a slab out of a 1x1 block.
    var rig = CastingCellScenes.RammedFull();

    bool handled = rig.Interact(rig.PatternStack("castslab", size: "longcell"));

    Assert.True(handled); // the click is consumed, not passed through
    Assert.False(rig.Cell.HasImpression); // ...but nothing was imprinted
    Assert.Equal("iwex-castingcell-wrongsize", rig.LastError);
  }

  [Fact]
  public void A_cell_sized_pattern_is_still_imprinted()
  {
    // The other half of the guard. A size check that refuses everything would pass the test above.
    var rig = CastingCellScenes.RammedFull();

    bool handled = rig.Interact(rig.PatternStack("plate"));

    Assert.True(handled);
    Assert.True(rig.Cell.HasImpression);
    Assert.Null(rig.LastError);
  }

  [Fact]
  public void The_refused_pattern_leaves_the_cavity_capacity_untouched()
  {
    // The failure this actually prevents. Imprint sets the molten cell's capacity from the spec, so a
    // longcell pattern that slipped through would leave a 1x1 cell claiming a long cell's cavity - and
    // the capacity outlives the refusal, because nothing else ever clears it.
    var rig = CastingCellScenes.RammedFull();
    int before = rig.Cell.GetBehavior<
      ExpandedLib.Blocks.Structures.BEBehaviorMoltenCell
    >()!.MaxUnitCapacity;

    rig.Interact(rig.PatternStack("castslab", size: "longcell", capacity: 3000));

    Assert.Equal(
      before,
      rig.Cell.GetBehavior<ExpandedLib.Blocks.Structures.BEBehaviorMoltenCell>()!
        .MaxUnitCapacity
    );
  }

  [Fact]
  public void A_pattern_carrying_no_mold_spec_is_refused_with_its_own_code()
  {
    // Distinct from the size refusal on purpose: "this pattern is broken" and "this pattern is for the
    // other station" are different problems and the player can only act on one of them.
    var rig = CastingCellScenes.RammedFull();

    rig.Interact(rig.SpeclessPatternStack());

    Assert.False(rig.Cell.HasImpression);
    Assert.Equal("iwex-castingcell-badpattern", rig.LastError);
  }

  #endregion
}
