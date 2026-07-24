using ExpandedLib.Networks;
using ExpandedLib.Testing;
using LowPressureExpanded.BlockNetworkPipe;
using Vintagestory.API.MathTools;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// One fixed engine bug, kept as its own reproduction. It stands up a small world through
/// <see cref="Scene"/>, but the subject is a single machine's own tick, so it is a block test.
/// </summary>
public class EngineRecursionRegressionTests
{
  /// <summary>
  /// A constructed machine's production tick must not stack-overflow. The bug:
  /// <c>BlockEntityProductionMachine.NetworkAt</c>/<c>ConnectedNetwork</c> delegated with
  /// <c>this.NetworkAt(...)</c>, which bound to the instance method itself (instance methods shadow
  /// extensions) and recursed forever on every machine tick.
  /// </summary>
  [Fact]
  public void Constructed_engine_tick_does_not_recurse()
  {
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var eng = new EngineFixture(scene, new BlockPos(0, 8, 0));
    scene.Build();
    eng.SetInletPressure(3f);

    scene.Step(); // would stack-overflow if the network-port recursion returned

    Assert.True(eng.Engine.AvailablePower > 0f);
  }
}
