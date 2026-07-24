using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the iron tier's furnaces and their fittings (migrated from
/// recipes/grid/{blastfurnace,cupola}.json): the cold blast-furnace core, the molten metal tap, the tuyere
/// and the tall hopper, plus the cupola core. Refractory brick is the through-line - the cores accept any
/// tier and take on it, the fittings are fixed to tier 3 - so both captures are defined once at the bottom.
/// </summary>
public class FurnaceRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [BlastFurnace(domain), Cupola(domain)];

  private static ExRecipeDef BlastFurnace(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "blastfurnace")
      .Grid(r =>
        // Iron bars set in refractory brick - the hearth grate the burden column stands on. Built from
        // any refractory tier; the core takes on that tier's brick ({tier} captured off the brick).
        r.Name("Blast Furnace Core")
          .Pattern("BRP,BN_,BRP")
          .Size(3, 3)
          .Ingredient("B", RefractoryTiered(4))
          .Ingredient("R", Rod(2))
          .Ingredient("N", Nails(4))
          .Ingredient("P", Plate(2))
          .OutputBlock("iwex:blastfurnacecore-{tier}-north")
      )
      .Grid(r =>
        r.Name("Molten Metal Tap")
          .Pattern("BPH,BFC,BBB")
          .Size(3, 3)
          .Ingredient("B", Refractory(2))
          .Ingredient("F", FireClay(12))
          .Ingredient("P", Plate(1))
          .Ingredient("H", Hammer)
          .Ingredient("C", Chisel)
          .OutputBlock("iwex:moltenmetaltap-south")
      )
      .Grid(r =>
        r.Name("Tuyere")
          .Pattern("BHB,BCB,BPB")
          .Size(3, 3)
          .Ingredient("B", Refractory(2))
          .Ingredient("P", i => i.Block("lpex:pipe-straight*"))
          .Ingredient("H", Hammer)
          .Ingredient("C", Chisel)
          .OutputBlock("iwex:tuyere-tuyere-s")
      )
      .Grid(r =>
        r.Name("Tall Hopper")
          .Pattern("_H_,PSP,SPS")
          .Size(3, 3)
          .Ingredient("P", Plate(1))
          .Ingredient("S", Nails(1))
          .Ingredient("H", Hammer)
          .OutputBlock("iwex:hopper-tall")
      );

  // The cupola core: refractory brick bound with iron rods over a fire-clay core. A distinct pattern
  // from the blast-furnace core (full 3x3, fire clay at its heart) so the two furnace anchors never
  // collide, and cheaper than the blast furnace - the cupola is the smaller re-melter.
  private static ExRecipeDef Cupola(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "cupola")
      .GridObject(r =>
        r.Name("Cupola Furnace Core")
          .Pattern("BRB,PCP,BRB")
          .Size(3, 3)
          .Ingredient("B", RefractoryTiered(4))
          .Ingredient("R", Rod(2))
          .Ingredient("P", Plate(1))
          .Ingredient("C", FireClay(8))
          .OutputBlock("iwex:cupolafurnacecore-{tier}-north")
      );

  // iwex's tier-3 refractory brick, used by the fittings that will not take a lower grade.
  private static Func<IngredientBuilder, IngredientBuilder> Refractory(int qty) =>
    i => i.Item("game:refractorybrick-fired-tier3").Quantity(qty);

  // The furnace cores accept any refractory tier and take on that tier's brick: capture the tier off the
  // fired-brick wildcard as {tier} so the crafted core resolves to the matching variant. (The tap/tuyere
  // fittings keep the fixed tier-3 Refractory helper above.)
  private static Func<IngredientBuilder, IngredientBuilder> RefractoryTiered(
    int qty
  ) =>
    i =>
      i.Item("game:refractorybrick-fired-*")
        .Named("tier", "tier1", "tier2", "tier3")
        .Quantity(qty);
}
