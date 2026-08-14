using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkPipe;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Regression cover for the engine's production tick. Stands up a small world through
/// <see cref="Scene"/>, but the subject is a single machine's own tick, so it sits with the block tests.
/// </summary>
public class EngineRecursionRegressionTests {
  /// <summary>
  /// A constructed machine's production tick must not stack-overflow. Machines reach a network port
  /// through the <c>MachinePorts</c> extensions, and several wrap one in a same-named instance helper;
  /// instance methods shadow extensions, so a wrapper written without its type argument binds back to
  /// itself and recurses on every tick.
  /// </summary>
  [Fact]
  public void Constructed_engine_tick_does_not_recurse() {
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var eng = new EngineFixture(scene, new BlockPos(0, 8, 0));
    scene.Build();
    eng.SetInletPressure(3f);

    scene.Step(); // would stack-overflow if the network-port recursion returned

    Assert.True(eng.Engine.AvailablePower > 0f);
  }
}
