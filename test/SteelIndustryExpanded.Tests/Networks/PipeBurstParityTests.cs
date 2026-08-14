using IronIndustryExpanded.Tests;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The shared <c>PipeTestWorld</c> fixture lives in the iiex suite and cannot reference siex, so it
/// restates the rolled-tier pipe constants. These assertions catch a retune of the <c>SiexValues</c>
/// originals that would otherwise leave every HP-tier test running against stale numbers and passing.
/// </summary>
public class PipeBurstParityTests {
  [Fact]
  public void The_shared_fixture_seeds_the_shipped_rolled_tier_burst_rating() {
    Assert.Equal(
      PipeTestWorld.RolledTierBurst,
      SiexValues.RolledPipeBurstPressure,
      3
    );
  }

  // Second restated constant, same drift check.
  [Fact]
  public void The_shared_fixture_seeds_the_shipped_rolled_tier_throughput() {
    Assert.Equal(
      PipeTestWorld.RolledTierThroughput,
      SiexValues.RolledPipeThroughput,
      3
    );
  }
}
