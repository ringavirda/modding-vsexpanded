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
  /// <summary>Units of metal in a piece, by form. Geometry decides both, at 1 vx³ = 2.5 u: the bar is the
  /// helve's output from two 200 u puddle balls (3 × 3 × 18), the slab six of them under the steam hammer
  /// (8 × 3 × 20). See docs/design/items/stock.md and docs/design/mechanics/density-rule.md.</summary>
  private static readonly Dictionary<string, int> Units = new() {
    ["shingledbar"] = 400,
    ["shingledslab"] = 1200,
  };

  // The states each form can be worked into are not declared here. They are the stage catalogue, in
  // assets/iiex/config/stageladders/, because items are generated from it and that has to happen before
  // the object loader builds items - a ladder carried on this itemtype could not be read in time.
  // See docs/design/mechanics/process-extension.md.

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    StockForm.All.Values.Select(form => Stock(domain, form));

  private static ExItemDef Stock(string domain, StockForm form) {
    // The shape is the form's as-shingled stage, and is only the fallback for a fresh, unworked piece: a
    // part-rolled piece overrides it per stack with a mesh drawn or composed at its own gauge.
    return ExItemDef
      .Create(domain, $"stock-{form.Name}")
      // Composes its own mesh per state, so a part-rolled piece reads as part-rolled in the hand.
      .Class<Items.ItemStockPiece>()
      .Shape($"iiex:forming/stock-{form.Name}-{(int)(form.BaseThickness * 10)}")
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
