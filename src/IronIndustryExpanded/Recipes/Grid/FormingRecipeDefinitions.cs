using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronIndustryExpanded.Items;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the forming shop: the mill stand, the crop shear, the two fastener benches
/// and their tooling. Roll sets are not here - they are lathe-turned from cast blanks under the machining
/// line's ruling, so they wait on the lathe.
/// <para>
/// Every recipe in this file shares one pattern space, and two recipes sharing a pattern with overlapping
/// ingredients collide with the loser silently never resolving. That is why the benches and their dies are
/// authored here beside the shear rather than in a file of their own: a clash is a property of the file.
/// </para>
/// </summary>
public class FormingRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      RollingMill(domain),
      Shear(domain),
      ShearBlades(domain),
      NailCutter(domain),
      Riveter(domain),
      Dies(domain),
    ];

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
  /// The nail cutter: the lightest bench on the line, and deliberately so. Rivets out-yield nails two to
  /// one, so once a shop can rivet the nail route survives on build cost alone - which means this recipe
  /// has to be the cheap one or the machine has no reason to exist. One plate against the shear's two, and
  /// no journalled rolls to pay for.
  /// </summary>
  private static ExRecipeDef NailCutter(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "nailcutter")
      .Grid(r =>
        r.Name("Nail Cutter")
          .Pattern("_R_,PGP,_H_")
          .Size(3, 3)
          .Ingredient("P", i => i.Item($"{domain}:castplate-heavy").Quantity(1))
          .Ingredient("R", Rod(1))
          .Ingredient(
            "G",
            i => i.Item($"{domain}:{SpurGearItemDefinitions.Code}").Quantity(1)
          )
          .Ingredient("H", Hammer)
          // `ns` is the authored (unrotated) orientation and the creative default.
          .OutputBlock($"{domain}:forming-nailcutter-ns", 1)
      );

  /// <summary>
  /// The riveter: a press rather than a cutter, so it carries the heavier gear train its drawing shows and
  /// costs what the shear does. That is the other half of the fastener trade - the rivet route yields more
  /// per unit of iron and pays for it in machines.
  /// </summary>
  private static ExRecipeDef Riveter(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "riveter")
      .Grid(r =>
        r.Name("Riveter")
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
          .OutputBlock($"{domain}:forming-riveter-ns", 1)
      );

  /// <summary>
  /// The dies both benches work with, forged from plate the way the shear's blades are. Two recipes on one
  /// pattern would collide, so they differ in the pattern itself rather than in their ingredients: the nail
  /// die is cut across, the rivet die is sunk.
  /// </summary>
  private static ExRecipeDef Dies(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "die")
      .Grid(r =>
        r.Name("Nail Die")
          .Pattern("PP,_H")
          .Size(2, 2)
          .Ingredient("P", i => i.Item("game:metalplate-iron").Quantity(1))
          .Ingredient("H", Hammer)
          .OutputItem($"{domain}:die-nail", 1)
      )
      .Grid(r =>
        r.Name("Rivet Die")
          .Pattern("P_,PH")
          .Size(2, 2)
          .Ingredient("P", i => i.Item("game:metalplate-iron").Quantity(1))
          .Ingredient("H", Hammer)
          .OutputItem($"{domain}:die-rivet", 1)
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
