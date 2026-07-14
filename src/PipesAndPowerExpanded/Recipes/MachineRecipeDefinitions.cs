using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace PipesAndPowerExpanded.Recipes;

/// <summary>
/// Code-first grid recipes for the ppex machines (migrated from recipes/grid/machines.json): the fluid intake,
/// steam condenser, both boilers, both engines, both pumps and the MP generator. The five gear-driven machines
/// each craft from vanilla rusty gears OR ppex's craftable gears - the source listed both, so here each engine
/// recipe is authored once and emitted for both gear codes in a loop (the DRY win; byte-identical output, and
/// the loop preserves the source order: all rusty-gear recipes, then all ppex-gear recipes).
/// </summary>
public class MachineRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain)
  {
    ExRecipeDef def = ExRecipeDef
      .Create(domain, "grid", "machines")
      .Grid(FluidIntake)
      .Grid(SteamCondenser)
      .Grid(CornishBoiler)
      .Grid(LancashireBoiler);

    foreach (string gear in new[] { "game:gear-rusty", "ppex:gear-*" })
      def = def.Grid(CornishEngine(gear))
        .Grid(WattEngine(gear))
        .Grid(FluidPump(gear))
        .Grid(ManualPump(gear))
        .Grid(MpGenerator(gear));

    return [def];
  }

  // --- non-gear machines ---
  private static void FluidIntake(GridRecipeBuilder r) =>
    r.Name("Fluid Intake")
      .Pattern("_H_,PIP,NPN")
      .Size(3, 3)
      .Ingredient("I", StraightPipe(1))
      .Ingredient("P", Plate(1))
      .Ingredient("N", Nails(2))
      .Ingredient("H", Hammer)
      .OutputBlock("ppex:pipe-fluidintake-s");

  private static void SteamCondenser(GridRecipeBuilder r) =>
    r.Name("Steam Condenser")
      .Pattern("_H_,IPI,NP_")
      .Size(3, 3)
      .Ingredient("I", StraightPipe(1))
      .Ingredient("P", Plate(1))
      .Ingredient("H", Hammer)
      .Ingredient("N", Nails(2))
      .OutputBlock("ppex:steamcondenser-north");

  private static void CornishBoiler(GridRecipeBuilder r) =>
    r.Name("Cornish Boiler")
      .Pattern("PHP,BNB")
      .Size(3, 2)
      .Ingredient("P", Plate(1))
      .Ingredient("B", BrickFire(2))
      .Ingredient("I", StraightPipe(1))
      .Ingredient("N", Nails(2))
      .Ingredient("H", Hammer)
      .OutputBlock("ppex:boilercornish-north");

  private static void LancashireBoiler(GridRecipeBuilder r) =>
    r.Name("Lancashire Boiler")
      .Pattern("BHB,PRP")
      .Size(3, 2)
      .Ingredient("P", PlateSteel(1))
      .Ingredient("B", BrickFire(2))
      .Ingredient("R", RodSteel(2))
      .Ingredient("H", Hammer)
      .OutputBlock("ppex:boilerlancashire-north");

  // --- gear-driven machines: authored once, emitted for both gear codes ---
  private static Action<GridRecipeBuilder> CornishEngine(string gear) =>
    r =>
      r.Name("Cornish Engine")
        .Pattern("GHR,PNP,PIP")
        .Size(3, 3)
        .Ingredient("P", PlateSteel(1))
        .Ingredient("R", RodSteel(4))
        .Ingredient("G", Gear(gear, 4))
        .Ingredient("I", StraightPipeSteel(2))
        .Ingredient("H", Hammer)
        .Ingredient("N", NailsSteel(4))
        .OutputBlock("ppex:enginecornish-north");

  private static Action<GridRecipeBuilder> WattEngine(string gear) =>
    r =>
      r.Name("Watt Engine")
        .Pattern("_H_,PRP,PIP")
        .Size(3, 3)
        .Ingredient("P", Plate(1))
        .Ingredient("R", Rod(2))
        .Ingredient("G", Gear(gear, 2))
        .Ingredient("I", StraightPipe(1))
        .Ingredient("H", Hammer)
        .OutputBlock("ppex:enginewatt-north");

  private static Action<GridRecipeBuilder> FluidPump(string gear) =>
    r =>
      r.Name("Fluid Pump")
        .Pattern("_HG,PIP,RIR")
        .Size(3, 3)
        .Ingredient("P", Plate(1))
        .Ingredient("G", Gear(gear, 1))
        .Ingredient("I", StraightPipe(1))
        .Ingredient("H", Hammer)
        .Ingredient("R", Rod(2))
        .OutputBlock("ppex:enginefluidpump-north");

  private static Action<GridRecipeBuilder> ManualPump(string gear) =>
    r =>
      r.Name("Manual Fluid Pump")
        .Pattern("_GH,PIP,BRB")
        .Size(3, 3)
        .Ingredient("P", Plate(1))
        .Ingredient("G", Gear(gear, 2))
        .Ingredient("I", StraightPipe(1))
        .Ingredient("R", Rod(2))
        .Ingredient("B", i => i.Block("game:supportbeam-*").Quantity(4))
        .Ingredient("H", Hammer)
        .OutputBlock("ppex:manualfluidpump-north");

  private static Action<GridRecipeBuilder> MpGenerator(string gear) =>
    r =>
      r.Name("Mechanical Power Generator")
        .Pattern("_H_,GAG,PRP")
        .Size(3, 3)
        .Ingredient("P", Plate(2))
        .Ingredient("R", Rod(2))
        .Ingredient("G", Gear(gear, 2))
        .Ingredient("A", i => i.Block("game:woodenaxle-ud").Quantity(1))
        .Ingredient("H", Hammer)
        .OutputBlock("ppex:enginempgenerator-north");

  // ppex-specific ingredient factories (the shared vanilla ones - Plate/PlateSteel/Nails/NailsSteel/Rod/
  // RodSteel/Gear/Hammer - come from ExIngredients via `using static`). These stay local: ppex codes + a
  // vanilla fire brick used only here.
  private static Func<IngredientBuilder, IngredientBuilder> StraightPipe(int qty) =>
    i => i.Block("ppex:pipe-straight-*").Quantity(qty);

  private static Func<IngredientBuilder, IngredientBuilder> StraightPipeSteel(int qty) =>
    i => i.Block("ppex:pipe-straight-*-steel").Quantity(qty);

  private static Func<IngredientBuilder, IngredientBuilder> BrickFire(int qty) =>
    i => i.Item("game:burnedbrick-fire").Quantity(qty);
}
