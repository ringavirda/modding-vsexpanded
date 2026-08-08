using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;

namespace HighPressureExpanded.BlockNetworkPipe;

/// <summary>
/// hpex's (rolled) pipe tier: the plain straight/bend/T/X segments, authored by the shared
/// <see cref="BlockPipe.Segments"/> factory under the <c>hpex</c> domain - the same shape as lpex's
/// <c>CastPipeDefinitions</c>. A stand-alone <see cref="IExBlockDefProvider"/> because the segment
/// class itself lives in exlib: discovered when the hpex assembly is scanned, it injects
/// <c>hpex:pipe-*</c> blocktypes bound to the registered <c>exlib.BlockPipe</c> /
/// <c>exlib.BlockEntityPipe</c> classes.
/// <para>
/// Until now the rolled tier was <b>a burst rating with no pipe behind it</b>: hpex registered
/// <see cref="HpexValues.RolledPipeBurstPressure"/> but shipped no segment block, so the highest
/// pressure tier existed only as a number. These are the blocks that number applies to.
/// </para>
/// </summary>
public class RolledPipeDefinitions : IExBlockDefProvider
{
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    BlockPipe.Segments(domain);
}
