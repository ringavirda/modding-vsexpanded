using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Grid recipes for the cast pipe tier and the fittings (valve, pressure valve, passthrough, outlet),
/// carried by a stand-alone provider because recipes have no natural block or item class. A fitting is
/// a reworked plain plated pipe, so each recipe takes an <c>iiex:pipe-plated-straight-*</c> segment as
/// its base. The valve and pressure-valve recipes are authored twice, once for vanilla rusty gears and
/// once for the craftable ones, so either gear crafts them; the outputs have no metal variant.
/// </summary>
public class CastPipeRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "pipes-cast")
        // --- passthrough / outlet: a pipe segment wrapped in brick ---
        .Grid(r =>
          r.Name("Pipe Passthrough (Straight)")
            .Pattern("BHB,BPB,B_B")
            .Size(3, 3)
            .Ingredient("B", Brick(2))
            .Ingredient("P", StraightBlock(1))
            .Ingredient("H", Hammer)
            .OutputBlock("iiex:pipe-cast-passthrough-{brick}-ns")
        )
        .Grid(r =>
          r.Name("Pipe Passthrough (Bend)")
            .Pattern("BPB,PHB,B_B")
            .Size(3, 3)
            .Ingredient("B", Brick(2))
            .Ingredient("P", StraightBlock(1))
            .Ingredient("H", Hammer)
            .OutputBlock("iiex:pipe-cast-passthroughbend-{brick}-nw")
        )
        .Grid(r =>
          r.Name("Pipe Outlet")
            .Pattern("BHB,BNB,BPB")
            .Size(3, 3)
            .Ingredient("B", Brick(2))
            .Ingredient("P", StraightBlock(1))
            .Ingredient("H", Hammer)
            .Ingredient("N", Nails(2))
            .OutputBlock("iiex:pipe-outlet-{brick}-n")
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
            .OutputBlock("iiex:pipe-cast-valve-sn")
        )
        .Grid(r =>
          r.Name("Piping (Pressure Valve)")
            .Pattern("_H_,LPL,G_G")
            .Size(3, 3)
            .Ingredient("P", ValvePipe(1))
            .Ingredient("H", Hammer)
            .Ingredient("L", Plate(1))
            .Ingredient("G", Gear("game:gear-rusty", 2))
            .OutputBlock("iiex:pipe-cast-pressurevalve-sn")
        )
        .Grid(r =>
          r.Name("Piping (Valve)")
            .Pattern("_H_,GPL,_L_")
            .Size(3, 3)
            .Ingredient("P", ValvePipe(1))
            .Ingredient("H", Hammer)
            .Ingredient("L", Plate(1))
            .Ingredient("G", Gear("iiex:gear-*", 2))
            .OutputBlock("iiex:pipe-cast-valve-sn")
        )
        .Grid(r =>
          r.Name("Piping (Pressure Valve)")
            .Pattern("_H_,LPL,G_G")
            .Size(3, 3)
            .Ingredient("P", ValvePipe(1))
            .Ingredient("H", Hammer)
            .Ingredient("L", Plate(1))
            .Ingredient("G", Gear("iiex:gear-*", 2))
            .OutputBlock("iiex:pipe-cast-pressurevalve-sn")
        ),
    ];

  // Fitting-specific ingredient factories; they stay local because they encode block codes or a
  // per-recipe wildcard. The shared vanilla ones (Plate/Nails/Hammer/Gear) come from ExIngredients via
  // `using static`.
  private static System.Func<IngredientBuilder, IngredientBuilder> Brick(
    int qty
  ) =>
    i =>
      i.Item("game:burnedbrick-*")
        .Named(
          "brick",
          "fire",
          "black",
          "brown",
          "cream",
          "gray",
          "orange",
          "red",
          "tan"
        )
        .Quantity(qty);

  // The fittings are worked from a plain plated pipe segment, one tier down.
  private static System.Func<
    IngredientBuilder,
    IngredientBuilder
  > StraightBlock(int qty) =>
    i => i.Block("iiex:pipe-plated-straight-*").Quantity(qty);

  private static System.Func<IngredientBuilder, IngredientBuilder> ValvePipe(
    int qty
  ) => i => i.Block("iiex:pipe-plated-straight-ns").Quantity(qty);
}
