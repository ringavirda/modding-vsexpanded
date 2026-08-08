using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using IronworkingExpanded.BlockStructures.Casting;

namespace IronworkingExpanded.Items;

/// <summary>
/// The bulk <b>cast stock</b> the long cell pours: billet, bloom and slab. These are the rolling mill's
/// cast-side feed - the wrought side is a hammered bloom - and they are the reason the long cell exists.
/// <para>
/// <b>Every mass here is its drawn cavity volume × ~2.5</b>, the
/// <c>docs/design/mechanics/density-rule.md</c> constant - and the <b>~</b> is deliberate. The ladder
/// table in <c>iwex.md</c> is the authority these numbers follow, over both the long cell's lane cavity
/// and the drawn item shape; see <c>docs/design/machines/long-cell.md</c> § Numbers for the audit.
/// </para>
/// <para>
/// <b>The masses are declared, not derived - and nothing in the art is authoritative over them.</b>
/// The drawn
/// <b>item shapes</b> are finished art, already mapped for derived material and siding, so they are sized
/// for the forming line rather than for mass arithmetic. The long cell's <b>sand cavities</b> are
/// illustrative - they draw the rammed sand and the fill glow, not the metal content. So the density rule
/// (<c>docs/design/mechanics/density-rule.md</c>) is what <em>sizes art plausibly</em>; the mass lives
/// here, and <c>capacity</c> on the pattern is what a pour actually measures against.
/// </para>
/// <para>
/// So a mismatch between one of these numbers and a voxel count is <b>not</b> a bug to be chased. The
/// numbers that must agree are the ones below and the crop table in <c>iwex.md</c> - which is what
/// <c>CastStockMassTests</c> pins.
/// </para>
/// <para>
/// The masses are not free choices even given the rule: they are the numbers that make every crop in the
/// ladder divide exactly. 600 u is the wide tier's quantum - a cast slab is 5 heavy plates, a billet is 3
/// plates at 9 long (27 = 3 × 9), a bloom is 5. Change one and the whole product table re-opens.
/// </para>
/// <para>
/// <b>All three are <see cref="MoldSize.LongCell"/> products and none of them can be cast in the 1×1
/// casting cell.</b> That is not a policy, it is arithmetic: the smallest of them is 600 u against a
/// casting cell's 100 u default cavity, and the slab at 3000 u is thirty times it. The cell enforces this
/// (<c>BlockEntitySandCastingCell.Imprint</c> refuses a non-<c>Cell</c> spec with
/// <c>iwex-castingcell-wrongsize</c>); <see cref="Forms"/> is what the long cell's patterns are generated
/// from, so the two sets cannot drift.
/// </para>
/// <para>
/// The long cell's pattern set is <b>these three plus <c>castframe</c></b>, which is a machine part
/// rather than stock and so has no row here.
/// </para>
/// </summary>
public class CastStockItemDefinitions : IExItemDefProvider
{
  /// <summary>Units of cast iron in one billet. A 240 vx³ lane cavity.</summary>
  public const int BilletUnits = 600;

  /// <summary>Units of cast iron in one bloom. A 400 vx³ lane cavity.</summary>
  public const int BloomUnits = 1000;

  /// <summary>Units of cast iron in one slab. A 1200 vx³ lane cavity.</summary>
  public const int SlabUnits = 3000;

  /// <summary>
  /// Each form, its mass and the <b>pattern type</b> that casts it, in emit order. One table so the item
  /// defs, the long cell's lane capacities and the mass test all read the same row.
  /// <para>
  /// <b>The pattern type is carried explicitly and is not <c>"cast" + Form</c>.</b> A concatenation
  /// cannot express the naming rule: a pattern is <b>plural where its impression
  /// yields more than one piece</b> - <c>castbillets</c> (3 lanes), <c>castblooms</c> (2), <c>castslab</c>
  /// (1) - while the <em>item</em> stays singular, because one item is one billet. So the table states
  /// both.
  /// </para>
  /// </summary>
  public static readonly IReadOnlyList<(string Form, int Units, string Pattern)> Forms =
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

  private static ExItemDef Stock(string domain)
  {
    // shapeByType and attributesByType both key off the single `form` variant, so adding a fourth cast
    // stock is one row in Forms plus its art - not four edits that can fall out of step.
    var shapeByType = new Dictionary<string, object>();
    var byType = new Dictionary<string, object>();
    foreach ((string form, int units, _) in Forms)
    {
      shapeByType["*-" + form] = new { @base = $"iwex:item/cast{form}" };
      byType["*-" + form] = new { materialUnits = units };
    }

    return ExItemDef
      .Create(domain, "caststock")
      .Shape("iwex:item/castbillet")
      .Raw("shapeByType", shapeByType)
      .VariantGroup("form", [.. Forms.Select(f => f.Form)])
      // Each piece carries its own heat and is walked back around the mill by hand, so they can never
      // merge - the same reason the wrought-side work piece is a single-item stack.
      .MaxStackSize(1)
      .MaterialDensity(7200)
      // Cast iron: it remelts in a cupola at the cast-iron melting point, which is what makes off-cut
      // stock genuinely recoverable rather than a dead end.
      .CombustibleProps(new { meltingPoint = 1150 })
      .Raw("attributesByType", byType)
      .CreativeCommon("*");
  }
}
