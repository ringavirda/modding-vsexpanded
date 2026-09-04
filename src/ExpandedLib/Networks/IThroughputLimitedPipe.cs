namespace ExpandedLib.Networks;

/// <summary>
/// A pipe-network block that limits how much can move through a run per second. The network takes the
/// smallest <see cref="MaxThroughput"/> across its nodes, the same weakest-link rule as
/// <see cref="IBurstablePipe"/>'s burst rating, and caps both production into and consumption out of the
/// pool at that figure each tick. A run with no implementor (only machine ports) is uncapped, and only
/// plain segments carry a real limit: fittings and ports return <see cref="float.MaxValue"/>. This is
/// throughput, not capacity - a run still holds <c>nodes x LitresPerPipe</c>.
/// See <c>docs/design/mechanics/pipe-network.md</c>.
/// </summary>
public interface IThroughputLimitedPipe {
  /// <summary>Litres per second this block will pass. The smallest across a run caps the whole run.</summary>
  float MaxThroughput { get; }
}
