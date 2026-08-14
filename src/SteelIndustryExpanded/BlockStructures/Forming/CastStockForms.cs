using IronIndustryExpanded.BlockStructures.Forming;

namespace SteelIndustryExpanded.BlockStructures.Forming;

/// <summary>
/// The three cast stock forms - billet, bloom and slab - as the rolling mill sees them. iiex pours the
/// pieces (<c>iiex:caststock-{billet,bloom,slab}</c>, cast by the long cell) and owns the mill itself;
/// what this mod adds is the ability to roll them, which is a form row, a stage route and a crop table
/// and no machinery at all. Without siex installed the pieces still exist and the mill refuses them,
/// which is the intended shape of the gate.
/// <para>
/// Every number here is the section the art is drawn at, and the same table drives
/// <c>scripts/tools/generate-rolled-stock.py</c>'s stage shapes - so a form edited here without the art
/// regenerated is caught by <c>CastStockStagesTests</c> rather than shipping as a piece that renders at
/// one gauge and rolls at another. See docs/design/items/stock.md and
/// docs/design/machines/steel-roll-sets.md.
/// </para>
/// </summary>
public static class CastStockForms {
  /// <summary>
  /// The billet: the long cell's three-lane pattern, 3 x 3 x 27 = 243 vx³ = 600 u. Square off the cell
  /// and so it spreads exactly as fast as it thins (e = 1), reaching its 9-wide ceiling at the 1.0 gap -
  /// where 27 long is three plates end to end. It is the one cast form narrow enough for the mill's own
  /// barrel.
  /// </summary>
  public static readonly StockForm CastBillet = new(
    "castbillet",
    3f,
    3f,
    9f,
    27f,
    1.0f
  );

  /// <summary>
  /// The bloom: the two-lane pattern, 4 x 4 x 25 = 400 vx³ = 1000 u. Also square, also e = 1, and its
  /// 8-wide ceiling is reached by the 2.0 gap - which is what makes five 8 x 2 x 5 blanks fall out of it
  /// exactly.
  /// </summary>
  public static readonly StockForm CastBloom = new(
    "castbloom",
    4f,
    4f,
    8f,
    25f,
    1.0f
  );

  /// <summary>
  /// The slab: the one-lane pattern, whose mold walls are the cell's own, 12 x 4 x 25 = 1200 vx³ =
  /// 3000 u. Wide already, so it spreads little and draws out long instead: e = log2(1.25), the exponent
  /// that takes 12 wide to the 15 the barrel caps it at across one halving.
  /// </summary>
  public static readonly StockForm CastSlab = new(
    "castslab",
    12f,
    4f,
    15f,
    25f,
    0.3219f
  );

  /// <summary>
  /// Puts the three cast forms in the shared registry. Idempotent, since <see cref="StockForm.Register"/>
  /// replaces rather than appends, so a second world in one process re-registers rather than duplicating.
  /// </summary>
  public static void Register() {
    StockForm.Register(CastBillet);
    StockForm.Register(CastBloom);
    StockForm.Register(CastSlab);
  }
}
