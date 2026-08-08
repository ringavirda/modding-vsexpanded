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
/// Reload reproduction for the in-line valve. A block-level test: one valve is driven directly and
/// no behaviour comes from adjacency.
/// </summary>
public class ValveReloadRegressionTests {
  /// <summary>
  /// Closing a valve drops the pool it cached while open. A persisted pool would be restored into
  /// the now-isolated cell and burst it.
  /// </summary>
  [Fact]
  public void Closed_valve_does_not_reload_a_pressurised_pool() {
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

  private static (TestWorld world, BlockEntityValve valve) ValveRun() {
    var world = new TestWorld();
    world.RegisterNetwork("pipe", s => new PipeNetwork(s));
    var pipe = PipeTestWorld.MakePipe();
    var valve = new BlockEntityValve {
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
