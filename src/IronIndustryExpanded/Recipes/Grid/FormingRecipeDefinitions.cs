using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronIndustryExpanded.Items;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the forming shop, currently the rolling mill stand. Roll sets are not here:
/// they are cast rather than fabricated, so their recipes belong with the casting patterns.
/// <para>
/// The mill is buildable before it is useful - no stock item resolves a work piece and no product comes off
/// the rolls yet. See docs/internal/plans/iiex-bringup.md, Stage 4.
/// </para>
/// </summary>
public class FormingRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [RollingMill(domain), Shear(domain), ShearBlades(domain)];

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

  /// <summary>
  /// The crop shear: the same cast bed and gear coupling as the mill, plus the plate the blade nest is
  /// seated in. It costs one plate less than the mill because a guillotine has one moving mass where a
  /// stand has two journalled rolls.
  /// </summary>
  private static ExRecipeDef Shear(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "shear")
      .Grid(r =>
        r.Name("Crop Shear")
          .Pattern("PRP,PGP,_H_")
          .Size(3, 3)
          .Ingredient("P", i => i.Item($"{domain}:castplate-heavy").Quantity(1))
          .Ingredient("R", Rod(2))
          .Ingredient(
            "G",
            i => i.Item($"{domain}:{SpurGearItemDefinitions.Code}").Quantity(1)
          )
          .Ingredient("H", Hammer)
          // `ns` is the authored (unrotated) orientation and the creative default.
          .OutputBlock($"{domain}:forming-shear-ns", 1)
      );

  /// <summary>
  /// A blade set, forged from plate of the metal that sets its hardness. The metal is captured off the
  /// plate so one recipe covers the whole temper ladder, which is what keeps the tier a vanilla mechanic
  /// rather than a table of ours.
  /// </summary>
  private static ExRecipeDef ShearBlades(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "shearblade")
      .Grid(r =>
        r.Name("Shear Blades")
          .Pattern("PP,PP,_H")
          .Size(2, 3)
          .Ingredient(
            "P",
            i =>
              i.Item("game:metalplate-*")
                .Named("metal", "iron", "steel")
                .Quantity(1)
          )
          .Ingredient("H", Hammer)
          .OutputItem($"{domain}:shearblade-{{metal}}", 1)
      );
}
