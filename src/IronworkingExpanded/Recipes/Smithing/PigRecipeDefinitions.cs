using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Recipes.Smithing;

/// <summary>
/// Smithing (anvil) recipe for the pig-breaking chain: a brittle <c>iwex:pig</c> is hammered down to a
/// 10-voxel shape worth one <c>iwex:pigchunk</c>, while
/// <see cref="IronworkingExpanded.Patches.AnvilPigBreakingPatches"/> pays the voxels the helve sheds on
/// the way there out as further chunks and bits (see
/// <see cref="IronworkingExpanded.Items.PigBreaking"/> for the conservation maths). Authored through
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
            ingredient = new { type = "item", code = "iwex:pig" },
            name = "Broken pig iron",
            // A 10-voxel target (two rows of five): the helve sheds the pig's other 50 voxels down to it.
            pattern = new[] { new[] { "#####", "#####" } },
            code = "iwexpigbreak",
            output = new { type = "item", code = "iwex:pigchunk" },
          }
        ),
    ];
}
