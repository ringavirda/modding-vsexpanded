using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Boiler;

namespace SteelIndustryExpanded.BlockStructures.Boiler.BlockEntities;

/// <summary>
/// The Lancashire boiler, the steel high-pressure tier. All behavior lives in iiex's
/// <see cref="BlockEntityBoiler"/>; this supplies the variant stats only, read from siex's own
/// config section.
/// </summary>
[BlockEntityRegister]
public class BlockEntityBoilerLancashire : BlockEntityBoiler {
  protected override float Capacity => SiexValues.LancashireBoilerCapacity;
  protected override float MinBoilWater =>
    SiexValues.LancashireBoilerMinBoilWater;
  protected override float MaxBoilWater =>
    SiexValues.LancashireBoilerMaxBoilWater;
  protected override float SteamPerSecond =>
    SiexValues.LancashireBoilerSteamPerSecond;
  protected override float MaxOutputPressure =>
    SiexValues.LancashireBoilerMaxOutputPressure;
  protected override int ExplosionRadius =>
    SiexValues.LancashireBoilerExplosionRadius;
}
