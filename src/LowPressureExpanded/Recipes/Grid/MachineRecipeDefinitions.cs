using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace LowPressureExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the lpex machines (migrated from recipes/grid/machines.json): the fluid intake,
/// steam condenser, the Cornish boiler, the Watt engine, both pumps and the MP generator. The four gear-driven
/// machines each craft from vanilla rusty gears OR lpex's craftable gears - the source listed both, so here each
/// recipe is authored once and emitted for both gear codes in a loop (the DRY win; byte-identical output, and
/// the loop preserves the source order: all rusty-gear recipes, then all lpex-gear recipes).
/// <para>
/// The two high-pressure machines (Lancashire boiler, Cornish engine) moved to hpex and take their grid
/// recipes with them - see <c>HighPressureExpanded.Recipes.Grid.MachineRecipeDefinitions</c>.
/// </para>
/// </summary>
public class MachineRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain)
  {
    ExRecipeDef def = ExRecipeDef
      .Create(domain, "grid", "machines")
      .Grid(FluidIntake)
      .Grid(SteamCondenser)
      .Grid(CornishBoiler);

    foreach (string gear in new[] { "game:gear-rusty", "lpex:gear-*" })
      def = def.Grid(WattEngine(gear))
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
      .OutputBlock("lpex:pipe-fluidintake-s");

  private static void SteamCondenser(GridRecipeBuilder r) =>
    r.Name("Steam Condenser")
      .Pattern("_H_,IPI,NP_")
      .Size(3, 3)
      .Ingredient("I", StraightPipe(1))
      .Ingredient("P", Plate(1))
      .Ingredient("H", Hammer)
      .Ingredient("N", Nails(2))
      .OutputBlock("lpex:steamcondenser-north");

  private static void CornishBoiler(GridRecipeBuilder r) =>
    r.Name("Cornish Boiler")
      .Pattern("PHP,BNB")
      .Size(3, 2)
      .Ingredient("P", Plate(1))
      .Ingredient("B", BrickFire(2))
      .Ingredient("I", StraightPipe(1))
      .Ingredient("N", Nails(2))
      .Ingredient("H", Hammer)
      .OutputBlock("lpex:boilercornish-north");

  // --- gear-driven machines: authored once, emitted for both gear codes ---
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
        .OutputBlock("lpex:enginewatt-north");

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
        .OutputBlock("lpex:enginefluidpump-north");

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
        .OutputBlock("lpex:manualfluidpump-north");

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
        .OutputBlock("lpex:enginempgenerator-north");

  // lpex-specific ingredient factories (the shared vanilla ones - Plate/PlateSteel/Nails/NailsSteel/Rod/
  // RodSteel/Gear/Hammer - come from ExIngredients via `using static`). These stay local: the pipe base
  // block + a vanilla fire brick used only here.
  //
  // The pipe ingredient is the plain bolted (iwex) segment - the base tier that actually has a craft
  // recipe - matching the fitting recipes in PipeRecipeDefinitions. lpex's own cast segments are a
  // higher tier with no recipe of their own yet.
  private static Func<IngredientBuilder, IngredientBuilder> StraightPipe(int qty) =>
    i => i.Block("iwex:pipe-straight-*").Quantity(qty);

  private static Func<IngredientBuilder, IngredientBuilder> BrickFire(int qty) =>
    i => i.Item("game:burnedbrick-fire").Quantity(qty);
}
