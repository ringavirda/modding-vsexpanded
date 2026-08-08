namespace ExpandedLib.Definitions;

/// <summary>
/// The catalogue of <b>exlib's own</b> block codes that multiblock layouts are drawn from. Small by
/// design: exlib owns almost no blocks, because it is the framework the mods are built on rather than
/// a content mod.
/// <para>
/// <b>Where the others live.</b> <see cref="VanillaCodes"/> holds the game's own codes - the ones that
/// can never be generated, and where the historical typo was. <c>IwexCodes</c> / <c>LpexCodes</c> hold
/// each mod's, so the dependency runs down the chain exlib ← iwex ← lpex ← smex/hpex and never back up.
/// </para>
/// <para>
/// <b>Nothing here holds a literal any more.</b> Every member forwards to <c>ExlibBlocks</c>, which is
/// emitted from the definitions themselves - so this file is a set of readable <em>names</em> for codes
/// the definitions state exactly, and cannot drift from them. What remains hand-written is only the
/// choice of which codes deserve a short name and what to call them.
/// </para>
/// <para>
/// Contrast <see cref="VanillaCodes"/>, which cannot work this way: the game declares those blocks, so
/// there is no definition to emit from and every string there is genuinely hand-kept.
/// </para>
/// </summary>
public static class ExCodes
{
  /// <summary>The invisible per-cell filler a mega-block places over its own footprint:
  /// <c>exlib:structurefiller</c>. Declared by <c>StructureFillers</c>.</summary>
  public const string Filler = ExlibBlocks.Structurefiller.Code;
}
