using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronworkingExpanded.BlockNetworkPipe.Blocks;

namespace LowPressureExpanded.BlockNetworkPipe;

/// <summary>
/// lpex's (cast) pipe tier: the plain straight/bend/T/X segments, authored by the shared
/// <see cref="BlockPipe.Segments"/> factory under the <c>lpex</c> domain. A stand-alone
/// <see cref="IExBlockDefProvider"/> because the segment class itself lives in iwex - discovered when
/// the lpex assembly is scanned, it injects <c>lpex:pipe-*</c> blocktypes that bind to the registered
/// <c>iwex.BlockPipe</c> / <c>iwex.BlockEntityPipe</c> classes. The fittings (valve, pressure valve,
/// outlet, passthrough, fluid intake) and the steam condenser are lpex-only and carry their own defs.
/// </summary>
public class CastPipeDefinitions : IExBlockDefProvider
{
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    BlockPipe.Segments(domain);
}
