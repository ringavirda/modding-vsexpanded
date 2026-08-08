using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;

namespace LowPressureExpanded.BlockNetworkPipe;

/// <summary>
/// lpex's (cast) pipe tier: the plain straight/bend/T/X segments, authored by the shared
/// <see cref="BlockPipe.Segments"/> factory under the <c>lpex</c> domain. A stand-alone
/// <see cref="IExBlockDefProvider"/> because the segment class itself lives in exlib; discovered when
/// the lpex assembly is scanned, it injects <c>lpex:pipe-*</c> blocktypes bound to the registered
/// <c>exlib.BlockPipe</c> and <c>exlib.BlockEntityPipe</c> classes. The fittings (valve, pressure
/// valve, outlet, passthrough, fluid intake) and the steam condenser are lpex-only and carry their
/// own defs.
/// </summary>
public class CastPipeDefinitions : IExBlockDefProvider {
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      .. BlockPipe.Segments(domain),
      // The passthroughs live in iwex with the pipe base they belong to, but keep their
      // `lpex:pipe-passthrough-*` codes.
      .. BlockPipePassthrough.Passthroughs(domain),
    ];
}
