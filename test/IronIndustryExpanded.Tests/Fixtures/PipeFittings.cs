using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkPipe.Blocks;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Real fitting blocks from the iiex (cast) pipe tier, for fixtures that must satisfy a machine's
/// multiblock layout rather than merely act as a network node. A generic
/// <c>PipeTestWorld.MakePipe</c> behaves identically on the graph but wears
/// <c>iiex:pipe-plated-straight-*</c>, so a layout cell asking for <c>iiex:pipe-outlet*</c> or
/// <c>iiex:pipe-cast-passthrough-*</c> stays unsatisfied and the machine never completes. Lives in the iiex
/// suite because iiex owns the fittings; smex and hpex reach it through the test-project chain.
/// </summary>
public static class PipeFittings {
  /// <summary>
  /// A pipe outlet: the open end a machine vents into or draws from, and the block the cowper's
  /// exhaust and hot-blast cells both call for.
  /// </summary>
  public static BlockPipeOutlet Outlet(int id, string orientation = "ns") =>
    Primed(
      TestBlocks.Configure(
        new BlockPipeOutlet(),
        $"iiex:pipe-outlet-{orientation}",
        id,
        ("type", "outlet"),
        ("orientation", orientation)
      ),
      "outlet",
      orientation
    );

  /// <summary>
  /// A pipe passthrough: the brick-cased segment that carries a line through a structure wall, which
  /// is how cool blast air is fed into a cowper stove.
  /// </summary>
  public static BlockPipePassthrough Passthrough(
    int id,
    string orientation = "ns"
  ) =>
    Primed(
      TestBlocks.Configure(
        new BlockPipePassthrough(),
        $"iiex:pipe-cast-passthrough-fire-{orientation}",
        id,
        ("tier", "cast"),
        ("type", "passthrough"),
        ("orientation", orientation)
      ),
      "passthrough",
      orientation
    );

  // OnLoaded, which parses these off the variants, is skipped headlessly, so set the protected
  // Type/Orientation the network code reads. Matches the priming PipeTestWorld.MakePipe does.
  private static T Primed<T>(T block, string type, string orientation)
    where T : class {
    ReflectionHelpers.SetProperty(block, "Type", type);
    ReflectionHelpers.SetProperty(block, "Orientation", orientation);
    return block;
  }
}
