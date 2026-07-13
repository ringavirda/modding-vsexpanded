namespace ExpandedLib.Fluids;

/// <summary>
/// The injectable medium policy the pipe network consults instead of hardcoded medium strings -
/// the same injection seam as <see cref="Blocks.Networks.IPipeVentStrategy"/>. Supplied to
/// <see cref="Blocks.Networks.PipeNetwork"/> at <c>RegisterNetworkType</c>; a bare-constructed network
/// (tests) falls back to <see cref="ExLiquids.Taxonomy"/>, which always knows the four built-ins.
/// </summary>
public interface IMediumTaxonomy
{
  /// <summary>True when <paramref name="code"/> is a liquid (incompressible, single-family) rather
  /// than a gas. An unknown code reads as a gas (matching the old <c>== "Water"</c> default).</summary>
  bool IsLiquid(string code);

  /// <summary>Whether <paramref name="medium"/> can be produced into a run currently carrying
  /// <paramref name="current"/>: an empty run accepts anything; two gases always mix; a liquid mixes
  /// only with the same liquid; gas and liquid never mix.</summary>
  bool Compatible(string current, string medium);

  /// <summary>The dominant of two media by merge priority (Exhaust &gt; Steam &gt; Air); ties keep
  /// <paramref name="a"/>. Called for gas merges - liquids never reach it (they only mix same-code).</summary>
  string HigherPriority(string a, string b);

  /// <summary>
  /// The condensation partner of <paramref name="code"/>, independent of temperature - for an
  /// <b>active</b> device that supplies the cooling itself (a condenser cools regardless of the gas's
  /// current temperature) and so decides <i>when</i> to change phase. On <c>true</c>,
  /// <paramref name="target"/> is the resulting liquid and <paramref name="volumeFactor"/> the volume
  /// multiplier (0 when the def leaves it to the caller's own default).
  /// </summary>
  bool CondensationTarget(string code, out string target, out float volumeFactor);

  /// <summary>
  /// The vaporisation partner of <paramref name="code"/>, independent of temperature - the mirror of
  /// <see cref="CondensationTarget"/> for an <b>active</b> device that supplies the heat (a still pot /
  /// the boiler boils its charge). On <c>true</c>, <paramref name="target"/> is the resulting gas and
  /// <paramref name="volumeFactor"/> the volume multiplier (0 = the caller's own default).
  /// </summary>
  bool VaporisationTarget(string code, out string target, out float volumeFactor);

  /// <summary>
  /// Whether <paramref name="code"/> condenses at <paramref name="tempC"/> - the temperature-gated
  /// form of <see cref="CondensationTarget"/> for <b>passive</b> phase change (a cold pipe, a still
  /// column below its dew point). On <c>true</c>, <paramref name="target"/> is the resulting medium
  /// and <paramref name="volumeFactor"/> the volume multiplier (0 = the caller's own default).
  /// </summary>
  bool TryCondensation(
    string code,
    float tempC,
    out string target,
    out float volumeFactor
  );

  /// <summary>
  /// Whether <paramref name="code"/> boils at <paramref name="tempC"/> - the temperature-gated mirror
  /// of <see cref="TryCondensation"/> (gates at/above the boil point rather than below the dew point),
  /// for <b>passive</b> vaporisation in a still column. Generalises the boiler's water → steam step to
  /// every distillation fraction. On <c>true</c>, <paramref name="target"/> is the resulting gas and
  /// <paramref name="volumeFactor"/> the volume multiplier (0 = the caller's own default).
  /// </summary>
  bool TryVaporisation(
    string code,
    float tempC,
    out string target,
    out float volumeFactor
  );
}
