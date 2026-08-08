using Vintagestory.API.MathTools;

namespace IronworkingExpanded;

/// <summary>
/// The iwex layout codes that <b>cannot be generated</b> - everything else comes from
/// <see cref="IwexBlocks"/>, which is emitted from the definitions themselves.
/// <para>
/// Only one kind of thing belongs here: an <b>alternation</b>. A cell that admits several
/// <em>different</em> blocks, or a mix of ours and vanilla's, is not any one block's code - no definition
/// states it, so nothing can emit it. Anything a generator does emit must not be hand-copied here
/// (aliases, typed helpers, per-def wildcards): a hand-written copy can drift and the generated one
/// cannot. Note that a wildcard spanning the defs of one skin group is still a single entry's
/// <c>Any</c> - <c>Any</c> wildcards the skin group whichever def it comes from, so every def in the
/// group yields the same string.
/// </para>
/// <para>
/// <see cref="ExpandedLib.Definitions.VanillaCodes"/> covers the other non-generatable case: the game
/// declares those blocks, so there is no definition to emit from at all.
/// </para>
/// </summary>
public static class IwexCodes
{
  #region Furnace cells

  // These two are iwex's rather than exlib's or vanilla's, even though their alternations are
  // mostly vanilla paths: what they describe is a SHAFT FURNACE's interior, and the blocks that
  // make them correct - the charge pile, the frozen pool - are iwex's own. smex's hot furnace
  // draws them too, which is fine: smex is downstream.

  /// <summary>
  /// A shaft cell holding a layered charge, a vanilla pile, or nothing:
  /// <c>*:@(air|coalpile|furnace-chargepile)</c>.
  /// <para>
  /// <b>The domain wildcard is load-bearing, not tidiness.</b> Without the leading <c>*:</c> the
  /// alternation is implicitly <c>game:</c> (see <see cref="ExpandedLib.Definitions.VanillaCodes.CoalBed"/>) and can never admit
  /// <c>iwex:furnace-chargepile</c> - so a charged shaft reads incomplete on the next monitor tick, and
  /// <c>OnStructureLost</c> on a lit furnace is an extinguish: the furnace puts itself out the
  /// moment the player charges it.
  /// </para>
  /// <para>
  /// <c>coalpile</c> stays in the alternation for <b>saved worlds</b>: a shaft charged before the
  /// charge-column cutover has real <c>game:coalpile</c> blocks standing in its cells, and dropping the
  /// branch would make that furnace read <em>incomplete</em> on its next monitor tick - which, on a lit
  /// furnace, is an extinguish. Nothing migrates those piles into columns, so the alternation is what
  /// keeps such a world loadable while the player digs them out.
  /// <c>furnace-chargepile</c> carries the family prefix like every other furnace part.
  /// </para>
  /// </summary>
  public const string ChargeShaft = "*:@(air|coalpile|furnace-chargepile)";

  /// <summary>
  /// The hearth floor of a shaft furnace: <see cref="ChargeShaft"/> <b>plus the pool</b> -
  /// <c>*:@(air|coalpile|furnace-chargepile|hearthmetal-*)</c>.
  /// <para>
  /// The bottom cell of a shaft is three things over a furnace's life: burden rests on it while the
  /// furnace runs, molten metal pools in it during a heat, and that metal <b>stands as a block</b>
  /// (<c>iwex:hearthmetal-pigiron</c> from a blast furnace, <c>iwex:hearthmetal-castiron</c> from a
  /// cupola). Only the first two are occupants <see cref="ChargeShaft"/> admits.
  /// </para>
  /// <para>
  /// A cell whose legend does not admit a block the
  /// machine itself places there reads <em>incomplete</em> on the next monitor tick - and for a shaft
  /// furnace, incomplete means extinguish. <see cref="ChargeShaft"/> had to gain the charge pile for
  /// exactly that reason; the pool block is the same argument one cell lower.
  /// </para>
  /// <para>
  /// <b>Adopting this on a shipped layout is a gameplay change, not a fix.</b> Today a furnace with a
  /// frozen salamander in its hearth cannot be relit until the block is chiselled out, and that may
  /// well be intended - <c>MoltenChisel</c> / <c>IChiselableMolten</c> exist to clear it. This constant
  /// says the frozen pool is a <em>legitimate occupant</em>; whether each furnace agrees is per-layout.
  /// </para>
  /// <para>
  /// The design drafts call this block <c>ironblock</c>; the live code is
  /// <c>iwex:hearthmetal-{pigiron|castiron}</c>.
  /// </para>
  /// <para>
  /// Caution: <b>the metal member is <c>hearthmetal-.*</c> - a regex, not a glob - and writing it the
  /// obvious way is silently inverted.</b> Verified against the real matcher, not reasoned about:
  /// inside <c>@(…)</c> the alternation body is a regular expression, so <c>*</c> there means "zero
  /// or more of the previous character", not "anything".
  /// <list type="table">
  /// <item><term><c>@(…|hearthmetal-*)</c></term><description>matches <c>hearthmetal</c> (zero dashes) and
  /// <b>neither</b> <c>hearthmetal-pigiron</c> nor <c>hearthmetal-castiron</c> - it admits a block that
  /// does not exist and rejects both that do.</description></item>
  /// <item><term><c>@(…|hearthmetal-.*)</c></term><description>matches both metals, rejects the bare word,
  /// rejects <c>hearthmetalother</c>. This is the correct spelling.</description></item>
  /// <item><term><c>*:hearthmetal-*</c></term><description>works - because <b>outside</b> an alternation
  /// <c>*</c> is a glob. Both syntaxes live in this one string.</description></item>
  /// </list>
  /// A tidy-up that rewrites <c>.*</c> as <c>*</c> compiles, reads better, and makes every furnace read
  /// <em>incomplete</em> the moment it pools metal - which on a shaft furnace is an extinguish.
  /// <c>IwexCodesHearthCellTests</c> pins both spellings against the real matcher so that edit fails
  /// loudly instead of shipping.
  /// </para>
  /// </summary>
  public const string HearthCell =
    "*:@(air|coalpile|furnace-chargepile|hearthmetal-.*)";

  #endregion
}
