namespace LowPressureExpanded;

/// <summary>
/// The lpex block codes that other layouts draw with - the pipe half of
/// <see cref="ExpandedLib.Definitions.ExCodes"/> / <see cref="IronworkingExpanded.IwexCodes"/>. Read
/// <c>ExCodes</c>'s remarks first; everything there applies here unchanged.
/// <para>
/// <b>Why lpex rather than iwex.</b> The pipe <em>base</em> lives in iwex, but the fire-brick
/// passthroughs and outlets these layouts draw are lpex's tier, and lpex is where the boilers that
/// need them live. hpex's Lancashire boiler and smex's cowper stove and hot blast furnace are all
/// downstream of lpex, so they can reference this table; nothing upstream needs to.
/// </para>
/// <para>
/// Caution: four codes here differ from one another by a single dash-separated word
/// (<c>passthrough</c> vs <c>passthroughbend</c>, <c>-fire-</c> present or absent). Inline, they
/// read as near-identical strings and a swapped pair would look right on the page while requiring
/// the wrong block. That confusability is the reason to name them, more than the repetition is.
/// </para>
/// </summary>
public static class LpexCodes
{
  /// <summary>A pipe outlet of any material and facing: <c>lpex:pipe-outlet*</c>. The gas port a
  /// cowper stove and the hot blast furnace expose to the network.</summary>
  public const string PipeOutlet = "lpex:pipe-outlet*";

  /// <summary>
  /// The upward fire-brick pipe outlet: <c>lpex:pipe-outlet-fire-u</c>. A boiler's flue mouth.
  /// <para>
  /// This is <b>not</b> a facing-parameterised helper, unlike
  /// <see cref="ExpandedLib.Definitions.VanillaCodes.FireSlab"/>. The pipe codes spell the direction as a
  /// single letter (<c>u</c>), not a word (<c>up</c>), so a <c>BlockFacing.UP.Code</c> helper would
  /// emit <c>…-up</c> and match no block. Vertical never rotates, so there is nothing to gain by
  /// forcing the two spellings into one shape.
  /// </para>
  /// </summary>
  public const string PipeOutletFireUp = "lpex:pipe-outlet-fire-u";

  /// <summary>A straight pipe passthrough of any material: <c>lpex:pipe-passthrough-*</c>. Note
  /// this admits every brick, unlike <see cref="PipePassthroughFire"/>.</summary>
  public const string PipePassthroughAny = "lpex:pipe-passthrough-*";

  /// <summary>A straight fire-brick pipe passthrough: <c>lpex:pipe-passthrough-fire-*</c>. A
  /// boiler's shell penetration, where the brick grade is part of the setting.</summary>
  public const string PipePassthroughFire = "lpex:pipe-passthrough-fire-*";

  /// <summary>An upward fire-brick pipe passthrough bend:
  /// <c>lpex:pipe-passthroughbend-fire-u*</c>. Note <c>passthroughbend</c> is one word - a dash
  /// would make it a different, non-existent block.</summary>
  public const string PipePassthroughBendFireUp =
    "lpex:pipe-passthroughbend-fire-u*";
}
