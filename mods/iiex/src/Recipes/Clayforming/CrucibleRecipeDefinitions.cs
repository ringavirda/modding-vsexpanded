using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.Recipes.Clayforming;

/// <summary>
/// Clayforming the steel crucible, from fire clay exactly as vanilla forms its own. Authored through the
/// <see cref="ExRecipeDef.Body"/> escape hatch because clayforming is a declarative voxel pattern with no
/// builder, the same reason the pig-breaking smithing recipe uses it.
/// </summary>
/// <remarks>
/// The output is the <c>-raw</c> pot and can be nothing else: a clayforming surface only ever yields
/// unfired clay, which the pit kiln fires through the blocktype's own <c>combustibleProps</c>. Fire clay
/// alone, where vanilla's crucible takes blue, fire or red: the pot exists because ordinary clay will not
/// survive 1600 C, so the recipe says which clay where a player will look.
/// </remarks>
public class CrucibleRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "clayforming", "steelcrucible")
        .Body(
          new
          {
            // `clay-*` with a captured `type` is the shape vanilla uses; narrowing allowedVariants to
            // fire is what makes it fireclay. Only ItemClay can open a clayforming surface at all, and it
            // resolves `game:clayform` unqualified, so the ingredient must stay a vanilla clay.
            ingredient = new
            {
              type = "item",
              code = "clay-*",
              name = "type",
              allowedVariants = new[] { "fire" },
            },
            // Layer / row / char is Y / X / Z. Vanilla's crucible walls are one voxel thick round a 3x3
            // well, seven courses high, closing to a rim - copied course for course, because the drawn
            // pot is the same vessel and a pattern that did not match its shape would read as a different
            // object being formed.
            pattern = new[]
            {
              new[] { "_____", "#####", "#####", "#####", "#####", "#####" },
              new[] { "_____", "#####", "#___#", "#___#", "#___#", "#####" },
              new[] { "_____", "#####", "#___#", "#___#", "#___#", "#####" },
              new[] { "_____", "#####", "#___#", "#___#", "#___#", "#####" },
              new[] { "_____", "#####", "#___#", "#___#", "#___#", "#####" },
              new[] { "_____", "#####", "#___#", "#___#", "#___#", "#####" },
              new[] { "_____", "#####", "#___#", "#___#", "#___#", "#####" },
              new[] { "_____", "_____", "_###_", "_#_#_", "_###_", "_____" },
            },
            // An AssetLocation used for logging and workability matching, never shown to a player: the
            // clayforming selector renders the output stack's own name. No lang key is owed for it.
            name = "Steel Crucible",
            output = new
            {
              type = "block",
              code = $"{domain}:steelcrucible-raw",
            },
          }
        ),
    ];
}
