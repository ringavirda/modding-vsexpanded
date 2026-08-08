using System.Collections.Generic;
using ExpandedLib.Registries.Config;
using ExpandedLib.Registries.Recipes;

namespace SteelmakingExpanded;

/// <summary>
/// The steelmaking recipe cost catalogue, written to <c>ModConfig/ex_recipes.json</c>. Each entry
/// names a grid recipe (by output) or an RCC construction (by block) to manage. The <c>normal</c>
/// level is auto-filled from the recipes as authored and the <c>cheap</c> level is scale-filled from
/// it, both editable in the file afterwards. The active level lives in
/// <see cref="SmexConfig.RecipeLevel"/> and takes effect on the next world reload.
/// </summary>
[ExConfigRegister(
  "ex_recipes.json",
  "smex",
  LegacyFileNames = new string[] { "smex_recipes.json" }
)]
public class SmexRecipeConfig : IExVersionedConfig {
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

  private static RecipeCostEntry Grid(string match) =>
    new() { Type = "grid", Match = match };

  // Hand-maintained list of every grid and RCC recipe this mod ships. Profiles are auto-filled at
  // load: the normal baseline is read from the live recipe, the cheap profile is half of it.
  private static Dictionary<string, RecipeCostEntry> Defaults() =>
    new() {
      // RCC construction (the Bessemer converter vessel).
      ["converterbessemer-rcc"] = new() {
        Type = "rcc",
        Match = "smex:converterbessemer-*",
      },

      // Converter and hot-blast machine grid recipes. Iron-tier content (blast-furnace components,
      // molten transport, slag paths) is registered by IwexRecipeConfig, so /exmod recipes smex
      // discounts steelmaking only.
      ["converter-intake-grid"] = Grid("smex:converter-intake-*"),
      ["convertercontrol-grid"] = Grid("smex:convertercontrol-*"),
      ["convertertransmission-grid"] = Grid("smex:convertertransmission-*"),
      ["cowperstove-intake-grid"] = Grid("smex:cowperstove-intake-*"),
      ["cowperstoveheatsink-grid"] = Grid("smex:cowperstoveheatsink-*"),
      ["engineairblower-grid"] = Grid("smex:engineairblower-*"),
      ["smokestack-intake-grid"] = Grid("smex:smokestack-intake-*"),

      // Hoppers, which belong to the hot blast furnace; the cold furnace in iwex charges through its
      // own tall hopper.
      ["hopperbell-grid"] = Grid("smex:hopperbell"),
      ["hopperreinforced-grid"] = Grid("smex:hopperreinforced"),
    };
}
