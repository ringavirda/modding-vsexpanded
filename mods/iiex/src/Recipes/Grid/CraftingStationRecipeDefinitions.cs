using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Grid recipes for iiex's crafting stations: the design table, which is the survival entry point to the
/// diagram to pattern to casting chain, and the workbench, which is where assemblies too large for the
/// player's own grid are put together. The table is costed in planks and candles only, with no metal and
/// no tool, because drafting carries no progression gate: the running cost is the parchment and charcoal
/// each diagram consumes. The bench costs iron for its vices, which is what it is fitted with.
/// See docs/design/mechanics/diagram-crafting.md and docs/design/machines/workbench.md.
/// </summary>
public class CraftingStationRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "designtable")
        // A candle at each end of a plank benchtop, matching the two wicks on the block's shape.
        .Grid(r =>
          r.Name("Design Table")
            .Pattern("CPC,PPP")
            .Size(3, 2)
            .Ingredient("C", i => i.Item("game:candle").Quantity(1))
            .Ingredient("P", i => i.Item("game:plank-*").Quantity(1))
            .OutputBlock("iiex:crafting-designtable-n", 1)
        ),
      ExRecipeDef
        .Create(domain, "grid", "workbench")
        // A vice at each end of a plank benchtop, matching the two the block's shape carries.
        .Grid(r =>
          r.Name("Workbench")
            .Pattern("_H_,V_V,PPP")
            .Size(3, 3)
            .Ingredient("H", Hammer)
            .Ingredient("V", Plate(2))
            .Ingredient("P", i => i.Item("game:plank-*").Quantity(2))
            .OutputBlock("iiex:crafting-workbench-n", 1)
        ),
      ExRecipeDef
        .Create(domain, "grid", "storagerack")
        // Planks and nothing else, two racks a craft. A rack returns no efficiency - it is limited by the
        // space given to it, not by its price - so its cost must never compete with a machine's.
        .Grid(r =>
          r.Name("Storage Rack")
            .Pattern("P_P,PPP")
            .Size(3, 2)
            .Ingredient("P", i => i.Item("game:plank-*").Quantity(1))
            .OutputBlock("iiex:storage-rack-n", 2)
        ),
    ];
}
