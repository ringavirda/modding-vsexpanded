namespace IronIndustryExpanded;

/// <summary>
/// The iiex layout codes that cannot be generated: alternations, where a cell admits several different
/// blocks or a mix of ours and vanilla's, so no single definition states the code; and the fitting
/// codes that differ by one dash-separated word, where a swapped pair reads correctly inline but
/// requires the wrong block. Generated codes (aliases, typed helpers, per-def and skin-group wildcards)
/// stay in <see cref="IiexBlocks"/>; <see cref="ExpandedLib.Definitions.VanillaCodes"/> holds the
/// vanilla blocks.
/// </summary>
public static class IiexCodes {
  #region Furnace cells

  // Both describe a shaft furnace's interior, and smex's hot furnace draws on them as well.

  /// <summary>
  /// A shaft cell holding a layered charge, a vanilla pile, or nothing. The leading <c>*:</c> is required:
  /// without it the alternation is implicitly <c>game:</c> and never admits <c>iiex:furnace-chargepile</c>,
  /// so a charged shaft reads incomplete on the next monitor tick, which on a lit furnace is an
  /// extinguish. <c>coalpile</c> stays for saved worlds charged before the charge-column cutover, whose
  /// real <c>game:coalpile</c> blocks nothing migrates into columns.
  /// </summary>
  public const string ChargeShaft = "*:@(air|coalpile|furnace-chargepile)";

  /// <summary>
  /// The hearth floor of a shaft furnace: <see cref="ChargeShaft"/> plus the pool the furnace leaves
  /// standing there (<c>iiex:hearthmetal-pigiron</c> from a blast furnace,
  /// <c>iiex:hearthmetal-castiron</c> from a cupola), so a frozen pool is a legitimate occupant rather
  /// than one that must be chiselled out before a relight. Whether a layout adopts it is per-layout.
  /// Inside <c>@(...)</c> the alternation body is a regular expression, so the metal member must be
  /// spelled <c>hearthmetal-.*</c>: <c>hearthmetal-*</c> there means zero or more dashes and rejects
  /// both metals, while the leading <c>*:</c> is a glob. <c>IiexCodesHearthCellTests</c> pins both.
  /// </summary>
  public const string HearthCell =
    "*:@(air|coalpile|furnace-chargepile|hearthmetal-.*)";

  #endregion

  #region Pipe fittings

  /// <summary>A pipe outlet of any material and facing: <c>iiex:pipe-outlet*</c>. The gas port a
  /// cowper stove and the hot blast furnace expose to the network.</summary>
  public const string PipeOutlet = "iiex:pipe-outlet*";

  /// <summary>The upward fire-brick pipe outlet: <c>iiex:pipe-outlet-fire-u</c>, a boiler's flue
  /// mouth. Not a facing-parameterised helper like
  /// <see cref="ExpandedLib.Definitions.VanillaCodes.FireSlab"/>: pipe codes spell the direction as
  /// a single letter, so a <c>BlockFacing.UP.Code</c> helper would emit <c>-up</c> and match no
  /// block.</summary>
  public const string PipeOutletFireUp = "iiex:pipe-outlet-fire-u";

  /// <summary>A cast-tier straight pipe passthrough, any brick: <c>iiex:pipe-cast-passthrough-*</c>. Admits
  /// every brick, unlike <see cref="PipePassthroughFire"/>.</summary>
  public const string PipePassthroughAny = "iiex:pipe-cast-passthrough-*";

  /// <summary>A cast-tier straight fire-brick pipe passthrough: <c>iiex:pipe-cast-passthrough-fire-*</c>. A
  /// boiler's shell penetration, where the brick grade is part of the setting.</summary>
  public const string PipePassthroughFire = "iiex:pipe-cast-passthrough-fire-*";

  /// <summary>An upward fire-brick pipe passthrough bend:
  /// <c>iiex:pipe-cast-passthroughbend-fire-u*</c>. <c>passthroughbend</c> is one word; a dash would name
  /// a different, non-existent block.</summary>
  public const string PipePassthroughBendFireUp =
    "iiex:pipe-cast-passthroughbend-fire-u*";

  #endregion
}
