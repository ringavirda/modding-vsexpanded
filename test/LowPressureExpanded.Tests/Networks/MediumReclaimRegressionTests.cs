using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.Tests;
using LowPressureExpanded.BlockNetworkPipe;
using Vintagestory.API.MathTools;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// Regressions for the network pool itself. These are network-math tests, not scenarios: there is no
/// spatial world and nothing emerges from adjacency, they drive <see cref="PipeNetwork"/> directly.
/// </summary>
public class MediumReclaimRegressionTests {
  /// <summary>
  /// Over-pressure (volume above the 1-atm capacity, up to the burst ceiling) must survive a graph
  /// re-walk. Clamping gas at MaxVolume in OnMerge/OnSplitFragment dumps a pressurised run back to
  /// 1 atm on every split or merge, such as a valve toggle.
  /// </summary>
  [Fact]
  public void Over_pressure_survives_a_node_remove_and_readd() {
    // The cast (lpex) tier: LP steam sits at 3-5 atm, above the plated tier's burst rating.
    var (w, net) = PipeTestWorld.Run(5, "steel", capEnds: true);
    // Charge well above 1 atm (maxVolume = 5 * 30 = 150 L; ~3 atm).
    PipeTestWorld.Saturate(
      net,
      150f,
      "Steam",
      w.Accessor,
      maxOutputPressure: 5f
    );
    Assert.True(net.State!.Pressure > 2.5f);

    // Re-walk the graph: remove a middle cell (splits) then re-add it (merges) with no tick in
    // between, so nothing leaks and only the merge/split clamp is in play.
    var mid = new BlockPos(0, 0, 2);
    w.RemoveNode(mid);
    w.AddNode(mid, "pipe");

    var rejoined = w.NetworkAt(new BlockPos(0, 0, 0))!;
    Assert.True(
      ((PipeNetworkState)rejoined.State!).Pressure > 1.5f,
      "the re-walked run was dumped back toward 1 atm (over-pressure clamp regression)"
    );
  }

  /// <summary>
  /// A run fully drained but not yet cleared still carries its old medium label: the 3-second
  /// empty-clear delay keeps its State alive so a busy push-and-drain line does not flicker. A
  /// physically empty run (Volume 0) must let a new medium re-claim it rather than latching the stale
  /// label for those 3 seconds.
  /// </summary>
  [Fact]
  public void A_drained_run_accepts_the_other_medium_before_the_label_clears() {
    var w = new TestWorld();
    var net = PipeTestWorld.LooseNet(w.Networks, 3); // MaxVolume 90

    // Fill with water, then drain it fully without ticking past the clear delay: the run is
    // physically empty (Volume 0) but still labelled "Water".
    net.TryProduceLiquid(60f, 20f, 1f, w.Accessor);
    Assert.Equal(60f, net.TryConsumeLiquid(999f, w.Accessor), 3);
    Assert.Equal(0f, net.State!.Volume, 3);
    Assert.Equal("Water", net.State.MediumType); // stale display label survives

    // Steam reusing the empty pipes must not be latched out by the leftover "Water" label.
    bool ok = net.TryProduceGas(45f, 150f, "Steam", w.Accessor);

    Assert.True(ok, "an empty run must let a new medium re-claim it");
    Assert.Equal("Steam", net.State!.MediumType);
    Assert.Equal(45f, net.State.Volume, 3);
  }

  /// <summary>The mirror case: a drained but still-labelled gas run accepts water.</summary>
  [Fact]
  public void A_drained_gas_run_accepts_water_before_the_label_clears() {
    var w = new TestWorld();
    var net = PipeTestWorld.LooseNet(w.Networks, 3);

    net.TryProduceGas(45f, 150f, "Steam", w.Accessor);
    Assert.Equal(45f, net.TryConsumeGas(999f, w.Accessor), 3);
    Assert.Equal(0f, net.State!.Volume, 3);
    Assert.Equal("Steam", net.State.MediumType);

    bool ok = net.TryProduceLiquid(30f, 20f, 1f, w.Accessor);

    Assert.True(ok, "an empty run must let water re-claim it");
    Assert.Equal("Water", net.State!.MediumType);
    Assert.Equal(30f, net.State.Volume, 3);
  }

  /// <summary>A run still physically carrying a medium rejects the other one: the guard is relaxed
  /// only at Volume 0.</summary>
  [Fact]
  public void A_run_still_holding_a_medium_rejects_the_other() {
    var w = new TestWorld();
    var net = PipeTestWorld.LooseNet(w.Networks, 3);
    net.TryProduceLiquid(30f, 20f, 1f, w.Accessor); // Volume 30, "Water"

    Assert.False(net.TryProduceGas(10f, 120f, "Air", w.Accessor));
    Assert.Equal("Water", net.State!.MediumType);
  }
}
