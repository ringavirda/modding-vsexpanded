using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Grid recipes for iiex's crafting stations, currently the design table, which is the survival entry point
/// to the diagram to pattern to casting chain. Costed in planks and candles only, with no metal and no
/// tool, because drafting carries no progression gate: the running cost is the parchment and charcoal each
/// diagram consumes. See docs/design/diagram-crafting.md.
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
    ];
}
