using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;

namespace IronIndustryExpanded.BlockNetworkPipe;

/// <summary>
/// iiex's cast pipe tier: the plain straight/bend/T/X segments, authored by the shared
/// <see cref="BlockPipe.Segments"/> factory under the <c>iiex</c> domain. A stand-alone
/// <see cref="IExBlockDefProvider"/> because the segment class itself lives in exlib; discovered when
/// the iiex assembly is scanned, it injects <c>iiex:pipe-cast-*</c> blocktypes bound to the registered
/// <c>exlib.BlockPipe</c> and <c>exlib.BlockEntityPipe</c> classes. The fittings (valve, pressure
/// valve, outlet, passthrough, fluid intake) and the steam condenser are iiex-only and carry their
/// own defs; the two valves and the passthroughs are cast-tier, the rest name no tier.
/// </summary>
public class CastPipeDefinitions : IExBlockDefProvider {
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      .. BlockPipe.Segments(domain, BlockPipe.CastTier),
      // The cast tier's own passthroughs. Tiered like the segments: iiex ships a plated pair off
      // the same factory, identical but for the sheet texture, and one code cannot carry both. The
      // sheet is named here rather than in exlib because the art is iiex's; a library that named it
      // would resolve to nothing the moment that domain is renamed.
      .. BlockPipePassthrough.Passthroughs(
        domain,
        BlockPipe.CastTier,
        "iiex:block/metal/castiron"
      ),
    ];
}
