using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Networks;

/// <summary>
/// Block-level interface for anything a network pipe can connect to. Implemented by
/// <see cref="BlockNetworkNode"/> (the pipes and canals themselves) and by structure blocks that
/// expose a fixed port on certain faces without being a full network node, such as the lancashire
/// boiler's water intake. Ports are valid connection targets but are not added to the network graph.
/// </summary>
public interface INetworkConnector {
  /// <summary>Network type this connector belongs to, e.g. "gas", "molten", "pipe".</summary>
  string NetworkType { get; }

  /// <summary>True when this block exposes a network connector on <paramref name="face"/>.</summary>
  bool HasConnectorAt(BlockFacing face);

  /// <summary>
  /// Position-aware network type, defaulting to <see cref="NetworkType"/>. A structure filler that
  /// exposes a port on one footprint cell overrides this to report a network on that cell only.
  /// </summary>
  string NetworkTypeAt(IBlockAccessor world, BlockPos pos) => NetworkType;

  /// <summary>
  /// Position-aware connector test, defaulting to <see cref="HasConnectorAt(BlockFacing)"/>.
  /// Per-cell connectors read the block entity at <paramref name="pos"/> to answer for that cell.
  /// </summary>
  bool HasConnectorAt(IBlockAccessor world, BlockPos pos, BlockFacing face) =>
    HasConnectorAt(face);
}
