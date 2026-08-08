using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the iwex (plated) pipe tier: the four plain straight/bend/T/X segments,
/// hammered together from a metal plate and nails. iwex owns the base pipe block, so its plated
/// segments are craftable on their own; the cast (lpex) and rolled (hpex) tiers author their own
/// segment recipes (casting / rolling) and the fittings live in lpex. Migrated from the old
/// <c>lpex</c> pipe recipes, retargeted to <c>iwex:pipe-*</c> with the dropped iron/steel variant.
/// </summary>
public class PipeRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "pipes")
        .Grid(r =>
          r.Name("Piping (Straight)")
            .Pattern("HPN")
            .Size(3, 1)
            .Ingredient("P", Plate(1))
            .Ingredient("N", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("iwex:pipe-straight-ns", 2)
        )
        .Grid(r =>
          r.Name("Piping (Bend)")
            .Pattern("_H,NP,_N")
            .Size(2, 3)
            .Ingredient("P", Plate(1))
            .Ingredient("N", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("iwex:pipe-bend-nw")
        )
        .Grid(r =>
          r.Name("Piping (TJunction)")
            .Pattern("_H_,NPN,_N_")
            .Size(3, 3)
            .Ingredient("P", Plate(1))
            .Ingredient("N", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("iwex:pipe-tjunction-uns")
        )
        .Grid(r =>
          r.Name("Piping (XJunction)")
            .Pattern("HN_,NPN,_N_")
            .Size(3, 3)
            .Ingredient("P", Plate(1))
            .Ingredient("N", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("iwex:pipe-xjunction-nswe")
        ),
    ];
}
