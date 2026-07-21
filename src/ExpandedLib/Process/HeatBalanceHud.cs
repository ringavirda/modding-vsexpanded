using System;
using System.Text;
using Vintagestory.API.Config;

namespace ExpandedLib.Process;

/// <summary>
/// The lang keys a <see cref="HeatBalance"/> ledger reads, supplied by the machine so exlib stays
/// content-free (it ships no lang of its own). The shaft furnace passes its <c>bf-info-*</c> keys; the
/// Bessemer converter passes its own equivalents. Every line takes the same argument shape, so the two
/// machines share one formatter and differ only in the strings.
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
/// Formats the shared part of a furnace/converter heat-balance readout - internal temperature, the
/// threshold it must clear, the heat-in and heat-loss ledger, and the blast state - the way a
/// furnaceman reads one: where the hearth is, where it needs to be, and which side of the ledger is at
/// fault. Everything comes off the <see cref="HeatBalance"/> the tick already computed; nothing is
/// recalculated here. Machine-specific lines (a furnace's burden grade and melt rate, a converter's
/// carbon and slag) are appended by the caller after this ledger.
/// </summary>
public static class HeatBalanceHud
{
  /// <summary>
  /// Appends the five shared ledger lines to <paramref name="sb"/>. <paramref name="temp"/> formats a
  /// temperature for display (the measurement helper lives downstream of exlib, so it is passed in
  /// rather than referenced). <paramref name="thresholdTemp"/> is the process temperature the machine
  /// must hold to keep working (a furnace's melt point, the converter's refine floor).
  /// </summary>
  public static void AppendLedger(
    StringBuilder sb,
    in HeatBalance hb,
    float internalTemp,
    float thresholdTemp,
    in HeatBalanceLedgerKeys keys,
    Func<float, string> temp
  )
  {
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
