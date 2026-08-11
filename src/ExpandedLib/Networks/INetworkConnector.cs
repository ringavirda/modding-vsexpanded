using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Networks;

/// <summary>
/// A port: a face another network may couple to, on a block that is not itself a graph member - the
/// lancashire boiler's water intake is one. A port is a valid connection target and is never added to
/// the graph. Membership is a different thing and belongs to <c>BEBehaviorNetworkMember</c>: a pipe is
/// a member, and a block that is on a network carries one of those rather than a port.
/// <para>
/// This is also how a block answers <see cref="INetworkMember"/>, which is what lets a cell stay
/// resolvable when it has no block entity - the second arm of <c>NetworkMembership.Resolve</c>. That is
/// why <see cref="BlockNetworkNode"/> implements it: its own cells answer the walk from the block.
/// </para>
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
