using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace SteelmakingExpanded.Recipes;

/// <summary>
/// Code-first grid recipes for smex (migrated from recipes/grid/*.json): the air blower, the Bessemer converter
/// parts (control/transmission/gas-intake), the cowper-stove intake + heat sink, and the smoke-stack intake.
/// Shared crafting ingredients (metal plate/nails/rod, gears, a pipe segment, tiered refractory brick) are
/// defined once as reusable factories. The gear-driven recipes (air blower, converter transmission) list both
/// vanilla rusty gears and ppex's craftable gears, exactly as the source did.
/// </summary>
public class SmexGridRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      AirBlower(domain),
      BessemerConverter(domain),
      CowperStove(domain),
      SmokeStack(domain),
      HotBlastFurnace(domain),
    ];

  // The hot-blast furnace parts moved here from iwex with the blocks themselves: the hot core (tier-3
  // refractory only - the hot blast is the one furnace that will not take a lower brick) and the two
  // hoppers, whose blocks now live in the smex domain.
  private static ExRecipeDef HotBlastFurnace(string domain) =>
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
          .OutputBlock("smex:blastfurnacecore-north")
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
      .Grid(BellHopperRecipe("game:gear-rusty"))
      .Grid(BellHopperRecipe("ppex:gear-*"));

  private static Action<GridRecipeBuilder> BellHopperRecipe(string gear) =>
    r =>
      r.Name("Bell Hopper")
        .Pattern("GHG,PSP,SPS")
        .Size(3, 3)
        .Ingredient("P", Plate(1))
        .Ingredient("S", Nails(1))
        .Ingredient("H", Hammer)
        .Ingredient("G", Gear(gear, 4))
        .OutputBlock("smex:hopperbell");

  private static ExRecipeDef AirBlower(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "airblower")
      .Grid(AirBlowerRecipe("game:gear-rusty"))
      .Grid(AirBlowerRecipe("ppex:gear-*"));

  private static Action<GridRecipeBuilder> AirBlowerRecipe(string gear) =>
    r =>
      r.Name("Air Blower")
        .Pattern("_H_,PGP,RPR")
        .Size(3, 3)
        .Ingredient("P", Plate(1))
        .Ingredient("R", Rod(2))
        .Ingredient("G", Gear(gear, 2))
        .Ingredient("H", Hammer)
        .OutputBlock("smex:engineairblower-north");

  // The two transmission variants (rusty/ppex gear) are interleaved with the non-gear control + gas intake, so
  // this file is authored in explicit source order rather than a gear loop.
  private static ExRecipeDef BessemerConverter(string domain) =>
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
          .OutputBlock("smex:convertercontrol-north")
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
          .OutputBlock("smex:converter-intake-north")
      )
      .Grid(Transmission("ppex:gear-*"));

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
        .OutputBlock("smex:convertertransmission-north");

  private static ExRecipeDef CowperStove(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "cowperstove")
      .Grid(r =>
        r.Name("Cowper Stove Intake")
          .Pattern("BHB,BPB,BNB")
          .Size(3, 3)
          .Ingredient("B", RefractoryTier(2))
          .Ingredient("N", Nails(2))
          .Ingredient("P", PipeStar(1))
          .Ingredient("H", Hammer)
          .OutputBlock("smex:cowperstove-intake-{tier}-south")
      )
      .Grid(r =>
        r.Name("Heat Sink")
          .Pattern("HP_,PNP,_P_")
          .Size(3, 3)
          .Ingredient("N", Nails(2))
          .Ingredient("H", Hammer)
          .Ingredient("P", PipeStar(1))
          .OutputBlock("smex:cowperstoveheatsink-north")
      );

  private static ExRecipeDef SmokeStack(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "smokestack")
      .GridObject(r =>
        r.Name("Smoke Stack Intake")
          .Pattern("BHB,_P_,BNB")
          .Size(3, 3)
          .Ingredient("B", RefractoryTier(4))
          .Ingredient("N", Nails(2))
          .Ingredient("H", Hammer)
          .Ingredient("P", PipeStar(1))
          .OutputBlock("smex:smokestack-intake-{tier}-n")
      );

  // smex-specific ingredient factories (the shared vanilla ones - Plate/Nails/Rod/Gear/Hammer - come from
  // ExIngredients via `using static`). These stay local: the ppex trailing-star pipe wildcard + the tiered
  // refractory-brick capture.
  // The cowper/smokestack/gas-intake pipe ingredient uses the trailing-star wildcard (no dash) from the source.
  private static Func<IngredientBuilder, IngredientBuilder> PipeStar(int qty) =>
    i => i.Block("ppex:pipe-straight*").Quantity(qty);

  private static Func<IngredientBuilder, IngredientBuilder> RefractoryTier(
    int qty
  ) =>
    i =>
      i.Item("game:refractorybrick-fired-*")
        .Named("tier", "tier1", "tier2", "tier3")
        .Quantity(qty);

  // The hot blast furnace is tier-3-only (unlike the cold furnace, which takes any tier).
  private static Func<IngredientBuilder, IngredientBuilder> RefractoryTier3(
    int qty
  ) => i => i.Item("game:refractorybrick-fired-tier3").Quantity(qty);
}
