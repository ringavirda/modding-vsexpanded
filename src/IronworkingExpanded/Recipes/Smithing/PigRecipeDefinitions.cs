using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Recipes.Smithing;

/// <summary>
/// Code-first smithing (anvil) recipe for the pig-breaking chain (migrated from recipes/smithing/pig.json).
/// One voxel-pattern recipe: a brittle <c>iwex:pig</c> is hammered down to a small 10-voxel shape worth a
/// single <c>iwex:pigchunk</c>, while <see cref="IronworkingExpanded.Patches.AnvilPigBreakingPatches"/> pays
/// the voxels the helve sheds on the way there out as further chunks and bits (see
/// <see cref="IronworkingExpanded.Items.PigBreaking"/> for the conservation maths).
/// <para>
/// Smithing recipes are declarative voxel patterns with no dedicated builder yet, so this is authored via the
/// <see cref="ExRecipeDef.Body"/> escape hatch (a lone anonymous object, the file's single recipe) - the same
/// approach as lpex's gear recipes.
/// </para>
/// </summary>
public class PigRecipeDefinitions : IExRecipeDefProvider
{
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
