namespace ExpandedLib.Networks;

/// <summary>
/// A pipe-network block that limits how much can move through a run per second. The pipe network walks
/// its nodes for the <b>smallest</b> <see cref="MaxThroughput"/> - the weakest-link rule, exactly as
/// <see cref="IBurstablePipe"/>'s burst rating works - and caps both production into and consumption out
/// of the pool at that figure each tick. A run with no implementors (only machine ports) is uncapped.
/// <para>
/// <b>Only plain segments should implement a real limit; fittings and ports must return
/// <see cref="float.MaxValue"/>.</b> The decisive case is the furnace <b>tuyere</b>: it is an <c>iwex</c>
/// block sitting on its own single-node network because it is the machine's intake <i>port</i>, not a length
/// of main - so limiting on it would cap every furnace in the suite at the iron tier's rate forever,
/// including the hot blast furnace two tiers above it. A pipe tier exists to lift that ceiling, not to nail
/// it to the cheapest block in the chain.
/// </para>
/// <para>
/// <b>This is throughput, not capacity, and not bore.</b> How much a run <i>holds</i> is
/// <c>nodes x LitresPerPipe</c>; how much it <i>passes</i> is this. There is deliberately no large-bore pipe
/// family - see <c>docs/design/mechanics/pipe-network.md</c> - partly because bore and burst are not
/// independent (hoop stress is <c>p*r/t</c>, so a wider bore bursts <i>lower</i>), so tier is the only axis
/// this rate may be derived from.
/// </para>
/// </summary>
public interface IThroughputLimitedPipe
{
  /// <summary>Litres per second this block will pass. The smallest across a run caps the whole run.</summary>
  float MaxThroughput { get; }
}
