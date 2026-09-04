using System.Collections.Generic;

namespace ExpandedLib.Fluids;

/// <summary>
/// One pipe or canal medium descriptor: the data the network's compatibility, priority and phase-change
/// logic reads in place of hardcoded medium strings. Deserialized from
/// <c>assets/&lt;domain&gt;/config/liquids.json</c> and registered into <see cref="ExLiquids"/>, so a mod
/// adds a medium by shipping or patching one JSON entry. <see cref="Code"/> is identical to the network
/// <c>MediumType</c> string, so persisted state resolves against it directly.
/// </summary>
public class LiquidDef {
  /// <summary>The medium code - equal to the network <c>MediumType</c> ("Air", "Steam", "Water"...).</summary>
  public string Code { get; set; } = "";

  /// <summary>Gas (mixable family) or Liquid (mixes only with the same code).</summary>
  public LiquidPhase Phase { get; set; } = LiquidPhase.Gas;

  /// <summary>Merge-dominance rank when two gas runs join (Exhaust 20 &gt; Steam 10 &gt; Air 0).</summary>
  public int Priority { get; set; }

  /// <summary>Phase-change target read by a condenser/boiler (Steam → "Water"); null = no condensation.</summary>
  public string? CondensesTo { get; set; }

  /// <summary>Condense only below this temperature (°C); null = temperature-independent.</summary>
  public float? CondenseBelowC { get; set; }

  /// <summary>Volume multiplier applied on condensation; null = the consuming mod's own default, which
  /// exlib does not carry.</summary>
  public float? CondenseVolumeFactor { get; set; }

  /// <summary>Gas-phase target a still or boiler boils this liquid into (Water → "Steam"), the mirror
  /// of <see cref="CondensesTo"/>; null = does not boil to a carried medium.</summary>
  public string? VaporisesTo { get; set; }

  /// <summary>Boil only at or above this temperature (°C); null = temperature-independent. Mirror of
  /// <see cref="CondenseBelowC"/>, which gates below rather than at or above.</summary>
  public float? BoilPointC { get; set; }

  /// <summary>Volume multiplier applied on vaporisation; null = the consuming mod's own default.
  /// Mirror of <see cref="CondenseVolumeFactor"/>.</summary>
  public float? VaporiseVolumeFactor { get; set; }
}

/// <summary>The <c>config/liquids.json</c> file shape: one wrapper object carrying the medium
/// entries.</summary>
public class LiquidCatalogue {
  /// <summary>The medium descriptors this file contributes.</summary>
  public List<LiquidDef>? Liquids { get; set; }
}
