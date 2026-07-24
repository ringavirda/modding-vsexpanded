using System.Collections.Generic;
using ExpandedLib.Definitions;
using static IronworkingExpanded.Recipes.RecipeIngredients;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Phase-2 proof of the diagram-crafting system (docs/design/diagram-crafting.md): the SAME material
/// pattern (a reusable diagram + cobblestone), the diagram picking the canal shape - what the seven
/// hand-authored canal patterns in <see cref="MoltenRecipeDefinitions"/> collapse into. The diagram is
/// <c>.Tool()</c> (isTool), so it is not consumed: draw it once, reuse it. Creative-only until the design
/// table can draft diagrams, and it coexists with the legacy canal recipes for now (Phase 6 migrates them
/// wholesale). The cobblestone capture is shared with those legacy recipes so both paths yield the same
/// <c>{rock}</c> variants.
/// </summary>
public class DiagramRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "diagram")
        .Grid(r =>
          r.Name("Molten Canal (Straight, from diagram)")
            .Pattern("D,C")
            .Size(1, 2)
            .Ingredient("D", i => i.Item("iwex:diagram-molten-straight").Tool())
            .Ingredient("C", Cobble)
            .OutputBlock("iwex:moltencanal-straight-{rock}-ns", 1)
        )
        .Grid(r =>
          r.Name("Molten Canal (Bend, from diagram)")
            .Pattern("D,C")
            .Size(1, 2)
            .Ingredient("D", i => i.Item("iwex:diagram-molten-bend").Tool())
            .Ingredient("C", Cobble)
            .OutputBlock("iwex:moltencanal-bend-{rock}-nw", 1)
        ),
    ];
}
