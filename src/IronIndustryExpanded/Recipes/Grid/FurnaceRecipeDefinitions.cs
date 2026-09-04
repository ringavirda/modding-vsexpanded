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
    [
      BlastFurnace(domain),
      Cupola(domain),
      Reverberatory(domain),
      CokeOven(domain),
      Crucible(domain),
    ];

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
          .Ingredient(
            "S",
            RecipeIngredients.Fastener(
              RecipeIngredients.Fasteners(domain)[0],
              1
            )
          )
          .Ingredient("H", Hammer)
          .OutputBlock("iiex:hopper-tall-n")
      )
      // The same hopper riveted rather than nailed. Its seams are structural, so either fastener serves;
      // two recipes because nothing in the game expresses one ingredient as "either of these".
      .Grid(r =>
        r.Name("Tall Hopper")
          .Pattern("_H_,PSP,SPS")
          .Size(3, 3)
          .Ingredient("P", Plate(1))
          .Ingredient(
            "S",
            RecipeIngredients.Fastener(
              RecipeIngredients.Fasteners(domain)[1],
              1
            )
          )
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

  /// <summary>
  /// The coke oven's core, and the crown lid that seals its chambers.
  /// <para>
  /// Fire brick rather than refractory, and deliberately the cheapest core in the file: coke is the fuel
  /// half of every shaft charge and burns in both reverberatory fireboxes, so an oven priced against what
  /// it unlocks would gate the whole iron tier behind the tier it is meant to open. Vanilla agrees about
  /// the material - <c>claybricks</c> carries <c>cokeOvenViableByType: { "*-fire": true }</c>, so this is
  /// the game's own coke-oven masonry.
  /// </para>
  /// </summary>
  private static ExRecipeDef CokeOven(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "cokeoven")
      // A beehive of brick round a void, which is what the pattern draws. It shares no pattern with the
      // blast core ("BRP,BN_,BRP") or the cupola core ("BRB,PCP,BRB"); two recipes on one pattern whose
      // ingredients overlap leave the loser silently unresolvable.
      .Grid(r =>
        r.Name("Coke Oven Core")
          .Pattern("BBB,BHB,BBB")
          .Size(3, 3)
          .Ingredient("B", FireBrick(1))
          .Ingredient("H", Hammer)
          // The creative default variant, which is the letter and not the word.
          .OutputBlock("iiex:furnace-cokeovencore-n")
      )
      // The crown lid: the charge door's chassis laid flat over a charging hole. Refractory like the door
      // it shares a class with, not fire brick like the oven - it is a furnace part, and the draft
      // crucible furnace wants it too.
      .Grid(r =>
        r.Name("Charge Lid")
          .Pattern("_P_,BSB,_H_")
          .Size(3, 3)
          .Ingredient("B", Refractory(2))
          .Ingredient("P", Plate(1))
          .Ingredient("S", Nails(2))
          .Ingredient("H", Hammer)
          .OutputBlock("iiex:furnace-chargelid-s")
      );

  /// <summary>
  /// The crucible furnace: a deep refractory pot over an ash pit, and the hearth of firebars and clay
  /// stands the pots sit on.
  /// </summary>
  /// <remarks>
  /// Priced like the other refractory cores rather than like the coke oven, because nothing downstream
  /// waits on it: crucible steel is the end of the iron line, not a gate into it. The hearth is the
  /// firebox recipe with its top course in fire clay - the four stands the pots stand on - which is also
  /// what keeps the two off one pattern.
  /// </remarks>
  private static ExRecipeDef Crucible(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "cruciblefurnace")
      // A fire-clay lining set in refractory on a rod-bound base. It shares no pattern with the other
      // four cores - blast BRP/BN_/BRP, cupola BRB/PCP/BRB, puddling BBB/RCR/BPB, reheat BPB/RCR/BBB,
      // coke oven BBB/BHB/BBB - and two recipes on one pattern whose ingredients overlap leave the loser
      // silently unresolvable.
      .Grid(r =>
        r.Name("Crucible Furnace Core")
          .Pattern("BBB,BCB,BRB")
          .Size(3, 3)
          .Ingredient("B", RefractoryTiered(4))
          .Ingredient("C", FireClay(8))
          .Ingredient("R", Rod(2))
          .OutputBlock("iiex:furnace-cruciblecore-{tier}-n")
      )
      // The hearth: the firebox's own bars and brick with a course of fire clay over them, which is the
      // four stands the pots stand on. The clay course is what tells it apart from the plain firebox
      // (BBB/RRR/BBB) - a different item in the top row, so a given grid fills exactly one of the two.
      .Grid(r =>
        r.Name("Crucible Hearth")
          .Pattern("CCC,RRR,BBB")
          .Size(3, 3)
          .Ingredient("C", FireClay(2))
          .Ingredient("R", Rod(1))
          .Ingredient("B", RefractoryTiered(2))
          .OutputBlock("iiex:furnace-cruciblehearth-{tier}-n")
      );

  // Vanilla's fired fire-brick, the item behind `claybricks-good-fire`. Cheaper than refractory and the
  // only masonry the coke oven asks for.
  private static Func<IngredientBuilder, IngredientBuilder> FireBrick(
    int qty
  ) => i => i.Item("game:burnedbrick-fire").Quantity(qty);

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
