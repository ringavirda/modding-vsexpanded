namespace ExpandedLib.Structures;

/// <summary>
/// A functional component of a multiblock machine (a molten tap, a charging hopper, a tuyere) whose own
/// block entity is not the anchor but belongs to one. The anchor pushes to its components by
/// structure-local offset and stores no reverse link, so the component scans up to find the anchor whose
/// layout owns its cell, typically via a <see cref="MultiblockAnchorLink{T}"/> over
/// <see cref="BlockEntityMultiblockStructure.FindAnchorOwning{T}"/>. The shared
/// <see cref="BlockBehaviorMultiblockStructure"/> build-outline projection uses this so the missing-block
/// outline is reachable from any component, not only the core.
/// </summary>
public interface IMultiblockComponent {
  /// <summary>The multiblock anchor whose layout owns this component's cell, or null when none is in range.</summary>
  BlockEntityMultiblockStructure? ResolveOwningAnchor();
}
