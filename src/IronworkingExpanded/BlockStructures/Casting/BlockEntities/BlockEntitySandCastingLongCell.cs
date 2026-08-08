using ExpandedLib.Registries.Entities;

namespace IronworkingExpanded.BlockStructures.Casting.BlockEntities;

/// <summary>
/// The 1×2 sand casting <b>long cell</b>: the cast-stock station. Rammed with green sand, impressed with a
/// long-cell pattern, fed from a canal on its launder face, and shaken out for billets, blooms or a slab.
/// <para>
/// <b>It is the 1×1 cell's loop unchanged, at a bigger size</b> - ram, imprint, pour, shake out - so it
/// is a subclass that changes which <see cref="MoldSize"/> it accepts and nothing else. Duplicating
/// <see cref="BlockEntitySandCastingCell"/> would mean two copies of the intake, the misrun rule, the
/// harvest denomination and the renderer, drifting apart on the first fix applied to one of them.
/// </para>
/// <para>
/// <b>The impression is one pool, not two.</b> The second cell is a filler for collision and interaction
/// only and hosts no molten cell - see <see cref="LongCellLayout"/>. A multi-lane pattern still yields more
/// than one item, but it does so out of a single charge: <c>capacity</c> is the whole impression, so a
/// short pour fills lanes progressively and comes out as scrap exactly like any other under-filled cast.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntitySandCastingLongCell : BlockEntitySandCastingCell
{
  /// <inheritdoc/>
  protected override MoldSize AcceptedSize => MoldSize.LongCell;

  /// <inheritdoc/>
  protected override string WrongSizeErrorCode => "iwex-longcell-wrongsize";
}
