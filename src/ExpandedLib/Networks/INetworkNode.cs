using Vintagestory.API.MathTools;

namespace ExpandedLib.Networks;

/// <summary>
/// Base interface for block entities that participate in a block network (gas pipes, molten canals).
/// </summary>
public interface INetworkNode {
  /// <summary>
  /// The first letter of each direction that has a network connector at this block, e.g. "ns" for
  /// north plus south. May be <c>null</c> while the block is loading.
  /// </summary>
  string? Orientation { get; }

  /// <summary>All orientation strings valid at this position, used for wrench cycling.</summary>
  string[] PossibleOrientations { get; }

  /// <summary>Network type identifier, e.g. "gas" or "molten".</summary>
  string NetworkType { get; }

  /// <summary>Returns <c>true</c> when this block has a connector on <paramref name="face"/>.</summary>
  bool HasConnectorAt(BlockFacing face);

  /// <summary>
  /// Called by the network tick with the connector faces that have no valid neighbour (open ends).
  /// Implementations may no-op.
  /// </summary>
  void OnOpenConnectorsChanged(BlockFacing[] openFaces);

  /// <summary>
  /// Called by the pipe network tick for a node on the open-ended boundary of a pressurised or
  /// flooded run, so it can emit leak feedback. <paramref name="isLiquid"/> distinguishes water from
  /// gas; <paramref name="intensity"/> is a density scale from 0 upwards. Implementations may no-op.
  /// </summary>
  void OnLeak(BlockFacing[] leakingFaces, bool isLiquid, float intensity);

  /// <summary>Receives the latest network state so clients can update their display.</summary>
  void OnNetworkUpdate(object? state);
}
