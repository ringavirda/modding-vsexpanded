using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.Tests;
using LowPressureExpanded.BlockNetworkPipe;
using Vintagestory.API.MathTools;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// Named reproductions of fixed bugs in the network pool itself, so they can never silently return.
/// These are <b>network-math</b> tests, not scenarios: there is no spatial world and nothing emerges
/// from adjacency - they drive <see cref="PipeNetwork"/> directly. They used to sit under
/// <c>Scenarios/</c>, which made that folder read as "wherever a regression happened to land" rather
/// than "several blocks in a world, behaving".
/// </summary>
public class MediumReclaimRegressionTests
{
  /// <summary>
  /// Over-pressure (volume above the 1-atm capacity, up to the burst ceiling) must survive a graph
  /// re-walk. The bug: OnMerge/OnSplitFragment clamped gas at MaxVolume, so every split/merge (e.g. a
  /// valve toggle) dumped a pressurised run back to 1 atm.
  /// </summary>
  [Fact]
  public void Over_pressure_survives_a_node_remove_and_readd()
  {
    // The cast (lpex) tier: LP steam sits at 3-5 atm, which is above the bolted tier's burst rating -
    // carrying steam on bolted pipe is exactly what the tier ladder forbids.
    var (w, net) = PipeTestWorld.Run(5, "steel", capEnds: true);
    // Charge well above 1 atm (maxVolume = 5 * 30 = 150 L; ~3 atm).
    net.TryProduceGas(450f, 150f, "Steam", w.Accessor, maxOutputPressure: 5f);
    Assert.True(net.State!.Pressure > 2.5f);

    // Re-walk the graph: remove a middle cell (splits) then re-add it (merges) with no tick in
    // between, so nothing leaks - isolating the merge/split clamp.
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
  /// A run that has been fully drained but not yet cleared (the 3-second empty-clear delay keeps its
  /// State alive so a busy push-and-drain line does not flicker) still carries its old medium LABEL.
  /// The bug class (same shape as the cowper mix-latch): a producer of the OTHER medium read that
  /// stale label and was rejected, so repurposing empty pipes was blocked for up to 3 seconds. A
  /// physically empty run (Volume 0) must let a new medium re-claim it.
  /// </summary>
  [Fact]
  public void A_drained_run_accepts_the_other_medium_before_the_label_clears()
  {
    var w = new TestWorld();
    var net = PipeTestWorld.LooseNet(w.Networks, 3); // MaxVolume 90

    // Fill with water, then drain it fully WITHOUT ticking past the clear delay: the run is
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

  /// <summary>The mirror of the above: a drained-but-labelled GAS run accepts water.</summary>
  [Fact]
  public void A_drained_gas_run_accepts_water_before_the_label_clears()
  {
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

  /// <summary>A run still physically carrying a medium must STILL reject the other one - the fix
  /// only relaxes the guard for an empty run, not a full one (guards against over-fixing).</summary>
  [Fact]
  public void A_run_still_holding_a_medium_rejects_the_other()
  {
    var w = new TestWorld();
    var net = PipeTestWorld.LooseNet(w.Networks, 3);
    net.TryProduceLiquid(30f, 20f, 1f, w.Accessor); // Volume 30, "Water"

    Assert.False(net.TryProduceGas(10f, 120f, "Air", w.Accessor));
    Assert.Equal("Water", net.State!.MediumType);
  }
}
