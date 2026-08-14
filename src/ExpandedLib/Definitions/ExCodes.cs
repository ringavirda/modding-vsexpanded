namespace ExpandedLib.Definitions;

/// <summary>
/// Catalogue of exlib's own block codes that multiblock layouts are drawn from. Small, since exlib is
/// the framework the mods are built on rather than a content mod. <see cref="VanillaCodes"/> holds the
/// game's own codes, <c>IiexCodes</c> and <c>IiexCodes</c> each mod's, so the dependency runs exlib to
/// iiex to iiex to siex/siex and never back up. Every member here forwards to <c>ExlibBlocks</c>, which
/// is emitted from the block definitions, so a name cannot drift from the code it stands for; only the
/// choice of which codes get a short name is hand-written. <see cref="VanillaCodes"/> cannot work that
/// way - the game declares those blocks, so every string there is hand-kept.
/// </summary>
public static class ExCodes {
  /// <summary>The invisible per-cell filler a mega-block places over its own footprint:
  /// <c>exlib:structurefiller</c>. Declared by <c>StructureFillers</c>.</summary>
  public const string Filler = ExlibBlocks.Structurefiller.Code;
}
