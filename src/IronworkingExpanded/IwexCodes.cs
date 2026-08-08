using Vintagestory.API.MathTools;

namespace IronworkingExpanded;

/// <summary>
/// The iwex layout codes that cannot be generated: alternations, where a cell admits several different
/// blocks or a mix of iwex's and vanilla's, so no single definition states the code. Generated codes
/// (aliases, typed helpers, per-def and skin-group wildcards) stay in <see cref="IwexBlocks"/>;
/// <see cref="ExpandedLib.Definitions.VanillaCodes"/> holds the vanilla blocks.
/// </summary>
public static class IwexCodes {
  #region Furnace cells

  // Both describe a shaft furnace's interior, and smex's hot furnace draws on them as well.

  /// <summary>
  /// A shaft cell holding a layered charge, a vanilla pile, or nothing. The leading <c>*:</c> is required:
  /// without it the alternation is implicitly <c>game:</c> and never admits <c>iwex:furnace-chargepile</c>,
  /// so a charged shaft reads incomplete on the next monitor tick, which on a lit furnace is an
  /// extinguish. <c>coalpile</c> stays for saved worlds charged before the charge-column cutover, whose
  /// real <c>game:coalpile</c> blocks nothing migrates into columns.
  /// </summary>
  public const string ChargeShaft = "*:@(air|coalpile|furnace-chargepile)";

  /// <summary>
  /// The hearth floor of a shaft furnace: <see cref="ChargeShaft"/> plus the pool the furnace leaves
  /// standing there (<c>iwex:hearthmetal-pigiron</c> from a blast furnace,
  /// <c>iwex:hearthmetal-castiron</c> from a cupola), so a frozen pool is a legitimate occupant rather
  /// than one that must be chiselled out before a relight. Whether a layout adopts it is per-layout.
  /// Inside <c>@(...)</c> the alternation body is a regular expression, so the metal member must be
  /// spelled <c>hearthmetal-.*</c>: <c>hearthmetal-*</c> there means zero or more dashes and rejects
  /// both metals, while the leading <c>*:</c> is a glob. <c>IwexCodesHearthCellTests</c> pins both.
  /// </summary>
  public const string HearthCell =
    "*:@(air|coalpile|furnace-chargepile|hearthmetal-.*)";

  #endregion
}
