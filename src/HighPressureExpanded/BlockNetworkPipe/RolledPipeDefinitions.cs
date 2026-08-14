using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;

namespace HighPressureExpanded.BlockNetworkPipe;

/// <summary>
/// hpex's rolled pipe tier: the plain straight/bend/T/X segments, authored by the shared
/// <see cref="BlockPipe.Segments"/> factory under the <c>hpex</c> domain, mirroring lpex's
/// <c>CastPipeDefinitions</c>. A stand-alone <see cref="IExBlockDefProvider"/> because the segment
/// class lives in exlib: scanned from the hpex assembly, it injects <c>hpex:pipe-rolled-*</c>
/// blocktypes bound to the registered <c>exlib.BlockPipe</c> / <c>exlib.BlockEntityPipe</c> classes.
/// <see cref="HpexValues.RolledPipeBurstPressure"/> is the burst rating that applies to them.
/// </summary>
public class RolledPipeDefinitions : IExBlockDefProvider {
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    BlockPipe.Segments(domain, BlockPipe.RolledTier);
}
