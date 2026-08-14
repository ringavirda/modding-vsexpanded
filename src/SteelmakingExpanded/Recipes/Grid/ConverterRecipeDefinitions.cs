using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;
using static SteelmakingExpanded.Recipes.RecipeIngredients;

namespace SteelmakingExpanded.Recipes.Grid;

/// <summary>
/// Grid recipes for the Bessemer converter's three parts: the control, the transmission and the gas
/// intake. The two transmission variants (vanilla rusty gear, iiex craftable gear) are interleaved with
/// the control and gas intake, so the recipes are listed in explicit source order rather than emitted
/// from a gear loop.
/// </summary>
public class ConverterRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "bessemerconverter")
        .Grid(r =>
          r.Name("Converter Control")
            .Pattern("H_R,NPP,PPR")
            .Size(3, 3)
            .Ingredient("R", Rod(12))
            .Ingredient("P", Plate(4))
            .Ingredient("H", Hammer)
            .Ingredient("N", Nails(8))
            .OutputBlock("smex:convertercontrol-n")
        )
        .Grid(Transmission("game:gear-rusty"))
        .Grid(r =>
          r.Name("Converter Gas Intake")
            .Pattern("HP_,LPP,RN_")
            .Size(3, 3)
            .Ingredient("R", Rod(16))
            .Ingredient("P", Plate(4))
            .Ingredient("H", Hammer)
            .Ingredient("N", Nails(8))
            .Ingredient("L", PipeStar(1))
            .OutputBlock("smex:converter-intake-n")
        )
        .Grid(Transmission("iiex:gear-*")),
    ];

  private static Action<GridRecipeBuilder> Transmission(string gear) =>
    r =>
      r.Name("Converter Transmission")
        .Pattern("HPR,AGP,NPR")
        .Size(3, 3)
        .Ingredient("R", Rod(12))
        .Ingredient("P", Plate(4))
        .Ingredient("H", Hammer)
        .Ingredient("N", Nails(8))
        .Ingredient("G", Gear(gear, 16))
        .Ingredient("A", i => i.Block("game:woodenaxle-ud").Quantity(1))
        .OutputBlock("smex:convertertransmission-n");
}
