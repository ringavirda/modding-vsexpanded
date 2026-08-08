using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the slag paths (migrated from recipes/grid/slagpath.json) - the block,
/// slab and stairs a furnace's waste slag is worked into with gravel. The whole point of the family is
/// that slag has a use, so these are cheap by design.
/// </summary>
public class SlagPathRecipeDefinitions : IExRecipeDefProvider
{
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
            .OutputBlock("iwex:slag-path-free", 2)
        )
        .Grid(r =>
          r.Name("Slag Path Slab")
            .Pattern("LSL")
            .Size(3, 1)
            .Ingredient("L", Slag(1))
            .Ingredient("S", Gravel)
            .OutputBlock("iwex:slag-pathslab-free", 1)
        )
        .Grid(r =>
          r.Name("Slag Path Stairs")
            .Pattern("LM,S_")
            .Size(2, 2)
            .Ingredient("L", Slag(3))
            .Ingredient("M", Slag(1))
            .Ingredient("S", Gravel)
            .OutputBlock("iwex:slag-pathstairs-up-north-free", 1)
        ),
      // Laying up cast slag bricks: vanilla's stonebrick patterns verbatim - block from eight bricks around
      // one mortar, slab from six, stairs from eight in an L, each for two. Same shapes, same yields, so a
      // player who has ever built with bricks already knows all three. The mortar can itself be slag
      // (smex's `mortarfromslag`), which closes the line on itself.
      ExRecipeDef
        .Create(domain, "grid", "slagbricks")
        .Grid(r =>
          r.Name("Slag Bricks")
            .Pattern("BBB,BCB,BBB")
            .Size(3, 3)
            .Ingredient("B", Brick)
            .Ingredient("C", Mortar)
            .OutputBlock("iwex:slag-bricks", 2)
        )
        .Grid(r =>
          r.Name("Slag Brick Slab")
            .Pattern("BCB,BBB")
            .Size(3, 2)
            .Ingredient("B", Brick)
            .Ingredient("C", Mortar)
            .OutputBlock("iwex:slag-brickslab-down-free", 2)
        )
        .Grid(r =>
          r.Name("Slag Brick Stairs")
            .Pattern("_BB,BCB,BBB")
            .Size(3, 3)
            .Ingredient("B", Brick)
            .Ingredient("C", Mortar)
            .OutputBlock("iwex:slag-brickstairs-up-north-free", 2)
        ),
    ];

  private static IngredientBuilder Brick(IngredientBuilder i) =>
    i.Item("iwex:slagbrick").Quantity(1);

  private static IngredientBuilder Mortar(IngredientBuilder i) =>
    i.Item("game:mortar").Quantity(1);

  private static Func<IngredientBuilder, IngredientBuilder> Slag(int qty) =>
    i => i.Item("iwex:slag").Quantity(qty);

  private static IngredientBuilder Gravel(IngredientBuilder i) =>
    i.Block("game:gravel-*");
}
