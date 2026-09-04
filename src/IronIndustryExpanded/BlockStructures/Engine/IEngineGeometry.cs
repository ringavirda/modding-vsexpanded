using Vintagestory.API.Datastructures;

namespace IronIndustryExpanded.BlockStructures.Engine;

/// <summary>
/// Engine placement geometry exposed through the generated attribute accessors, so the abstract
/// <see cref="BlockEngine"/> base resolves its offsets without reading attributes by name. Concrete
/// engines satisfy it through their generated <c>SubmachineOffset</c> / <c>GearHousingOffset</c>
/// members; the base casts <c>this</c> to this interface.
/// </summary>
public interface IEngineGeometry {
  /// <summary>Sub-machine cell offset (the block's <c>submachineOffset</c> node), or null.</summary>
  JsonObject? SubmachineOffset { get; }

  /// <summary>Gear-housing cell offset (the block's <c>gearHousingOffset</c> node), or null.</summary>
  JsonObject? GearHousingOffset { get; }
}
