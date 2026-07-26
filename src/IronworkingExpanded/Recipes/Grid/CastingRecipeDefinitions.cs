using System.Collections.Generic;
using ExpandedLib.Definitions;
using static IronworkingExpanded.Recipes.RecipeIngredients;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for iwex's sand-casting <b>stations</b> (as opposed to the parts they cast, whose
/// recipes live with the patterns). The 1×1 casting cell is a fired-brick shell hammered and chiselled over
/// fire clay - the same brick-tinted idiom as the molten canal, so it shares the canal's
/// <see cref="RecipeIngredients.Fhk"/> / <see cref="RecipeIngredients.RunningBrick"/> /
/// <see cref="RecipeIngredients.FireBrick"/> captures.
/// <para>
/// The cell carries an 8-state <c>brick</c> variant (fire + seven colours), so it takes two recipes sharing
/// one pattern: a coloured route that captures the running-brick colour into <c>{brick}</c>, and a fire-brick
/// route that outputs the <c>fire</c> default. The two never conflict - the coloured brick course
/// (<c>brickcourse-four-running-*</c>) and the fire bricks (<c>claybricks-good-fire</c>) do not overlap -
/// exactly as the canal's coloured/fire pairs.
/// </para>
/// </summary>
public class CastingRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [SandCastingCell(domain)];

  private static ExRecipeDef SandCastingCell(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "sandcastingcell")
      // Six bricks walled around a fire-clay core, hammered and chiselled into the launder shell.
      .Grid(r =>
        Fhk(
            r.Name("Sand Casting Cell (Colored Brick)")
              .Pattern("BHB,BFB,BKB")
              .Size(3, 3)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iwex:sandcastingcell-{brick}-north", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Sand Casting Cell (Fire Brick)")
              .Pattern("BHB,BFB,BKB")
              .Size(3, 3)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iwex:sandcastingcell-fire-north", 1)
      );
}
