using ExpandedLib.Registries.Entities;
using LowPressureExpanded.BlockStructures.Boiler;

namespace HighPressureExpanded.BlockStructures.Boiler.BlockEntities;

/// <summary>
/// The Lancashire boiler - the steel, high-pressure tier (chokes at 12 atm). All behavior lives in
/// lpex's <see cref="BlockEntityBoiler"/>; this only supplies the variant stats, read from hpex's
/// own config section.
/// </summary>
[BlockEntityRegister]
public class BlockEntityBoilerLancashire : BlockEntityBoiler
{
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
