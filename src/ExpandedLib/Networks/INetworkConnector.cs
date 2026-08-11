using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Networks;

/// <summary>
/// The block-side answer to <see cref="INetworkMember"/>: anything a network pipe can connect to,
/// asked of the block rather than of a block entity. Implemented by <see cref="BlockNetworkNode"/>
/// (the pipes and canals themselves) and by structure blocks that expose a fixed port on certain
/// faces without being a full network node, such as the lancashire boiler's water intake. Ports are
/// valid connection targets but are not added to the network graph.
/// </summary>
public interface INetworkConnector : INetworkMember {
  /// <summary>True when this block exposes a network connector on <paramref name="face"/>.</summary>
  bool HasConnectorAt(BlockFacing face);

  /// <summary>
  /// Position-aware connector test, defaulting to <see cref="HasConnectorAt(BlockFacing)"/>.
  /// Per-cell connectors read the block entity at <paramref name="pos"/> to answer for that cell,
  /// declaring it as a plain <c>public</c> member (or an override of one) so it outranks this
  /// default. Explicit here because a derived interface cannot implement a base member any other
  /// way; see <see cref="INetworkMember"/>.
  /// </summary>
  bool INetworkMember.HasConnectorAt(
    IBlockAccessor world,
    BlockPos pos,
    BlockFacing face
  ) => HasConnectorAt(face);

  /// <summary>
  /// Severing is block-entity state, so the block-side answer reads the node at
  /// <paramref name="pos"/> and reports <c>false</c> when there is none.
  /// </summary>
  bool INetworkMember.IsConnectionBroken(IBlockAccessor world, BlockPos pos) =>
    world.GetBlockEntity(pos) is INetworkNode node && node.IsConnectionBroken();
}
