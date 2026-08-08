using Vintagestory.API.Datastructures;

namespace LowPressureExpanded.BlockStructures.Boiler;

/// <summary>
/// Boiler placement and render geometry, surfaced through the generated attribute accessors so that
/// the abstract <see cref="BlockBoiler"/> base and the shared <c>BlockEntityBoiler</c> resolve the
/// offsets and the water-surface box without reading attributes by name. The concrete boilers
/// (Lancashire, Cornish) satisfy it through their generated members; the base casts to it.
/// </summary>
public interface IBoilerGeometry {
  JsonObject? FuelOffset { get; }
  JsonObject? ExhaustOutletOffset { get; }
  JsonObject? LidOffset { get; }
  JsonObject? SteamConnectorOffset { get; }
  JsonObject? LightSampleOffset { get; }
  JsonObject? ExplosionCenterOffset { get; }
  JsonObject? WaterRendererBox { get; }
}
