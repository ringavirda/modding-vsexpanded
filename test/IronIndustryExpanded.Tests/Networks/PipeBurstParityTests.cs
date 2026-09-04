using IronIndustryExpanded.Tests;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Pins the pipe-tier numbers the shared fixture restates. <c>PipeTestWorld</c> seeds the per-domain
/// burst and throughput ratings the mods' ModSystems register in game, and it lives in the iiex suite,
/// which cannot reference iiex - so the cast-tier figures are constants there rather than config reads.
/// A retune that leaves them stale fails here instead of quietly gating every burst, over-pressure,
/// capacity and throughput test in three suites on the old ceiling.
/// </summary>
public class PipeBurstParityTests {
  [Fact]
  public void The_shared_fixture_seeds_the_shipped_cast_tier_burst_rating() {
    // The fixture constant is the expected side: it is the copy that has to track the config.
    Assert.Equal(
      PipeTestWorld.CastTierBurst,
      IiexValues.CastPipeBurstPressure,
      3
    );
  }

  [Fact]
  public void The_shared_fixture_seeds_the_shipped_cast_tier_throughput() {
    Assert.Equal(
      PipeTestWorld.CastTierThroughput,
      IiexValues.CastPipeThroughput,
      3
    );
  }
}
