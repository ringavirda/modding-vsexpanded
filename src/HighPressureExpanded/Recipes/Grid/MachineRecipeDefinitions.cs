using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace HighPressureExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the hpex machines: the Lancashire boiler and the Cornish engine
/// (carved out of lpex's <c>MachineRecipeDefinitions</c> when the two HP leaves moved here). The
/// Cornish engine, like every gear-driven machine in the family, crafts from vanilla rusty gears OR
/// lpex's craftable gears - authored once and emitted for both gear codes in a loop, preserving the
/// source order (rusty-gear recipe first, then the lpex-gear one).
/// </summary>
public class MachineRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain)
  {
    ExRecipeDef def = ExRecipeDef
      .Create(domain, "grid", "machines")
      .Grid(LancashireBoiler);

    foreach (string gear in new[] { "game:gear-rusty", "lpex:gear-*" })
      def = def.Grid(CornishEngine(gear));

    return [def];
  }

  private static void LancashireBoiler(GridRecipeBuilder r) =>
    r.Name("Lancashire Boiler")
      .Pattern("BHB,PRP")
      .Size(3, 2)
      .Ingredient("P", PlateSteel(1))
      .Ingredient("B", BrickFire(2))
      .Ingredient("R", RodSteel(2))
      .Ingredient("H", Hammer)
      .OutputBlock("hpex:boilerlancashire-n");

  private static Action<GridRecipeBuilder> CornishEngine(string gear) =>
    r =>
      r.Name("Cornish Engine")
        .Pattern("GHR,PNP,PIP")
        .Size(3, 3)
        .Ingredient("P", PlateSteel(1))
        .Ingredient("R", RodSteel(4))
        .Ingredient("G", Gear(gear, 4))
        .Ingredient("I", StraightPipe(2))
        .Ingredient("H", Hammer)
        .Ingredient("N", NailsSteel(4))
        .OutputBlock("hpex:enginecornish-n");

  // hpex-specific ingredient factories (the shared vanilla ones - PlateSteel/NailsSteel/RodSteel/
  // Gear/Hammer - come from ExIngredients via `using static`).
  //
  // The pipe ingredient is the plain plated (iwex) segment, matching every other machine + fitting
  // recipe in the family - the pipe tiers carry no iron/steel material axis, so a material-suffixed
  // code (e.g. `...-steel`) resolves to no block.
  // Tier-gating the HP builds to the cast (lpex) / rolled (hpex) segments waits on those segments
  // getting craft recipes of their own, and on the hadfield material gate (see docs/design/hpex.md).
  private static Func<IngredientBuilder, IngredientBuilder> StraightPipe(int qty) =>
    i => i.Block("iwex:pipe-straight-*").Quantity(qty);

  private static Func<IngredientBuilder, IngredientBuilder> BrickFire(int qty) =>
    i => i.Item("game:burnedbrick-fire").Quantity(qty);
}
