using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the iron tier's furnaces and their fittings: the blast-furnace core, the two
/// taps, the tuyere, the tall hopper and the twin-tub blower, plus the cupola core. Refractory brick runs
/// through all of them - the cores accept any tier and take it on, the fittings are fixed to tier 3 - so
/// both ingredient forms are defined once at the bottom of the file.
/// </summary>
public class FurnaceRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [BlastFurnace(domain), Cupola(domain), Reverberatory(domain)];

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
          .OutputBlock("iiex:furnace-blastcore-{tier}-n")
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
          .OutputBlock("iiex:furnace-irontap-s")
      )
      .Grid(r =>
        r.Name("Slag Tap")
          .Pattern("B_H,BFC,BBB")
          .Size(3, 3)
          .Ingredient("B", Refractory(2))
          .Ingredient("F", FireClay(12))
          .Ingredient("H", Hammer)
          .Ingredient("C", Chisel)
          .OutputBlock("iiex:furnace-slagtap-s")
      )
      .Grid(r =>
        r.Name("Tuyere")
          .Pattern("BHB,BCB,BPB")
          .Size(3, 3)
          .Ingredient("B", Refractory(2))
          // iiex owns the base pipe block, so the ingredient is its plated segment. iiex registers no
          // straight pipe at all (PipeMigration remaps those codes to iiex), so `iiex:pipe-cast-straight*`
          // would match nothing and leave the furnace uncraftable.
          .Ingredient("P", i => i.Block("iiex:pipe-plated-straight*"))
          .Ingredient("H", Hammer)
          .Ingredient("C", Chisel)
          .OutputBlock("iiex:furnace-tuyere-s")
      )
      .Grid(r =>
        r.Name("Tall Hopper")
          .Pattern("_H_,PSP,SPS")
          .Size(3, 3)
          .Ingredient("P", Plate(1))
          .Ingredient("S", Nails(1))
          .Ingredient("H", Hammer)
          .OutputBlock("iiex:hopper-tall-n")
      )
      // The twin-tub blower, the iron tier's only air source. Two leather-topped wooden tubs on a nailed
      // frame, driven by a plain vanilla wood axle (BEBehaviorMPFillerPort), so the recipe needs nothing
      // from iiex itself and the tier can be started from vanilla materials.
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
          .OutputBlock("iiex:furnace-twintubblower-n")
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
          .OutputBlock("iiex:furnace-cupolacore-{tier}-n")
      );

  /// <summary>
  /// The whole reverberatory chassis in one place: both cores, both hearths, both doors, the chimney cap
  /// and the firebox they share. Together rather than split between the puddling and reheat units because
  /// grid-pattern collision is a property of the file, not of a recipe - two recipes sharing a pattern
  /// with overlapping ingredients collide and the loser silently never resolves, so a clash authored a
  /// unit apart would surface only after the first had blessed its golden with nothing red in between.
  /// </summary>
  /// <remarks>
  /// The puddling and reheat cores are the same chassis one row apart, and their patterns are mirrors of
  /// each other for that reason. Nothing here is right-click construction: the layout already makes the
  /// player set 76 bricks and 10 slabs by hand off a projection, so the brick-by-brick fiction is
  /// delivered by the drawing. The grid makes only the iron fittings and the anchor core - the shop-made
  /// parts a founder would have bought in.
  /// </remarks>
  private static ExRecipeDef Reverberatory(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "reverberatory")
      // Brick shell round a fire-clay core bound with rods, on a plate bottom. Distinct from both shaft
      // cores: the blast furnace is BRP/BN_/BRP and the cupola BRB/PCP/BRB.
      .Grid(r =>
        r.Name("Puddling Furnace Core")
          .Pattern("BBB,RCR,BPB")
          .Size(3, 3)
          .Ingredient("B", RefractoryTiered(4))
          .Ingredient("R", Rod(2))
          .Ingredient("C", FireClay(8))
          .Ingredient("P", Plate(1))
          .OutputBlock("iiex:furnace-puddlingcore-{tier}-n")
      )
      // The reheat furnace's core: the same chassis, its plate course over the fire rather than under it.
      .Grid(r =>
        r.Name("Reheat Furnace Core")
          .Pattern("BPB,RCR,BBB")
          .Size(3, 3)
          .Ingredient("B", RefractoryTiered(4))
          .Ingredient("R", Rod(2))
          .Ingredient("C", FireClay(8))
          .Ingredient("P", Plate(1))
          .OutputBlock("iiex:furnace-heatingcore-{tier}-n")
      )
      // The bottom plate a puddling hearth is: three heavy cast plates nailed into a bed, three cells
      // wide. This is the block U1's casting route exists to supply.
      .Grid(r =>
        r.Name("Puddling Hearth")
          .Pattern("_H_,VVV,N_N")
          .Size(3, 3)
          .Ingredient("V", HeavyCastPlate(1))
          .Ingredient("N", Nails(2))
          .Ingredient("H", Hammer)
          .OutputBlock("iiex:furnace-puddlinghearth-n")
      )
      // The reheat hearth is the same bed two cells deep, so it costs twice the plate.
      .Grid(r =>
        r.Name("Reheat Hearth")
          .Pattern("_H_,VVV,VVV")
          .Size(3, 3)
          .Ingredient("V", HeavyCastPlate(1))
          .Ingredient("H", Hammer)
          .OutputBlock("iiex:furnace-heatinghearth-n")
      )
      // The puddling door carries a small working door in its main leaf, which is the extra plate.
      .Grid(r =>
        r.Name("Puddling Charge Door")
          .Pattern("_H_,PSP,BPB")
          .Size(3, 3)
          .Ingredient("B", Refractory(2))
          .Ingredient("P", Plate(1))
          .Ingredient("S", Nails(2))
          .Ingredient("H", Hammer)
          .OutputBlock("iiex:furnace-puddlingchargedoor-s")
      )
      // The plain door: one leaf, no working hatch. Shared by the reheat furnace and the coke oven, which
      // is why it lands here rather than in either machine's own unit.
      .Grid(r =>
        r.Name("Charge Door")
          .Pattern("_H_,PSP,BBB")
          .Size(3, 3)
          .Ingredient("B", Refractory(2))
          .Ingredient("P", Plate(1))
          .Ingredient("S", Nails(2))
          .Ingredient("H", Hammer)
          .OutputBlock("iiex:furnace-chargedoor-s")
      )
      // The damper: a plate on a control rod in a brick housing. The furnace's only air control, so it is
      // the one fitting a player cannot skip and still have a working machine.
      .Grid(r =>
        r.Name("Chimney Cap")
          .Pattern("_R_,BPB,_H_")
          .Size(3, 3)
          .Ingredient("B", Refractory(2))
          .Ingredient("R", Rod(1))
          .Ingredient("P", Plate(1))
          .Ingredient("H", Hammer)
          .OutputBlock("iiex:furnace-puddlingchimneycap-n")
      )
      // The fuel bed both reverberatory furnaces stand on: firebars set in brick. Required by both
      // layouts and craftable by neither of them until now, which is why neither machine could be built.
      .Grid(r =>
        r.Name("Firebox")
          .Pattern("BBB,RRR,BBB")
          .Size(3, 3)
          .Ingredient("B", RefractoryTiered(2))
          .Ingredient("R", Rod(1))
          .OutputBlock("iiex:furnace-firebox-{tier}-n")
      );

  // The heavy cast plate a hearth's bottom is made of - U1's casting route, and the reason that route is
  // this unit's entry condition.
  private static Func<IngredientBuilder, IngredientBuilder> HeavyCastPlate(
    int qty
  ) => i => i.Item("iiex:castplate-heavy").Quantity(qty);

  // iiex's tier-3 refractory brick, used by the fittings that will not take a lower grade.
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
