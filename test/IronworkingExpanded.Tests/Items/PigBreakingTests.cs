using IronworkingExpanded.Items;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The mass-conservation arithmetic of breaking a pig on the anvil: a 150-unit pig fills 60 voxels
/// (2.5 u each), and the units freed by the voxels a helve hit sheds are paid out as whole 25u chunks
/// and 5u bits, carrying the sub-bit remainder to the next hit. The anvil wiring is verified in-game;
/// this pins the maths.
/// </summary>
public class PigBreakingTests
{
  [Fact]
  public void A_pig_is_worth_exactly_two_and_a_half_units_per_voxel()
  {
    Assert.Equal(2.5f, PigBreaking.UnitsPerVoxel, 4);
  }

  [Theory]
  [InlineData(60, 6, 0)] // the whole pig at once -> six chunks (150u)
  [InlineData(50, 5, 0)] // the 50 shed voxels -> five chunks (125u)
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

    // Every unit of the 150u pig comes out (60 voxels x 2.5 = 150, an exact multiple of the 5u bit).
    Assert.Equal(ItemPig.PigUnits, units);
    Assert.Equal(0f, remainder, 4);
  }
}
