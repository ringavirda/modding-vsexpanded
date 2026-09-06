using System.Collections.Generic;
using ExpandedLib.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Industry.Pipes;

namespace IronIndustryExpanded.BlockNetworkPipe;

/// <summary>
/// iiex's plated pipe tier: straight/bend/T/X segments plus wall passthroughs, authored by the shared
/// <see cref="BlockPipe.Segments"/> and <see cref="BlockPipePassthrough.Passthroughs"/> factories under
/// the <c>iiex</c> domain. A stand-alone <see cref="IExBlockDefProvider"/> because the pipe base classes
/// live in exlib; discovered when the iiex assembly is scanned, it injects
/// <c>iiex:pipe-plated-*</c> segments and passthroughs bound to the
/// registered <c>exlib.BlockPipe</c> and <c>exlib.BlockEntityPipe</c> classes. The passthroughs are
/// plated-tier too: they bear no pressure, but iiex ships its own and without the axis the two would
/// carry one code between them once both land in a single domain.
/// </summary>
public class PlatedPipeDefinitions : IExBlockDefProvider {
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      .. BlockPipe.Segments(domain, BlockPipe.PlatedTier),
      .. BlockPipePassthrough.Passthroughs(domain, BlockPipe.PlatedTier),
    ];
}
