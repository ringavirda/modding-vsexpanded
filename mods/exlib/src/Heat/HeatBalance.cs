using System;

namespace ExpandedLib.Heat;

/// <summary>One evaluation of a process heat balance, captured as the tick computed it so block info
/// reads the contributors without recomputing. Shared by the shaft furnace (coke combustion) and the
/// Bessemer converter (autothermal). See docs/design/mechanics/heat-balance.md.</summary>
/// <param name="TIn">Heat the burning or oxidising charge makes (C).</param>
/// <param name="TLoss">Heat taken back by radiation, cold charge mass and a cold ambient (C).</param>
/// <param name="TProcess">Settled temperature: <c>TIn - TLoss</c>, floored at ambient.</param>
/// <param name="FuelFrac">Coke fraction of the burden column, 0-1.</param>
/// <param name="FuelFactor">Unitless combustion multiplier from the coke ratio.</param>
/// <param name="AirFactor">Unitless combustion multiplier from blast reaching the tuyeres.</param>
/// <param name="BlastSupplied">Whether any tuyere drew pressurised air this tick.</param>
/// <param name="BlastTemp">Blast temperature at the tuyeres (C); ambient unless a cowper preheats it.</param>
/// <param name="PreheatGain">Share of <paramref name="TIn"/> from that preheat (C).</param>
/// <param name="ChargeLoss">Share of <paramref name="TLoss"/> from cold charge mass (C).</param>
/// <param name="AmbientLoss">Share of <paramref name="TLoss"/> from a below-reference ambient (C).</param>
/// <param name="TransferLoss">Share of <paramref name="TLoss"/> spent carrying the flame to the work (C).
/// Zero wherever fuel and work share a chamber; a reverberatory furnace pays it across the bridge.</param>
public readonly record struct HeatBalance(
  float TIn,
  float TLoss,
  float TProcess,
  float FuelFrac,
  float FuelFactor,
  float AirFactor,
  bool BlastSupplied,
  float BlastTemp,
  float PreheatGain,
  float ChargeLoss,
  float AmbientLoss,
  float TransferLoss = 0f
) {
  /// <summary>Whether the blast arrives preheated by a cowper rather than cold off the blower. The
  /// half-degree threshold keeps float noise from reading as a hot blast.</summary>
  public bool IsHotBlast => PreheatGain > 0.5f;

  /// <summary>
  /// Applies <c>T_process = T_in - T_loss</c>, floored at ambient and with no upper bound. The caller
  /// derives <paramref name="tIn"/> and <paramref name="tLoss"/> in its own domain and passes them
  /// with the contributor figures the block info reads.
  /// </summary>
  public static HeatBalance Compute(
    float tIn,
    float tLoss,
    float ambient,
    float fuelFrac,
    float fuelFactor,
    float airFactor,
    bool blastSupplied,
    float blastTemp,
    float preheatGain,
    float chargeLoss,
    float ambientLoss,
    float transferLoss = 0f
  ) =>
    new(
      tIn,
      tLoss,
      Math.Max(ambient, tIn - tLoss),
      fuelFrac,
      fuelFactor,
      airFactor,
      blastSupplied,
      blastTemp,
      preheatGain,
      chargeLoss,
      ambientLoss,
      transferLoss
    );
}
