using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronworkingExpanded.BlockStructures.Casting;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Carves the sand-casting <b>patterns</b> from their diagrams: a reusable pattern diagram (<c>isTool</c>)
/// plus a knife (<c>isTool</c>) shape a wooden positive out of plank stock. One recipe per pattern, derived
/// from <see cref="PatternItemDefinitions.PatternTypes"/> - the same single source the pattern variants and
/// their diagram variants come from - so adding a castable part carries its craft automatically.
/// <para>
/// The diagram itself is creative-only until the design table can draft it (exactly like the canal diagrams
/// in <see cref="DiagramRecipeDefinitions"/>); this is the diagram→pattern leg of that phase. The diagram
/// is a tool (draw once, reuse); the knife is a tool; the planks are consumed.
/// </para>
/// </summary>
public class PatternRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain)
  {
    var def = ExRecipeDef.Create(domain, "grid", "pattern");
    foreach (string type in PatternItemDefinitions.PatternTypes)
      def = def.Grid(r =>
        r.Name($"Pattern ({type}, from diagram)")
          .Pattern("DKP")
          .Size(3, 1)
          .Ingredient("D", i => i.Item($"iwex:diagram-item-{type}").Tool())
          .Ingredient("K", i => i.Item("game:knife-*").Tool())
          // The plank's wood is captured and carried into the pattern variant (cosmetic).
          .Ingredient(
            "P",
            i =>
              i.Item("game:plank-*")
                .Named("wood", PatternItemDefinitions.PatternWoods)
                .Quantity(2)
          )
          .OutputItem($"iwex:pattern-{type}-{{wood}}", 1)
      );
    return [def];
  }
}
