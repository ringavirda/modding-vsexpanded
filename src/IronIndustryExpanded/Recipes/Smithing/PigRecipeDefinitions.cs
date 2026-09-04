using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.Recipes.Smithing;

/// <summary>
/// Smithing (anvil) recipe for the pig-breaking chain: a brittle <c>iiex:pig</c> is hammered down to a
/// 10-voxel shape worth one <c>iiex:pigchunk</c>, while
/// <see cref="IronIndustryExpanded.Patches.AnvilPigBreakingPatches"/> pays the voxels the helve sheds on
/// the way there out as further chunks and bits (see
/// <see cref="IronIndustryExpanded.Items.PigBreaking"/> for the conservation maths). Authored through
/// the <see cref="ExRecipeDef.Body"/> escape hatch because smithing recipes are declarative voxel
/// patterns with no builder.
/// </summary>
public class PigRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "smithing", "pig")
        .Body(
          new
          {
            ingredient = new { type = "item", code = "iiex:pig" },
            name = "Broken pig iron",
            // A 10-voxel target (two rows of five): the helve sheds the pig's other 50 voxels down to it.
            pattern = new[] { new[] { "#####", "#####" } },
            code = "iwexpigbreak",
            output = new { type = "item", code = "iiex:pigchunk" },
          }
        ),
    ];
}
