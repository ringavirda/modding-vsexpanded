using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.BlockNetworkPipe;

/// <summary>
/// iwex's (plated) pipe tier: the plain straight/bend/T/X segments plus the wall passthroughs,
/// authored by the shared <see cref="BlockPipe.Segments"/> /
/// <see cref="BlockPipePassthrough.Passthroughs"/> factories under the <c>iwex</c> domain. A
/// stand-alone <see cref="IExBlockDefProvider"/> because the pipe base classes live in exlib (moved
/// there 2026-08-07) - the same shape as lpex's <c>CastPipeDefinitions</c> and hpex's
/// <c>RolledPipeDefinitions</c>: discovered when the iwex assembly is scanned, it injects
/// <c>iwex:pipe-*</c> blocktypes that bind to the registered <c>exlib.BlockPipe</c> /
/// <c>exlib.BlockEntityPipe</c> classes.
/// </summary>
public class PlatedPipeDefinitions : IExBlockDefProvider
{
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      .. BlockPipe.Segments(domain),
      .. BlockPipePassthrough.Passthroughs(domain),
    ];
}
