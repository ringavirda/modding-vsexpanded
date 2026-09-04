using System;
using System.Text;
using Vintagestory.API.Config;

namespace ExpandedLib.Heat;

/// <summary>
/// The lang keys a <see cref="HeatBalance"/> ledger reads, supplied by the machine because exlib
/// ships no lang of its own. The argument shape of each line is the same for every machine.
/// </summary>
/// <param name="Temp">Internal temperature line (one temperature arg).</param>
/// <param name="HeatOk">At-or-above-threshold line (one temperature arg: the threshold).</param>
/// <param name="HeatStall">Below-threshold line (one temperature arg: the threshold).</param>
/// <param name="HeatIn">Heat-in line (temperature, fuel %, preheat temperature).</param>
/// <param name="HeatLoss">Heat-loss line (total, charge-mass, ambient temperatures).</param>
/// <param name="BlastNone">No-blast line (one temperature arg: the blast temperature).</param>
/// <param name="BlastHot">Preheated-blast line (one temperature arg: the blast temperature).</param>
/// <param name="BlastCold">Cold-blast line (one temperature arg: the blast temperature).</param>
public readonly record struct HeatBalanceLedgerKeys(
  string Temp,
  string HeatOk,
  string HeatStall,
  string HeatIn,
  string HeatLoss,
  string BlastNone,
  string BlastHot,
  string BlastCold
);

/// <summary>
/// Formats the shared part of a furnace or converter heat-balance readout: internal temperature, the
/// threshold it must clear, the heat-in and heat-loss ledger, and the blast state. Every figure comes
/// off the <see cref="HeatBalance"/> the tick computed; nothing is recalculated. The caller appends
/// its machine-specific lines after this ledger.
/// </summary>
public static class HeatBalanceHud {
  /// <summary>Appends the five shared ledger lines to <paramref name="sb"/>.
  /// <paramref name="temp"/> formats a temperature for display, passed in because the measurement
  /// helper lives downstream of exlib. <paramref name="thresholdTemp"/> is the temperature the
  /// machine must hold to keep working (a furnace's melt point, the converter's refine floor).</summary>
  public static void AppendLedger(
    StringBuilder sb,
    in HeatBalance hb,
    float internalTemp,
    float thresholdTemp,
    in HeatBalanceLedgerKeys keys,
    Func<float, string> temp
  ) {
    sb.AppendLine(Lang.Get(keys.Temp, temp(internalTemp)));

    sb.AppendLine(
      Lang.Get(
        internalTemp >= thresholdTemp ? keys.HeatOk : keys.HeatStall,
        temp(thresholdTemp)
      )
    );

    sb.AppendLine(
      Lang.Get(
        keys.HeatIn,
        temp(hb.TIn),
        (int)Math.Round(hb.FuelFrac * 100f),
        temp(hb.PreheatGain)
      )
    );

    sb.AppendLine(
      Lang.Get(
        keys.HeatLoss,
        temp(hb.TLoss),
        temp(hb.ChargeLoss),
        temp(hb.AmbientLoss)
      )
    );

    sb.AppendLine(
      Lang.Get(
        !hb.BlastSupplied ? keys.BlastNone
          : hb.IsHotBlast ? keys.BlastHot
          : keys.BlastCold,
        temp(hb.BlastTemp)
      )
    );
  }
}
