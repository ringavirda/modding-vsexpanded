using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace SteelmakingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipe for smex's engine sub-machine, the air blower (migrated from
/// recipes/grid/airblower.json) - the steam tier's answer to iwex's twin-tub bellows. Like every
/// gear-driven machine in the family it crafts from vanilla rusty gears OR lpex's craftable gears, so the
/// recipe is authored once and emitted for both codes in the source's order.
/// </summary>
public class EngineRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "airblower")
        .Grid(AirBlower("game:gear-rusty"))
        .Grid(AirBlower("lpex:gear-*")),
    ];

  private static Action<GridRecipeBuilder> AirBlower(string gear) =>
    r =>
      r.Name("Air Blower")
        .Pattern("_H_,PGP,RPR")
        .Size(3, 3)
        .Ingredient("P", Plate(1))
        .Ingredient("R", Rod(2))
        .Ingredient("G", Gear(gear, 2))
        .Ingredient("H", Hammer)
        .OutputBlock("smex:engineairblower-n");
}
