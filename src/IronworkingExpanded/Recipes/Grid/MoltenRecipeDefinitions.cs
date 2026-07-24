using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;
using static IronworkingExpanded.Recipes.RecipeIngredients;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for iwex's molten-metal handling (migrated from
/// recipes/grid/{moltenbarrel,moltencanal}.json): the molten barrel, and the canal network's six shapes in
/// each of their three materials plus the tap.
/// <para>
/// Every canal shape is craftable from cobblestone (a 20-rock capture) OR a coloured running-brick (a
/// 7-brick capture) OR fire brick; all are hammered + chiselled over fire clay, so that F/H/K trio is folded
/// into one helper. The diagram-crafted equivalents live in <see cref="DiagramRecipeDefinitions"/> and share
/// this file's cobblestone capture through <see cref="RecipeIngredients"/>.
/// </para>
/// </summary>
public class MoltenRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [MoltenBarrel(domain), MoltenCanal(domain)];

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

  private static IngredientBuilder RunningBrick(IngredientBuilder i) =>
    i.Block("game:brickcourse-four-running-*")
      .Named("brick", Bricks)
      .Quantity(1);

  private static IngredientBuilder FireBrick(IngredientBuilder i) =>
    i.Block("game:claybricks-good-fire").Quantity(1);
}
