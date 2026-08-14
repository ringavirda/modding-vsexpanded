using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace SteelmakingExpanded.Recipes.Grid;

/// <summary>
/// Grid recipe for smex's engine sub-machine, the air blower. It crafts from either vanilla rusty gears
/// or iiex craftable gears, so the recipe is authored once and emitted once per gear code.
/// </summary>
public class EngineRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "airblower")
        .Grid(AirBlower("game:gear-rusty"))
        .Grid(AirBlower("iiex:gear-*")),
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
