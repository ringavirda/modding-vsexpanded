using IronworkingExpanded.Tests;
using Xunit;

namespace HighPressureExpanded.Tests;

/// <summary>
/// Pins the rolled-tier burst rating the shared <c>PipeTestWorld</c> fixture has to restate, for the
/// same reason its lpex sibling does: the fixture lives in the iwex suite and cannot reference hpex, so
/// a retune of <c>HpexValues.RolledPipeBurstPressure</c> would otherwise leave every HP-tier test
/// running against a stale ceiling and still passing.
/// </summary>
public class PipeBurstParityTests
{
  [Fact]
  public void The_shared_fixture_seeds_the_shipped_rolled_tier_burst_rating()
  {
    Assert.Equal(
      PipeTestWorld.RolledTierBurst,
      HpexValues.RolledPipeBurstPressure,
      3
    );
  }

  // Second restated number, same silent-drift failure - see the lpex sibling.
  [Fact]
  public void The_shared_fixture_seeds_the_shipped_rolled_tier_throughput()
  {
    Assert.Equal(
      PipeTestWorld.RolledTierThroughput,
      HpexValues.RolledPipeThroughput,
      3
    );
  }
}
