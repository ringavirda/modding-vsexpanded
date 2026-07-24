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
        ),
    ];

  private static Func<IngredientBuilder, IngredientBuilder> Slag(int qty) =>
    i => i.Item("iwex:slag").Quantity(qty);

  private static IngredientBuilder Gravel(IngredientBuilder i) =>
    i.Block("game:gravel-*");
}
