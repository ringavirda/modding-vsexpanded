using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronIndustryExpanded.Items;

namespace IronIndustryExpanded.Recipes.Smithing;

/// <summary>
/// The smithing recipe a cold blister-steel ingot is crushed down to. Authored through the
/// <see cref="ExRecipeDef.Body"/> escape hatch, as <see cref="PigRecipeDefinitions"/> is: smithing recipes
/// are declarative voxel patterns and the mod ships no builder for them.
/// </summary>
/// <remarks>
/// The recipe is only half the payout. It hands back the last 25 u as five vanilla bits; the three chunks
/// come off the shed voxels, which
/// <see cref="IronIndustryExpanded.Patches.AnvilBlisterBreakingPatches"/> pays out hit by hit. Together
/// they are the ingot exactly - see <see cref="BlisterBreaking"/> for the conservation maths.
/// </remarks>
public class BlisterRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "smithing", "blister")
        .Body(
          new
          {
            ingredient = new
            {
              type = "item",
              code = BlisterBreaking.IngotCode,
            },
            // Read by the anvil, never by a player: the patch keeps or drops this recipe by its name, and
            // the name is deliberately not one of the two vanilla grants helve workability by.
            name = BlisterBreaking.RecipeName,
            // A 10-voxel target, two rows of five. A smithing pattern is centred and transposed as it is
            // laid out, so this lands at x 5..9, z 7..8 - which is what BlisterBreaking.CreateVoxels
            // covers. The helve crushes the block's other 30 voxels down to it.
            pattern = new[] { new[] { "#####", "#####" } },
            code = BlisterBreaking.RecipeCode,
            output = new
            {
              type = "item",
              code = BlisterBreaking.BitCode,
              quantity = BlisterBreaking.ChunkUnits / BlisterBreaking.BitUnits,
            },
          }
        ),
    ];
}
