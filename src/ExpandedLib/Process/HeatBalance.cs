using System;

namespace ExpandedLib.Process;

/// <summary>
/// One evaluation of a metallurgical process's dynamic heat balance: the heat its charge makes, the
/// heat its surroundings take back, and the process temperature that leaves. Every figure the block
/// info shows is captured here at the moment the tick computed it, so the HUD reports the contributors
/// rather than recomputing them per looking player - and so a stall reads as "1410 C, needs 1482 C,
/// add coke or hot blast" instead of an unexplained number.
/// <para>
/// Shared by the shaft furnace (which supplies a coke-combustion <see cref="TIn"/>) and the Bessemer
/// converter (which supplies an autothermal one): both compute their own domain-specific
/// <see cref="TIn"/>/<see cref="TLoss"/> and call <see cref="Compute"/> to settle the process
/// temperature and package the contributors. Lifted out of the iwex furnace core so the two do not
/// carry independent copies of the <c>T_process = T_in - T_loss</c> law.
/// </para>
/// </summary>
/// <param name="TIn">Heat the burning/oxidising charge makes (C).</param>
/// <param name="TLoss">Heat radiation, cold charge mass and a cold day take back (C).</param>
/// <param name="TProcess">Temperature the process settles at: <c>TIn - TLoss</c>, floored at ambient.</param>
/// <param name="FuelFrac">Coke fraction of the burden column driving <paramref name="FuelFactor"/>.</param>
/// <param name="FuelFactor">Combustion multiplier from the coke ratio.</param>
/// <param name="AirFactor">Combustion multiplier from how much blast actually reached the tuyeres.</param>
/// <param name="BlastSupplied">Whether any tuyere drew pressurised air this tick.</param>
/// <param name="BlastTemp">Blast temperature at the tuyeres (C); ambient unless a cowper preheats it.</param>
/// <param name="PreheatGain">Share of <paramref name="TIn"/> contributed by that preheat (C).</param>
/// <param name="ChargeLoss">Share of <paramref name="TLoss"/> from cold charge mass (C).</param>
/// <param name="AmbientLoss">Share of <paramref name="TLoss"/> from a below-reference ambient (C).</param>
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
  float AmbientLoss
)
{
  /// <summary>Whether the blast arrives preheated (a cowper is on the line) rather than cold off the
  /// blower. Half a degree of headroom keeps float noise from reading as a hot blast.</summary>
  public bool IsHotBlast => PreheatGain > 0.5f;

  /// <summary>
  /// The shared law: <c>T_process = T_in - T_loss</c>, floored at ambient, with no maximum temperature
  /// anywhere in it. The caller derives <paramref name="tIn"/> and <paramref name="tLoss"/> in its own
  /// domain (coke combustion for the shaft furnace, carbon oxidation for the converter) and hands them
  /// in along with the contributor figures the HUD reads; this settles the process temperature and
  /// packages the balance so both machines share one place the floor and the record shape live.
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
    float ambientLoss
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
      ambientLoss
    );
}
