using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Grid recipes for iwex's crafting <b>stations</b> - currently the design table
/// (docs/design/diagram-crafting.md).
/// <para>
/// This one recipe is what takes the whole <b>diagram → pattern → casting</b> chain out of creative. Every
/// downstream craft already exists in survival - drafting a diagram at the table, carving a pattern from a
/// diagram, ramming that pattern into a sand cell - but none of it was reachable, because the table itself
/// could only be spawned in. The gate was never the patterns; it was here.
/// </para>
/// <para>
/// Cost is deliberately trivial: planks and candles, no metal and no tool. Drafting is the drawing-office
/// fantasy the design is built around and the doc is explicit that there are <b>no progression gates</b> on
/// it - the only real cost is the parchment and charcoal each diagram consumes. Making the table cheap is
/// what lets a player start planning early, which is the point of the whole system.
/// </para>
/// </summary>
public class CraftingStationRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "designtable")
        // A candle at each end of a plank benchtop - the shape's two wicks, in the recipe.
        .Grid(r =>
          r.Name("Design Table")
            .Pattern("CPC,PPP")
            .Size(3, 2)
            .Ingredient("C", i => i.Item("game:candle").Quantity(1))
            .Ingredient("P", i => i.Item("game:plank-*").Quantity(1))
            .OutputBlock("iwex:crafting-designtable-n", 1)
        ),
    ];
}
