using Vintagestory.API.Datastructures;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// A mega-block whose footprint cells are declared by its JSON <c>fillerOffsets</c> attribute,
/// surfaced through the generated <c>FillerOffsets</c> member.
/// <see cref="StructureFillers.FootprintCells"/> consumes this contract rather than reading the
/// attribute by name, so the attribute key lives only in the generated accessor. The attribute source
/// generator emits a matching <c>public JsonObject? FillerOffsets</c>, so a concrete block satisfies
/// this by adding <c>IFillerHost</c> to its class declaration; an abstract base that places fillers for
/// its subclasses casts <c>this</c> to <c>IFillerHost</c>.
/// </summary>
public interface IFillerHost {
  /// <summary>The block's <c>fillerOffsets</c> JSON node (the generated accessor), or null if none.</summary>
  JsonObject? FillerOffsets { get; }
}
