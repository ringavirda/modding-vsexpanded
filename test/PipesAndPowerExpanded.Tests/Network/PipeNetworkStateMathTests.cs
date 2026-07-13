using ExpandedLib.Blocks.Networks;
using ExpandedLib.Fluids;
using PipesAndPowerExpanded.BlockNetworkPipe;
using Xunit;

namespace PipesAndPowerExpanded.Tests;

/// <summary>Pure pressure/medium math on <see cref="PipeNetworkState"/> - no world needed.</summary>
public class PipeNetworkStateMathTests
{
  [Theory]
  [InlineData(0f, 90f, 0f)]
  [InlineData(45f, 90f, 0.5f)]
  [InlineData(90f, 90f, 1f)]
  [InlineData(180f, 90f, 2f)] // gas is compressible - the ratio runs past 1 atm
  public void ComputeGasPressure_is_volume_ratio(
    float vol,
    float max,
    float expected
  ) => Assert.Equal(expected, PipeNetworkState.ComputeGasPressure(vol, max), 3);

  [Fact]
  public void ComputeGasPressure_zero_capacity_is_zero() =>
    Assert.Equal(0f, PipeNetworkState.ComputeGasPressure(50f, 0f));

  [Theory]
  [InlineData(45f, 90f, 3f, 0.5f)] // below brim-full: tracks the fill ratio
  [InlineData(90f, 90f, 3f, 3f)] // brim-full: jumps to the pump feed pressure
  public void ComputeLiquidPressure_uses_feed_only_when_full(
    float vol,
    float max,
    float feed,
    float expected
  ) =>
    Assert.Equal(
      expected,
      PipeNetworkState.ComputeLiquidPressure(vol, max, feed),
      3
    );

  // The medium compatibility / priority helpers moved off PipeNetworkState into the injected
  // ExLiquids taxonomy (always seeded with the four built-ins); the behaviour is unchanged.
  [Theory]
  [InlineData("", "Air", true)] // empty run accepts anything
  [InlineData("", "Water", true)]
  [InlineData("Air", "Steam", true)] // gases mix
  [InlineData("Air", "Water", false)] // gas run rejects water
  [InlineData("Water", "Air", false)] // water run rejects gas
  [InlineData("Water", "Water", true)]
  public void Media_compatible_same_family_only(
    string current,
    string medium,
    bool expected
  ) => Assert.Equal(expected, ExLiquids.Taxonomy.Compatible(current, medium));

  [Theory]
  [InlineData("Air", "Air", "Air")]
  [InlineData("Air", "Steam", "Steam")]
  [InlineData("Steam", "Exhaust", "Exhaust")]
  [InlineData("Air", "Exhaust", "Exhaust")]
  public void Higher_priority_ranks_exhaust_over_steam_over_air(
    string a,
    string b,
    string expected
  ) => Assert.Equal(expected, ExLiquids.Taxonomy.HigherPriority(a, b));
}
