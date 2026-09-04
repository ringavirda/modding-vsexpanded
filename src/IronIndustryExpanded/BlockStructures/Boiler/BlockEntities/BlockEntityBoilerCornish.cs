using ExpandedLib.Registries.Entities;

namespace IronIndustryExpanded.BlockStructures.Boiler.BlockEntities;

/// <summary>
/// The Cornish boiler: the cheap, iron-buildable entry tier. Smaller water and steam
/// capacity, slower conversion, and capped at 5 atm, so it cannot drive a Cornish
/// engine. Behavior lives in <see cref="BlockEntityBoiler"/>; this supplies the
/// variant stats only. The shorter water-surface footprint comes from the block's
/// <c>waterRendererBox</c> attribute.
/// </summary>
[BlockEntityRegister]
public class BlockEntityBoilerCornish : BlockEntityBoiler {
  protected override float Capacity => IiexValues.CornishBoilerCapacity;
  protected override float MinBoilWater => IiexValues.CornishBoilerMinBoilWater;
  protected override float MaxBoilWater => IiexValues.CornishBoilerMaxBoilWater;
  protected override float SteamPerSecond =>
    IiexValues.CornishBoilerSteamPerSecond;
  protected override float MaxOutputPressure =>
    IiexValues.CornishBoilerMaxOutputPressure;
  protected override int ExplosionRadius =>
    IiexValues.CornishBoilerExplosionRadius;
}
