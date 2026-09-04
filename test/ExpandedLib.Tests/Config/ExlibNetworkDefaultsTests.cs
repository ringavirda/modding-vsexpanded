using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Locks the network tunable defaults in <see cref="ExlibValues"/>. The headless network tests and the
/// pipe fixtures' capacity math assume exactly these numbers, and the config lives in a different
/// assembly from those tests, so drift here would shift every pipe and molten scenario unnoticed. The
/// accessors return the coded defaults until <c>Load</c> runs, which the headless harness never does.
/// </summary>
public class ExlibNetworkDefaultsTests {
  [Fact]
  public void PipeNetworkDefaults_MatchPreMoveValues() {
    Assert.Equal(30f, ExlibValues.LitresPerPipe);
    Assert.Equal(8.0f, ExlibValues.GasLeakRate);
    Assert.Equal(10.0f, ExlibValues.LiquidLeakRate);
    Assert.Equal(50f, ExlibValues.EvaporationLitresPerDay);
    Assert.Equal(30f, ExlibValues.PipeOverpressureSeconds);
  }

  [Fact]
  public void MoltenNetworkDefaults_MatchPreMoveValues() {
    Assert.Equal(50, ExlibValues.MoltenFlowRate);
    Assert.Equal(10, ExlibValues.MoltenMinFlowAmount);
  }
}
