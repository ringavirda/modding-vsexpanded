using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;

namespace LowPressureExpanded.BlockNetworkPipe;

/// <summary>
/// lpex's (cast) pipe tier: the plain straight/bend/T/X segments, authored by the shared
/// <see cref="BlockPipe.Segments"/> factory under the <c>lpex</c> domain. A stand-alone
/// <see cref="IExBlockDefProvider"/> because the segment class itself lives in exlib - discovered when
/// the lpex assembly is scanned, it injects <c>lpex:pipe-*</c> blocktypes that bind to the registered
/// <c>exlib.BlockPipe</c> / <c>exlib.BlockEntityPipe</c> classes. The fittings (valve, pressure valve,
/// outlet, passthrough, fluid intake) and the steam condenser are lpex-only and carry their own defs.
/// </summary>
public class CastPipeDefinitions : IExBlockDefProvider
{
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      .. BlockPipe.Segments(domain),
      // The passthroughs live in iwex with the pipe base they belong to - a passthrough is a pipe
      // in a brick wall, not a cast fitting. lpex's codes are unchanged (`lpex:pipe-passthrough-*`), so
      // nothing is owed a migration; only the class they bind to and the shape they draw moved.
      .. BlockPipePassthrough.Passthroughs(domain),
    ];
}
