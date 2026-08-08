namespace LowPressureExpanded;

/// <summary>
/// The lpex block codes other layouts draw with - the pipe half of
/// <see cref="ExpandedLib.Definitions.ExCodes"/> and <see cref="IronworkingExpanded.IwexCodes"/>,
/// whose remarks apply here unchanged. The pipe base lives in iwex, but these fire-brick fittings
/// are lpex's tier, so only mods downstream of lpex reference this table. Named rather than inlined
/// because several codes differ by one dash-separated word (<c>passthrough</c> vs
/// <c>passthroughbend</c>, <c>-fire-</c> present or absent), where a swapped pair reads correctly
/// inline but requires the wrong block.
/// </summary>
public static class LpexCodes {
  /// <summary>A pipe outlet of any material and facing: <c>lpex:pipe-outlet*</c>. The gas port a
  /// cowper stove and the hot blast furnace expose to the network.</summary>
  public const string PipeOutlet = "lpex:pipe-outlet*";

  /// <summary>The upward fire-brick pipe outlet: <c>lpex:pipe-outlet-fire-u</c>, a boiler's flue
  /// mouth. Not a facing-parameterised helper like
  /// <see cref="ExpandedLib.Definitions.VanillaCodes.FireSlab"/>: pipe codes spell the direction as
  /// a single letter, so a <c>BlockFacing.UP.Code</c> helper would emit <c>-up</c> and match no
  /// block.</summary>
  public const string PipeOutletFireUp = "lpex:pipe-outlet-fire-u";

  /// <summary>A straight pipe passthrough of any material: <c>lpex:pipe-passthrough-*</c>. Admits
  /// every brick, unlike <see cref="PipePassthroughFire"/>.</summary>
  public const string PipePassthroughAny = "lpex:pipe-passthrough-*";

  /// <summary>A straight fire-brick pipe passthrough: <c>lpex:pipe-passthrough-fire-*</c>. A
  /// boiler's shell penetration, where the brick grade is part of the setting.</summary>
  public const string PipePassthroughFire = "lpex:pipe-passthrough-fire-*";

  /// <summary>An upward fire-brick pipe passthrough bend:
  /// <c>lpex:pipe-passthroughbend-fire-u*</c>. <c>passthroughbend</c> is one word; a dash would name
  /// a different, non-existent block.</summary>
  public const string PipePassthroughBendFireUp =
    "lpex:pipe-passthroughbend-fire-u*";
}
