using ExpandedLib;
using IronworkingExpanded;
using System.Linq;
using ExpandedLib.Testing;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>Time-driven behaviour: throughput/pressure refresh, idle clearing, evaporation,
/// open-end leak loss, and over-pressure burst - all via <see cref="TestWorld.Tick"/>.</summary>
public class PipeTickTests
{
  [Fact]
  public void Tick_refreshes_pressure_and_flow_rate()
  {
    var (w, net) = PipeTestWorld.Run(3, "iron", capEnds: true);
    net.TryProduceGas(45f, 120f, "Steam", w.Accessor);

    w.Tick();

    Assert.True(
      net.State!.FlowRate > 0f,
      "throughput should register the production"
    );
    Assert.Equal(0.5f, net.State.Pressure, 2);
  }

  #region Passive cooling

  // These exist because passive cooling shipped as dead code and nothing noticed. It was
  // gated on `pass.Consumers == 0`, but Consumers counts one per pipe-node BE - i.e. once per segment -
  // so the guard could never be true on a run made of pipes. A suite with no test for it passes
  // identically whether the feature works or does not, which is exactly how it survived.

  [Fact]
  public void A_hot_gas_run_sheds_heat_toward_ambient()
  {
    // LiveRun, not Run: the bare fixture has no block entities, so the tick counts zero consumers
    // and the very guard that made this dead code in game is satisfied in the test. This must stand up
    // the same node classification the server sees or it proves nothing.
    var (w, net) = PipeTestWorld.LiveRun(3, "iron", capEnds: true);
    net.TryProduceGas(45f, 300f, "Steam", w.Accessor);

    w.Tick(5);

    Assert.True(
      net.State!.Temperature < 300f,
      $"parked gas must cool - stayed at {net.State.Temperature} C"
    );
  }

  [Fact]
  public void Cooling_stops_at_ambient_and_never_goes_below()
  {
    var (w, net) = PipeTestWorld.LiveRun(3, "iron", capEnds: true);
    net.TryProduceGas(45f, 30f, "Air", w.Accessor);

    w.Tick(60); // far longer than it takes to shed 10 C

    Assert.Equal(ExlibValues.PipeAmbientTemperature, net.State!.Temperature, 3);
  }

  // The "a flowing line stays hot" half is emergent, not a special case: cooling always applies, and
  // the volume-weighted blend in TryProduceGas pulls the average back up each time hot gas arrives.
  // That is why the fix removed the idle condition rather than refining it.
  [Fact]
  public void A_run_kept_fed_with_hot_gas_stays_hot()
  {
    var (w, net) = PipeTestWorld.LiveRun(3, "iron", capEnds: true);

    for (int i = 0; i < 10; i++)
    {
      net.TryProduceGas(45f, 300f, "Steam", w.Accessor);
      net.TryConsumeGas(45f, w.Accessor); // a consumer keeps drawing it through
      w.Tick();
    }

    Assert.True(
      net.State!.Temperature > 200f,
      $"a fed line should stay hot - fell to {net.State.Temperature} C"
    );
  }

  #endregion

  [Fact]
  public void Drained_run_clears_only_after_idle_delay()
  {
    var (w, net) = PipeTestWorld.Run(3, "iron", capEnds: true);
    net.TryProduceGas(45f, 120f, "Air", w.Accessor);
    net.TryConsumeGas(45f, w.Accessor); // volume now 0, but just had flow

    w.Tick(6); // > the 3 s empty-clear delay with no further flow

    Assert.Null(net.State);
  }

  [Fact]
  public void Water_run_evaporates_with_the_calendar()
  {
    var (w, net) = PipeTestWorld.Run(3, "iron", capEnds: true);
    // Fill over several passes: since the throughput gate one call moves at most the
    // weakest segment's litres-per-second, so a single huge push no longer brims a run.
    for (int i = 0; i < 32 && (net.State?.Volume ?? 0f) < 90f; i++)
      net.TryProduceLiquid(float.MaxValue, 20f, 1f, w.Accessor); // full = 90 L

    w.Tick(); // stamps the evaporation clock, charges nothing yet
    w.AdvanceDays(1);
    w.Tick();

    Assert.Equal(40f, net.State!.Volume, 1); // 90 - 50 L/day
  }

  [Fact]
  public void Open_ended_run_leaks_gas_each_tick()
  {
    var (w, net) = PipeTestWorld.Run(3, "iron", capEnds: false); // open air ends
    net.TryProduceGas(450f, 200f, "Steam", w.Accessor, maxOutputPressure: 10f);

    w.Tick();

    Assert.True(
      net.State!.OpeningsCount > 0,
      "open ends should be detected as leaks"
    );
    Assert.True(net.State.Volume < 450f, "leaking gas should bleed volume");
  }

  [Fact]
  public void Idle_sealed_gas_run_cools_two_degrees_per_tick()
  {
    // Passive cooling pass: a sealed hot gas run with no consumers drawing it sheds a fixed 2 C per
    // tick. Pinned across two post-production ticks so it's independent of the exact production temp.
    var (w, net) = PipeTestWorld.Run(3, "iron", capEnds: true);
    net.TryProduceGas(45f, 120f, "Steam", w.Accessor);

    w.Tick();
    float t1 = net.State!.Temperature;
    w.Tick();
    float t2 = net.State!.Temperature;

    Assert.True(t1 > 22f, "the run should still be hot after one cooling tick");
    Assert.Equal(t1 - 2f, t2, 2); // idle gas cools a fixed 2 C/tick (no consumers, no leaks)
  }

  [Fact]
  public void Sealed_overpressured_run_bursts_after_the_grace_period()
  {
    var (w, net) = PipeTestWorld.Run(3, "iron", capEnds: true);
    PipeTestWorld.Saturate(net, 200f, "Steam", w.Accessor, maxOutputPressure: 10f);
    // Sitting exactly at the tier's burst pressure, derived from config rather than restated.
    Assert.Equal(
      3 * PipeTestWorld.LitresPerPipe * IwexValues.PlatedPipeBurstPressure,
      net.State!.Volume,
      3
    );

    w.Tick(30); // PipeOverpressureSeconds

    Assert.NotEmpty(w.Drops); // a burst pipe drops its materials
    int remaining = w.Networks.AllNetworks.Sum(n => n.Nodes.Count);
    Assert.True(remaining < 3, "a pipe should have burst out of the run");
  }
}
