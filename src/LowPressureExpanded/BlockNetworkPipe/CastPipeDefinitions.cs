using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;

namespace LowPressureExpanded.BlockNetworkPipe;

/// <summary>
/// lpex's cast pipe tier: the plain straight/bend/T/X segments, authored by the shared
/// <see cref="BlockPipe.Segments"/> factory under the <c>lpex</c> domain. A stand-alone
/// <see cref="IExBlockDefProvider"/> because the segment class itself lives in exlib; discovered when
/// the lpex assembly is scanned, it injects <c>lpex:pipe-cast-*</c> blocktypes bound to the registered
/// <c>exlib.BlockPipe</c> and <c>exlib.BlockEntityPipe</c> classes. The fittings (valve, pressure
/// valve, outlet, passthrough, fluid intake) and the steam condenser are lpex-only and carry their
/// own defs; the two valves and the passthroughs are cast-tier, the rest name no tier.
/// </summary>
public class CastPipeDefinitions : IExBlockDefProvider {
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      .. BlockPipe.Segments(domain, BlockPipe.CastTier),
      // The cast tier's own passthroughs. Tiered like the segments: iwex ships a plated pair off
      // the same factory, identical but for the sheet texture, and one code cannot carry both.
      .. BlockPipePassthrough.Passthroughs(domain, BlockPipe.CastTier),
    ];
}
