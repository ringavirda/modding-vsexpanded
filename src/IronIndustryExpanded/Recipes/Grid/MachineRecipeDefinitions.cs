using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the iiex machines: the fluid intake, steam condenser, Cornish boiler,
/// Watt engine, both pumps and the MP generator. Each of the four gear-driven machines crafts from
/// vanilla rusty gears or from iiex's craftable gears, so it is authored once and emitted for both
/// gear codes in a loop. The high-pressure machines (Lancashire boiler, Cornish engine) live in
/// <c>HighPressureExpanded.Recipes.Grid.MachineRecipeDefinitions</c>.
/// </summary>
public class MachineRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) {
    ExRecipeDef def = ExRecipeDef
      .Create(domain, "grid", "machines")
      .Grid(FluidIntake)
      .Grid(SteamCondenser)
      .Grid(CornishBoiler);

    foreach (string gear in new[] { "game:gear-rusty", "iiex:gear-*" })
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
      .OutputBlock("iiex:pipe-fluidintake-s");

  private static void SteamCondenser(GridRecipeBuilder r) =>
    r.Name("Steam Condenser")
      .Pattern("_H_,IPI,NP_")
      .Size(3, 3)
      .Ingredient("I", StraightPipe(1))
      .Ingredient("P", Plate(1))
      .Ingredient("H", Hammer)
      .Ingredient("N", Nails(2))
      .OutputBlock("iiex:steamcondenser-n");

  // A boiler frame is plate, brick and a fastener - no pipe. The vessel's own couplings are footprint
  // port cells rather than fittings the player sets, so nothing in the frame is plumbing; the siex
  // Lancashire's frame is the same shape for the same reason.
  private static void CornishBoiler(GridRecipeBuilder r) =>
    r.Name("Cornish Boiler")
      .Pattern("PHP,BNB")
      .Size(3, 2)
      .Ingredient("P", Plate(1))
      .Ingredient("B", BrickFire(2))
      .Ingredient("N", Nails(2))
      .Ingredient("H", Hammer)
      .OutputBlock("iiex:boilercornish-n");

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
        .OutputBlock("iiex:enginewatt-n");

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
        .OutputBlock("iiex:enginefluidpump-n");

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
        .OutputBlock("iiex:manualfluidpump-n");

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
        .OutputBlock("iiex:enginempgenerator-n");

  // iiex-specific ingredient factories; the shared vanilla ones (Plate/PlateSteel/Nails/NailsSteel/
  // Rod/RodSteel/Gear/Hammer) come from ExIngredients via `using static`. The pipe ingredient is the
  // plain plated (iiex) segment, the base tier that has a craft recipe, matching the fitting recipes
  // in PipeRecipeDefinitions; iiex's own cast segments are a higher tier with no recipe of their own.
  private static Func<IngredientBuilder, IngredientBuilder> StraightPipe(
    int qty
  ) => i => i.Block("iiex:pipe-plated-straight-*").Quantity(qty);

  private static Func<IngredientBuilder, IngredientBuilder> BrickFire(
    int qty
  ) => i => i.Item("game:burnedbrick-fire").Quantity(qty);
}
