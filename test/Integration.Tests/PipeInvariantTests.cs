using System;
using System.Linq;
using ExpandedLib.Testing;
using Xunit;

namespace PipesAndPowerExpanded.Tests;

/// <summary>
/// Property/invariant tests: instead of one hand-picked scenario, hammer the pipe simulation with
/// many randomized sequences of produce/consume/tick and assert the laws that must hold for ALL of
/// them - finite, non-negative state; pressure never past the burst ceiling without bursting; no
/// gas created from nothing. These catch whole classes of bugs (NaNs, runaway pressure, sign slips)
/// that example tests miss.
/// </summary>
public class PipeInvariantTests
{
  // The weakest (iron) pipe bursts at 5 atm; over-pressure may sit AT the ceiling but never above.
  private const float IronBurst = 5.0f;

  private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

  [Theory]
  [InlineData(1)]
  [InlineData(7)]
  [InlineData(13)]
  [InlineData(99)]
  [InlineData(1234)]
  public void Sealed_gas_run_never_goes_NaN_or_past_the_burst_ceiling(int seed)
  {
    var rng = new Random(seed);
    int length = rng.Next(1, 9);
    var (w, net) = PipeTestWorld.Run(length, "iron", capEnds: true);

    for (int op = 0; op < 40; op++)
    {
      switch (rng.Next(3))
      {
        case 0:
          net.TryProduceGas(
            (float)rng.NextDouble() * 400f,
            20f + (float)rng.NextDouble() * 300f,
            "Steam",
            w.Accessor,
            maxOutputPressure: 1f + (float)rng.NextDouble() * 12f
          );
          break;
        case 1:
          net.TryConsumeGas((float)rng.NextDouble() * 400f, w.Accessor);
          break;
        default:
          w.Tick(rng.Next(1, 4));
          break;
      }

      var s = net.State;
      if (s == null)
        continue;

      Assert.True(Finite(s.Volume) && Finite(s.Pressure) && Finite(s.Temperature),
        $"state went non-finite (seed {seed}, op {op})");
      Assert.True(s.Volume >= -0.001f, $"negative volume {s.Volume} (seed {seed})");
      // Over-pressure is allowed up to the burst ceiling; beyond it the run must have burst
      // (fewer nodes) rather than hold an impossible pressure.
      bool burst = w.Networks.AllNetworks.Sum(n => n.Nodes.Count) < length;
      Assert.True(
        s.Pressure <= IronBurst + 0.01f || burst,
        $"pressure {s.Pressure} exceeded burst ceiling without bursting (seed {seed})"
      );
    }
  }

  [Theory]
  [InlineData(2)]
  [InlineData(42)]
  [InlineData(777)]
  public void Consume_never_returns_more_than_was_present(int seed)
  {
    var rng = new Random(seed);
    var (w, net) = PipeTestWorld.Run(rng.Next(1, 6), "iron", capEnds: true);
    net.TryProduceGas(200f, 150f, "Steam", w.Accessor, maxOutputPressure: 10f);

    float beforeVol = net.State?.Volume ?? 0f;
    float drawn = net.TryConsumeGas(1_000_000f, w.Accessor); // ask for far more than exists

    Assert.True(drawn <= beforeVol + 0.001f, $"drew {drawn} from {beforeVol} (seed {seed})");
    Assert.True(drawn >= 0f);
    Assert.True((net.State?.Volume ?? 0f) >= -0.001f);
  }
}
