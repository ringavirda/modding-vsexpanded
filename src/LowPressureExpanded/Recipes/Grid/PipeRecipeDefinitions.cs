using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace LowPressureExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the lpex pipe fittings (valve, pressure valve, passthrough, outlet) -
/// migrated from recipes/grid/pipes.json. The plain pipe segments themselves moved to iwex (the bolted
/// tier); a fitting is a plain bolted pipe reworked, so each recipe takes an <c>iwex:pipe-straight-*</c>
/// segment as its base. Recipes have no natural block/item class, so a stand-alone provider carries them
/// (the pattern the tool-mold blocks and gear items use). The valve + pressure-valve recipes are authored
/// twice (once for vanilla rusty gears, once for lpex's craftable gears) exactly as the source did, so
/// either gear crafts them. The iron/steel material variant is gone, so the outputs are single-variant.
/// </summary>
public class PipeRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "pipes")
        // --- passthrough / outlet: a pipe segment wrapped in brick ---
        .Grid(r =>
          r.Name("Pipe Passthrough (Straight)")
            .Pattern("BHB,BPB,B_B")
            .Size(3, 3)
            .Ingredient("B", Brick(2))
            .Ingredient("P", StraightBlock(1))
            .Ingredient("H", Hammer)
            .OutputBlock("lpex:pipe-passthrough-{brick}-ns")
        )
        .Grid(r =>
          r.Name("Pipe Passthrough (Bend)")
            .Pattern("BPB,PHB,B_B")
            .Size(3, 3)
            .Ingredient("B", Brick(2))
            .Ingredient("P", StraightBlock(1))
            .Ingredient("H", Hammer)
            .OutputBlock("lpex:pipe-passthroughbend-{brick}-nw")
        )
        .Grid(r =>
          r.Name("Pipe Outlet")
            .Pattern("BHB,BNB,BPB")
            .Size(3, 3)
            .Ingredient("B", Brick(2))
            .Ingredient("P", StraightBlock(1))
            .Ingredient("H", Hammer)
            .Ingredient("N", Nails(2))
            .OutputBlock("lpex:pipe-outlet-{brick}-n")
        )
        // --- valves: a pipe segment + plate + gear; authored for both gear kinds ---
        .Grid(r =>
          r.Name("Piping (Valve)")
            .Pattern("_H_,GPL,_L_")
            .Size(3, 3)
            .Ingredient("P", ValvePipe(1))
            .Ingredient("H", Hammer)
            .Ingredient("L", Plate(1))
            .Ingredient("G", Gear("game:gear-rusty", 2))
            .OutputBlock("lpex:pipe-valve-sn")
        )
        .Grid(r =>
          r.Name("Piping (Pressure Valve)")
            .Pattern("_H_,LPL,G_G")
            .Size(3, 3)
            .Ingredient("P", ValvePipe(1))
            .Ingredient("H", Hammer)
            .Ingredient("L", Plate(1))
            .Ingredient("G", Gear("game:gear-rusty", 2))
            .OutputBlock("lpex:pipe-pressurevalve-sn")
        )
        .Grid(r =>
          r.Name("Piping (Valve)")
            .Pattern("_H_,GPL,_L_")
            .Size(3, 3)
            .Ingredient("P", ValvePipe(1))
            .Ingredient("H", Hammer)
            .Ingredient("L", Plate(1))
            .Ingredient("G", Gear("lpex:gear-*", 2))
            .OutputBlock("lpex:pipe-valve-sn")
        )
        .Grid(r =>
          r.Name("Piping (Pressure Valve)")
            .Pattern("_H_,LPL,G_G")
            .Size(3, 3)
            .Ingredient("P", ValvePipe(1))
            .Ingredient("H", Hammer)
            .Ingredient("L", Plate(1))
            .Ingredient("G", Gear("lpex:gear-*", 2))
            .OutputBlock("lpex:pipe-pressurevalve-sn")
        ),
    ];

  // lpex-specific ingredient factories (the shared vanilla ones - Plate/Nails/Hammer/Gear - come from
  // ExIngredients via `using static`). These stay local: they encode block codes or a per-recipe wildcard.
  private static System.Func<IngredientBuilder, IngredientBuilder> Brick(int qty) =>
    i =>
      i.Item("game:burnedbrick-*")
        .Named("brick", "fire", "black", "brown", "cream", "gray", "orange", "red", "tan")
        .Quantity(qty);

  // The fittings are worked from a plain bolted (iwex) pipe segment.
  private static System.Func<IngredientBuilder, IngredientBuilder> StraightBlock(int qty) =>
    i => i.Block("iwex:pipe-straight-*").Quantity(qty);

  private static System.Func<IngredientBuilder, IngredientBuilder> ValvePipe(int qty) =>
    i => i.Block("iwex:pipe-straight-ns").Quantity(qty);
}
