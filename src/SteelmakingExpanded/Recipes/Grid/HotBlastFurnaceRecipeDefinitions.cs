using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace SteelmakingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the hot blast furnace and its charging gear (migrated from
/// recipes/grid/hotblastfurnace.json). These moved here from iwex with the blocks themselves: the hot core
/// (tier-3 refractory only - the hot blast is the one furnace that will not take a lower brick) and the two
/// hoppers, whose blocks now live in the smex domain.
/// </summary>
public class HotBlastFurnaceRecipeDefinitions : IExRecipeDefProvider
{
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
            .OutputBlock("smex:blastfurnacecore-n")
        )
        .Grid(r =>
          r.Name("Reinforced Hopper")
            .Pattern("_H_,PSP,SPS")
            .Size(3, 3)
            .Ingredient("P", Plate(1))
            .Ingredient("S", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("smex:hopperreinforced")
        )
        .Grid(BellHopper("game:gear-rusty"))
        .Grid(BellHopper("lpex:gear-*")),
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
        .OutputBlock("smex:hopperbell");

  // The hot blast furnace is tier-3-only (unlike the cold furnace, which takes any tier).
  private static Func<IngredientBuilder, IngredientBuilder> RefractoryTier3(
    int qty
  ) => i => i.Item("game:refractorybrick-fired-tier3").Quantity(qty);
}
