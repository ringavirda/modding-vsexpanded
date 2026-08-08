using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Registries.Entities;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// Block entity for the tuyere; a gas-pipe node that the furnace draws
/// air/blast from as a consumer.
/// <para>
/// As a cell of the furnace layout it is also an <see cref="IMultiblockComponent"/>: it scans up to the
/// core whose layout owns it so the shared build-outline projection is reachable from the tuyere too (a
/// tuyere on an incomplete furnace previews it; a bare tuyere does nothing). It shows no HUD slice of its
/// own beyond the pipe readout, so the link exists only for the projection.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityTuyere : BlockEntityPipe, IMultiblockComponent
{
  private MultiblockAnchorLink<BlockEntityFurnaceCore>? _anchor;

  /// <inheritdoc/>
  public BlockEntityMultiblockStructure? ResolveOwningAnchor() =>
    (
      _anchor ??= new MultiblockAnchorLink<BlockEntityFurnaceCore>(
        this,
        BlockEntityFurnaceCore.ComponentScanHorizontal,
        BlockEntityFurnaceCore.ComponentScanBelow,
        BlockEntityFurnaceCore.ComponentScanAbove
      )
    ).Resolve();
}
