using System.Collections.Generic;
using ExpandedLib.Registries.Config;
using ExpandedLib.Registries.Recipes;

namespace SteelIndustryExpanded;

/// <summary>
/// The steel-industry recipe cost catalogue, written to <c>ModConfig/ex_recipes.json</c>. Each entry
/// names a grid recipe (by output) or an RCC construction (by block) to manage. The <c>normal</c>
/// level is auto-filled from the recipes as authored and the <c>cheap</c> level is scale-filled from
/// it, both editable in the file afterwards. The active level lives in
/// <see cref="SiexConfig.RecipeLevel"/> and takes effect on the next world reload.
/// </summary>
[ExConfigRegister(
  "ex_recipes.json",
  "siex",
  LegacyFileNames = new string[] { "smex_recipes.json" },
  LegacySectionIds = new string[] { "smex", "hpex" }
)]
public class SiexRecipeConfig : IExVersionedConfig {
  public string? ConfigVersion { get; set; }

  private Dictionary<string, RecipeCostEntry>? _recipes;

  /// <summary>Never null: a missing or null <c>Recipes</c> in the file falls back to the code
  /// defaults. Missing/broken individual entries are repaired against <see cref="DefaultCatalogue"/>
  /// at load by <see cref="ExRecipeCosts.Reconcile"/>.</summary>
  public Dictionary<string, RecipeCostEntry> Recipes {
    get => _recipes ??= Defaults();
    set => _recipes = value;
  }

  /// <summary>A fresh copy of the shipped catalogue defaults, used to repair the loaded file.</summary>
  public static Dictionary<string, RecipeCostEntry> DefaultCatalogue() =>
    Defaults();

  private static RecipeCostEntry Rcc(string match) =>
    new() { Type = "rcc", Match = match };

  private static RecipeCostEntry Grid(string match) =>
    new() { Type = "grid", Match = match };

  // Hand-maintained list of every grid and RCC recipe this mod ships. Profiles are auto-filled at
  // load: the normal baseline is read from the live recipe, the cheap profile is half of it.
  private static Dictionary<string, RecipeCostEntry> Defaults() =>
    new() {
      // RCC constructions (the heavy multiblock build costs).
      ["converterbessemer-rcc"] = Rcc("siex:converterbessemer-*"),
      ["boilerlancashire-rcc"] = Rcc("siex:boilerlancashire-*"),
      ["enginecornish-rcc"] = Rcc("siex:enginecornish-*"),

      // Converter and hot-blast machine grid recipes. Iron-tier content (blast-furnace components,
      // molten transport, slag paths) is registered by IiexRecipeConfig, so /exmod recipes siex
      // discounts the steel line only.
      ["converter-intake-grid"] = Grid("siex:converter-intake-*"),
      ["convertercontrol-grid"] = Grid("siex:convertercontrol-*"),
      ["convertertransmission-grid"] = Grid("siex:convertertransmission-*"),
      ["cowperstove-intake-grid"] = Grid("siex:cowperstove-intake-*"),
      ["cowperstoveheatsink-grid"] = Grid("siex:cowperstoveheatsink-*"),
      ["engineairblower-grid"] = Grid("siex:engineairblower-*"),
      ["smokestack-intake-grid"] = Grid("siex:smokestack-intake-*"),

      // High-pressure machine grid "frame" recipes.
      ["boilerlancashire-grid"] = Grid("siex:boilerlancashire-*"),
      ["enginecornish-grid"] = Grid("siex:enginecornish-*"),

      // Hoppers, which belong to the hot blast furnace; the cold furnace in iiex charges through its
      // own tall hopper.
      ["hopperbell-grid"] = Grid("siex:hopperbell"),
      ["hopperreinforced-grid"] = Grid("siex:hopperreinforced"),
    };
}
