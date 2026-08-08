using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the iron tier's furnaces and their fittings: the blast-furnace core, the two
/// taps, the tuyere, the tall hopper and the twin-tub blower, plus the cupola core. Refractory brick runs
/// through all of them - the cores accept any tier and take it on, the fittings are fixed to tier 3 - so
/// both ingredient forms are defined once at the bottom of the file.
/// </summary>
public class FurnaceRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [BlastFurnace(domain), Cupola(domain)];

  private static ExRecipeDef BlastFurnace(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "blastfurnace")
      .Grid(r =>
        // Iron bars set in refractory brick, the hearth grate the burden column stands on. Built from any
        // refractory tier; the core takes on that tier's brick ({tier} captured off the brick).
        r.Name("Blast Furnace Core")
          .Pattern("BRP,BN_,BRP")
          .Size(3, 3)
          .Ingredient("B", RefractoryTiered(4))
          .Ingredient("R", Rod(2))
          .Ingredient("N", Nails(4))
          .Ingredient("P", Plate(2))
          .OutputBlock("iwex:furnace-blastcore-{tier}-n")
      )
      // The two tap-holes are two blocks and so two recipes. The iron notch runs a full canal and takes an
      // iron plate to line the channel; the cinder notch is a shallow slot with a spout and needs none.
      // The patterns must differ - here in the plate's cell, `P` against `_` - because two grid recipes
      // sharing a pattern and overlapping ingredients collide and the loser never resolves.
      .Grid(r =>
        r.Name("Iron Tap")
          .Pattern("BPH,BFC,BBB")
          .Size(3, 3)
          .Ingredient("B", Refractory(2))
          .Ingredient("F", FireClay(12))
          .Ingredient("P", Plate(1))
          .Ingredient("H", Hammer)
          .Ingredient("C", Chisel)
          .OutputBlock("iwex:furnace-irontap-s")
      )
      .Grid(r =>
        r.Name("Slag Tap")
          .Pattern("B_H,BFC,BBB")
          .Size(3, 3)
          .Ingredient("B", Refractory(2))
          .Ingredient("F", FireClay(12))
          .Ingredient("H", Hammer)
          .Ingredient("C", Chisel)
          .OutputBlock("iwex:furnace-slagtap-s")
      )
      .Grid(r =>
        r.Name("Tuyere")
          .Pattern("BHB,BCB,BPB")
          .Size(3, 3)
          .Ingredient("B", Refractory(2))
          // iwex owns the base pipe block, so the ingredient is its plated segment. lpex registers no
          // straight pipe at all (PipeMigration remaps those codes to iwex), so `lpex:pipe-straight*`
          // would match nothing and leave the furnace uncraftable.
          .Ingredient("P", i => i.Block("iwex:pipe-straight*"))
          .Ingredient("H", Hammer)
          .Ingredient("C", Chisel)
          .OutputBlock("iwex:furnace-tuyere-s")
      )
      .Grid(r =>
        r.Name("Tall Hopper")
          .Pattern("_H_,PSP,SPS")
          .Size(3, 3)
          .Ingredient("P", Plate(1))
          .Ingredient("S", Nails(1))
          .Ingredient("H", Hammer)
          .OutputBlock("iwex:hopper-tall-n")
      )
      // The twin-tub blower, the iron tier's only air source. Two leather-topped wooden tubs on a nailed
      // frame, driven by a plain vanilla wood axle (BEBehaviorMPFillerPort), so the recipe needs nothing
      // from iwex itself and the tier can be started from vanilla materials.
      .Grid(r =>
        r.Name("Twin Tub Blower")
          .Pattern("LPL,PNP,_H_")
          .Size(3, 3)
          // `game:leather-normal-plain`, the spelling vanilla's own armour and jerkin recipes use. Leather
          // is fully variant-grouped (type x colour) with `allowedVariants: ["leather-normal-*",
          // "leather-sturdy-plain"]`, so no concrete `game:leather` item is registered in 1.20, 1.21 or
          // 1.22. A code without `*` resolves through a hard GetItem lookup, so a bare `leather` fails
          // outright and the blower loses its recipe.
          .Ingredient("L", i => i.Item("game:leather-normal-plain").Quantity(2))
          .Ingredient("P", i => i.Item("game:plank-*").Quantity(1))
          .Ingredient("N", Nails(1))
          .Ingredient("H", Hammer)
          .OutputBlock("iwex:furnace-twintubblower-n")
      );

  // The cupola core: refractory brick bound with iron rods over a fire-clay core. Its pattern differs from
  // the blast-furnace core (full 3x3, fire clay at the centre) so the two furnace anchors never collide.
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
          .OutputBlock("iwex:furnace-cupolacore-{tier}-n")
      );

  // iwex's tier-3 refractory brick, used by the fittings that will not take a lower grade.
  private static Func<IngredientBuilder, IngredientBuilder> Refractory(
    int qty
  ) => i => i.Item("game:refractorybrick-fired-tier3").Quantity(qty);

  // The furnace cores accept any refractory tier and take on that tier's brick: the tier is captured off
  // the fired-brick wildcard as {tier} so the crafted core resolves to the matching variant. The tap and
  // tuyere fittings use the fixed tier-3 Refractory helper above.
  private static Func<IngredientBuilder, IngredientBuilder> RefractoryTiered(
    int qty
  ) =>
    i =>
      i.Item("game:refractorybrick-fired-*")
        .Named("tier", "tier1", "tier2", "tier3")
        .Quantity(qty);
}
