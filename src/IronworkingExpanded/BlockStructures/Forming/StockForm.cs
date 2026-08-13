using System;
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
/// <param name="FormerNames">Names this form used to go by. A piece already in a world carries its form name
/// on the stack, so a rename that did not declare its old name would read as a form nobody registered and
/// the mill would refuse the piece. Declared rather than detected: a name that vanished and one that
/// appeared are indistinguishable from a rename without the hint.</param>
public sealed record StockForm(
  string Name,
  float BaseWidth,
  float BaseThickness,
  float MaxWidth,
  float BaseLength,
  float SpreadExponent,
  string[]? FormerNames = null
) {
  /// <summary>Names this form used to go by. Empty rather than null, so a caller never has to check.</summary>
  public string[] FormerNames { get; init; } = FormerNames ?? [];

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

  /// <summary>The shingled bar: two puddled balls drawn out on the helve, square off it (3 × 3 × 18), so it
  /// spreads hard. The exponent is set so the piece reaches nearly its full 8 wide at the 1-voxel gap, the
  /// proportion of the vanilla metal plate it is cut into.</summary>
  public static readonly StockForm ShingledBar = new(
    "shingledbar",
    3f,
    3f,
    8f,
    18f,
    0.846f,
    ["bloom"]
  );

  /// <summary>The shingled slab: six balls under the steam hammer, already wide (8 × 3 × 20), so it spreads
  /// far less and mostly draws out long. Too wide for a narrow barrel from the start, so it runs only on
  /// wide rolls.</summary>
  public static readonly StockForm ShingledSlab = new(
    "shingledslab",
    8f,
    3f,
    14f,
    20f,
    0.463f,
    ["slab"]
  );

  // A registry rather than a closed table. A third party's roll set can declare it accepts their own
  // stock, and without this the piece dead-ends at WrongForm because nothing can add the form itself -
  // named in machining-line.md as the literal wall on mill extensibility.
  private static readonly Dictionary<string, StockForm> _all = new(
    StringComparer.OrdinalIgnoreCase
  );

  // Kept apart from the table above so a former name is never a form in its own right: it resolves a piece
  // already in a world and is absent from All, the count every shipped-corpus guard reads.
  private static readonly Dictionary<string, StockForm> _former = new(
    StringComparer.OrdinalIgnoreCase
  );

  static StockForm() => SeedDefaults();

  /// <summary>Every registered form, by name.</summary>
  public static IReadOnlyDictionary<string, StockForm> All => _all;

  /// <summary>Registers (or replaces) a form under its own name, and under any name it used to go by.</summary>
  public static void Register(StockForm form) {
    _all[form.Name] = form;
    foreach (string former in form.FormerNames)
      _former[former] = form;
  }

  /// <summary>Drops a form. There is deliberately no clear: a mod emptying the table would take our stock
  /// with it, and every shipped roll set with it.</summary>
  public static void Unregister(string name) {
    if (_all.Remove(name, out StockForm? form))
      foreach (string former in form.FormerNames)
        _former.Remove(former);
  }

  /// <summary>Looks up a form by name, or by a name it used to go by; <c>false</c> when none is
  /// registered.</summary>
  public static bool TryGet(string? name, out StockForm? form) {
    form = null;
    return name != null
      && (
        _all.TryGetValue(name, out form) || _former.TryGetValue(name, out form)
      );
  }

  /// <summary>Puts the shipped forms back, replacing any override of them.</summary>
  public static void SeedDefaults() {
    Register(ShingledBar);
    Register(ShingledSlab);
  }
}
