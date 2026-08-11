using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Networks;

/// <summary>
/// One cell's participation in a block network, as the graph walk sees it. Answered either by a
/// <c>BEBehaviorNetworkMember</c> on the block entity or by the block itself through
/// <see cref="INetworkConnector"/>, so a cell stays a node while its chunk is unloaded and its block
/// entity is gone. See docs/design/mechanics/framework-composition.md.
/// </summary>
/// <remarks>
/// Most members below carry a default, and a default is silently reached when an implementor
/// declares its answer in a shape the interface map does not see. A per-cell answer must therefore
/// be a plain <c>public</c> class member, or an override of one. Never an explicit
/// <c>INetworkMember.Member(...)</c>, which nothing can then override, and never a fresh member on a
/// subclass of a class that already lists this interface, which never enters that class's map. Both
/// wrong shapes compile and are simply not called.
/// </remarks>
public interface INetworkMember {
  /// <summary>The network this cell belongs to, e.g. "pipe", "molten".</summary>
  string NetworkType { get; }

  /// <summary>Position-aware network type, for a member whose type varies by cell.</summary>
  string NetworkTypeAt(IBlockAccessor world, BlockPos pos) => NetworkType;

  /// <summary>True when this cell exposes a network connector on <paramref name="face"/>.</summary>
  bool HasConnectorAt(IBlockAccessor world, BlockPos pos, BlockFacing face);

  /// <summary>
  /// When <c>true</c>, this cell is a fixed endpoint and is excluded from neighbour discovery, so a
  /// run terminates here rather than continuing through.
  /// </summary>
  bool IsNetworkEndPoint => false;

  /// <summary>Whether this cell currently severs the network at its position, e.g. a closed valve.</summary>
  bool IsConnectionBroken(IBlockAccessor world, BlockPos pos) => false;

  /// <summary>
  /// Whether this cell will physically join <paramref name="neighbour"/>, on top of the geometric
  /// checks. Implementations must be symmetric: the graph asks from whichever side it walks, so a
  /// rule that answers differently by direction yields a one-directional network.
  /// </summary>
  bool AcceptsNeighbour(Block neighbour) => true;
}
