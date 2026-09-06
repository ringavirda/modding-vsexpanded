namespace ExpandedLib.Catalogues;

/// <summary>
/// Medium policy the pipe network consults instead of hardcoded medium strings, injected like the
/// family layer's <c>IPipeVentStrategy</c>. Supplied to the family layer's <c>PipeNetwork</c> at
/// <c>RegisterNetworkType</c>; a network constructed without one falls back to
/// <see cref="ExLiquids.Taxonomy"/>, which always knows the four built-ins.
/// </summary>
public interface IMediumTaxonomy {
  /// <summary>True when <paramref name="code"/> is a liquid (incompressible, single-family) rather
  /// than a gas. An unknown code reads as a gas.</summary>
  bool IsLiquid(string code);

  /// <summary>Whether <paramref name="medium"/> can be produced into a run currently carrying
  /// <paramref name="current"/>: an empty run accepts anything; two gases always mix; a liquid mixes
  /// only with the same liquid; gas and liquid never mix.</summary>
  bool Compatible(string current, string medium);

  /// <summary>The dominant of two media by merge priority (Exhaust &gt; Steam &gt; Air); ties keep
  /// <paramref name="a"/>. Called for gas merges only, since liquids mix only same-code.</summary>
  string HigherPriority(string a, string b);

  /// <summary>
  /// The condensation partner of <paramref name="code"/>, independent of temperature, for an active
  /// device that supplies the cooling itself and so decides when to change phase. On <c>true</c>,
  /// <paramref name="target"/> is the resulting liquid and <paramref name="volumeFactor"/> the volume
  /// multiplier (0 = the caller's own default).
  /// </summary>
  bool CondensationTarget(
    string code,
    out string target,
    out float volumeFactor
  );

  /// <summary>
  /// The vaporisation partner of <paramref name="code"/>, independent of temperature; the mirror of
  /// <see cref="CondensationTarget"/> for an active device that supplies the heat itself. On
  /// <c>true</c>, <paramref name="target"/> is the resulting gas and <paramref name="volumeFactor"/>
  /// the volume multiplier (0 = the caller's own default).
  /// </summary>
  bool VaporisationTarget(
    string code,
    out string target,
    out float volumeFactor
  );

  /// <summary>
  /// Whether <paramref name="code"/> condenses at <paramref name="tempC"/> (°C): the temperature-gated
  /// form of <see cref="CondensationTarget"/>, for passive phase change below the dew point. On
  /// <c>true</c>, <paramref name="target"/> is the resulting medium and <paramref name="volumeFactor"/>
  /// the volume multiplier (0 = the caller's own default).
  /// </summary>
  bool TryCondensation(
    string code,
    float tempC,
    out string target,
    out float volumeFactor
  );

  /// <summary>
  /// Whether <paramref name="code"/> boils at <paramref name="tempC"/> (°C): the mirror of
  /// <see cref="TryCondensation"/>, gating at or above the boil point rather than below the dew point,
  /// for passive vaporisation. On <c>true</c>, <paramref name="target"/> is the resulting gas and
  /// <paramref name="volumeFactor"/> the volume multiplier (0 = the caller's own default).
  /// </summary>
  bool TryVaporisation(
    string code,
    float tempC,
    out string target,
    out float volumeFactor
  );
}
