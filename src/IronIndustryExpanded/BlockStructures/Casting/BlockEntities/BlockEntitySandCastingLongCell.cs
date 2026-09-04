using ExpandedLib.Registries.Entities;

namespace IronIndustryExpanded.BlockStructures.Casting.BlockEntities;

/// <summary>
/// The 1×2 sand casting long cell, the cast-stock station: rammed with green sand, impressed with a
/// long-cell pattern, fed from a canal on its launder face and shaken out for billets, blooms or a slab.
/// It runs the 1×1 cell's loop (ram, imprint, pour, shake out) at a larger size and differs from
/// <see cref="BlockEntitySandCastingCell"/> only in the <see cref="MoldSize"/> it accepts.
/// <para>
/// The impression is a single molten pool: the second cell is a filler for collision and interaction and
/// hosts no molten cell (see <see cref="LongCellLayout"/>). A multi-lane pattern yields several items out
/// of one charge, and <c>capacity</c> covers the whole impression, so a short pour fills lanes
/// progressively and comes out as scrap.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntitySandCastingLongCell : BlockEntitySandCastingCell {
  /// <inheritdoc/>
  protected override MoldSize AcceptedSize => MoldSize.LongCell;

  /// <inheritdoc/>
  protected override string WrongSizeErrorCode => "iiex-longcell-wrongsize";
}
