using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>
/// The rolling-mill work pieces: stock as it comes off the helve hammer and as it exists part-way through a
/// schedule. One item per <see cref="StockForm"/>, carrying its per-strip thicknesses and its heat on the
/// stack (<see cref="WorkPiece"/>), since a two-high stand cannot be fed backwards and the piece is walked
/// back around the mill after every pass.
/// <para>
/// It is not hot-workable at the anvil; only the mill forms it. Its heat uses vanilla's temperature
/// attribute, so the engine cools it during the carry-back.
/// </para>
/// </summary>
public class StockItemDefinitions : IExItemDefProvider {
  /// <summary>Units of metal in a piece, by form. A bloom is the helve's output from two puddle balls; a
  /// slab is the heavier cast piece.</summary>
  private static readonly Dictionary<string, int> Units = new() {
    ["bloom"] = 180,
    ["slab"] = 400,
  };

  // The states each form can be worked into are not declared here. They are the stage catalogue, in
  // assets/iwex/config/stageladders/, because items are generated from it and that has to happen before
  // the object loader builds items - a ladder carried on this itemtype could not be read in time.
  // See docs/design/mechanics/process-extension.md.

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    StockForm.All.Values.Select(form => Stock(domain, form));

  private static ExItemDef Stock(string domain, StockForm form) {
    // The shape is the form's as-shingled stage, and is only the fallback for a fresh, unworked piece: a
    // part-rolled piece overrides it per stack with a mesh composed from its strips.
    return ExItemDef
      .Create(domain, $"stock-{form.Name}")
      // Composes its own mesh per state, so a part-rolled piece reads as part-rolled in the hand.
      .Class<Items.ItemStockPiece>()
      .Shape($"iwex:forming/stock-{form.Name}-{(int)(form.BaseThickness * 10)}")
      .MaxStackSize(1) // each piece carries its own strip state, so they can never merge
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
