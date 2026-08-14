using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace HighPressureExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the hpex machines: the Lancashire boiler and the Cornish engine. Like
/// every gear-driven machine in the family, the Cornish engine crafts from vanilla rusty gears or
/// from lpex's craftable gears; it is authored once and emitted for both gear codes, rusty first.
/// </summary>
public class MachineRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) {
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

  // hpex-specific ingredient factories; the shared vanilla ones (PlateSteel, NailsSteel, RodSteel,
  // Gear, Hammer) come from ExIngredients through `using static`.
  //
  // The pipe ingredient is the plain plated (iwex) segment, as in every other machine and fitting
  // recipe in the family. The pipe tiers carry no iron/steel material axis, so a material-suffixed
  // code such as `...-steel` resolves to no block. Tier-gating the HP builds to the cast (lpex) or
  // rolled (hpex) segments waits on craft recipes for those segments and on the hadfield material
  // gate; see docs/design/hpex.md.
  private static Func<IngredientBuilder, IngredientBuilder> StraightPipe(
    int qty
  ) => i => i.Block("iwex:pipe-plated-straight-*").Quantity(qty);

  private static Func<IngredientBuilder, IngredientBuilder> BrickFire(
    int qty
  ) => i => i.Item("game:burnedbrick-fire").Quantity(qty);
}
