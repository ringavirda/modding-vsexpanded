using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Networks;

/// <summary>
/// Optional per-network strategy for gas vents: open connectors that draw gas away as a sink rather
/// than leaking it, such as a chimney capping a vertical pipe. The content mod supplies an instance
/// through the network factory at <c>RegisterNetworkType</c>; a network without one treats every open
/// end as a leak. The strategy holds per-network bookkeeping such as sound throttling, so the factory
/// creates a fresh instance per network.
/// </summary>
public interface IPipeVentStrategy {
  /// <summary>
  /// Returns <c>true</c> when the open <paramref name="face"/> of <paramref name="node"/> at
  /// <paramref name="pos"/> is a vent given its <paramref name="neighbour"/>, so the network counts it
  /// as a draw sink rather than a leak. On <c>true</c>, <paramref name="ventPos"/> is where venting
  /// effects play, typically the neighbour cell.
  /// </summary>
  bool TryClassifyVent(
    IBlockAccessor blockAccessor,
    BlockNetworkNode node,
    BlockPos pos,
    BlockFacing face,
    Block neighbour,
    out BlockPos ventPos
  );

  /// <summary>
  /// Draws gas out through the collected <paramref name="vents"/>, mutating <paramref name="state"/>'s
  /// volume and playing per-vent feedback, and drops bookkeeping for vents no longer active this tick.
  /// A <paramref name="liquid"/> run vents nothing. Returns the litres vented, for the network's
  /// throughput accounting.
  /// </summary>
  float Vent(
    IReadOnlyList<BlockPos> vents,
    PipeNetworkState state,
    bool liquid,
    BlockNetworkModSystem manager
  );
}
