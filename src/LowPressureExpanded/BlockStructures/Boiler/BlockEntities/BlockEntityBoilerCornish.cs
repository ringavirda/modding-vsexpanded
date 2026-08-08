using ExpandedLib.Registries.Entities;

namespace LowPressureExpanded.BlockStructures.Boiler.BlockEntities;

/// <summary>
/// The Cornish boiler: the cheap, iron-buildable entry tier. Smaller water and steam
/// capacity, slower conversion, and capped at 5 atm, so it cannot drive a Cornish
/// engine. Behavior lives in <see cref="BlockEntityBoiler"/>; this supplies the
/// variant stats only. The shorter water-surface footprint comes from the block's
/// <c>waterRendererBox</c> attribute.
/// </summary>
[BlockEntityRegister]
public class BlockEntityBoilerCornish : BlockEntityBoiler {
  protected override float Capacity => LpexValues.CornishBoilerCapacity;
  protected override float MinBoilWater => LpexValues.CornishBoilerMinBoilWater;
  protected override float MaxBoilWater => LpexValues.CornishBoilerMaxBoilWater;
  protected override float SteamPerSecond =>
    LpexValues.CornishBoilerSteamPerSecond;
  protected override float MaxOutputPressure =>
    LpexValues.CornishBoilerMaxOutputPressure;
  protected override int ExplosionRadius =>
    LpexValues.CornishBoilerExplosionRadius;
}
