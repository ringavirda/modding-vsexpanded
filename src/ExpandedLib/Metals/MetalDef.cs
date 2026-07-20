using System.Collections.Generic;

namespace ExpandedLib.Metals;

/// <summary>
/// One metal (or alloy) descriptor - the single source of truth the molten system reads instead of
/// string-munging item codes. Deserialized from <c>assets/&lt;domain&gt;/config/metals/*.json</c> and
/// registered into <see cref="MetalRegistry"/>; any content mod (or EM) can contribute or override a
/// metal by shipping/patching one JSON file.
/// <para>
/// Only <see cref="Code"/> and <see cref="MoltenItem"/> carry meaning without derivation; every other
/// field is optional and, when left null, falls back to the convention <see cref="MetalRegistry"/>
/// applies for an unregistered metal (so nothing regresses for a metal that ships no <c>MetalDef</c>).
/// The melting point is deliberately absent - it stays deferred to the item's vanilla
/// <c>combustibleProps</c> via <see cref="MoltenMetal.MeltingPointOf"/>, never duplicated here.
/// </para>
/// </summary>
public class MetalDef
{
  /// <summary>Short key - the token the converter / blast furnace uses ("iron", "steel", "slag").</summary>
  public string Code { get; set; } = "";

  /// <summary>The <c>AssetLocation</c> carried in a canal cell / barrel / mold ("game:ingot-iron").</summary>
  public string MoltenItem { get; set; } = "";

  /// <summary>Solid item chipped/broken out. Null → the vanilla <c>shatteredStack</c> convention
  /// (<c>ingot-X → metalbit-X</c>, non-ingot items drop as themselves).</summary>
  public string? SolidDrop { get; set; }

  /// <summary>Molten units recovered per solid drop item. Null → 5 (the shared bit ratio).</summary>
  public int? UnitsPerBit { get; set; }

  /// <summary>Localization key for the player-facing name. Null → the "strip <c>ingot-</c> + capitalise"
  /// convention (the historical <see cref="MoltenMetal.DisplayName"/> behaviour).</summary>
  public string? DisplayLangKey { get; set; }

  /// <summary>Optional melting-point override (°C). Null → defer to the item's vanilla
  /// <c>GetMeltingPoint</c>; kept as an escape hatch, not a duplication of vanilla data.</summary>
  public float? MeltingPointOverride { get; set; }

  /// <summary>Fraction of the melting point above which this metal flows. Null → the global
  /// <see cref="ExlibValues.MetalLiquidThreshold"/>.</summary>
  public float? LiquidThreshold { get; set; }

  /// <summary>Fraction of the melting point below which this metal is chisellable. Null → the global
  /// <see cref="ExlibValues.MetalHardenedThreshold"/>.</summary>
  public float? HardenedThreshold { get; set; }

  /// <summary>Temperature (°C) below which this metal emits no glow. Null → the global
  /// <see cref="ExlibValues.MetalGlowMinTemp"/>.</summary>
  public float? GlowMinTemp { get; set; }

  /// <summary>Network media this metal flows in (the bridge to the liquid taxonomy). Null → <c>["molten"]</c>.</summary>
  public List<string>? Media { get; set; }

  /// <summary>True when this entry is a produced alloy rather than an elemental metal.</summary>
  public bool IsAlloy { get; set; }

  /// <summary>Item code recovered when the solid drop cannot resolve. Null → the global
  /// <see cref="ExlibValues.MetalRecoveryFallback"/> (historically <c>iwex:slag</c>).</summary>
  public string? RecoveryFallback { get; set; }

  /// <summary>Domain owning this metal's cast products (the mold's <c>{metal}</c> drops). Null → keep
  /// the drop template's own domain, i.e. today's substitute-only behaviour. A mod-added metal has no
  /// <c>game:metalplate-X</c> to resolve into, so its molds must rehome the drop into the mod's domain;
  /// see <see cref="MetalRegistry.CastProductOf"/>.</summary>
  public string? CastDomain { get; set; }

  /// <summary>Optional inline alloy ratios - exlib can emit the vanilla <c>AlloyRecipe</c> from these so
  /// the ratios are authored once alongside the metal identity.</summary>
  public MetalAlloySpec? Alloy { get; set; }
}

/// <summary>Inline alloy ratios for a <see cref="MetalDef"/> (the vanilla <c>AlloyRecipe</c> shape).</summary>
public class MetalAlloySpec
{
  /// <summary>The metal ingredients and their min/max ratios that smelt into this alloy.</summary>
  public List<MetalAlloyIngredient> Ingredients { get; set; } = new();
}

/// <summary>One ingredient of a <see cref="MetalAlloySpec"/>: a metal short code and its ratio band.</summary>
public class MetalAlloyIngredient
{
  /// <summary>Short code of the ingredient metal ("iron", "manganese").</summary>
  public string Metal { get; set; } = "";

  /// <summary>Minimum ratio of this ingredient in the melt (0..1).</summary>
  public float Min { get; set; }

  /// <summary>Maximum ratio of this ingredient in the melt (0..1).</summary>
  public float Max { get; set; }
}
