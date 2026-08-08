using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Registries.Entities;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// Block entity for the tuyere: a gas-pipe node the furnace draws air or blast from as a consumer. As a
/// cell of the furnace layout it is also an <see cref="IMultiblockComponent"/>, scanning up to the core
/// whose layout owns it so the shared build-outline projection is reachable from the tuyere; a bare
/// tuyere resolves no core and does nothing. It adds no HUD beyond the pipe readout, so the link exists
/// only for the projection.
/// </summary>
[BlockEntityRegister]
public class BlockEntityTuyere : BlockEntityPipe, IMultiblockComponent {
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
