using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronworkingExpanded.Items;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the forming shop, currently the rolling mill stand. Roll sets are not here:
/// they are cast rather than fabricated, so their recipes belong with the casting patterns.
/// <para>
/// The mill is buildable before it is useful - no stock item resolves a work piece and no product comes off
/// the rolls yet. See docs/internal/plans/iwex-bringup.md, Stage 4.
/// </para>
/// </summary>
public class FormingRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [RollingMill(domain)];

  /// <summary>
  /// The mill stand: a heavy cast-iron housing, journals for the two rolls, and a gear coupling it to the
  /// line shaft. Costed in heavy cast plate, which takes a cupola, a casting cell and a pattern before the
  /// first plate exists.
  /// </summary>
  private static ExRecipeDef RollingMill(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "rollingmill")
      .Grid(r =>
        r.Name("Rolling Mill")
          .Pattern("PRP,PGP,PHP")
          .Size(3, 3)
          .Ingredient("P", i => i.Item($"{domain}:castplate-heavy").Quantity(1))
          .Ingredient("R", Rod(2))
          .Ingredient(
            "G",
            i => i.Item($"{domain}:{SpurGearItemDefinitions.Code}").Quantity(1)
          )
          .Ingredient("H", Hammer)
          // `we` is the authored (unrotated) orientation and the creative default.
          .OutputBlock($"{domain}:forming-rollingmill-we", 1)
      );
}
