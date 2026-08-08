using System.Collections.Generic;

namespace ExpandedLib.Fluids;

/// <summary>
/// One pipe/canal medium descriptor - the data the network's compatibility / priority / condensation
/// logic reads in place of hardcoded medium strings in <c>PipeNetworkState</c>.
/// Deserialized from <c>assets/&lt;domain&gt;/config/liquids.json</c> and registered into
/// <see cref="ExLiquids"/>; a mod adds a medium by shipping/patching one JSON entry.
/// <para>
/// <see cref="Code"/> is kept identical to the network <c>MediumType</c> string ("Air"/"Steam"/
/// "Exhaust"/"Water") so persisted state resolves unchanged - zero save migration.
/// </para>
/// </summary>
public class LiquidDef
{
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

  /// <summary>Volume multiplier applied on condensation; null = the consumer's own default
  /// (lpex's steam-expansion factor). Kept out of exlib so the library carries no lpex dependency.</summary>
  public float? CondenseVolumeFactor { get; set; }

  /// <summary>Gas-phase target a still/boiler boils this liquid into (Water → "Steam") - the mirror
  /// of <see cref="CondensesTo"/>; null = does not boil to a carried medium. The pair (Water↔Steam,
  /// fraction↔vapour) makes the boiler the degenerate single-fraction case of the general still.</summary>
  public string? VaporisesTo { get; set; }

  /// <summary>Boil only at or above this temperature (°C); null = temperature-independent. The mirror
  /// of <see cref="CondenseBelowC"/> (that gates below, this gates at/above).</summary>
  public float? BoilPointC { get; set; }

  /// <summary>Volume multiplier applied on vaporisation; null = the consumer's own default (a still's
  /// own expansion factor). Mirror of <see cref="CondenseVolumeFactor"/>; kept out of exlib so the
  /// library carries no lpex/add-on dependency.</summary>
  public float? VaporiseVolumeFactor { get; set; }
}

/// <summary>The <c>config/liquids.json</c> file shape: a wrapper carrying the medium entries
/// (mirrors the metal catalogue but one file with a <c>liquids</c> array, per the design).</summary>
public class LiquidCatalogue
{
  /// <summary>The medium descriptors this file contributes.</summary>
  public List<LiquidDef>? Liquids { get; set; }
}
