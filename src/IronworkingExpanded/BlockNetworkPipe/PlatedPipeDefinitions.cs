using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.BlockNetworkPipe;

/// <summary>
/// iwex's plated pipe tier: straight/bend/T/X segments plus wall passthroughs, authored by the shared
/// <see cref="BlockPipe.Segments"/> and <see cref="BlockPipePassthrough.Passthroughs"/> factories under
/// the <c>iwex</c> domain. A stand-alone <see cref="IExBlockDefProvider"/> because the pipe base classes
/// live in exlib; discovered when the iwex assembly is scanned, it injects <c>iwex:pipe-*</c> blocktypes
/// bound to the registered <c>exlib.BlockPipe</c> and <c>exlib.BlockEntityPipe</c> classes.
/// </summary>
public class PlatedPipeDefinitions : IExBlockDefProvider {
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      .. BlockPipe.Segments(domain),
      .. BlockPipePassthrough.Passthroughs(domain),
    ];
}
