using ExpandedLib.Testing;
using LowPressureExpanded.BlockNetworkPipe.Blocks;

namespace LowPressureExpanded.Tests;

/// <summary>
/// Real fitting blocks from the lpex (cast) pipe tier, for fixtures that must satisfy a machine's
/// multiblock layout rather than merely act as a network node. A generic
/// <c>PipeTestWorld.MakePipe</c> behaves identically on the graph but wears
/// <c>iwex:pipe-straight-*</c>, so a layout cell asking for <c>lpex:pipe-outlet*</c> or
/// <c>lpex:pipe-passthrough-*</c> stays unsatisfied and the machine never completes. Lives in the lpex
/// suite because lpex owns the fittings; smex and hpex reach it through the test-project chain.
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
        $"lpex:pipe-outlet-{orientation}",
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
        $"lpex:pipe-passthrough-fire-{orientation}",
        id,
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
