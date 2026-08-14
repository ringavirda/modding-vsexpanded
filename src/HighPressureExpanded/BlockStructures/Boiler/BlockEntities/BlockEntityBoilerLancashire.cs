using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Boiler;

namespace HighPressureExpanded.BlockStructures.Boiler.BlockEntities;

/// <summary>
/// The Lancashire boiler, the steel high-pressure tier. All behavior lives in iiex's
/// <see cref="BlockEntityBoiler"/>; this supplies the variant stats only, read from hpex's own
/// config section.
/// </summary>
[BlockEntityRegister]
public class BlockEntityBoilerLancashire : BlockEntityBoiler {
  protected override float Capacity => HpexValues.LancashireBoilerCapacity;
  protected override float MinBoilWater =>
    HpexValues.LancashireBoilerMinBoilWater;
  protected override float MaxBoilWater =>
    HpexValues.LancashireBoilerMaxBoilWater;
  protected override float SteamPerSecond =>
    HpexValues.LancashireBoilerSteamPerSecond;
  protected override float MaxOutputPressure =>
    HpexValues.LancashireBoilerMaxOutputPressure;
  protected override int ExplosionRadius =>
    HpexValues.LancashireBoilerExplosionRadius;
}
