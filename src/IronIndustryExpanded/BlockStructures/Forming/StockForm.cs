using System;
using System.Collections.Generic;

namespace IronIndustryExpanded.BlockStructures.Forming;

/// <summary>
/// Dimensions of a piece of rolling stock as it enters the mill, and how it spreads as the rolls flatten it.
/// <c>RolledStockStagesTests</c> pins the art its route draws against this model.
/// </summary>
/// <param name="Name">The form's code, as a roll set's <c>accepts</c> lists it.</param>
/// <param name="BaseWidth">Width of the authored base shape.</param>
/// <param name="BaseThickness">Thickness of the authored base shape (both bases leave the helve 3 thick).</param>
/// <param name="MaxWidth">Width the piece will not exceed however far it is rolled, sized to fit between the mill's housings.</param>
/// <param name="BaseLength">Length of the authored base shape - how far the piece travels through the rolls before it clears.</param>
/// <param name="SpreadExponent">How much of the reduction goes sideways rather than into length:
/// <c>w = w₀·(t₀/t)^e</c>. Per form, because lateral spread falls as stock gets wider relative to its thickness.</param>
/// <param name="Shape">Shape file holding this form's drawn states - the one its route names - or null when it draws none.</param>
/// <param name="BaseElement">The element that IS the piece as it arrives, rendered by the item and scaled for every undrawn gauge.</param>
/// <param name="FormerNames">Names this form used to go by, so a piece already in a world still resolves.
/// Declared rather than detected: a name that vanished and one that appeared are indistinguishable.</param>
public sealed record StockForm(
  string Name,
  float BaseWidth,
  float BaseThickness,
  float MaxWidth,
  float BaseLength,
  float SpreadExponent,
  string? Shape = null,
  string? BaseElement = null,
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
    "iiex:forming/shingledbar",
    "ShingledBar1",
    ["bloom"]
  );

  /// <summary>The shingled slab: six balls under the steam hammer, already wide (8 × 3 × 20). Too wide for
  /// a narrow barrel from the start, so it runs only on wide rolls, and it spreads as fast as it thins -
  /// 8 wide at 3 becoming 12 at 2, which is two heavy plates side by side and where its route ends.</summary>
  public static readonly StockForm ShingledSlab = new(
    "shingledslab",
    8f,
    3f,
    14f,
    20f,
    1.0f,
    "iiex:forming/shingledslab",
    "ShingledSlab1",
    ["slab"]
  );

  /// <summary>The rod: the one form that reaches the mill without passing a hammer. A player feeds vanilla's
  /// <c>game:rod-iron</c> at the deck and it is admitted as this (<see cref="Feedstock"/>), which is why the
  /// base section is vanilla's own 2 × 2 × 10. It spreads exactly as fast as it thins, and its ceiling is
  /// the narrow barrel's own width - so it is 2 wide entering and 4 wide leaving, never overhangs, and both
  /// branches of the fork cost the same four feeds.</summary>
  public static readonly StockForm Rod = new(
    "rod",
    2f,
    2f,
    4f,
    10f,
    1.0f,
    "iiex:forming/rod",
    "RolledRod200"
  );

  /// <summary>The beam: what a shingled bar taken flat to 2.0 becomes, and a piece the mill takes back.
  /// Its own form because it is its own item - the bar's route ends where the beam is claimed, and rolling
  /// a beam on to plate is a second walk. 4.5 x 2 x 18 off the rolls, spreading to the 9 a vanilla plate
  /// is wide.</summary>
  public static readonly StockForm Beam = new(
    "beam",
    4.5f,
    2f,
    9f,
    18f,
    1.0f,
    "iiex:forming/beam",
    "Beam"
  );

  /// <summary>The heavy plate: two come off a shingled slab at its last gap, and each goes back through the
  /// rolls on its own. 12 × 2 × 10 in, spreading to the 15 the wide barrel bottoms out at - which is where
  /// it stops widening and starts running out lengthways, ending as one boiler plate at 15 × 1 × 16.</summary>
  public static readonly StockForm HeavyPlate = new(
    "heavyplate",
    12f,
    2f,
    15f,
    10f,
    1.0f,
    "iiex:forming/heavyplate",
    "HeavyPlate1"
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

  // What the mill admits at its deck that is not stock: the offered code, and the stock item it enters as.
  // Kept as codes rather than forms because the piece a code becomes is an item, and an item's own
  // `stockForm` attribute is what names the form - so a caller wiring up feedstock never has to know one.
  private static readonly Dictionary<string, string> _feedstock = new(
    StringComparer.OrdinalIgnoreCase
  );

  /// <summary>Every admitted feedstock code, and the stock item it is converted into on entry.</summary>
  public static IReadOnlyDictionary<string, string> Feedstock => _feedstock;

  /// <summary>Admits <paramref name="offered"/> at the mill's deck, entering as <paramref name="entersAs"/>.
  /// Both are full item codes. This is how a product that already exists - vanilla's rod, another mod's
  /// bar - is re-rolled without minting a second copy of it for consumers to split over.</summary>
  public static void RegisterFeedstock(string offered, string entersAs) =>
    _feedstock[offered] = entersAs;

  /// <summary>The stock item <paramref name="offered"/> enters the mill as, or null when it is not admitted
  /// feedstock (which includes every stock item: those are already work pieces).</summary>
  public static string? EntersAs(string? offered) =>
    offered != null && _feedstock.TryGetValue(offered, out string? code)
      ? code
      : null;

  /// <summary>Puts the shipped forms back, replacing any override of them.</summary>
  public static void SeedDefaults() {
    Register(ShingledBar);
    Register(ShingledSlab);
    Register(Rod);
    Register(Beam);
    Register(HeavyPlate);

    // The re-rollable rod is vanilla's and stays vanilla's - 30-odd call sites across the mods already ask
    // for `game:rod-*` by name, so minting ours would split every consumer in two. It becomes a work piece
    // only on entering the rolls. See docs/design/items/rolled-parts.md § The rod is the fork.
    RegisterFeedstock("game:rod-iron", "iiex:stock-rod");
    // The beam is ours and is claimed at the mill, so it goes back in the same way rather than being
    // rolled on in place: one item, one route, and the player carries it round.
    RegisterFeedstock("iiex:beam", "iiex:stock-beam");
    RegisterFeedstock("iiex:heavyplate", "iiex:stock-heavyplate");
  }
}
