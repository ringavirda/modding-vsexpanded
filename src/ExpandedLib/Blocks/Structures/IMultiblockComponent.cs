namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// A functional component of a multiblock machine (a molten tap, a charging hopper, a tuyere) whose own
/// block entity is <b>not</b> the anchor, but which belongs to one. It resolves the anchor whose layout
/// owns its cell - the reverse lookup the anchor never stores, because the anchor pushes to its components
/// by structure-local offset and nothing points back, so the component scans up to find it (typically via a
/// <see cref="MultiblockAnchorLink{T}"/> over <see cref="BlockEntityMultiblockStructure.FindAnchorOwning{T}"/>).
/// <para>
/// The shared <see cref="BlockBehaviorMultiblockStructure"/> build-outline projection asks this so the
/// "show missing blocks" gesture is reachable from any real component, not only the core. A component with
/// no owning anchor in range (placed before its anchor, or a broken structure) returns null, and the
/// projection then does nothing - the one contract every functional component shares.
/// </para>
/// </summary>
public interface IMultiblockComponent
{
  /// <summary>The multiblock anchor whose layout owns this component's cell, or null when none is in range.</summary>
  BlockEntityMultiblockStructure? ResolveOwningAnchor();
}
