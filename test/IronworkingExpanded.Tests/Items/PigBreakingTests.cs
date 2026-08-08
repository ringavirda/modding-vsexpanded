using IronworkingExpanded.Items;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The mass-conservation arithmetic of breaking a pig on the anvil: a 375-unit pig fills 150 voxels
/// (2.5 u each), and the units freed by the voxels a helve hit sheds are paid out as whole 25u chunks
/// and 5u bits, carrying the sub-bit remainder to the next hit. The anvil wiring is verified in-game;
/// this pins the maths.
/// </summary>
public class PigBreakingTests
{
  #region The density rule

  [Fact]
  public void A_pig_is_worth_exactly_two_and_a_half_units_per_voxel()
  {
    Assert.Equal(2.5f, PigBreaking.UnitsPerVoxel, 4);
  }

  // The invariant behind the number above, stated so a future re-mass cannot quietly break it: the voxel
  // count and the unit mass must move together, because 2.5 u/vx³ is the mod's one density rule and this
  // derived constant is the only place the codebase expresses it. Asserting the ratio alone would still
  // pass if both constants drifted; asserting the product is what ties them to each other.
  [Fact]
  public void The_voxel_count_and_the_mass_move_together()
  {
    Assert.Equal(ItemPig.PigUnits, PigBreaking.PigVoxels * PigBreaking.UnitsPerVoxel, 4);
  }

  #endregion

  #region Denomination

  [Theory]
  [InlineData(150, 15, 0)] // the whole pig at once -> fifteen chunks (375u)
  [InlineData(140, 14, 0)] // the shed voxels -> fourteen chunks (350u)
  [InlineData(10, 1, 0)] // the recipe leftover's worth -> one chunk (25u)
  [InlineData(2, 0, 1)] // 5u -> one bit
  [InlineData(1, 0, 0)] // 2.5u -> nothing yet (carried)
  public void Emit_pays_out_chunks_and_bits_for_the_voxels_removed(
    int voxelsRemoved,
    int chunks,
    int bits
  )
  {
    float remainder = 0f;
    Assert.Equal((chunks, bits), PigBreaking.Emit(voxelsRemoved, ref remainder));
  }

  [Fact]
  public void The_sub_bit_remainder_carries_between_hits()
  {
    float remainder = 0f;

    // Two single-voxel hits: 2.5u then 5.0u -> the second crosses a bit.
    Assert.Equal((0, 0), PigBreaking.Emit(1, ref remainder));
    Assert.Equal((0, 1), PigBreaking.Emit(1, ref remainder));
  }

  [Fact]
  public void Breaking_a_whole_pig_one_voxel_at_a_time_conserves_the_mass()
  {
    float remainder = 0f;
    int units = 0;
    for (int i = 0; i < PigBreaking.PigVoxels; i++)
    {
      (int chunks, int bits) = PigBreaking.Emit(1, ref remainder);
      units += chunks * ItemPig.ChunkUnits + bits * ItemPig.BitUnits;
    }

    // Every unit of the 375u pig comes out (150 voxels x 2.5 = 375, an exact multiple of the 5u bit).
    Assert.Equal(ItemPig.PigUnits, units);
    Assert.Equal(0f, remainder, 4);
  }

  #endregion
}
