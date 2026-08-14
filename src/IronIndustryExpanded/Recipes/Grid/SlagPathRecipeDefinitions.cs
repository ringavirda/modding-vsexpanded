using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Grid recipes for the slag paths: the block, slab and stairs a furnace's waste slag is worked into
/// with gravel. Costs are deliberately low, since the family exists to give slag an outlet.
/// </summary>
public class SlagPathRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "slagpath")
        .Grid(r =>
          r.Name("Slag Path")
            .Pattern("L,S")
            .Size(1, 2)
            .Ingredient("L", Slag(4))
            .Ingredient("S", Gravel)
            .OutputBlock("iiex:slag-path-free", 2)
        )
        .Grid(r =>
          r.Name("Slag Path Slab")
            .Pattern("LSL")
            .Size(3, 1)
            .Ingredient("L", Slag(1))
            .Ingredient("S", Gravel)
            .OutputBlock("iiex:slag-pathslab-free", 1)
        )
        .Grid(r =>
          r.Name("Slag Path Stairs")
            .Pattern("LM,S_")
            .Size(2, 2)
            .Ingredient("L", Slag(3))
            .Ingredient("M", Slag(1))
            .Ingredient("S", Gravel)
            .OutputBlock("iiex:slag-pathstairs-up-north-free", 1)
        ),
      // Cast slag bricks laid up on vanilla's stonebrick patterns verbatim: block from eight bricks
      // around one mortar, slab from six, stairs from eight in an L, each yielding two. The mortar
      // can itself be slag, through smex's `mortarfromslag`.
      ExRecipeDef
        .Create(domain, "grid", "slagbricks")
        .Grid(r =>
          r.Name("Slag Bricks")
            .Pattern("BBB,BCB,BBB")
            .Size(3, 3)
            .Ingredient("B", Brick)
            .Ingredient("C", Mortar)
            .OutputBlock("iiex:slag-bricks", 2)
        )
        .Grid(r =>
          r.Name("Slag Brick Slab")
            .Pattern("BCB,BBB")
            .Size(3, 2)
            .Ingredient("B", Brick)
            .Ingredient("C", Mortar)
            .OutputBlock("iiex:slag-brickslab-down-free", 2)
        )
        .Grid(r =>
          r.Name("Slag Brick Stairs")
            .Pattern("_BB,BCB,BBB")
            .Size(3, 3)
            .Ingredient("B", Brick)
            .Ingredient("C", Mortar)
            .OutputBlock("iiex:slag-brickstairs-up-north-free", 2)
        ),
    ];

  private static IngredientBuilder Brick(IngredientBuilder i) =>
    i.Item("iiex:slagbrick").Quantity(1);

  private static IngredientBuilder Mortar(IngredientBuilder i) =>
    i.Item("game:mortar").Quantity(1);

  private static Func<IngredientBuilder, IngredientBuilder> Slag(int qty) =>
    i => i.Item("iiex:slag").Quantity(qty);

  private static IngredientBuilder Gravel(IngredientBuilder i) =>
    i.Block("game:gravel-*");
}
