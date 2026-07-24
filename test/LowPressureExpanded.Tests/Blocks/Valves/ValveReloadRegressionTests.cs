using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.Tests;
using LowPressureExpanded.BlockNetworkPipe;
using LowPressureExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// One fixed valve bug, kept as its own reproduction. A block test, not a scenario: a single valve is
/// driven directly and nothing emerges from adjacency.
/// </summary>
public class ValveReloadRegressionTests
{
  /// <summary>
  /// A closed valve must not restore a pressurised pool on reload. The bug: a valve cached its pool
  /// while open, then persisted+restored it into the now-isolated cell, bursting it.
  /// </summary>
  [Fact]
  public void Closed_valve_does_not_reload_a_pressurised_pool()
  {
    var (world, valve) = ValveRun();
    valve.ToggleOpen(); // open

    var net = (PipeNetwork)world.NetworkAt(valve.Pos)!;
    net.TryProduceGas(
      450f,
      150f,
      "Steam",
      world.Accessor,
      maxOutputPressure: 10f
    );
    net.BroadcastUpdate(world.Accessor);

    valve.ToggleOpen(); // close - must drop the cached pool

    Assert.Null(ReflectionHelpers.GetField(valve, "_savedNetworkState"));
    Assert.Equal(0f, valve.Pressure, 3);
  }

  private static (TestWorld world, BlockEntityValve valve) ValveRun()
  {
    var world = new TestWorld();
    world.RegisterNetwork("pipe", s => new PipeNetwork(s));
    var pipe = PipeTestWorld.MakePipe();
    var valve = new BlockEntityValve
    {
      Pos = new BlockPos(0, 0, 1),
      Block = pipe,
    };
    world.Place(new BlockPos(0, 0, 0), pipe);
    world.Place(new BlockPos(0, 0, 1), pipe, valve);
    world.Place(new BlockPos(0, 0, 2), pipe);
    var rock = TestBlocks.Configure(new Block(), "game:rock", 99);
    world.Place(new BlockPos(0, 0, -1), rock);
    world.Place(new BlockPos(0, 0, 3), rock);
    world.Initialize(valve);
    world.AddNode(new BlockPos(0, 0, 0), "pipe");
    world.AddNode(new BlockPos(0, 0, 2), "pipe");
    return (world, valve);
  }
}
