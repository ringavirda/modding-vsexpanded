using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronworkingExpanded.BlockStructures.Casting;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Grid recipes carving the sand-casting patterns from their diagrams: a pattern diagram and a knife,
/// both tools, shape a wooden positive out of plank stock. One recipe per entry in
/// <see cref="PatternItemDefinitions.PatternTypes"/>, the same source the pattern and diagram variants
/// are generated from, so a new castable part carries its craft with it. The diagram is creative-only
/// until the design table can draft it, as with the canal diagrams in
/// <see cref="DiagramRecipeDefinitions"/>.
/// </summary>
public class PatternRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) {
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
