using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.BlockStructures.Forming;

/// <summary>
/// The rolling-mill work pieces: stock as it comes off the helve hammer and as it exists part-way through a
/// schedule. One item per <see cref="StockForm"/>, carrying its gauge and its heat on the stack
/// (<see cref="WorkPiece"/>), since a two-high stand cannot be fed backwards and the piece is walked
/// back around the mill after every pass.
/// <para>
/// It is not hot-workable at the anvil; only the mill forms it. Its heat uses vanilla's temperature
/// attribute, so the engine cools it during the carry-back.
/// </para>
/// </summary>
public class StockItemDefinitions : IExItemDefProvider {
  /// <summary>Units of metal in a piece, by form, and thereby the forms this mod mints an item for.
  /// Geometry decides both, at 1 vx³ = 2.5 u: the bar is the helve's output from two 200 u puddle balls
  /// (3 × 3 × 18), the slab six of them under the steam hammer (8 × 3 × 20).
  /// See docs/design/items/stock.md and docs/design/mechanics/density-rule.md.</summary>
  private static readonly Dictionary<string, int> Units = new() {
    ["shingledbar"] = 400,
    ["shingledslab"] = 1200,
    // The rod is vanilla's own 2 × 2 × 10 = 40 vx³, and this item is what one becomes on entering the mill.
    // Its mass has to be the rod's exactly or the fork mints or loses metal at the deck.
    ["rod"] = 100,
    // A whole shingled bar's metal: the flat 2.0 stage is the beam, so the two masses must agree exactly
    // or the claim at the mill mints or loses on every bar.
    ["beam"] = 400,
    // Half a shingled slab: 12 x 2 x 10, the wide tier's 600 u quantum, so a slab divides into exactly two.
    ["heavyplate"] = 600,
  };

  /// <summary>
  /// The metal a piece of <paramref name="form"/> carries, or 0 for a form this mod pours no item for.
  /// The masses are not free: the helve piles whole puddled balls, so a stock mass that is not a multiple
  /// of the ball's would mint or lose metal on every piece shingled.
  /// </summary>
  public static int UnitsOf(string form) =>
    Units.TryGetValue(form, out int units) ? units : 0;

  // The states each form can be worked into are not declared here. They are the stage catalogue, in
  // assets/iiex/config/processroutes/, because items are generated from it and that has to happen before
  // the object loader builds items - a route carried on this itemtype could not be read in time.
  // See docs/design/mechanics/process-extension.md.

  // The forms this mod pours, not every form registered. StockForm.All is a shared registry any mod may
  // add to - siex puts the three cast forms in it, and a third party may add its own - and each of those
  // ships its own item under its own domain. Emitting the whole registry here would mint an iiex item for
  // somebody else's stock, and would throw on the first form we hold no mass for.
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    Units
      .Keys.Where(name => StockForm.All.ContainsKey(name))
      .Select(name => Stock(domain, StockForm.All[name]));

  private static ExItemDef Stock(string domain, StockForm form) {
    // The shape is the form's as-shingled stage, and is only the fallback for a fresh, unworked piece: a
    // part-rolled piece overrides it per stack with a mesh drawn or composed at its own gauge.
    return ExItemDef
      .Create(domain, $"stock-{form.Name}")
      // Composes its own mesh per state, so a part-rolled piece reads as part-rolled in the hand.
      .Class<Items.ItemStockPiece>()
      // The base state inside the family shape its route draws, rather than a generated file of its
      // own: `selectiveElements` keeps the item - and the composed mesh scaled from it - to the one
      // element that IS the piece.
      .Shape(form.Shape ?? "game:item/ingot")
      .ShapeSelectiveElements(form.BaseElement ?? "")
      .MaxStackSize(1) // each piece carries its own gauge and heat, so they can never merge
      .MaterialDensity(7800)
      .Attribute("materialUnits", Units[form.Name])
      .Attribute("stockForm", form.Name)
      // The piece comes off the helve at forging heat and cools in the hand. `temperature` is vanilla's own
      // attribute, so the engine does the cooling.
      .Raw(
        "combustibleProps",
        new {
          meltingPoint = 1500,
          meltingDuration = 30,
          smeltedRatio = 1,
        }
      )
      .Raw("temperatureDamage", 4f)
      .CreativeCommon("*");
  }
}
