using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace SteelIndustryExpanded.Recipes.Grid;

/// <summary>
/// Grid recipes for the hot blast furnace core and its charging gear, the reinforced hopper and the bell
/// hopper. All three blocks live in the siex domain.
/// </summary>
public class HotBlastFurnaceRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "hotblastfurnace")
        .Grid(r =>
          r.Name("Blast Furnace Core")
            .Pattern("BRP,BN_,BRP")
            .Size(3, 3)
            .Ingredient("B", RefractoryTier3(4))
            .Ingredient("R", Rod(2))
            .Ingredient("N", Nails(4))
            .Ingredient("P", Plate(2))
            .OutputBlock("siex:blastfurnacecore-n")
        )
        .Grid(r =>
          r.Name("Reinforced Hopper")
            .Pattern("_H_,PSP,SPS")
            .Size(3, 3)
            .Ingredient("P", Plate(1))
            .Ingredient("S", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("siex:hopperreinforced")
        )
        .Grid(BellHopper("game:gear-rusty"))
        .Grid(BellHopper("iiex:gear-*")),
    ];

  private static Action<GridRecipeBuilder> BellHopper(string gear) =>
    r =>
      r.Name("Bell Hopper")
        .Pattern("GHG,PSP,SPS")
        .Size(3, 3)
        .Ingredient("P", Plate(1))
        .Ingredient("S", Nails(1))
        .Ingredient("H", Hammer)
        .Ingredient("G", Gear(gear, 4))
        .OutputBlock("siex:hopperbell");

  // The hot blast furnace is tier-3-only (unlike the cold furnace, which takes any tier).
  private static Func<IngredientBuilder, IngredientBuilder> RefractoryTier3(
    int qty
  ) => i => i.Item("game:refractorybrick-fired-tier3").Quantity(qty);
}
