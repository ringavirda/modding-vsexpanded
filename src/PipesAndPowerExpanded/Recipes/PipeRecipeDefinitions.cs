using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace PipesAndPowerExpanded.Recipes;

/// <summary>
/// Code-first grid recipes for the ppex piping family (migrated from recipes/grid/pipes.json) - the FIRST
/// code-first RECIPE file. Recipes have no natural block/item class, so a stand-alone provider carries them
/// (the pattern the tool-mold blocks and gear items use). The file's crafting ingredients repeat heavily
/// (metal plate, nails, hammer, brick, a pipe segment, a gear), so they are defined once as reusable
/// ingredient factories and referenced by name - the DRY win code-first buys over the hand-written JSON, with
/// byte-identical output. The valve + pressure-valve recipes are authored twice (once for vanilla rusty gears,
/// once for ppex's craftable gears) exactly as the source did, so either gear crafts them.
/// </summary>
public class PipeRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "pipes")
        // --- straight / bend / junctions: plate + nails, hammered ---
        .Grid(r =>
          r.Name("Piping (Straight)")
            .Pattern("HPN")
            .Size(3, 1)
            .Ingredient("P", Plate(1))
            .Ingredient("N", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("ppex:pipe-straight-ns-{metal}", 2)
        )
        .Grid(r =>
          r.Name("Piping (Bend)")
            .Pattern("_H,NP,_N")
            .Size(2, 3)
            .Ingredient("P", Plate(1))
            .Ingredient("N", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("ppex:pipe-bend-nw-{metal}")
        )
        .Grid(r =>
          r.Name("Piping (TJunction)")
            .Pattern("_H_,NPN,_N_")
            .Size(3, 3)
            .Ingredient("P", Plate(1))
            .Ingredient("N", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("ppex:pipe-tjunction-uns-{metal}")
        )
        .Grid(r =>
          r.Name("Piping (XJunction)")
            .Pattern("HN_,NPN,_N_")
            .Size(3, 3)
            .Ingredient("P", Plate(1))
            .Ingredient("N", Nails(1))
            .Ingredient("H", Hammer)
            .OutputBlock("ppex:pipe-xjunction-nswe-{metal}")
        )
        // --- passthrough / outlet: a pipe segment wrapped in brick ---
        .Grid(r =>
          r.Name("Pipe Passthrough (Straight)")
            .Pattern("BHB,BPB,B_B")
            .Size(3, 3)
            .Ingredient("B", Brick(2))
            .Ingredient("P", StraightBlock(1))
            .Ingredient("H", Hammer)
            .OutputBlock("ppex:pipe-passthrough-{brick}-ns")
        )
        .Grid(r =>
          r.Name("Pipe Passthrough (Bend)")
            .Pattern("BPB,PHB,B_B")
            .Size(3, 3)
            .Ingredient("B", Brick(2))
            .Ingredient("P", StraightBlock(1))
            .Ingredient("H", Hammer)
            .OutputBlock("ppex:pipe-passthroughbend-{brick}-nw")
        )
        .Grid(r =>
          r.Name("Pipe Outlet")
            .Pattern("BHB,BNB,BPB")
            .Size(3, 3)
            .Ingredient("B", Brick(2))
            .Ingredient("P", StraightBlock(1))
            .Ingredient("H", Hammer)
            .Ingredient("N", Nails(2))
            .OutputBlock("ppex:pipe-outlet-{brick}-n")
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
            .OutputBlock("ppex:pipe-valve-sn-{metal}")
        )
        .Grid(r =>
          r.Name("Piping (Pressure Valve)")
            .Pattern("_H_,LPL,G_G")
            .Size(3, 3)
            .Ingredient("P", ValvePipe(1))
            .Ingredient("H", Hammer)
            .Ingredient("L", Plate(1))
            .Ingredient("G", Gear("game:gear-rusty", 2))
            .OutputBlock("ppex:pipe-pressurevalve-sn-{metal}")
        )
        .Grid(r =>
          r.Name("Piping (Valve)")
            .Pattern("_H_,GPL,_L_")
            .Size(3, 3)
            .Ingredient("P", ValvePipe(1))
            .Ingredient("H", Hammer)
            .Ingredient("L", Plate(1))
            .Ingredient("G", Gear("ppex:gear-*", 2))
            .OutputBlock("ppex:pipe-valve-sn-{metal}")
        )
        .Grid(r =>
          r.Name("Piping (Pressure Valve)")
            .Pattern("_H_,LPL,G_G")
            .Size(3, 3)
            .Ingredient("P", ValvePipe(1))
            .Ingredient("H", Hammer)
            .Ingredient("L", Plate(1))
            .Ingredient("G", Gear("ppex:gear-*", 2))
            .OutputBlock("ppex:pipe-pressurevalve-sn-{metal}")
        ),
    ];

  // ppex-specific ingredient factories (the shared vanilla ones - Plate/Nails/Hammer/Gear - come from
  // ExIngredients via `using static`). These stay local: they encode ppex block codes or a per-recipe wildcard.
  private static System.Func<IngredientBuilder, IngredientBuilder> Brick(int qty) =>
    i =>
      i.Item("game:burnedbrick-*")
        .Named("brick", "fire", "black", "brown", "cream", "gray", "orange", "red", "tan")
        .Quantity(qty);

  private static System.Func<IngredientBuilder, IngredientBuilder> StraightBlock(int qty) =>
    i => i.Block("ppex:pipe-straight-*").Quantity(qty);

  // The valve recipes take a metal-captured straight pipe (so the output inherits the metal variant).
  private static System.Func<IngredientBuilder, IngredientBuilder> ValvePipe(int qty) =>
    i => i.Block("ppex:pipe-straight-ns-*").Metal().Quantity(qty);
}
