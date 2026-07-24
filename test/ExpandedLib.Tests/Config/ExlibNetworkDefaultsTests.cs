using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Locks the exlib network-tunable defaults. These constants moved out of the lpex / iwex configs
/// into <see cref="ExlibValues"/> when the concrete <c>PipeNetwork</c> / <c>MoltenNetwork</c> moved
/// into exlib. The headless network tests and the pipe fixtures' capacity math assume exactly these
/// numbers, and the config now lives in a different assembly from those tests, so a silent drift
/// here would quietly shift every pipe / molten scenario. (The accessor returns the coded defaults
/// until <c>Load</c> runs, which it never does in the headless harness - which is what we assert.)
/// </summary>
public class ExlibNetworkDefaultsTests
{
  [Fact]
  public void PipeNetworkDefaults_MatchPreMoveValues()
  {
    Assert.Equal(30f, ExlibValues.LitresPerPipe);
    Assert.Equal(8.0f, ExlibValues.GasLeakRate);
    Assert.Equal(10.0f, ExlibValues.LiquidLeakRate);
    Assert.Equal(50f, ExlibValues.EvaporationLitresPerDay);
    Assert.Equal(30f, ExlibValues.PipeOverpressureSeconds);
  }

  [Fact]
  public void MoltenNetworkDefaults_MatchPreMoveValues()
  {
    Assert.Equal(50, ExlibValues.MoltenFlowRate);
    Assert.Equal(10, ExlibValues.MoltenMinFlowAmount);
  }
}
