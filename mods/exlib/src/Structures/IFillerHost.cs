using Vintagestory.API.Datastructures;

namespace ExpandedLib.Structures;

/// <summary>
/// A mega-block whose footprint cells are declared by its <c>fillerOffsets</c> attribute node.
/// <see cref="StructureFillers.FootprintCells"/> consumes this contract rather than reading the
/// attribute by name, so the attribute key lives only in the accessor. The attribute is populated
/// either from blocktype JSON or, code-first, from
/// <see cref="ExpandedLib.Definitions.ExBlockDef.FillerOffsets"/>; a concrete block satisfies this
/// contract by adding <c>IFillerHost</c> to its class declaration (or inheriting
/// <see cref="BlockFilledMegastructure"/>, which already implements it), and returns null when it
/// declares no footprint.
/// </summary>
public interface IFillerHost {
  /// <summary>The block's <c>fillerOffsets</c> JSON node, or null if none.</summary>
  JsonObject? FillerOffsets { get; }
}
