using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>
/// The rolling-mill <b>work pieces</b>: stock as it comes off the helve hammer, and as it exists part-way
/// through a schedule. One item per <see cref="StockForm"/>, carrying its per-strip thicknesses and its heat on
/// the stack (<see cref="WorkPiece"/>), because a two-high stand cannot be fed backwards - the piece is walked
/// back around the mill after every pass, so it spends most of its life in the player's hands or on the ground.
/// <para>
/// It is a plain forgeable-free item and deliberately <b>not</b> hot-workable at the anvil: the mill forms it,
/// the helve only consolidated it. Its heat uses vanilla's temperature attribute, so it keeps cooling during
/// the carry-back - which is what turns the walk into a real cost rather than only tedium, and what gives the
/// reheat furnace its job.
/// </para>
/// </summary>
public class StockItemDefinitions : IExItemDefProvider
{
  /// <summary>Units of metal in a piece, by form. A bloom is the helve's output from two puddle balls; the
  /// slab is the heavier cast piece.</summary>
  private static readonly Dictionary<string, int> Units = new()
  {
    ["bloom"] = 180,
    ["slab"] = 400,
  };

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    StockForm.All.Values.Select(form => Stock(domain, form));

  private static ExItemDef Stock(string domain, StockForm form)
  {
    // The shape is the form's AS-SHINGLED stage. A part-rolled piece overrides it per stack with a mesh
    // composed from its strips, so a half-worked bloom really does read thin down one side - see the work
    // piece's renderer. This is only the fallback for a fresh, unworked piece.
    return ExItemDef
      .Create(domain, $"stock-{form.Name}")
      // Composes its own mesh per state, so a part-rolled piece reads as part-rolled in the hand.
      .Class<Items.ItemStockPiece>()
      .Shape($"iwex:forming/stock-{form.Name}-{(int)(form.BaseThickness * 10)}")
      .MaxStackSize(1) // each piece carries its own strip state, so they can never merge
      .MaterialDensity(7800)
      .Attribute("materialUnits", Units[form.Name])
      .Attribute("stockForm", form.Name)
      // Hot work: it comes off the helve at forging heat and cools in the hand, which is the whole point of
      // the carry-back. `temperature` is vanilla's own attribute, so the cooling is the engine's.
      .Raw(
        "combustibleProps",
        new
        {
          meltingPoint = 1500,
          meltingDuration = 30,
          smeltedRatio = 1,
        }
      )
      .Raw("temperatureDamage", 4f)
      .CreativeCommon("*");
  }
}
