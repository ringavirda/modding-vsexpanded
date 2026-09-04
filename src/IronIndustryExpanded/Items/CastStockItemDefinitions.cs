using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using IronIndustryExpanded.BlockStructures.Casting;
using IronIndustryExpanded.BlockStructures.Forming.Items;

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

  /// <summary>
  /// The <see cref="BlockStructures.Forming.StockForm"/> a piece of <paramref name="form"/> is rolled
  /// as. Composed rather than listed because the item code already spells it: <c>caststock-billet</c>
  /// read without its <c>stock-</c> segment is <c>castbillet</c>, which is the form siex registers and
  /// the name its stage art is filed under. It must never be the bare variant - <c>bloom</c> is the
  /// shingled bar's former name, so a cast bloom declaring it would be rolled as a 400 u wrought bar.
  /// </summary>
  public static string FormOf(string form) => "cast" + form;

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Stock(domain)];

  private static ExItemDef Stock(string domain) {
    // shapeByType and attributesByType both key off the single `form` variant, so adding a cast stock is
    // one row in Forms plus its art.
    var shapeByType = new Dictionary<string, object>();
    var byType = new Dictionary<string, object>();
    foreach ((string form, int units, _) in Forms) {
      shapeByType["*-" + form] = new { @base = $"iiex:item/cast{form}" };
      // `stockForm` is what makes the piece a work piece: WorkPiece.FromStack falls back to the itemtype
      // attribute when a fresh stack carries no state of its own. The form it names is siex's, so on an
      // iiex-only install it resolves to nothing and the mill refuses the piece - which is the tier gate,
      // not a defect. See docs/design/machines/steel-roll-sets.md.
      byType["*-" + form] = new {
        materialUnits = units,
        stockForm = FormOf(form),
      };
    }

    return ExItemDef
      .Create(domain, "caststock")
      // Composes its own mesh per state, as the shingled stock does, so a part-rolled billet reads as
      // part-rolled in the hand rather than as the piece that left the long cell.
      .Class<ItemStockPiece>()
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
