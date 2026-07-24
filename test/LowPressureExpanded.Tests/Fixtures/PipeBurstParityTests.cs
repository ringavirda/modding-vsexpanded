using IronworkingExpanded.Tests;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// Pins the one pipe-tier number the shared test fixture has to restate. <c>PipeTestWorld</c> seeds the
/// per-domain burst ratings that the mods' ModSystems register in game, and it lives in the iwex suite -
/// which cannot reference lpex, so the cast-tier rating is a constant there rather than a config read.
/// <para>
/// A stale copy is not a loud failure: every burst, over-pressure and capacity test in three suites
/// quietly runs against the wrong ceiling and still passes. That is what happened when the tiers were
/// rebalanced (bolted 5 → 2.5, cast 8 → 5) and the fixture kept the old pair, so this guard exists to
/// make the next retune fail here instead of nowhere.
/// </para>
/// </summary>
public class PipeBurstParityTests
{
  [Fact]
  public void The_shared_fixture_seeds_the_shipped_cast_tier_burst_rating()
  {
    // The fixture constant is the 'expected' side: it is the copy that has to track the config, and
    // xUnit's analyzer wants the constant there anyway.
    Assert.Equal(
      PipeTestWorld.CastTierBurst,
      LpexValues.CastPipeBurstPressure,
      3
    );
  }
}
