using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronworkingExpanded.Recipes;

/// <summary>
/// Code-first grid recipes for iwex (migrated from recipes/grid/*.json). A stand-alone provider carries the
/// mod's grid recipe files; the crafting ingredients (metal plate/nails/rod, fire clay, hammer/chisel tools,
/// refractory brick, gears) repeat across recipes, so they are defined once as reusable ingredient factories
/// and referenced by name - the DRY win over the hand-written JSON, with byte-identical output.
/// <para>
/// Covers blast-furnace fittings, the ore bunker, the molten barrel, the slag paths, and the molten canals.
/// </para>
/// </summary>
public class IwexGridRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      BlastFurnace(domain),
      Cupola(domain),
      Bunker(domain),
      MoltenBarrel(domain),
      SlagPath(domain),
      MoltenCanal(domain),
    ];

  private static ExRecipeDef BlastFurnace(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "blastfurnace")
      .Grid(r =>
        // Iron bars set in refractory brick - the hearth grate the burden column stands on. Same
        // pattern and cost the door had, so the auto-filled "normal" recipe-cost baseline is unmoved.
        r.Name("Blast Furnace Core")
          .Pattern("BRP,BN_,BRP")
          .Size(3, 3)
          .Ingredient("B", Refractory(4))
          .Ingredient("R", Rod(2))
          .Ingredient("N", Nails(4))
          .Ingredient("P", Plate(2))
          .OutputBlock("iwex:blastfurnacecore-north")
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
          .Ingredient("P", i => i.Block("ppex:pipe-straight*"))
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
          .Ingredient("B", Refractory(4))
          .Ingredient("R", Rod(2))
          .Ingredient("P", Plate(1))
          .Ingredient("C", FireClay(8))
          .OutputBlock("iwex:cupolafurnacecore-north")
      );

  // A one-recipe file authored as a lone object: 8 same-colour burned bricks -> an ore bunker of that brick.
  private static ExRecipeDef Bunker(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "bunker")
      .GridObject(r =>
        r.Pattern("B")
          .Size(1, 1)
          .Ingredient(
            "B",
            i =>
              i.Item("game:burnedbrick-*")
                .Named(
                  "brick",
                  "black",
                  "brown",
                  "cream",
                  "gray",
                  "orange",
                  "red",
                  "tan"
                )
                .Quantity(8)
          )
          .OutputBlock("iwex:bunker-{brick}-north", 1)
      );

  private static ExRecipeDef MoltenBarrel(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "moltenbarrel")
      .GridObject(r =>
        r.Name("Molten Barrel")
          .Pattern("PHP,PCP,PNP")
          .Size(3, 3)
          .Ingredient("P", Plate(1))
          .Ingredient("C", FireClay(4))
          .Ingredient("N", Nails(4))
          .Ingredient("H", Hammer)
          .OutputBlock("iwex:moltenbarrel")
      );

  private static ExRecipeDef SlagPath(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "slagpath")
      .Grid(r =>
        r.Name("Slag Path")
          .Pattern("L,S")
          .Size(1, 2)
          .Ingredient("L", Slag(4))
          .Ingredient("S", Gravel)
          .OutputBlock("iwex:slagpath-free", 2)
      )
      .Grid(r =>
        r.Name("Slag Path Slab")
          .Pattern("LSL")
          .Size(3, 1)
          .Ingredient("L", Slag(1))
          .Ingredient("S", Gravel)
          .OutputBlock("iwex:slagpathslab-free", 1)
      )
      .Grid(r =>
        r.Name("Slag Path Stairs")
          .Pattern("LM,S_")
          .Size(2, 2)
          .Ingredient("L", Slag(3))
          .Ingredient("M", Slag(1))
          .Ingredient("S", Gravel)
          .OutputBlock("iwex:slagpathstairs-up-north-free", 1)
      );

  // The molten canals: each shape (straight/bend/t/x/start/moldpedestal) is craftable from cobblestone (a
  // 20-rock capture) OR a coloured running-brick (a 7-brick capture) OR fire brick; plus a cobble-only tap.
  // All are hammered + chiselled over fire clay, so that F/H/K trio is folded into one helper.
  private static ExRecipeDef MoltenCanal(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "moltencanal")
      // cobblestone family
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Straight)")
              .Pattern("H_K,FCF")
              .Size(3, 2)
              .Ingredient("C", Cobble),
            2
          )
          .OutputBlock("iwex:moltencanal-straight-{rock}-ns", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Bend)")
              .Pattern("HFK,FC_")
              .Size(3, 2)
              .Ingredient("C", Cobble),
            2
          )
          .OutputBlock("iwex:moltencanal-bend-{rock}-nw", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (T-Junction)")
              .Pattern("HFK,FCF")
              .Size(3, 2)
              .Ingredient("C", Cobble),
            2
          )
          .OutputBlock("iwex:moltencanal-tjunction-{rock}-esw", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (X-Junction)")
              .Pattern("HFK,FCF,_F_")
              .Size(3, 3)
              .Ingredient("C", Cobble),
            2
          )
          .OutputBlock("iwex:moltencanal-xjunction-{rock}-nswe", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Start)")
              .Pattern("HCK,FFF")
              .Size(3, 2)
              .Ingredient("C", Cobble),
            2
          )
          .OutputBlock("iwex:moltencanal-start-{rock}-s", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Mold Pedestal)")
              .Pattern("HFK,CFC")
              .Size(3, 2)
              .Ingredient("C", Cobble),
            2
          )
          .OutputBlock("iwex:moltencanal-moldpedestal-{rock}-s", 1)
      )
      .Grid(r =>
        Fhk(r.Name("Molten Canal (Tap)").Pattern("HFK").Size(3, 1), 4)
          .OutputBlock("iwex:moltencanal-tap-s", 1)
      )
      // coloured running-brick + fire-brick pairs
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Straight, Colored Brick)")
              .Pattern("H_K,FBF")
              .Size(3, 2)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iwex:moltencanal-straight-{brick}-ns", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Straight, Fire Brick)")
              .Pattern("H_K,FBF")
              .Size(3, 2)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iwex:moltencanal-straight-fire-ns", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Bend, Colored Brick)")
              .Pattern("HFK,FB_")
              .Size(3, 2)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iwex:moltencanal-bend-{brick}-nw", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Bend, Fire Brick)")
              .Pattern("HFK,FB_")
              .Size(3, 2)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iwex:moltencanal-bend-fire-nw", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (T-Junction, Colored Brick)")
              .Pattern("HFK,FBF")
              .Size(3, 2)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iwex:moltencanal-tjunction-{brick}-esw", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (T-Junction, Fire Brick)")
              .Pattern("HFK,FBF")
              .Size(3, 2)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iwex:moltencanal-tjunction-fire-esw", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (X-Junction, Colored Brick)")
              .Pattern("HFK,FBF,_F_")
              .Size(3, 3)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iwex:moltencanal-xjunction-{brick}-nswe", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (X-Junction, Fire Brick)")
              .Pattern("HFK,FBF,_F_")
              .Size(3, 3)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iwex:moltencanal-xjunction-fire-nswe", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Start, Colored Brick)")
              .Pattern("HBK,FFF")
              .Size(3, 2)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iwex:moltencanal-start-{brick}-s", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Start, Fire Brick)")
              .Pattern("HBK,FFF")
              .Size(3, 2)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iwex:moltencanal-start-fire-s", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Mold Pedestal, Colored Brick)")
              .Pattern("HFK,BFB")
              .Size(3, 2)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iwex:moltencanal-moldpedestal-{brick}-s", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Molten Canal (Mold Pedestal, Fire Brick)")
              .Pattern("HFK,BFB")
              .Size(3, 2)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iwex:moltencanal-moldpedestal-fire-s", 1)
      );

  // The fire-clay + hammer + chisel trio every canal recipe shares (clay quantity varies: 4 for the tap, else 2).
  private static GridRecipeBuilder Fhk(GridRecipeBuilder r, int clayQty) =>
    r.Ingredient("F", FireClay(clayQty))
      .Ingredient("H", Hammer)
      .Ingredient("K", Chisel);

  private static readonly string[] Rocks =
  [
    "andesite",
    "chalk",
    "chert",
    "conglomerate",
    "limestone",
    "claystone",
    "granite",
    "sandstone",
    "shale",
    "basalt",
    "peridotite",
    "phyllite",
    "slate",
    "obsidian",
    "kimberlite",
    "bauxite",
    "suevite",
    "whitemarble",
    "redmarble",
    "greenmarble",
  ];

  private static readonly string[] Bricks =
  [
    "black",
    "brown",
    "cream",
    "gray",
    "orange",
    "red",
    "tan",
  ];

  private static IngredientBuilder Cobble(IngredientBuilder i) =>
    i.Block("game:cobblestone-*").Named("rock", Rocks);

  private static IngredientBuilder RunningBrick(IngredientBuilder i) =>
    i.Block("game:brickcourse-four-running-*")
      .Named("brick", Bricks)
      .Quantity(1);

  private static IngredientBuilder FireBrick(IngredientBuilder i) =>
    i.Block("game:claybricks-good-fire").Quantity(1);

  // iwex-specific ingredient factories (the shared vanilla ones - Plate/Nails/Rod/FireClay/Gear/Hammer/Chisel -
  // come from ExIngredients via `using static`). These stay local: iwex's tier-3 refractory brick, iwex:slag,
  // and vanilla gravel used only here (the coloured/fire brick + cobblestone captures are just above).
  private static Func<IngredientBuilder, IngredientBuilder> Refractory(
    int qty
  ) => i => i.Item("game:refractorybrick-fired-tier3").Quantity(qty);

  private static Func<IngredientBuilder, IngredientBuilder> Slag(int qty) =>
    i => i.Item("iwex:slag").Quantity(qty);

  private static IngredientBuilder Gravel(IngredientBuilder i) =>
    i.Block("game:gravel-*");
}
