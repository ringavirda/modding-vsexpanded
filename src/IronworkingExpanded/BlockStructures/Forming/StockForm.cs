using System.Collections.Generic;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>
/// The dimensions of a piece of rolling stock as it leaves the helve hammer, and how it spreads as the mill
/// flattens it. One entry per authored base shape; every thinner stage is derived from these by
/// <c>scripts/generate-rolled-stock.py</c>, and the same numbers drive the simulation, so the art and the
/// mechanics cannot disagree (<c>RolledStockStagesTests</c> pins them together).
/// </summary>
/// <param name="Name">The form's code, as a roll set's <c>accepts</c> lists it.</param>
/// <param name="BaseWidth">Width of the authored base shape.</param>
/// <param name="BaseThickness">Thickness of the authored base shape (both bases leave the helve 3 thick).</param>
/// <param name="MaxWidth">
/// Width the piece will not exceed however far it is rolled. A real bound, not a fudge: the piece has to fit
/// between the mill's housings, and these values are sized to the rolling-mill model.
/// </param>
/// <param name="BaseLength">Length of the authored base shape - how far the piece travels through the rolls before it clears.</param>
/// <param name="SpreadExponent">
/// How much of the reduction goes sideways rather than into length: <c>w = w₀·(t₀/t)^e</c>.
/// <para>
/// It differs per form for a real reason - <b>lateral spread falls as stock gets wider relative to its
/// thickness</b>, because friction across a wide face resists sideways flow while a narrow bar has nothing
/// holding it in. So a bloom (entering square, w/t ≈ 1) spreads hard, and a slab (w/t ≈ 2.7) mostly draws out
/// long instead.
/// </para>
/// </param>
public sealed record StockForm(
  string Name,
  float BaseWidth,
  float BaseThickness,
  float MaxWidth,
  float BaseLength,
  float SpreadExponent
)
{
  /// <summary>Width this form reaches once rolled to <paramref name="thickness"/>, capped at
  /// <see cref="MaxWidth"/>.</summary>
  public float WidthAt(float thickness) =>
    RollingPass.SpreadWidth(BaseWidth, BaseThickness, thickness, SpreadExponent, MaxWidth);

  /// <summary>
  /// The shingled <b>bloom</b>: square-ish off the helve (3 × 3), so it spreads hard. Its exponent is set so
  /// it arrives at nearly its full 8 wide by the <b>1-voxel gap</b> - one voxel thick and about eight wide is
  /// the proportion of a vanilla metal plate, which is exactly what the piece gets cut into, so the schedule
  /// lands the stock on plate geometry rather than near it.
  /// </summary>
  public static readonly StockForm Bloom = new("bloom", 3f, 3f, 8f, 16f, 0.846f);

  /// <summary>
  /// The cast <b>slab</b>: already wide (8 × 3), so it spreads far less and mostly draws out long. It is too
  /// wide for a narrow barrel from the start, which is why it can only be run on wide rolls.
  /// </summary>
  public static readonly StockForm Slab = new("slab", 8f, 3f, 14f, 20f, 0.463f);

  /// <summary>Every form with authored base art, by name.</summary>
  public static readonly IReadOnlyDictionary<string, StockForm> All = new Dictionary<
    string,
    StockForm
  >
  {
    [Bloom.Name] = Bloom,
    [Slab.Name] = Slab,
  };
}
