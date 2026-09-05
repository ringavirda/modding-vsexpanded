namespace ExpandedLib.Fluids;

/// <summary>
/// Phase of a pipe/canal medium: gases share one mixable family, while a liquid mixes only with the
/// same liquid.
/// </summary>
public enum LiquidPhase {
  /// <summary>Air / Steam / Exhaust - compressible, all mutually mixable, priority-ranked on merge.</summary>
  Gas,

  /// <summary>Water / Oil / molten-as-liquid - incompressible, mixes only with the same liquid code.</summary>
  Liquid,
}
