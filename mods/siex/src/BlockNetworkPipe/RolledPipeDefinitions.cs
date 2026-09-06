using System.Collections.Generic;
using ExpandedLib.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Industry.Pipes;

namespace SteelIndustryExpanded.BlockNetworkPipe;

/// <summary>
/// siex's rolled pipe tier: the plain straight/bend/T/X segments, authored by the shared
/// <see cref="BlockPipe.Segments"/> factory under the <c>siex</c> domain, mirroring iiex's
/// <c>CastPipeDefinitions</c>. A stand-alone <see cref="IExBlockDefProvider"/> because the segment
/// class lives in exlib: scanned from the siex assembly, it injects <c>siex:pipe-rolled-*</c>
/// blocktypes bound to the registered <c>exlib.BlockPipe</c> / <c>exlib.BlockEntityPipe</c> classes.
/// <see cref="SiexValues.RolledPipeBurstPressure"/> is the burst rating that applies to them.
/// </summary>
public class RolledPipeDefinitions : IExBlockDefProvider {
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    BlockPipe.Segments(domain, BlockPipe.RolledTier);
}
