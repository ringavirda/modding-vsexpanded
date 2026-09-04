using System.Collections.Generic;
using ExpandedLib.Definitions;
using static IronIndustryExpanded.Recipes.RecipeIngredients;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Diagram-driven grid recipes for molten canals: one material pattern (a reusable diagram plus
/// cobblestone) with the diagram picking the canal shape, in place of the seven hand-authored canal
/// patterns in <see cref="MoltenRecipeDefinitions"/>. The diagram is <c>.Tool()</c> (isTool), so it is
/// not consumed. Creative-only until the design table can draft diagrams, and it coexists with the
/// legacy canal recipes, sharing their cobblestone capture so both paths yield the same <c>{rock}</c>
/// variants. See docs/design/diagram-crafting.md.
/// </summary>
public class DiagramRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "diagram")
        .Grid(r =>
          r.Name("Molten Canal (Straight, from diagram)")
            .Pattern("D,C")
            .Size(1, 2)
            .Ingredient("D", i => i.Item("iiex:diagram-molten-straight").Tool())
            .Ingredient("C", Cobble)
            .OutputBlock("iiex:molten-canal-straight-{rock}-ns", 1)
        )
        .Grid(r =>
          r.Name("Molten Canal (Bend, from diagram)")
            .Pattern("D,C")
            .Size(1, 2)
            .Ingredient("D", i => i.Item("iiex:diagram-molten-bend").Tool())
            .Ingredient("C", Cobble)
            .OutputBlock("iiex:molten-canal-bend-{rock}-nw", 1)
        ),
    ];
}
