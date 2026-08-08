using System.Collections.Generic;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>
/// Dimensions of a piece of rolling stock as it leaves the helve hammer, and how it spreads as the mill
/// flattens it. One entry per authored base shape; thinner stages are derived from these by
/// <c>scripts/generate-rolled-stock.py</c>, and <c>RolledStockStagesTests</c> pins art and simulation together.
/// </summary>
/// <param name="Name">The form's code, as a roll set's <c>accepts</c> lists it.</param>
/// <param name="BaseWidth">Width of the authored base shape.</param>
/// <param name="BaseThickness">Thickness of the authored base shape (both bases leave the helve 3 thick).</param>
/// <param name="MaxWidth">Width the piece will not exceed however far it is rolled, sized to fit between the mill's housings.</param>
/// <param name="BaseLength">Length of the authored base shape - how far the piece travels through the rolls before it clears.</param>
/// <param name="SpreadExponent">How much of the reduction goes sideways rather than into length:
/// <c>w = w₀·(t₀/t)^e</c>. Per form, because lateral spread falls as stock gets wider relative to its thickness.</param>
public sealed record StockForm(
  string Name,
  float BaseWidth,
  float BaseThickness,
  float MaxWidth,
  float BaseLength,
  float SpreadExponent
) {
  /// <summary>Width this form reaches once rolled to <paramref name="thickness"/>, capped at
  /// <see cref="MaxWidth"/>.</summary>
  public float WidthAt(float thickness) =>
    RollingPass.SpreadWidth(
      BaseWidth,
      BaseThickness,
      thickness,
      SpreadExponent,
      MaxWidth
    );

  /// <summary>The shingled bloom: square off the helve (3 × 3), so it spreads hard. The exponent is set so
  /// the piece reaches nearly its full 8 wide at the 1-voxel gap, the proportion of the vanilla metal plate
  /// it is cut into.</summary>
  public static readonly StockForm Bloom = new(
    "bloom",
    3f,
    3f,
    8f,
    16f,
    0.846f
  );

  /// <summary>The cast slab: already wide (8 × 3), so it spreads far less and mostly draws out long. Too
  /// wide for a narrow barrel from the start, so it runs only on wide rolls.</summary>
  public static readonly StockForm Slab = new("slab", 8f, 3f, 14f, 20f, 0.463f);

  /// <summary>Every form with authored base art, by name.</summary>
  public static readonly IReadOnlyDictionary<string, StockForm> All =
    new Dictionary<string, StockForm> {
      [Bloom.Name] = Bloom,
      [Slab.Name] = Slab,
    };
}
