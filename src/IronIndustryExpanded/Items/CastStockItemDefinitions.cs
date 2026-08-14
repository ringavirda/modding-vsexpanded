using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using IronIndustryExpanded.BlockStructures.Casting;

namespace IronIndustryExpanded.Items;

/// <summary>
/// The bulk cast stock the long cell pours: billet, bloom and slab, the rolling mill's cast-side feed. The
/// masses below are declared, not derived from the art - the density rule
/// (<c>docs/design/mechanics/density-rule.md</c>) only sizes art plausibly, and <c>capacity</c> on the
/// pattern is what a pour measures against. They must agree with the crop table in
/// <c>docs/design/items/stock.md</c>, pinned by <c>CastStockMassTests</c>, and every crop divides exactly
/// on the wide tier's 600 u quantum. All three are <see cref="MoldSize.LongCell"/> products; none fits the
/// 1x1 cell, whose <c>Imprint</c> refuses a non-<c>Cell</c> spec with <c>iiex-castingcell-wrongsize</c>.
/// <see cref="Forms"/> generates the long cell's patterns: these three plus <c>castframe</c>, a machine
/// part with no row here. See <c>docs/design/machines/long-cell.md</c> § Numbers.
/// </summary>
public class CastStockItemDefinitions : IExItemDefProvider {
  /// <summary>Units of cast iron in one billet. A 240 vx³ lane cavity.</summary>
  public const int BilletUnits = 600;

  /// <summary>Units of cast iron in one bloom. A 400 vx³ lane cavity.</summary>
  public const int BloomUnits = 1000;

  /// <summary>Units of cast iron in one slab. A 1200 vx³ lane cavity.</summary>
  public const int SlabUnits = 3000;

  /// <summary>
  /// Each form, its mass and the pattern type that casts it, in emit order. One table, so the item defs,
  /// the long cell's lane capacities and the mass test all read the same row. The pattern type is stated
  /// rather than composed as <c>"cast" + Form</c>: a pattern is plural where its impression yields more
  /// than one piece - <c>castbillets</c> (3 lanes), <c>castblooms</c> (2), <c>castslab</c> (1) - while the
  /// item stays singular.
  /// </summary>
  public static readonly IReadOnlyList<(
    string Form,
    int Units,
    string Pattern
  )> Forms =
  [
    ("billet", BilletUnits, "castbillets"),
    ("bloom", BloomUnits, "castblooms"),
    ("slab", SlabUnits, "castslab"),
  ];

  /// <summary>Units in one piece of <paramref name="form"/>, or 0 for a form that is not cast stock.</summary>
  public static int UnitsOf(string form) =>
    Forms.FirstOrDefault(f => f.Form == form).Units;

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Stock(domain)];

  private static ExItemDef Stock(string domain) {
    // shapeByType and attributesByType both key off the single `form` variant, so adding a cast stock is
    // one row in Forms plus its art.
    var shapeByType = new Dictionary<string, object>();
    var byType = new Dictionary<string, object>();
    foreach ((string form, int units, _) in Forms) {
      shapeByType["*-" + form] = new { @base = $"iiex:item/cast{form}" };
      byType["*-" + form] = new { materialUnits = units };
    }

    return ExItemDef
      .Create(domain, "caststock")
      .Shape("iiex:item/castbillet")
      .Raw("shapeByType", shapeByType)
      .VariantGroup("form", [.. Forms.Select(f => f.Form)])
      // Each piece carries its own heat and is handled individually at the mill, so pieces never merge.
      .MaxStackSize(1)
      .MaterialDensity(7200)
      // Cast iron: remelts in a cupola at the cast-iron melting point, so off-cut stock is recoverable.
      .CombustibleProps(new { meltingPoint = 1150 })
      .Raw("attributesByType", byType)
      .CreativeCommon("*");
  }
}
