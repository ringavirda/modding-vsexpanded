using Vintagestory.API.Datastructures;

namespace IronIndustryExpanded.BlockStructures.Boiler;

/// <summary>
/// Boiler placement and render geometry, surfaced through the generated attribute accessors so that
/// the abstract <see cref="BlockBoiler"/> base and the shared <c>BlockEntityBoiler</c> resolve the
/// offsets and the water-surface box without reading attributes by name. The concrete boilers
/// (Lancashire, Cornish) satisfy it through their generated members; the base casts to it.
/// </summary>
public interface IBoilerGeometry {
  JsonObject? FuelOffset { get; }
  JsonObject? ExhaustOutletOffset { get; }
  JsonObject? MainHatchOffset { get; }
  JsonObject? ManHatchOffset { get; }
  JsonObject? SteamConnectorOffset { get; }
  JsonObject? LightSampleOffset { get; }
  JsonObject? ExplosionCenterOffset { get; }
  JsonObject? WaterRendererBox { get; }

  /// <summary>
  /// North-orientation face the feedwater pipe couples to, as a side word or letter. A vessel whose
  /// principal cell stands at the open end of the body takes water on a horizontal face; one walled in
  /// on every side takes it from below. <c>null</c> falls back to <c>down</c>.
  /// </summary>
  string? FeedwaterFace { get; }
}
