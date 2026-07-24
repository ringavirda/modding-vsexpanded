using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Networks;

/// <summary>
/// Optional per-network strategy for gas <em>vents</em> - open connectors that draw gas away as a
/// sink rather than leaking it (e.g. a chimney capping a vertical pipe). The content mod supplies an
/// instance at <c>RegisterNetworkType</c> (via the network factory); a network with no strategy
/// treats every open end as a leak. The strategy owns whatever per-network bookkeeping the venting
/// needs (sound throttling, etc.), so a fresh instance is created per network by the factory.
/// </summary>
public interface IPipeVentStrategy
{
  /// <summary>
  /// Returns <c>true</c> when the open <paramref name="face"/> of <paramref name="node"/> at
  /// <paramref name="pos"/> is a vent (given its <paramref name="neighbour"/>), so the network counts
  /// it as a draw sink rather than a leak. On <c>true</c>, <paramref name="ventPos"/> is where venting
  /// effects should play (typically the neighbour cell).
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
  /// Draws gas out through the collected <paramref name="vents"/> (gas runs only - <paramref name="liquid"/>
  /// runs vent nothing), mutating <paramref name="state"/>'s volume, and plays per-vent feedback. Also
  /// drops any per-vent bookkeeping for vents no longer active this tick. Returns the litres vented,
  /// for the network's throughput accounting.
  /// </summary>
  float Vent(
    IReadOnlyList<BlockPos> vents,
    PipeNetworkState state,
    bool liquid,
    BlockNetworkModSystem manager
  );
}
